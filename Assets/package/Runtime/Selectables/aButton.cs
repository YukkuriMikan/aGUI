using System;
using System.Threading;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ANest.UI {
	/// <summary>複数の入力ガードや長押し対応を備えた拡張Button</summary>
	[RequireComponent(typeof(aGuiInfo))]
	public class aButton : Button {
		#region Serialize Fields
		[Header("Shared Parameters")]
		[Tooltip("共通パラメータを使用するかどうか")]
		[SerializeField] private bool useSharedParameters; // 共通パラメータを使用するか
		[Tooltip("共通パラメータの参照")]
		[SerializeField] private aSelectablesSharedParameters sharedParameters; // 共通パラメータの参照

		[Header("Initial Guard")]
		[Tooltip("有効化直後の入力を抑制するかどうか")]
		[SerializeField] private bool useInitialGuard = false; // 有効化直後の入力を抑制するか、基本的にContainer側の機能を使うのでデフォルトはfalse
		[Tooltip("有効化直後に抑制する秒数")]
		[SerializeField] private float initialGuardDuration = 0.05f; // 有効化直後に抑制する秒数

		[Header("Multiple Input Guard")]
		[Tooltip("入力ガード（連打防止）を使用するかどうか")]
		[SerializeField] private bool useMultipleInputGuard = true; // 入力ガードを使うか
		[Tooltip("入力ガードの待機秒数")]
		[SerializeField] private float multipleInputGuardInterval = 0.5f; // 入力ガードの待機秒数

		[Header("ShortCut")]
		[Tooltip("ショートカット入力の設定")]
		[SerializeReference, SerializeReferenceDropdown] private IShortCut shortCut; // ショートカット入力

		[Header("Text Transition")]
		[Tooltip("状態遷移に合わせてテキストを変更する対象")]
		[SerializeField] private TMP_Text targetText; // 遷移対象のテキスト
		[Tooltip("テキスト遷移の種別")]
		[SerializeField] private TextTransitionType textTransition = TextTransitionType.TextColor; // テキスト遷移種別
		[Tooltip("テキストカラー設定")]
		[SerializeField] private ColorBlock textColors = ColorBlock.defaultColorBlock; // テキストカラー設定
		[Tooltip("テキスト差し替え設定")]
		[SerializeField] private TextSwapState textSwapState; // テキスト差し替え設定
		[Tooltip("テキストアニメーション用トリガー")]
		[SerializeField] private AnimationTriggers textAnimationTriggers = new(); // テキストアニメーション用トリガー
		[Tooltip("テキスト用アニメーター")]
		[SerializeField] private Animator textAnimator; // テキスト用アニメーター

		[Header("Animation")]
		[Tooltip("個別のアニメーション設定を使用するかどうか")]
		[SerializeField] private bool m_useCustomAnimation; // カスタムアニメーションを使用するか
		[Tooltip("共通のアニメーション設定を使用するかどうか")]
		[SerializeField] private bool m_useSharedAnimation = false; // 共有アニメーションを使用するか
		[Tooltip("共通のアニメーション設定")]
		[SerializeField] private UiAnimationSet m_sharedAnimation; // 共有アニメーション設定
		[Tooltip("クリック時に再生するアニメーション")]
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_clickAnimations; // クリック時のカスタムアニメーション

		[Header("Long Press")]
		[Tooltip("長押しを有効にするかかどうか")]
		[SerializeField] private bool enableLongPress = false; // 長押しを有効にするか
		[Tooltip("長押し成立までの時間（秒）")]
		[SerializeField] private float longPressDuration = 0.5f; // 長押し成立までの時間（秒）
		[Tooltip("長押し成立時のイベント")]
		[SerializeField] private UnityEvent onLongPress = new(); // 長押し成立イベント
		[Tooltip("長押しキャンセル時のイベント")]
		[SerializeField] private UnityEvent onLongPressCancel = new(); // 長押しキャンセルイベント
		[Tooltip("長押し進捗を反映するImage（Filledタイプを推奨）")]
		[SerializeField] private Image longPressImage; // 長押し進捗を反映するImage

		[Header("Events")]
		[Tooltip("右クリック時のイベント")]
		[SerializeField] private UnityEvent onRightClick = new(); // 右クリック用イベント

		[Header("Reference")]
		[Tooltip("GUI情報の参照")]
		[SerializeField] private aGuiInfo m_guiInfo; // GUI情報の参照
		#endregion

		#region Private Fields
		private float _lastAcceptedClickTime = -999f;             // 最後に受理した入力時刻
		private float _initialGuardEndTime = -999f;               // 有効化直後のガード解除時刻
		private bool _pressAccepted;                              // 現在の押下を処理対象にするか
		private bool _isPointerDown;                              // ポインタ押下中か
		private bool _longPressTriggered;                         // 長押しが成立したか
		private float _pointerDownTime;                           // 押下開始時間
		private bool _loggedInvalidLongPressImage;                // 不正なImageタイプ警告を出したか
		private CancellationTokenSource _textColorTransitionCts;  // テキストカラー遷移のCTS
		private bool _submitPressActive;                          // Submit入力による押下中か
		private BaseEventData _cachedSubmitEventData;             // Submit入力を後段処理に渡すためのキャッシュ
		private object _inputSystemSubmitAction;                  // Input SystemのSubmitアクション（リフレクションで扱う）
		private bool _inputSystemSubmitModuleActive;              // Input System UI Module経由のSubmit入力か
		private bool _shortCutPressed;                            // ショートカット入力の押下状態
		private static Type s_inputSystemUIModuleType;            // InputSystemUIInputModule型キャッシュ
		private static Type s_inputActionReferenceType;           // InputActionReference型キャッシュ
		private static Type s_inputActionType;                    // InputAction型キャッシュ
		private static MethodInfo s_inputActionWasReleasedMethod; // WasReleasedThisFrameメソッドキャッシュ
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

		#region Lifecycle Methods
		/// <summary> 有効化時に初期化とRectTransformの初期値取得を行う </summary>
		protected override void OnEnable() {
			ApplySharedParametersIfNeeded();
			ResetPressState();
			StartInitialGuard();

			base.OnEnable();
		}

		/// <summary> 無効化時に状態リセットと長押しキャンセルを実行 </summary>
		protected override void OnDisable() {
			base.OnDisable();

			if(!Application.isPlaying) return;

			TryInvokeLongPressCancel();
			ResetPressState();
		}

		/// <summary> フレーム更新で長押しの進捗を監視 </summary>
		private void Update() {
			if(!Application.isPlaying) return;

			UpdateSubmitPressState();
			UpdateShortCutState();

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
		#endregion

		#region Pointer Events
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
			PlayClickAnimations();
			_pressAccepted = false;
		}
		#endregion

		#region Input Events
		/// <summary> Submit入力時にガードを適用 </summary>
		public override void OnSubmit(BaseEventData eventData) {
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);

			if(!enableLongPress) {
				base.OnSubmit(eventData);
				return;
			}

			// Submit入力でも長押しを開始できるようにポインタ押下と同等の状態をセット
			_pressAccepted = true;
			_isPointerDown = true;
			_longPressTriggered = false;
			_pointerDownTime = now;
			LongPressProgress = 0f;
			UpdateLongPressImage();
			_submitPressActive = true;
			var currentEventSystem = aGuiManager.EventSystem;
			_cachedSubmitEventData = eventData ?? (currentEventSystem != null ? new BaseEventData(currentEventSystem) : null);
			ConfigureInputSystemSubmitTracking(currentEventSystem);
		}
		#endregion

		#region Internal Logic
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

		/// <summary> クリック時のアニメーションを再生する </summary>
		private void PlayClickAnimations() {
			if(!m_useCustomAnimation && !m_useSharedAnimation) return;
			if(m_clickAnimations == null || m_clickAnimations.Length == 0) return;
			if(m_guiInfo == null) return;

			aGuiUtils.PlayAnimation(m_clickAnimations, m_guiInfo.RectTransform, targetGraphic, m_guiInfo.OriginalRectTransformValues);
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
			_submitPressActive = false;
			_cachedSubmitEventData = null;
			_inputSystemSubmitAction = null;
			_inputSystemSubmitModuleActive = false;
			_shortCutPressed = false;
			aGuiUtils.StopTextColorTransition(ref _textColorTransitionCts);
		}

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

			enableLongPress = sharedParameters.enableLongPress;
			longPressDuration = sharedParameters.longPressDuration;

			useMultipleInputGuard = sharedParameters.useMultipleInputGuard;
			multipleInputGuardInterval = sharedParameters.multipleInputGuardInterval;

			textTransition = sharedParameters.textTransition;
			textColors = sharedParameters.textColors;
			textSwapState = sharedParameters.textSwapState;
			textAnimationTriggers = sharedParameters.textAnimationTriggers;
		}
		#endregion

		#region ShortCut Support
		/// <summary> ショートカット入力状態の更新と押下・解放処理 </summary>
		private void UpdateShortCutState() {
			if(shortCut == null) return;

			bool isPressed = shortCut.IsPressed;

			if(isPressed && !_shortCutPressed) {
				HandleShortCutPress();
			} else if(!isPressed && _shortCutPressed) {
				HandleShortCutRelease();
			}

			_shortCutPressed = isPressed;
		}

		/// <summary> ショートカット押下開始時の処理 </summary>
		private void HandleShortCutPress() {
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			_pressAccepted = true;
			_isPointerDown = true;
			_longPressTriggered = false;
			_pointerDownTime = now;
			LongPressProgress = 0f;
			UpdateLongPressImage();
		}

		/// <summary> ショートカット押下終了時の処理 </summary>
		private void HandleShortCutRelease() {
			if(!_pressAccepted) {
				_shortCutPressed = false;
				return;
			}

			TryInvokeLongPressCancel();

			_isPointerDown = false;
			LongPressProgress = 0f;
			UpdateLongPressImage();

			if(!_longPressTriggered) {
				var eventSystem = aGuiManager.EventSystem;
				base.OnSubmit(eventSystem != null ? new BaseEventData(eventSystem) : null);
				PlayClickAnimations();
			}

			_pressAccepted = false;
		}
		#endregion

		#region Submit Support
		/// <summary> Submit入力の押下/解放を監視し、長押しとクリックの分岐を処理 </summary>
		private void UpdateSubmitPressState() {
			if(!_submitPressActive) return;

			if(_inputSystemSubmitModuleActive) {
				if(!TryGetInputSystemSubmitRelease(out bool isReleased)) return;
				if(isReleased) {
					ReleaseSubmitPress(!_longPressTriggered);
				}
				return;
			}

			if(!TryGetSubmitButtonRelease(out bool released)) {
				ReleaseSubmitPress(!_longPressTriggered);
				return;
			}

			if(released) {
				ReleaseSubmitPress(!_longPressTriggered);
			}
		}

		/// <summary> Submitボタン解放をEventSystem経由で取得 </summary>
		private bool TryGetSubmitButtonRelease(out bool isReleased) {
			isReleased = false;

			var eventSystem = aGuiManager.EventSystem;
			if(eventSystem == null) return false;

			if(eventSystem.currentInputModule is not StandaloneInputModule module) return false;

			var submitButton = module.submitButton;
			if(string.IsNullOrEmpty(submitButton)) submitButton = "Submit";

			isReleased = Input.GetButtonUp(submitButton);
			return true;
		}

		/// <summary> Submit解放時の後処理とクリック発火 </summary>
		private void ReleaseSubmitPress(bool triggerClick) {
			TryInvokeLongPressCancel();

			_isPointerDown = false;
			LongPressProgress = 0f;
			UpdateLongPressImage();

			if(triggerClick) {
				var eventSystem = aGuiManager.EventSystem;
				if(eventSystem != null) {
					base.OnSubmit(_cachedSubmitEventData ?? new BaseEventData(eventSystem));
				}
			}

			_pressAccepted = false;
			_submitPressActive = false;
			_cachedSubmitEventData = null;
			_inputSystemSubmitAction = null;
			_inputSystemSubmitModuleActive = false;
		}

		/// <summary> Input System環境におけるSubmit入力のトラッキング設定を行う </summary>
		private void ConfigureInputSystemSubmitTracking(EventSystem currentEventSystem) {
			_inputSystemSubmitAction = null;
			_inputSystemSubmitModuleActive = false;

			if(currentEventSystem == null) return;

			var module = currentEventSystem.currentInputModule;
			if(!IsInputSystemUIModule(module)) return;

			_inputSystemSubmitAction = GetInputSystemSubmitAction(module);
			_inputSystemSubmitModuleActive = _inputSystemSubmitAction != null;
		}

		/// <summary> 指定されたモジュールが InputSystemUIInputModule かどうかを判定する </summary>
		private bool IsInputSystemUIModule(object module) {
			var moduleType = GetInputSystemUIModuleType();
			return module != null && moduleType != null && moduleType.IsInstanceOfType(module);
		}

		/// <summary> InputSystemUIInputModule から Submit アクションを取得する </summary>
		private object GetInputSystemSubmitAction(object module) {
			var moduleType = GetInputSystemUIModuleType();
			if(moduleType == null || module == null) return null;

			var submitProperty = moduleType.GetProperty("submit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var actionReference = submitProperty?.GetValue(module);
			if(actionReference == null) return null;

			var actionRefType = GetInputActionReferenceType();
			var actionProperty = actionRefType?.GetProperty("action", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return actionProperty?.GetValue(actionReference);
		}

		/// <summary> Input System の Submit アクションが解放されたかどうかを取得する </summary>
		private bool TryGetInputSystemSubmitRelease(out bool isReleased) {
			isReleased = false;
			if(_inputSystemSubmitAction == null) return false;

			var actionType = GetInputActionType();
			if(actionType == null || !actionType.IsInstanceOfType(_inputSystemSubmitAction)) return false;

			s_inputActionWasReleasedMethod ??= actionType.GetMethod("WasReleasedThisFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if(s_inputActionWasReleasedMethod == null) return false;

			isReleased = (bool)s_inputActionWasReleasedMethod.Invoke(_inputSystemSubmitAction, null);
			return true;
		}

		/// <summary> InputSystemUIInputModule の Type を取得する </summary>
		private static Type GetInputSystemUIModuleType() {
			if(s_inputSystemUIModuleType == null) {
				s_inputSystemUIModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
			}
			return s_inputSystemUIModuleType;
		}

		/// <summary> InputActionReference の Type を取得する </summary>
		private static Type GetInputActionReferenceType() {
			if(s_inputActionReferenceType == null) {
				s_inputActionReferenceType = Type.GetType("UnityEngine.InputSystem.InputActionReference, Unity.InputSystem");
			}
			return s_inputActionReferenceType;
		}

		/// <summary> InputAction の Type を取得する </summary>
		private static Type GetInputActionType() {
			if(s_inputActionType == null) {
				s_inputActionType = Type.GetType("UnityEngine.InputSystem.InputAction, Unity.InputSystem");
			}
			return s_inputActionType;
		}
		#endregion

		#region Editor Support
		#if UNITY_EDITOR
		/// <summary> インスペクターでの値変更時の処理 </summary>
		protected override void OnValidate() {
			base.OnValidate();
			if(Application.isPlaying) return;

			ApplySharedParametersIfNeeded();
			ApplySharedAnimationsFromSet();

			if(m_guiInfo == null) {
				m_guiInfo = GetComponent<aGuiInfo>();
			}

			if(targetGraphic == null) {
				targetGraphic = GetComponentInChildren<Graphic>();
			}
		}

		/// <summary> 共有アニメーションセットからアニメーションを複製して適用する </summary>
		private void ApplySharedAnimationsFromSet() {
			if(!m_useSharedAnimation) return;
			if(m_sharedAnimation == null) return;

			m_clickAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.clickAnimations);

			UnityEditor.EditorUtility.SetDirty(this);
		}
		#endif
		#endregion
	}
}
