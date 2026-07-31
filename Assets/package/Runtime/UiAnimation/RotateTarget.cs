using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary> 指定ターゲットを回転させるアニメーション </summary>
	[Serializable]
	public class RotateTarget : IUiAnimation {
		#region SerializeField
		[SerializeField] private aGuiInfo m_target;                                             // 回転対象のaGuiInfo
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
		/// <summary> 指定ターゲットを回転させるアニメーションを実行 </summary>
		/// <param name="_">アニメーション対象の Graphic（未使用）</param>
		/// <param name="callerRect">呼び出し元の RectTransform（DOKillによる中断用ターゲット）</param>
		/// <param name="___">復元用のRectTransform初期値（未使用）</param>
		public Tween DoAnimate(Graphic _, RectTransform callerRect, RectTransformValues ___) {
			m_tween.Kill();

			// 再生中の再トリガーで回転が累積しないよう、基準回転は初回再生時の値を使い続ける
			if(!m_hasBaseRotation) {
				m_baseRotation = m_target.RectTransform.localRotation;
				m_hasBaseRotation = true;
			}

			// 初期回転を設定（基準回転に相対オフセットを適用）
			var startRotation = m_baseRotation * Quaternion.Euler(m_startValue);
			var endRotation = m_baseRotation * Quaternion.Euler(m_endValue);
			m_target.RectTransform.localRotation = startRotation;

			m_tween = m_target.RectTransform
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
