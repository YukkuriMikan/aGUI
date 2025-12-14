using System;
using System.Collections;
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
		private Coroutine _textColorTransitionCoroutine;                  // テキストカラー遷移のコルーチン
		private CancellationTokenSource _toggleAnimationCts;              // トグルアニメーション用CTS
		#endregion

		#region Unity Methods
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

		protected override void OnDisable() {
			base.OnDisable();

			if(!Application.isPlaying) return;

			RegisterToggleListener(false);
			ResetTextColorCoroutine();
			CancelToggleAnimations();
		}

		public override void OnPointerClick(PointerEventData eventData) {
			if(eventData.button != PointerEventData.InputButton.Left) return;
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			TryPlayAnimations(m_clickAnimations, targetGraphic, m_rectTransform, m_originalRectTransformValues);

			base.OnPointerClick(eventData);
		}

		public override void OnSubmit(BaseEventData eventData) {
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			base.OnSubmit(eventData);
		}

		protected override void DoStateTransition(SelectionState state, bool instant) {
			base.DoStateTransition(state, instant);
			if(targetText == null) return;

			switch(textTransition) {
				case TextTransitionType.TextColor:
					ApplyTextColorTransition(state, instant);
					break;
				case TextTransitionType.TextSwap:
					ApplyTextSwapTransition(state);
					break;
				case TextTransitionType.TextAnimation:
					ApplyTextAnimationTransition(state);
					break;
			}
		}
		#endregion

		#region Toggle Events
		private void RegisterToggleListener(bool register) {
			if(register) {
				onValueChanged.AddListener(OnToggleValueChanged);
			} else {
				onValueChanged.RemoveListener(OnToggleValueChanged);
			}
		}

		private void OnToggleValueChanged(bool isOn) {
			if(!m_useCustomAnimation) return;
			_ = PlayToggleAnimationsAsync(isOn);
		}
		#endregion

		#region Toggle Animation Async
		private async UniTask PlayToggleAnimationsAsync(bool isOn) {
			CancelToggleAnimations();
			_toggleAnimationCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			var token = _toggleAnimationCts.Token;

			RectTransform target = m_targetRectTransform != null ? m_targetRectTransform : m_rectTransform;
			RectTransformValues? original = m_targetRectTransform != null ? m_originalTargetRectTransformValues : m_originalRectTransformValues;

			if(target == null || original == null) return;

			// ONの場合は従来通り再生のみ
			if(isOn) {
				TryPlayAnimations(m_onAnimations, graphic, target, original);
				return;
			}

			// OFFの場合は非表示を遅延させるため、アニメ終了を待つ
			CanvasRenderer renderer = graphic != null ? graphic.canvasRenderer : null;

			// 標準のフェード開始で非表示にならないよう、まずAlphaを1に戻す
			if(graphic != null) {
				graphic.CrossFadeAlpha(1f, 0f, true);
				graphic.canvasRenderer.SetAlpha(1f);
			} else if(renderer != null) {
				renderer.SetAlpha(1f);
			}

			// グラフィックはON状態のままにしておき、アニメ完了後に非表示へ
			TryPlayAnimations(m_offAnimations, graphic, target, original);

			// すべてのカスタムアニメーションのDurationとDelayを考慮した最大時間を待つ
			float waitSec = CalcMaxAnimationTime(m_offAnimations);
			if(waitSec > 0f) {
				try {
					await UniTask.Delay(TimeSpan.FromSeconds(waitSec), DelayType.DeltaTime, cancellationToken: token);
				} catch (OperationCanceledException) {
					return;
				}
			}

			// トグル標準の非表示処理を適用
			if(graphic != null) {
				graphic.CrossFadeAlpha(0f, 0f, true);
				graphic.canvasRenderer.SetAlpha(0f);
			} else if(renderer != null) {
				renderer.SetAlpha(0f);
			}
		}

		private float CalcMaxAnimationTime(IUiAnimation[] animations) {
			if(animations == null || animations.Length == 0) return 0f;
			float max = 0f;
			for (int i = 0; i < animations.Length; i++) {
				var anim = animations[i];
				if(anim == null) continue;
				float t = Mathf.Max(0f, anim.Delay) + Mathf.Max(0f, anim.Duration);
				if(t > max) max = t;
			}
			return max;
		}

		private void CancelToggleAnimations() {
			if(_toggleAnimationCts == null) return;
			_toggleAnimationCts.Cancel();
			_toggleAnimationCts.Dispose();
			_toggleAnimationCts = null;
		}
		#endregion

		#region Guards
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

		private void StartGuard(float now) {
			_lastAcceptedClickTime = now;
		}

		private void StartInitialGuard() {
			if(!useInitialGuard || initialGuardDuration <= 0f) {
				_initialGuardEndTime = -999f;
				return;
			}
			_initialGuardEndTime = Time.unscaledTime + initialGuardDuration;
		}
		#endregion

		#region Text Transition
		private void ApplyTextColorTransition(SelectionState state, bool instant) {
			Color targetColor = GetStateColor(state);
			float duration = instant ? 0f : textColors.fadeDuration;

			ResetTextColorCoroutine();

			if(duration <= 0f || !targetText.gameObject.activeInHierarchy) {
				SetTextColorImmediate(targetColor);
				return;
			}

			_textColorTransitionCoroutine = StartCoroutine(FadeTextColor(targetColor, duration));
		}

		private IEnumerator FadeTextColor(Color targetColor, float duration) {
			Color startColor = targetText.color;
			float elapsed = 0f;

			while (elapsed < duration) {
				if(targetText == null) {
					yield break;
				}

				elapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				SetTextColorImmediate(Color.Lerp(startColor, targetColor, t));
				yield return null;
			}

			_textColorTransitionCoroutine = null;
		}

		private void ResetTextColorCoroutine() {
			if(_textColorTransitionCoroutine != null) {
				StopCoroutine(_textColorTransitionCoroutine);
				_textColorTransitionCoroutine = null;
			}
		}

		private void SetTextColorImmediate(Color color) {
			targetText.canvasRenderer.SetColor(color);
			targetText.color = color;
		}

		private Color GetStateColor(SelectionState state) {
			return state switch {
				SelectionState.Disabled => textColors.disabledColor,
				SelectionState.Highlighted => textColors.highlightedColor,
				SelectionState.Pressed => textColors.pressedColor,
				SelectionState.Selected => textColors.selectedColor,
				_ => textColors.normalColor
			};
		}

		private void ApplyTextSwapTransition(SelectionState state) {
			string text = GetStateText(state);
			targetText.text = text;
		}

		private string GetStateText(SelectionState state) {
			return state switch {
				SelectionState.Disabled => string.IsNullOrEmpty(textSwapState.disabledText) ? textSwapState.normalText : textSwapState.disabledText,
				SelectionState.Highlighted => string.IsNullOrEmpty(textSwapState.highlightedText) ? textSwapState.normalText : textSwapState.highlightedText,
				SelectionState.Pressed => string.IsNullOrEmpty(textSwapState.pressedText) ? textSwapState.normalText : textSwapState.pressedText,
				SelectionState.Selected => string.IsNullOrEmpty(textSwapState.selectedText) ? textSwapState.normalText : textSwapState.selectedText,
				_ => textSwapState.normalText
			};
		}

		private void ApplyTextAnimationTransition(SelectionState state) {
			Animator animator = textAnimator != null ? textAnimator : targetText.GetComponent<Animator>();
			if(animator == null || animator.runtimeAnimatorController == null) return;

			switch(state) {
				case SelectionState.Disabled:
					PlayTextAnimation(animator, textAnimationTriggers.disabledTrigger);
					break;
				case SelectionState.Highlighted:
					PlayTextAnimation(animator, textAnimationTriggers.highlightedTrigger);
					break;
				case SelectionState.Pressed:
					PlayTextAnimation(animator, textAnimationTriggers.pressedTrigger);
					break;
				case SelectionState.Selected:
					PlayTextAnimation(animator, textAnimationTriggers.selectedTrigger);
					break;
				default:
					PlayTextAnimation(animator, textAnimationTriggers.normalTrigger);
					break;
			}
		}

		private static void PlayTextAnimation(Animator animator, string stateName) {
			if(string.IsNullOrEmpty(stateName)) return;
			animator.ResetTrigger(stateName);
			animator.SetTrigger(stateName);
		}
		#endregion

		#region Animation
		private void TryPlayAnimations(IUiAnimation[] animations, Graphic targetGraphic, RectTransform target, RectTransformValues? originalValues) {
			if(!m_useCustomAnimation) return;
			if(animations == null || animations.Length == 0) return;
			if(target == null) return;
			if(originalValues == null) return;

			var original = originalValues.Value;
			for (int i = 0; i < animations.Length; i++) {
				var anim = animations[i];
				#if UNITY_EDITOR
				Debug.Log($"[{nameof(aToggle)}] Animation[{i}] 再生開始: {anim?.GetType().Name ?? "null"}", this);
				#endif
				anim?.DoAnimate(targetGraphic, target, original).Forget();
			}
		}

		private async UniTask CaptureInitialRectTransformsAsync() {
			var rectTrans = transform as RectTransform;
			if(rectTrans != null && m_originalRectTransformValues == null) {
				try {
					await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: destroyCancellationToken);
					await UniTask
						.WaitUntil(() => rectTrans.rect.width > 1f, cancellationToken: destroyCancellationToken)
						.Timeout(TimeSpan.FromSeconds(0.1f));

					m_originalRectTransformValues = RectTransformValues.CreateValues(transform);
				} catch (OperationCanceledException) {
					// Disable/Destroy などでキャンセルされた場合はログを出さない
				} catch (TimeoutException) {
					Debug.LogWarning($"{gameObject.name}: 最初のRectTransformの値を取得出来ませんでした (timeout)", gameObject);
					// 取得できなかった場合でも現在値を保持してアニメーションが動くようにする
					m_originalRectTransformValues = RectTransformValues.CreateValues(transform);
				}
			}

			if(m_targetRectTransform != null && m_originalTargetRectTransformValues == null) {
				try {
					await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: destroyCancellationToken);
					await UniTask
						.WaitUntil(() => m_targetRectTransform.rect.width > 1f, cancellationToken: destroyCancellationToken)
						.Timeout(TimeSpan.FromSeconds(0.1f));

					m_originalTargetRectTransformValues = RectTransformValues.CreateValues(m_targetRectTransform);
				} catch (OperationCanceledException) {
					// Disable/Destroy などでキャンセルされた場合はログを出さない
				} catch (TimeoutException) {
					Debug.LogWarning($"{gameObject.name}: TargetGraphicのRectTransformの初期値を取得出来ませんでした (timeout)", gameObject);
					// 取得できなかった場合でも現在値を保持してアニメーションが動くようにする
					m_originalTargetRectTransformValues = RectTransformValues.CreateValues(m_targetRectTransform);
				}
			}
		}
		#endregion
	}
}
