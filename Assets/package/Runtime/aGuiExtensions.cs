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

			// OnComplete/OnKillの登録は他所で設定済みのコールバックを上書きしてしまうため、ポーリングで完了を検知する
			CancellationTokenRegistration reg = default;
			try {
				if(ct.CanBeCanceled) {
					reg = ct.Register(() => {
						if(tween.IsActive()) {
							tween.Kill();
						}
					});
				}

				// Kill（autoKill含む）でIsActiveがfalseになり、autoKill無効の完了はIsCompleteで検知する
				while (tween.IsActive() && !tween.IsComplete()) {
					await UniTask.Yield(ct);
				}
			} finally {
				reg.Dispose();
			}

			return tween;
		}
	}
}
