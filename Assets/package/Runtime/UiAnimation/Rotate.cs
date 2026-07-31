using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary> 回転アニメーション </summary>
	[Serializable]
	public class Rotate : IUiAnimation {
		#region SerializeField
		[SerializeField] private Vector3 m_startValue = Vector3.zero;                           // 回転開始時の相対オイラー角
		[SerializeField] private Vector3 m_endValue = Vector3.zero;                             // 回転終了時の相対オイラー角
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
		private Quaternion m_baseRotation; // 相対回転の基準（初回再生時にキャッシュ）
		private bool m_hasBaseRotation;    // 基準回転をキャッシュ済みか
		#endregion

		#region Methods
		/// <summary> RectTransform の回転を補間するアニメーションを実行 </summary>
		/// <param name="graphic">アニメーション対象の Graphic（未使用）</param>
		/// <param name="callerRect">アニメーション対象の RectTransform</param>
		/// <param name="original">復元用のRectTransform初期値</param>
		/// <param name="ct">キャンセルトークン</param>
		public Tween DoAnimate(Graphic graphic, RectTransform callerRect, RectTransformValues original) {
			if(callerRect == null) return null;

			m_tween.Kill();

			// 再生中の再トリガーで回転が累積しないよう、基準回転は初回再生時の値を使い続ける
			if(!m_hasBaseRotation) {
				m_baseRotation = callerRect.localRotation;
				m_hasBaseRotation = true;
			}

			// 初期回転を設定（基準回転に相対オフセットを適用）
			Quaternion startRotation = m_baseRotation * Quaternion.Euler(m_startValue);
			Quaternion endRotation = m_baseRotation * Quaternion.Euler(m_endValue);
			callerRect.localRotation = startRotation;

			m_tween = callerRect
				.DOLocalRotate(endRotation.eulerAngles, IsYoYo ? m_duration / 2f : m_duration) // ヨーヨー時は2ループ合計でm_durationになるよう半分にする
				.SetDelay(Delay)
				.SetTarget(callerRect); // 呼び出し元Rect単位のDOKillで中断できるようターゲットを設定

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
