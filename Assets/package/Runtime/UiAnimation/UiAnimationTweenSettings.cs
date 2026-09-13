using DG.Tweening;

namespace ANest.UI {
	/// <summary>各UIアニメーションで共通の補間方法とYoyo設定を適用する</summary>
	internal static class UiAnimationTweenSettings {
		internal static void Apply(Tween tween, IUiAnimation animation) {
			if(animation.UseCurve) tween.SetEase(animation.Curve);
			else tween.SetEase(animation.Ease);

			// Yoyoは往復2ループ。片道の時間はTween生成側でDuration / 2に設定する。
			if(animation.IsYoYo) tween.SetLoops(2, LoopType.Yoyo);
		}
	}
}
