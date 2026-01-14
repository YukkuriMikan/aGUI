using UnityEngine;
using System;
using UniRx;
using UniRx.Triggers;

namespace ANest.UI {
	/// <summary>
	/// 指定したターゲットの RectTransform と位置・サイズ・ピボット・アンカーを同期させるコンポーネント。
	/// </summary>
	[RequireComponent(typeof(RectTransform))]
	public class RectSync : MonoBehaviour {
		#region SerializeField
		[SerializeField] private RectTransform target; // 同期対象
		[SerializeField] private bool syncPosition = true;
		[SerializeField] private bool syncSize = true;
		[SerializeField] private bool syncPivot = true;
		[SerializeField] private bool syncAnchors = true;
		#endregion

		#region Fields
		private RectTransform m_rectTransform;
		private RectTransform RectTransform => m_rectTransform ? m_rectTransform : (m_rectTransform = transform as RectTransform);
		private IDisposable m_disposable;
		#endregion

		#region Unity Methods
		private void OnEnable() {
			Subscribe();
		}

		private void OnDisable() {
			Unsubscribe();
		}

		private void OnDestroy() {
			Unsubscribe();
		}

		private void LateUpdate() {
			// ターゲットの変更を毎フレームチェック（念のため）
			Sync();
		}
		#endregion

		#region Methods
		private void Subscribe() {
			Unsubscribe();
			if(target == null) return;

			// ターゲットの RectTransform の変更を監視
			m_disposable = target.OnRectTransformDimensionsChangeAsObservable()
				.Subscribe(_ => Sync())
				.AddTo(this);
		}

		private void Unsubscribe() {
			m_disposable?.Dispose();
			m_disposable = null;
		}

		/// <summary>
		/// ターゲットの状態を自身に同期させる
		/// </summary>
		public void Sync() {
			if(target == null || RectTransform == null) return;

			if(syncAnchors) {
				if(RectTransform.anchorMin != target.anchorMin) RectTransform.anchorMin = target.anchorMin;
				if(RectTransform.anchorMax != target.anchorMax) RectTransform.anchorMax = target.anchorMax;
			}

			if(syncPivot) {
				if(RectTransform.pivot != target.pivot) RectTransform.pivot = target.pivot;
			}

			if(syncPosition) {
				if(RectTransform.anchoredPosition != target.anchoredPosition) RectTransform.anchoredPosition = target.anchoredPosition;
			}

			if(syncSize) {
				if(RectTransform.sizeDelta != target.sizeDelta) RectTransform.sizeDelta = target.sizeDelta;
			}
		}
		#endregion
	}
}
