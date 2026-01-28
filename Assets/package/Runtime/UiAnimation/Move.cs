using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary> 移動アニメーション </summary>
	public class Move : IUiAnimation {
		#region SerializeField
		[SerializeField] private Vector3 m_startValue = Vector3.left;                           // 移動開始時の相対座標
		[SerializeField] private Vector3 m_endValue = Vector3.zero;                             // 移動終了時の相対座標
		[SerializeField] private float m_delay;                                                 // 再生までの遅延秒数
		[SerializeField] private float m_duration = 0.5f;                                       // 再生時間
		[SerializeField] private bool m_isYoYo;                                                 // ヨーヨー再生か？
		[SerializeField] private Ease m_ease = Ease.OutQuad;                                    // イージング種別
		[SerializeField] private bool m_useCurve = false;                                       // カーブ補間を使うか
		[SerializeField] private AnimationCurve m_curve = AnimationCurve.EaseInOut(0, 0, 1, 1); // カーブ設定
		#endregion

		#region Properties
		/// <summary> アニメーション開始までの遅延秒数 </summary>
		public float Delay => m_delay;

		/// <summary> アニメーション再生時間 </summary>
		public float Duration => m_duration;

		/// <summary> アニメーションをヨーヨーで再生するか？ </summary>
		public bool IsYoYo => m_isYoYo;

		/// <summary> 曲線補間用のカーブ </summary>
		public AnimationCurve Curve => m_curve;

		/// <summary> DOTween のイージング </summary>
		public Ease Ease => m_ease;

		/// <summary> 曲線補間を使用するか </summary>
		public bool UseCurve => m_useCurve;
		#endregion

		#region Fields
		private Tween m_tween;
		#endregion

		#region Methods
		/// <summary> RectTransform を移動させるアニメーションを実行 </summary>
		/// <param name="graphic">アニメーション対象の Graphic</param>
		/// <param name="callerRect">呼び出し元の RectTransform（位置復元用）</param>
		/// <param name="original">復元用のRectTransform初期値</param>
		public Tween DoAnimate(Graphic graphic, RectTransform callerRect, RectTransformValues original) {
			if(callerRect == null) return null;

			m_tween.Kill();

			callerRect.localPosition = original.LocalPosition + m_startValue; //開始座標へ

			m_tween = callerRect
				.DOLocalMove(original.LocalPosition + m_endValue, m_duration / 2f)
				.SetDelay(Delay);

			if(UseCurve) {
				if(IsYoYo) {
					m_tween.SetEase(Curve).SetLoops(2, LoopType.Yoyo);
				} else {
					m_tween.SetEase(Curve);
				}
			} else {
				if(IsYoYo) {
					m_tween.SetEase(Ease).SetLoops(2, LoopType.Yoyo);
				} else {
					m_tween.SetEase(Ease);
				}
			}

			return m_tween;
		}
		#endregion
	}
}
