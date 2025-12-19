using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ANest.UI {
	/// <summary> 各種ガードとテキスト遷移・カスタムアニメーションを備えた拡張Toggle </summary>
	public class aToggle : Toggle {
		#region SerializeField
		[Header("Shared Parameters")]
		[SerializeField] private bool useSharedParameters;                      // 共通パラメータを使用するか
		[SerializeField] private aSelectablesSharedParameters sharedParameters; // 共通パラメータの参照

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
		[SerializeField] private bool m_useSharedAnimation = false;                                // 共有アニメーションを使用するか
		[SerializeField] private UiAnimationSet m_sharedAnimation;                                 // 共有アニメーション設定
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_clickAnimations; // クリック時のカスタムアニメーション
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_onAnimations;    // ON時のカスタムアニメーション
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_offAnimations;   // OFF時のカスタムアニメーション

		[Header("RectTransform")]
		[SerializeField] private RectTransform m_rectTransform;                           // 自身のRectTransformキャッシュ
		[SerializeField] private RectTransform m_targetRectTransform;                     // Toggleのgraphicの RectTransform キャッシュ
		[SerializeField] private RectTransformValues m_originalTargetRectTransformValues; // 自身の初期RectTransform値
		[SerializeField] private RectTransformValues m_originalToggleRectTransformValues; // Toggleのgraphicの初期RectTransform値
		#endregion


		#region Fields
		private float _lastAcceptedClickTime = -999f;            // 最後に受理した入力時刻
		private float _initialGuardEndTime = -999f;              // 有効化直後のガード解除時刻
		private CancellationTokenSource _textColorTransitionCts; // テキストカラー遷移のCTS
		#endregion

	    #region Unity Methods
		/// <summary> 有効化時に初期化とRectTransformの初期値取得を行う </summary>
		protected override void OnEnable() {
			ApplySharedParametersIfNeeded();
			base.OnEnable();

			StartInitialGuard();
			RegisterToggleListener(true);
		}

		/// <summary> 無効化時にリスナー解除やアニメーションのキャンセルを行う </summary>
		protected override void OnDisable() {
			base.OnDisable();

			RegisterToggleListener(false);
			aGuiUtils.StopTextColorTransition(ref _textColorTransitionCts);
		}

		/// <summary> クリック入力時のガード判定とアニメーション再生を処理する </summary>
		public override void OnPointerClick(PointerEventData eventData) {
			if(eventData.button != PointerEventData.InputButton.Left) return;
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			base.OnPointerClick(eventData);
			PlayClickAnimations();
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
			if(!Application.isPlaying) return;
			if(!m_useCustomAnimation) return;

			PlayToggleAnimations(isOn);
		}
		#endregion

		#region Toggle Animation Async
		/// <summary> ON/OFF切替時のカスタムアニメーションを非同期待機で再生する </summary>
		private void PlayToggleAnimations(bool isOn) {
			if(!m_useCustomAnimation) return;

			//ToggleのGraphic指定がないなら無視
			if(graphic == null) return;

			if(isOn) {
				aGuiUtils.PlayAnimation(m_onAnimations, graphic.rectTransform, graphic, m_originalToggleRectTransformValues);
			} else {
				//デフォルト処理によるトグルグラフィックの透明化を抑制
				graphic.canvasRenderer.SetAlpha(1f);

				aGuiUtils.PlayAnimation(m_offAnimations, graphic.rectTransform, graphic, m_originalToggleRectTransformValues,
					() => {
						// OFF時、最後は非表示になるようにトグル標準の非表示処理を適用
						if(graphic != null) {
							graphic.canvasRenderer.SetAlpha(0f);
						}
					});
			}
		}

		/// <summary> クリック時のカスタムアニメーションを再生する </summary>
		private void PlayClickAnimations() {
			if(!m_useCustomAnimation && !m_useSharedAnimation) return;
			if(m_clickAnimations == null || m_clickAnimations.Length == 0) return;
			if(m_rectTransform == null) return;

			var target = m_rectTransform != null ? m_rectTransform : m_targetRectTransform;

			aGuiUtils.PlayAnimation(m_clickAnimations, target, targetGraphic, m_originalTargetRectTransformValues);
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

		#region Shared Apply
		/// <summary> 共通パラメータを使用している場合に値を反映する </summary>
		private void ApplySharedParametersIfNeeded() {
			if(!useSharedParameters) return;
			if(sharedParameters == null) return;

			transition = sharedParameters.transition;
			colors = sharedParameters.transitionColors;
			spriteState = sharedParameters.spriteState;
			animationTriggers = sharedParameters.selectableAnimationTriggers;

			useInitialGuard = sharedParameters.useInitialGuard;
			initialGuardDuration = sharedParameters.initialGuardDuration;

			useMultipleInputGuard = sharedParameters.useMultipleInputGuard;
			multipleInputGuardInterval = sharedParameters.multipleInputGuardInterval;

			textTransition = sharedParameters.textTransition;
			textColors = sharedParameters.textColors;
			textSwapState = sharedParameters.textSwapState;
			textAnimationTriggers = sharedParameters.textAnimationTriggers;
		}
	        #endregion
	}
}
