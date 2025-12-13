using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace ANest.UI {
	/// <summary> CanvasGroup のアルファを補間するフェードアニメーション </summary>
	public class Fade : IUiAnimation {
	#region SerializeField
		[SerializeField] private CanvasGroup m_canvasGroup;                                     // フェード対象のCanvasGroup
		[SerializeField] private float m_startValue = 0;                                        // フェード開始時のアルファ
		[SerializeField] private float m_endValue = 0;                                          // フェード終了時のアルファ
		[SerializeField] private float m_delay;                                                 // 再生までの遅延秒数
		[SerializeField] private float m_duration;                                              // 再生時間
		[SerializeField] private Ease m_ease = Ease.OutQuad;                                    // イージング種別
		[SerializeField] private bool m_useCurve = false;                                       // カーブ補間を使うか
		[SerializeField] private AnimationCurve m_curve = AnimationCurve.EaseInOut(0, 0, 1, 1); // カーブ設定
	#endregion

	#region Properties
		/// <summary> アニメーション開始までの遅延秒数 </summary>
		public float Delay => m_delay;

		/// <summary> アニメーション再生時間 </summary>
		public float Duration => m_duration;

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
		/// <summary> CanvasGroup のアルファをフェードさせるアニメーションを実行 </summary>
		/// <param name="caller">呼び出し元の RectTransform（位置復元用）</param>
		/// <param name="original">復元用のRectTransform初期値</param>
		public async UniTask<Tween> DoAnimate(RectTransform caller, RectTransformValues original) {
			await UniTask.Delay(TimeSpan.FromSeconds(Delay)); // 遅延後に実行

			m_tween?.Complete();    // 既存Tweenを完了させ競合を防止

			// 位置リセットは他アニメーション（Moveなど）と干渉するため行わない

			m_canvasGroup.alpha = m_startValue;

			m_tween = m_canvasGroup.DOFade(m_endValue, m_duration); // アルファを補間

			return m_tween;
		}
	#endregion
	}
}
