using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aGUIの共通ユーティリティ</summary>
	public static class aGuiUtils {
		#region Public Method
		
		/// <summary>アニメーション配列のディープコピーを作成する</summary>
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

		/// <summary>指定されたオブジェクトに対してアニメーションを再生する</summary>
		public static void PlayAnimation(IUiAnimation[] animations, RectTransform targetRect, Graphic targetGraphic, RectTransformValues originalValues, Action completeCallback = null, Action killCallback = null) {
			// アニメーション配列が空でも、実行中の逆方向アニメーションは停止して中断仕様を保つ
			if(targetGraphic != null) {
				targetGraphic.DOKill();
			}

			if(targetRect != null) {
				targetRect.DOKill();
			}

			if(animations == null || animations.Length == 0 || targetRect == null) {
				completeCallback?.Invoke();
				return;
			}

			Tween lastEndTween = null;
			float maxDuration = -1f;

			// 実際に生成できたTweenの中から最後に終わるものを選ぶ。
			for(var i = 0; i < animations.Length; i++) {
				var anim = animations[i];
				if(anim == null) continue;
				var tween = anim.DoAnimate(targetGraphic, targetRect, originalValues);
				if(tween == null || !tween.IsActive()) continue;
				if(completeCallback == null && killCallback == null) continue;

				var duration = tween.Delay() + tween.Duration(true);
				if(lastEndTween == null || !lastEndTween.IsActive() || duration >= maxDuration) {
					maxDuration = duration;
					lastEndTween = tween;
				}
			}

			if(lastEndTween == null || !lastEndTween.IsActive()) {
				completeCallback?.Invoke();
				return;
			}

			AttachAnimationCallbacks(lastEndTween, completeCallback, killCallback);
		}

		private static void AttachAnimationCallbacks(Tween lastEndTween, Action completeCallback, Action killCallback) {
			AnimationCallbacks.Attach(lastEndTween, completeCallback, killCallback);
		}

		// Tweenごとに独立した通知状態を持ち、Kill後に再利用する。
		// 既存の通知は合成デリゲートを生成せずに引き継ぐ。
		private sealed class AnimationCallbacks {
			private static readonly System.Collections.Generic.Stack<AnimationCallbacks> s_pool = new();
			private readonly TweenCallback m_onComplete;
			private readonly TweenCallback m_onKill;
			private TweenCallback m_originalComplete;
			private TweenCallback m_originalKill;
			private Action m_complete;
			private Action m_kill;
			private bool m_notified;
			private bool m_killed;
			private int m_callbackDepth;

			private AnimationCallbacks() {
				m_onComplete = OnComplete;
				m_onKill = OnKill;
			}

			public static void Attach(Tween tween, Action complete, Action kill) {
				var callbacks = s_pool.Count > 0 ? s_pool.Pop() : new AnimationCallbacks();
				callbacks.m_originalComplete = tween.onComplete;
				callbacks.m_originalKill = tween.onKill;
				callbacks.m_complete = complete;
				callbacks.m_kill = kill;
				callbacks.m_notified = false;
				callbacks.m_killed = false;
				tween.onComplete = callbacks.m_onComplete;
				tween.onKill = callbacks.m_onKill;
			}

			private void OnComplete() {
				m_callbackDepth++;
				try {
					m_originalComplete?.Invoke();
					if(m_notified) return;
					m_notified = true;
					m_complete?.Invoke();
				} finally { EndCallback(); }
			}

			private void OnKill() {
				m_callbackDepth++;
				try {
					m_originalKill?.Invoke();
					if(m_notified) return;
					m_notified = true;
					m_kill?.Invoke();
				} finally {
					m_killed = true;
					EndCallback();
				}
			}

			private void EndCallback() {
				m_callbackDepth--;
				// 通知内で別のアニメーションが始まっても、実行中の通知状態を再利用しない。
				if(!m_killed || m_callbackDepth != 0) return;
				m_originalComplete = null;
				m_originalKill = null;
				m_complete = null;
				m_kill = null;
				s_pool.Push(this);
			}
		}

		/// <summary>ステートに応じてテキストカラー遷移を適用する</summary>
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

			Color targetColor = GetStateColor(colors, selectionState) * colors.colorMultiplier;
			float duration = instant ? 0f : colors.fadeDuration;

			if(duration <= 0f || !targetText.gameObject.activeInHierarchy) {
				SetTextColorImmediate(targetText, targetColor);
				onComplete?.Invoke();
				return;
			}

			var destroyToken = owner.GetCancellationTokenOnDestroy();
			runningCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
			FadeTextColorAsync(targetText, targetColor, duration, runningCts, onComplete).Forget();
		}

		/// <summary>進行中のテキストカラー遷移を停止する</summary>
		public static void StopTextColorTransition(ref CancellationTokenSource runningCts) {
			if(runningCts == null) return;
			try { runningCts.Cancel(); }
			catch(ObjectDisposedException) { /* 完了側で既に解放されている場合も停止を許容する。 */ }
			runningCts.Dispose();
			runningCts = null;
		}

		/// <summary>ステートに応じたテキスト内容を差し替える</summary>
		public static void ApplyTextSwapTransition(TMP_Text targetText, TextSwapState swapState, int selectionState) {
			if(targetText == null) return;
			targetText.text = GetStateText(swapState, selectionState);
		}

		/// <summary>ステートに応じてテキストアニメーションを再生する</summary>
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

		/// <summary>ステートに応じたColorBlockの色を取得する</summary>
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

		/// <summary>ステートに対応するテキストを返す</summary>
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

		/// <summary>テキストカラーを即時反映する</summary>
		public static void SetTextColorImmediate(TMP_Text targetText, Color color) {
			if(targetText == null) return;
			// 頂点色へ反映するため、CanvasRenderer側では同じ色を重ねて乗算しない。
			targetText.canvasRenderer.SetColor(Color.white);
			targetText.color = color;
		}
		#endregion

		#region Private Method
		/// <summary>テキストカラーを非同期でフェードさせる</summary>
		private static async UniTask FadeTextColorAsync(TMP_Text targetText, Color targetColor, float duration, CancellationTokenSource source, Action onComplete) {
			var ct = source.Token;
			try {
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
			} finally {
				// 正常完了でも破棄トークンへの登録を解除する。
				source.Dispose();
			}
		}

		/// <summary>指定されたトリガー名でアニメーションを再生する</summary>
		private static void PlayTextAnimation(Animator animator, string stateName) {
			if(string.IsNullOrEmpty(stateName)) return;
			animator.ResetTrigger(stateName);
			animator.SetTrigger(stateName);
		}
		#endregion
	}
}
