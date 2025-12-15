using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ANest.UI {
	/// <summary> テキスト遷移の種別 </summary>
	public enum TextTransitionType {
		TextColor,
		TextSwap,
		TextAnimation
	}

	/// <summary> テキスト差し替え用のステート値 </summary>
	[Serializable]
	public struct TextSwapState {
		public string normalText;      // 通常時テキスト
		public string highlightedText; // ハイライト時テキスト
		public string pressedText;     // 押下時テキスト
		public string selectedText;    // 選択時テキスト
		public string disabledText;    // 無効時テキスト
	}

	/// <summary> 複数の入力ガードや長押し対応を備えた拡張Button </summary>
	public class aButton : Button {
		#region SerializeField
		[SerializeField] private UnityEvent onRightClick = new(); // 右クリック用イベント

		[Header("Shared Parameters")]
		[SerializeField] private bool useSharedParameters;                 // 共通パラメータを使用するか
		[SerializeField] private aButtonSharedParameters sharedParameters; // 共通パラメータの参照

		[Header("Initial Guard")]
		[SerializeField] private bool useInitialGuard = true;       // 有効化直後の入力を抑制するか
		[SerializeField] private float initialGuardDuration = 0.5f; // 有効化直後に抑制する秒数

		[Header("Long Press")]
		[SerializeField] private bool enableLongPress = false;         // 長押しを有効にするか
		[SerializeField] private float longPressDuration = 0.5f;       // 長押し成立までの時間（秒）
		[SerializeField] private UnityEvent onLongPress = new();       // 長押し成立イベント
		[SerializeField] private UnityEvent onLongPressCancel = new(); // 長押しキャンセルイベント
		[SerializeField] private Image longPressImage;                 // 長押し進捗を反映するImage

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
		[SerializeField] private bool m_useCustomAnimation;                                        // カスタムアニメーションを使用するか
		[SerializeField] private bool m_disableInteractableDuringAnimation = true;                 // アニメーション中は操作不可にするか
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_clickAnimations; // クリック時のカスタムアニメーション
		#endregion

		#region Shared Apply
		/// <summary> 共通パラメータを使用している場合に値を反映する </summary>
		private void ApplySharedParametersIfNeeded() {
			if(!useSharedParameters) return;
			if(sharedParameters == null) return;

			useInitialGuard = sharedParameters.useInitialGuard;
			initialGuardDuration = sharedParameters.initialGuardDuration;

			enableLongPress = sharedParameters.enableLongPress;
			longPressDuration = sharedParameters.longPressDuration;

			useMultipleInputGuard = sharedParameters.useMultipleInputGuard;
			multipleInputGuardInterval = sharedParameters.multipleInputGuardInterval;

			textTransition = sharedParameters.textTransition;
			textColors = sharedParameters.textColors;
			textSwapState = sharedParameters.textSwapState;
			textAnimationTriggers = sharedParameters.textAnimationTriggers;

			m_useCustomAnimation = sharedParameters.useCustomAnimation;
			m_disableInteractableDuringAnimation = sharedParameters.disableInteractableDuringAnimation;
			m_clickAnimations = sharedParameters.clickAnimations;
		}
		#endregion

		#region Properties
		/// <summary> 長押し進捗（0〜1） </summary>
		public float LongPressProgress { get; private set; }

		/// <summary> テキスト差し替え状態の参照と設定 </summary>
		public TextSwapState TextSwapState {
			get => textSwapState;
			set => textSwapState = value;
		}

		/// <summary> 右クリックイベント </summary>
		public UnityEvent OnRightClick => onRightClick;

		/// <summary> 長押し成立イベント </summary>
		public UnityEvent OnLongPress => onLongPress;

		/// <summary> 長押しキャンセルイベント </summary>
		public UnityEvent OnLongPressCancel => onLongPressCancel;
		#endregion

		#region Fields
		private RectTransform m_rectTransform;                      // 自身のRectTransformキャッシュ
		private float _lastAcceptedClickTime = -999f;               // 最後に受理した入力時刻
		private float _initialGuardEndTime = -999f;                 // 有効化直後のガード解除時刻
		private bool _pressAccepted;                                // 現在の押下を処理対象にするか
		private bool _isPointerDown;                                // ポインタ押下中か
		private bool _longPressTriggered;                           // 長押しが成立したか
		private float _pointerDownTime;                             // 押下開始時間
		private bool _loggedInvalidLongPressImage;                  // 不正なImageタイプ警告を出したか
		private CancellationTokenSource _textColorTransitionCts;    // テキストカラー遷移のCTS
		private RectTransformValues? m_originalRectTransformValues; // アニメーション用に保存した初期RectTransform値
		private CancellationTokenSource _clickAnimationCts;         // クリックアニメーション用CTS
		#endregion

		#region Unity Methods
		/// <summary> 有効化時に初期化とRectTransformの初期値取得を行う </summary>
		protected override async void OnEnable() {
			ApplySharedParametersIfNeeded();
			base.OnEnable();

			if(!Application.isPlaying) return;

			m_rectTransform = transform as RectTransform;

			ResetPressState();
			StartInitialGuard();

			var capturedRectTransformValues = await aGuiUtils.CaptureInitialRectTransformAsync(
				m_rectTransform,
				m_originalRectTransformValues,
				destroyCancellationToken,
				false);
			if(this == null || this.Equals(null)) return; // 破棄済みなら以降の処理をスキップ
			m_originalRectTransformValues = capturedRectTransformValues;
		}

		/// <summary> 無効化時に状態リセットと長押しキャンセルを実行 </summary>
		protected override void OnDisable() {
			base.OnDisable();

			if(!Application.isPlaying) return;

			TryInvokeLongPressCancel();
			CancelClickAnimations();
			ResetPressState();
		}

		/// <summary> フレーム更新で長押しの進捗を監視 </summary>
		private void Update() {
			if(!enableLongPress || !_pressAccepted || !_isPointerDown || _longPressTriggered) return;
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			LongPressProgress = Mathf.Clamp01((now - _pointerDownTime) / longPressDuration);
			UpdateLongPressImage();

			// 規定時間を超えたら長押し成立
			if(now - _pointerDownTime >= longPressDuration) {
				_longPressTriggered = true;
				LongPressProgress = 1f;
				UpdateLongPressImage();
				onLongPress?.Invoke();
			}
		}

		/// <summary> ポインタ押下時の処理。ガード判定と長押し開始を管理する </summary>
		public override void OnPointerDown(PointerEventData eventData) {
			base.OnPointerDown(eventData);
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;

			if(IsGuardActive(now)) return;

			StartGuard(now);

			// 右クリックはそのまま受理のみ（解放時のイベントはなし）
			if(eventData.button == PointerEventData.InputButton.Right) {
				_pressAccepted = true;
				return;
			}

			if(eventData.button != PointerEventData.InputButton.Left) return;

			_pressAccepted = true;
			_isPointerDown = true;
			_longPressTriggered = false;
			_pointerDownTime = now;
			LongPressProgress = 0f;
			UpdateLongPressImage();
		}

		/// <summary> ポインタ解放時の処理。長押しキャンセル判定を行う </summary>
		public override void OnPointerUp(PointerEventData eventData) {
			base.OnPointerUp(eventData);
			if(eventData.button != PointerEventData.InputButton.Left) return;

			if(!_pressAccepted) return;

			TryInvokeLongPressCancel();

			_isPointerDown = false;
			LongPressProgress = 0f;
			UpdateLongPressImage();
		}

		/// <summary> クリック成立時の処理。長押し成立済みなら通常クリックを抑制 </summary>
		public override void OnPointerClick(PointerEventData eventData) {
			if(!IsActive() || !IsInteractable()) return;

			if(!_pressAccepted) return;

			if(eventData.button == PointerEventData.InputButton.Right) {
				onRightClick?.Invoke();
				_pressAccepted = false;
				return;
			}

			if(eventData.button != PointerEventData.InputButton.Left) return;

			// 長押しが成立していた場合はクリック系を発火しない
			if(_longPressTriggered) {
				_pressAccepted = false;
				return;
			}

			base.OnPointerClick(eventData);
			PlayClickAnimationsAsync().Forget();
			_pressAccepted = false;
		}

		/// <summary> Submit入力時にガードを適用 </summary>
		public override void OnSubmit(BaseEventData eventData) {
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			base.OnSubmit(eventData);
		}

		/// <summary> 選択状態の遷移に合わせてテキストの見た目を更新 </summary>
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

		#region Methods
		private async UniTask PlayClickAnimationsAsync() {
			if(!m_useCustomAnimation) return;
			if(m_clickAnimations == null || m_clickAnimations.Length == 0) return;
			if(m_rectTransform == null || m_originalRectTransformValues == null) return;

			CancelClickAnimations();
			_clickAnimationCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			var token = _clickAnimationCts.Token;

			try {
				var original = m_originalRectTransformValues.Value;
				await aGuiUtils.PlayAnimationsWithInteractableGuardAsync(this, m_disableInteractableDuringAnimation, m_clickAnimations, targetGraphic, m_rectTransform, original, token);
			} catch (OperationCanceledException) {
				return;
			} finally {
				_clickAnimationCts?.Dispose();
				_clickAnimationCts = null;
			}
		}

		private void CancelClickAnimations() {
			if(_clickAnimationCts == null) return;
			_clickAnimationCts.Cancel();
			_clickAnimationCts.Dispose();
			_clickAnimationCts = null;
		}

		/// <summary> 長押し進捗をImageに反映（Filledタイプのみ） </summary>
		private void UpdateLongPressImage() {
			if(longPressImage == null) return;

			if(longPressImage.type != Image.Type.Filled) {
				if(!_loggedInvalidLongPressImage) {
					_loggedInvalidLongPressImage = true;
					Debug.LogWarning($"[{nameof(aButton)}] LongPressImage must be of type Filled.", this);
				}
				return;
			}

			longPressImage.fillAmount = LongPressProgress;
		}

		/// <summary> 長押しが未成立のまま解放された際にキャンセルイベントを発火 </summary>
		private void TryInvokeLongPressCancel() {
			if(!_pressAccepted) return;
			if(!enableLongPress) return;
			if(!_isPointerDown) return;
			if(_longPressTriggered) return;

			onLongPressCancel?.Invoke();
		}

		/// <summary> 入力ガードが有効かを判定し、必要ならデバッグログを出す </summary>
		private bool IsGuardActive(float now) {
			if(useInitialGuard && now < _initialGuardEndTime) {
				#if UNITY_EDITOR
				Debug.Log($"[{nameof(aButton)}] Initial Guard active. Input blocked until {_initialGuardEndTime:F3} (remaining {_initialGuardEndTime - now:F3}s).", this);
				#endif
				return true;
			}
			if(!useMultipleInputGuard) return false;

			bool active = (now - _lastAcceptedClickTime) < multipleInputGuardInterval;
			#if UNITY_EDITOR
			if(active) {
				Debug.Log($"[{nameof(aButton)}] Multiple Input Guard active. Input blocked.", this);
			}
			#endif
			return active;
		}

		/// <summary> 入力ガードを開始し、次の入力までのブロック時間を記録 </summary>
		private void StartGuard(float now) {
			_lastAcceptedClickTime = now;
		}

		/// <summary> 有効化直後の入力を一定時間抑制 </summary>
		private void StartInitialGuard() {
			if(!useInitialGuard || initialGuardDuration <= 0f) {
				_initialGuardEndTime = -999f;
				return;
			}
			_initialGuardEndTime = Time.unscaledTime + initialGuardDuration;
		}

		/// <summary> 押下状態などの入力フラグを初期化 </summary>
		private void ResetPressState() {
			_pressAccepted = false;
			_isPointerDown = false;
			_longPressTriggered = false;
			_pointerDownTime = 0f;
			LongPressProgress = 0f;
			_loggedInvalidLongPressImage = false;
			aGuiUtils.StopTextColorTransition(ref _textColorTransitionCts);
			UpdateLongPressImage();
		}
		#endregion


		#if UNITY_EDITOR
		protected　override void OnValidate() {
			base.OnValidate();

			ApplySharedParametersIfNeeded();
		}
		#endif
	}
}
