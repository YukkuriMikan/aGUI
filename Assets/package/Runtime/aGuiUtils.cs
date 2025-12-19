using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>
	/// aGUI の共通ユーティリティ
	/// </summary>
	public static class aGuiUtils {
		/// <summary> アニメーション配列のディープコピーを作成する </summary>
		public static IUiAnimation[] CloneAnimations(IUiAnimation[] animations) {
			if(animations == null) return null;
			if(animations.Length == 0) return Array.Empty<IUiAnimation>();

			var cloned = new IUiAnimation[animations.Length];
			for (int i = 0; i < animations.Length; i++) {
				var anim = animations[i];
				if(anim == null) continue;

				try {
					string json = JsonUtility.ToJson(anim);
					cloned[i] = (IUiAnimation)JsonUtility.FromJson(json, anim.GetType());
				} catch (Exception ex) {
#if UNITY_EDITOR
					Debug.LogWarning($"[{nameof(aGuiUtils)}] {anim.GetType().Name} のクローンに失敗しました: {ex.Message}");
#endif
					cloned[i] = anim;
				}
			}

			return cloned;
		}

		public static void PlayAnimation(IUiAnimation[] animations, RectTransform targetRect, Graphic targetGraphic, RectTransformValues originalValues, Action callback = null) {
			if(animations == null || animations.Length == 0) return;
			if(targetRect == null) return;

			if(targetGraphic != null) {
				targetGraphic.DOKill();
			}

			targetRect.DOKill();

			IUiAnimation lastEndAnim = null;
			float maxDuration = 0f;

			if(callback != null) {
				//最後に終わるアニメーションを取得
				for (int i = 0; i < animations.Length; i++) {
					var anim = animations[i];

					if(anim != null) {
						var animDuration = anim.Delay + anim.Duration;

						if(maxDuration < animDuration) {
							maxDuration = animDuration;
							lastEndAnim = anim;
						}
					}
				}
			}

			// それぞれのアニメーションを個別に起動
			for (int i = 0; i < animations.Length; i++) {
				var anim = animations[i];

				if(anim != null) {
					var tween = anim?.DoAnimate(targetGraphic, targetRect, originalValues);

					if(lastEndAnim == anim && callback != null) {
						tween.OnComplete(() => callback());
					}
				}
			}
		}

		/// <summary> ステートに応じてテキストカラー遷移を適用する </summary>
		public static void ApplyTextColorTransition(
			MonoBehaviour owner,
			TMP_Text targetText,
			ColorBlock colors,
			int selectionState,
			bool instant,
			ref CancellationTokenSource runningCts,
			Action onComplete) {
			if(owner == null || targetText == null) return;

			StopTextColorTransition(ref runningCts);

			Color targetColor = GetStateColor(colors, selectionState);
			float duration = instant ? 0f : colors.fadeDuration;

			if(duration <= 0f || !targetText.gameObject.activeInHierarchy) {
				SetTextColorImmediate(targetText, targetColor);
				onComplete?.Invoke();
				return;
			}

			var destroyToken = owner.GetCancellationTokenOnDestroy();
			runningCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
			var token = runningCts.Token;

			FadeTextColorAsync(targetText, targetColor, duration, token, onComplete).Forget();
		}

		/// <summary> 進行中のテキストカラー遷移を停止する </summary>
		public static void StopTextColorTransition(ref CancellationTokenSource runningCts) {
			if(runningCts == null) return;
			runningCts.Cancel();
			runningCts.Dispose();
			runningCts = null;
		}

		/// <summary> ステートに応じたテキスト内容を差し替える </summary>
		public static void ApplyTextSwapTransition(TMP_Text targetText, TextSwapState swapState, int selectionState) {
			if(targetText == null) return;
			targetText.text = GetStateText(swapState, selectionState);
		}

		/// <summary> ステートに応じてテキストアニメーションを再生する </summary>
		public static void ApplyTextAnimationTransition(TMP_Text targetText, Animator textAnimator, AnimationTriggers triggers, int selectionState) {
			Animator animator = textAnimator != null ? textAnimator : targetText != null ? targetText.GetComponent<Animator>() : null;
			if(animator == null || animator.runtimeAnimatorController == null) return;

			switch(selectionState) {
				case 4: // Disabled
					PlayTextAnimation(animator, triggers.disabledTrigger);
					break;
				case 1: // Highlighted
					PlayTextAnimation(animator, triggers.highlightedTrigger);
					break;
				case 2: // Pressed
					PlayTextAnimation(animator, triggers.pressedTrigger);
					break;
				case 3: // Selected
					PlayTextAnimation(animator, triggers.selectedTrigger);
					break;
				default:
					PlayTextAnimation(animator, triggers.normalTrigger);
					break;
			}
		}

		/// <summary> ステートに応じたColorBlockの色を取得する </summary>
		public static Color GetStateColor(ColorBlock colors, int selectionState) {
			switch(selectionState) {
				case 4: // Disabled
					return colors.disabledColor;
				case 1: // Highlighted
					return colors.highlightedColor;
				case 2: // Pressed
					return colors.pressedColor;
				case 3: // Selected
					return colors.selectedColor;
				default:
					return colors.normalColor;
			}
		}

		/// <summary> ステートに対応するテキストを返す </summary>
		public static string GetStateText(TextSwapState swapState, int selectionState) {
			switch(selectionState) {
				case 4: // Disabled
					return string.IsNullOrEmpty(swapState.disabledText) ? swapState.normalText : swapState.disabledText;
				case 1: // Highlighted
					return string.IsNullOrEmpty(swapState.highlightedText) ? swapState.normalText : swapState.highlightedText;
				case 2: // Pressed
					return string.IsNullOrEmpty(swapState.pressedText) ? swapState.normalText : swapState.pressedText;
				case 3: // Selected
					return string.IsNullOrEmpty(swapState.selectedText) ? swapState.normalText : swapState.selectedText;
				default:
					return swapState.normalText;
			}
		}

		/// <summary> テキストカラーを即時反映する </summary>
		public static void SetTextColorImmediate(TMP_Text targetText, Color color) {
			if(targetText == null) return;
			targetText.canvasRenderer.SetColor(color);
			targetText.color = color;
		}

		private static async UniTask FadeTextColorAsync(TMP_Text targetText, Color targetColor, float duration, CancellationToken ct, Action onComplete) {
			if(targetText == null) {
				onComplete?.Invoke();
				return;
			}

			Color startColor = targetText.color;
			float elapsed = 0f;

			while (elapsed < duration) {
				if(targetText == null) {
					onComplete?.Invoke();
					return;
				}

				elapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				SetTextColorImmediate(targetText, Color.Lerp(startColor, targetColor, t));
				await UniTask.Yield(PlayerLoopTiming.Update, ct);
			}

			if(targetText != null) {
				SetTextColorImmediate(targetText, targetColor);
			}

			onComplete?.Invoke();
		}

		private static void PlayTextAnimation(Animator animator, string stateName) {
			if(string.IsNullOrEmpty(stateName)) return;
			animator.ResetTrigger(stateName);
			animator.SetTrigger(stateName);
		}
	}
}
