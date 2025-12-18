using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary> UIアニメーションの共通インターフェイス </summary>
	public interface IUiAnimation {
		/// <summary> アニメーション開始までの遅延秒数 </summary>
		public float Delay { get; }

		/// <summary> アニメーション再生時間 </summary>
		public float Duration { get; }

		/// <summary> アニメーションをヨーヨーで再生するか？ </summary>
		public bool IsYoYo { get; }

		/// <summary> カーブ指定（UseCurve が有効な場合に使用） </summary>
		public AnimationCurve Curve { get; }

		/// <summary> DOTween の Ease 設定 </summary>
		public Ease Ease { get; }

		/// <summary> 曲線で補間するかどうか </summary>
		public bool UseCurve { get; }

		/// <summary> RectTransform を対象にアニメーションを実行 </summary>
		/// <param name="graphic">アニメーション対象の Graphic</param>
		/// <param name="callerRect">アニメーション対象の RectTransform</param>
		/// <param name="original">元のRectTransform値</param>
		public Tween DoAnimate(Graphic graphic, RectTransform callerRect, RectTransformValues original);
	}
}
