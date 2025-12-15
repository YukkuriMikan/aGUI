using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace ANest.UI {
	/// <summary> DOTween の Tween を安全に完了待機するための拡張メソッド群 </summary>
	public static class aGuiExtensions {
		/// <summary> Tween が完了または Kill されるまで待機する </summary>
		/// <param name="tween">待機対象の Tween</param>
		/// <param name="ct">キャンセル用トークン</param>
		/// <returns>完了または Kill 済みの Tween</returns>
		public static async UniTask<Tween> AwaitCompletion(this Tween tween, CancellationToken ct = default) {
			if(tween == null) throw new ArgumentNullException(nameof(tween));

			// 既に完了/無効の場合は即時返す
			if(!tween.IsActive() || !tween.IsPlaying()) {
				return tween;
			}

			var tcs = new UniTaskCompletionSource();
			TweenCallback onComplete = null;
			TweenCallback onKill = null;
			CancellationTokenRegistration reg = default;

			void TrySetFinished() {
				tcs.TrySetResult();
			}

			onComplete = () => {
				tween.OnComplete(null);
				tween.OnKill(null);
				TrySetFinished();
			};

			onKill = () => {
				tween.OnComplete(null);
				tween.OnKill(null);
				TrySetFinished();
			};

			tween.OnComplete(onComplete).OnKill(onKill);

			try {
				if(ct.CanBeCanceled) {
					reg = ct.Register(() => {
						if(tween.IsActive()) {
							tween.Kill();
						}
					});
				}

				await tcs.Task.AttachExternalCancellation(ct);
			} finally {
				reg.Dispose();
				tween.OnComplete(null);
				tween.OnKill(null);
			}

			return tween;
		}
	}
}
