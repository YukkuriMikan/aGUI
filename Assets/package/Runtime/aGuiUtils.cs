using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>
	/// aGUI の共通ユーティリティ
	/// </summary>
	public static class aGuiUtils {
		/// <summary> RectTransform の初期値を取得する（既に取得済みならそのまま返す） </summary>
		/// <param name="rectTransform">対象のRectTransform</param>
		/// <param name="cachedValues">既に保持している初期値</param>
		/// <param name="ct">キャンセルトークン</param>
		/// <param name="fallbackToCurrentOnTimeout">タイムアウト時に現在値で代替取得するか</param>
		public static async UniTask<RectTransformValues?> CaptureInitialRectTransformAsync(
			RectTransform rectTransform,
			RectTransformValues? cachedValues,
			CancellationToken ct,
			bool fallbackToCurrentOnTimeout = true) {
			if(rectTransform == null) return cachedValues;
			if(cachedValues != null) return cachedValues;

			try {
				await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: ct);
				await UniTask
					.WaitUntil(() => rectTransform.rect.width > 1f, cancellationToken: ct)
					.Timeout(TimeSpan.FromSeconds(0.1f));

				return RectTransformValues.CreateValues(rectTransform);
			} catch (OperationCanceledException) {
				return cachedValues;
			} catch (TimeoutException) {
                #if UNITY_EDITOR
				string ownerName = rectTransform != null ? rectTransform.gameObject.name : "(null)";
				Debug.LogWarning($"{ownerName}: RectTransformの初期値を取得出来ませんでした (timeout)", rectTransform);
                #endif
				return fallbackToCurrentOnTimeout
					? RectTransformValues.CreateValues(rectTransform)
					: cachedValues;
			}
		}

		/// <summary> 複数の UI アニメーションをまとめて再生・待機する </summary>
		/// <param name="animations">再生するアニメーション配列</param>
		/// <param name="targetGraphic">アニメーション対象の Graphic</param>
		/// <param name="targetRect">アニメーション対象の RectTransform</param>
		/// <param name="original">RectTransform の初期値</param>
		/// <param name="ct">キャンセルトークン</param>
		public static async UniTask PlayAnimationsAsync(IUiAnimation[] animations, Graphic targetGraphic, RectTransform targetRect, RectTransformValues original, CancellationToken ct) {
			if(animations == null || animations.Length == 0) return;
			if(targetRect == null) return;

			var tasks = new List<UniTask>();
			for (int i = 0; i < animations.Length; i++) {
				var anim = animations[i];
				if(anim == null) continue;
				tasks.Add(anim.DoAnimate(targetGraphic, targetRect, original, ct).AttachExternalCancellation(ct));
			}

			if(tasks.Count == 0) return;
			await UniTask.WhenAll(tasks);
		}

		/// <summary> アニメーション実行中はinteractableを抑止しつつ再生・待機する </summary>
		/// <param name="selectable">操作抑止対象のSelectable</param>
		/// <param name="disableInteractableDuringAnimation">アニメーション中にinteractableを無効にするか</param>
		/// <param name="animations">再生するアニメーション配列</param>
		/// <param name="targetGraphic">アニメーション対象のGraphic</param>
		/// <param name="targetRect">アニメーション対象のRectTransform</param>
		/// <param name="original">RectTransformの初期値</param>
		/// <param name="ct">キャンセルトークン</param>
		/// <param name="logContext">ログ出力用のコンテキスト</param>
		public static async UniTask PlayAnimationsWithInteractableGuardAsync(
			Selectable selectable,
			bool disableInteractableDuringAnimation,
			IUiAnimation[] animations,
			Graphic targetGraphic,
			RectTransform targetRect,
			RectTransformValues original,
			CancellationToken ct) {

			if(animations == null || animations.Length == 0) return;
			if(targetRect == null) return;

			bool previousInteractable = selectable != null && selectable.interactable;
			bool disableInteraction = disableInteractableDuringAnimation && previousInteractable;
			ColorBlock previousColors = default;
			bool disabledColorOverridden = false;
			if(disableInteraction && selectable != null) {
				previousColors = selectable.colors;
				if(previousColors.disabledColor != Color.white) {
					var tempColors = previousColors;
					tempColors.disabledColor = Color.white;
					selectable.colors = tempColors;
					disabledColorOverridden = true;
				}
				selectable.interactable = false;
			}

			try {
                #if UNITY_EDITOR
				for (int i = 0; i < animations.Length; i++) {
					var anim = animations[i];
					string ownerName = selectable.GetType().Name;
					Debug.Log($"[{ownerName}] Animation[{i}] 再生開始: {anim?.GetType().Name ?? "null"}", selectable);
				}
                #endif
				await PlayAnimationsAsync(animations, targetGraphic, targetRect, original, ct);
			} catch (OperationCanceledException) {
				// キャンセル時は何もしない
			} finally {
				if(disabledColorOverridden && selectable != null) {
					selectable.colors = previousColors;
				}
				if(disableInteraction && previousInteractable && !ct.IsCancellationRequested && selectable != null) {
					selectable.interactable = true;
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
