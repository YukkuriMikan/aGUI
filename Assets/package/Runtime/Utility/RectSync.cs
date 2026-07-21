using UnityEngine;

namespace ANest.UI {
	/// <summary>指定したターゲットの RectTransform と位置・サイズ・ピボット・アンカーを同期させるコンポーネント。</summary>
	[RequireComponent(typeof(RectTransform))]
	public class RectSync : MonoBehaviour {
		#region SerializeField
		[Tooltip("同期対象のRectTransform")]
		[SerializeField] private RectTransform target; // 同期対象
		[Tooltip("位置(anchoredPosition)を同期するか")]
		[SerializeField] private bool syncPosition = true; // 位置を同期するか
		[Tooltip("サイズ(sizeDelta)を同期するか")]
		[SerializeField] private bool syncSize = true; // サイズを同期するか
		[Tooltip("ピボットを同期するか")]
		[SerializeField] private bool syncPivot = true; // ピボットを同期するか
		[Tooltip("アンカーを同期するか")]
		[SerializeField] private bool syncAnchors = true; // アンカーを同期するか
		#endregion

		#region Fields
		private RectTransform m_rectTransform;
		/// <summary>自身のRectTransformキャッシュ</summary>
		private RectTransform RectTransform => m_rectTransform ? m_rectTransform : (m_rectTransform = transform as RectTransform);
		#endregion

		#region Unity Methods
		/// <summary>毎フレームターゲットの変更をチェックして同期する</summary>
		private void LateUpdate() {
			Sync();
		}
		#endregion

		#region Methods
		/// <summary>ターゲットの状態を自身に同期させる</summary>
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
