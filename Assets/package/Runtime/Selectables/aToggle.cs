using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ANest.UI {
	/// <summary> 各種ガードとテキスト遷移・カスタムアニメーションを備えた拡張Toggle </summary>
	public class aToggle : Toggle {
		#region SerializeField
		[Header("Initial Guard")]
		[SerializeField] private bool useInitialGuard = true;       // 有効化直後の入力を抑制するか
		[SerializeField] private float initialGuardDuration = 0.5f; // 有効化直後に抑制する秒数

		[Header("Multiple Input Guard")]
		[SerializeField] private bool useMultipleInputGuard = true;       // 入力ガードを使うか
		[SerializeField] private float multipleInputGuardInterval = 0.5f; // 入力ガードの待機秒数

		[Header("Text Transition")]
		[SerializeField] private TMP_Text targetText;                                              // 遷移対象のテキスト
		[SerializeField] private TextTransitionType textTransition = TextTransitionType.TextColor; // テキスト遷移種別
		[SerializeField] private ColorBlock textColors = ColorBlock.defaultColorBlock;             // テキストカラー設定
		[SerializeField] private TextSwapState textSwapState;                                      // テキスト差し替え設定
		[SerializeField] private AnimationTriggers textAnimationTriggers = new();                  // テキストアニメーション用トリガー
		[SerializeField] private Animator textAnimator;                                            // テキスト用アニメーター

		[Header("Animation")]
		[SerializeField] private bool m_useCustomAnimation;                                        // カスタムアニメーションを使用するか
		[SerializeField] private bool m_disableInteractableDuringAnimation = true;                 // アニメーション中は操作不可にするか
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_clickAnimations; // クリック時のカスタムアニメーション
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_onAnimations;    // ON時のカスタムアニメーション
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_offAnimations;   // OFF時のカスタムアニメーション
		#endregion

		#region Fields
		private RectTransform m_rectTransform;                            // 自身のRectTransformキャッシュ
		private RectTransform m_targetRectTransform;                      // Toggleのgraphicの RectTransform キャッシュ
		private RectTransformValues? m_originalRectTransformValues;       // 自身の初期RectTransform値
		private RectTransformValues? m_originalTargetRectTransformValues; // Toggleのgraphicの初期RectTransform値
		private float _lastAcceptedClickTime = -999f;                     // 最後に受理した入力時刻
		private float _initialGuardEndTime = -999f;                       // 有効化直後のガード解除時刻
		private CancellationTokenSource _textColorTransitionCts;          // テキストカラー遷移のCTS
		private CancellationTokenSource _toggleAnimationCts;              // トグルアニメーション用CTS
		private CancellationTokenSource _clickAnimationCts;               // クリックアニメーション用CTS
		#endregion

    #region Unity Methods
		/// <summary> 有効化時に初期化とRectTransformの初期値取得を行う </summary>
		protected override async void OnEnable() {
			base.OnEnable();

			if(!Application.isPlaying) return;

			m_rectTransform = transform as RectTransform;
			// ON/OFF アニメーションは Toggle の graphic を対象にする
			m_targetRectTransform = graphic != null ? graphic.transform as RectTransform : null;

			StartInitialGuard();
			RegisterToggleListener(true);

			await CaptureInitialRectTransformsAsync();
		}

		/// <summary> 無効化時にリスナー解除やアニメーションのキャンセルを行う </summary>
		protected override void OnDisable() {
			base.OnDisable();

			if(!Application.isPlaying) return;

			RegisterToggleListener(false);
			aGuiUtils.StopTextColorTransition(ref _textColorTransitionCts);
			CancelToggleAnimations();
			CancelClickAnimations();
		}

		/// <summary> クリック入力時のガード判定とアニメーション再生を処理する </summary>
		public override void OnPointerClick(PointerEventData eventData) {
			if(eventData.button != PointerEventData.InputButton.Left) return;
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			base.OnPointerClick(eventData);
			PlayClickAnimationsAsync().Forget();
		}

		/// <summary> Submit入力時にガードを適用する </summary>
		public override void OnSubmit(BaseEventData eventData) {
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			base.OnSubmit(eventData);
		}

		/// <summary> ステート遷移に応じてテキスト遷移を適用する </summary>
		protected override void DoStateTransition(SelectionState state, bool instant) {
			base.DoStateTransition(state, instant);
			if(targetText == null) return;

			switch(textTransition) {
				case TextTransitionType.TextColor:
					aGuiUtils.ApplyTextColorTransition(this, targetText, textColors, (int)state, instant, ref _textColorTransitionCts, () => _textColorTransitionCts = null);
					break;
				case TextTransitionType.TextSwap:
					aGuiUtils.ApplyTextSwapTransition(targetText, textSwapState, (int)state);
					break;
				case TextTransitionType.TextAnimation:
					aGuiUtils.ApplyTextAnimationTransition(targetText, textAnimator, textAnimationTriggers, (int)state);
					break;
			}
		}
		#endregion

    #region Toggle Events
		/// <summary> トグルのON/OFFリスナーを登録または解除する </summary>
		private void RegisterToggleListener(bool register) {
			if(register) {
				onValueChanged.AddListener(OnToggleValueChanged);
			} else {
				onValueChanged.RemoveListener(OnToggleValueChanged);
			}
		}

		/// <summary> トグル状態変化時にカスタムアニメーションを再生する </summary>
		private void OnToggleValueChanged(bool isOn) {
			if(!m_useCustomAnimation) return;
			_ = PlayToggleAnimationsAsync(isOn);
		}
		#endregion

		#region Toggle Animation Async
		/// <summary> ON/OFF切替時のカスタムアニメーションを非同期待機で再生する </summary>
		private async UniTask PlayToggleAnimationsAsync(bool isOn) {
			CancelToggleAnimations();
			_toggleAnimationCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			var token = _toggleAnimationCts.Token;

			RectTransform target = m_targetRectTransform != null ? m_targetRectTransform : m_rectTransform;
			RectTransformValues? original = m_targetRectTransform != null ? m_originalTargetRectTransformValues : m_originalRectTransformValues;

			if(target == null || original == null) return;

			try {
				var originalValue = original.Value;
				if(isOn) {
					await aGuiUtils.PlayAnimationsWithInteractableGuardAsync(this, m_disableInteractableDuringAnimation, m_onAnimations, graphic, target, originalValue, token);
					return;
				}

				CanvasRenderer renderer = graphic != null ? graphic.canvasRenderer : null;

				// 標準のフェード開始で非表示にならないよう、まずAlphaを1に戻す
				if(graphic != null) {
					graphic.CrossFadeAlpha(1f, 0f, true);
					graphic.canvasRenderer.SetAlpha(1f);
				} else if(renderer != null) {
					renderer.SetAlpha(1f);
				}

				await aGuiUtils.PlayAnimationsWithInteractableGuardAsync(this, m_disableInteractableDuringAnimation, m_offAnimations, graphic, target, originalValue, token);

				if(token.IsCancellationRequested) return;

				// トグル標準の非表示処理を適用
				if(graphic != null) {
					graphic.CrossFadeAlpha(0f, 0f, true);
					graphic.canvasRenderer.SetAlpha(0f);
				} else if(renderer != null) {
					renderer.SetAlpha(0f);
				}
			} catch (OperationCanceledException) {
				return;
			} finally {
				_toggleAnimationCts?.Dispose();
				_toggleAnimationCts = null;
			}
		}

		/// <summary> クリック時のカスタムアニメーションを再生する </summary>
		private async UniTask PlayClickAnimationsAsync() {
			if(!m_useCustomAnimation) return;
			if(m_clickAnimations == null || m_clickAnimations.Length == 0) return;
			if(m_rectTransform == null || m_originalRectTransformValues == null) return;

			CancelClickAnimations();
			_clickAnimationCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			var token = _clickAnimationCts.Token;

			await aGuiUtils.PlayAnimationsWithInteractableGuardAsync(this, m_disableInteractableDuringAnimation, m_clickAnimations, targetGraphic, m_rectTransform, m_originalRectTransformValues.Value, token);
			_clickAnimationCts?.Dispose();
			_clickAnimationCts = null;
		}

		/// <summary> ON/OFFアニメーション用のCTSをキャンセル・破棄する </summary>
		private void CancelToggleAnimations() {
			if(_toggleAnimationCts == null) return;
			_toggleAnimationCts.Cancel();
			_toggleAnimationCts.Dispose();
			_toggleAnimationCts = null;
		}

		/// <summary> クリックアニメーション用のCTSをキャンセル・破棄する </summary>
		private void CancelClickAnimations() {
			if(_clickAnimationCts == null) return;
			_clickAnimationCts.Cancel();
			_clickAnimationCts.Dispose();
			_clickAnimationCts = null;
		}
		#endregion

		#region Guards
		/// <summary> 初期ガード・連打ガードの状態を判定する </summary>
		private bool IsGuardActive(float now) {
			if(useInitialGuard && now < _initialGuardEndTime) {
				#if UNITY_EDITOR
				Debug.Log($"[{nameof(aToggle)}] Initial Guard active. Input blocked until {_initialGuardEndTime:F3} (remaining {_initialGuardEndTime - now:F3}s).", this);
				#endif
				return true;
			}
			if(!useMultipleInputGuard) return false;

			bool active = (now - _lastAcceptedClickTime) < multipleInputGuardInterval;
			#if UNITY_EDITOR
			if(active) {
				Debug.Log($"[{nameof(aToggle)}] Multiple Input Guard active. Input blocked.", this);
			}
			#endif
			return active;
		}

		/// <summary> 入力ガード開始時刻を記録する </summary>
		private void StartGuard(float now) {
			_lastAcceptedClickTime = now;
		}

		/// <summary> 有効化直後の入力を一定時間抑制するための初期ガードを設定 </summary>
		private void StartInitialGuard() {
			if(!useInitialGuard || initialGuardDuration <= 0f) {
				_initialGuardEndTime = -999f;
				return;
			}
			_initialGuardEndTime = Time.unscaledTime + initialGuardDuration;
		}
		#endregion

		#region Animation
		/// <summary> アニメーション用に自身とターゲットのRectTransform初期値を取得する </summary>
		private async UniTask CaptureInitialRectTransformsAsync() {
			var capturedRectTransformValues = await aGuiUtils.CaptureInitialRectTransformAsync(
				m_rectTransform,
				m_originalRectTransformValues,
				destroyCancellationToken,
				true);
			if(this == null || this.Equals(null)) return;
			m_originalRectTransformValues = capturedRectTransformValues;

			var capturedTargetRectTransformValues = await aGuiUtils.CaptureInitialRectTransformAsync(
				m_targetRectTransform,
				m_originalTargetRectTransformValues,
				destroyCancellationToken,
				true);
			if(this == null || this.Equals(null)) return;
			m_originalTargetRectTransformValues = capturedTargetRectTransformValues;
		}
        #endregion
	}
}
