using System;
using UnityEngine;

namespace ANest.UI {
	/// <summary> トランスフォームの値の保存クラス </summary>
	[Serializable]
	public struct RectTransformValues {
		[SerializeField]
		private Vector3 m_worldPosition; //座標のワールド値
		[SerializeField]
		private Vector3 m_localPosition; //座標のローカル値
		[SerializeField]
		private Quaternion m_worldRotation; //回転のワールド値
		[SerializeField]
		private Quaternion m_localRotation; //回転のローカル値
		[SerializeField]
		private Vector3 m_localScale; //スケールのローカル値

		[SerializeField]
		private Vector2 m_anchoredPosition;
		[SerializeField]
		private Vector2 m_anchorMin;
		[SerializeField]
		private Vector2 m_anchorMax;
		[SerializeField]
		private Vector2 m_sizeDelta;

		[SerializeField]
		private Vector2 m_pivot; //ピボットのローカル値
		[SerializeField]
		private Rect m_rect; //TransformのRect

		public Vector3 WorldPosition => m_worldPosition;
		public Vector3 LocalPosition => m_localPosition;
		public Quaternion WorldRotation => m_worldRotation;
		public Quaternion LocalRotation => m_localRotation;
		public Vector3 LocalScale => m_localScale;

		public Vector2 AnchoredPosition => m_anchoredPosition;
		public Vector2 AnchorMin => m_anchorMin;
		public Vector2 AnchorMax => m_anchorMax;
		public Vector2 SizeDelta => m_sizeDelta;

		public Vector2 Pivot => m_pivot;
		public Rect Rect => m_rect;

		public static RectTransformValues CreateValues(Transform trans) {
			var rect = trans as RectTransform;
			var newValues = new RectTransformValues();

			if(rect == null) {
#if KKRW_DEBUG
				Debug.Log("RectTransformではありません");
#endif
				return newValues;
			}

			newValues.m_worldPosition = rect.position;
			newValues.m_localPosition = rect.localPosition;
			newValues.m_worldRotation = rect.rotation;
			newValues.m_localRotation = rect.localRotation;
			newValues.m_localScale = rect.localScale;
			newValues.m_anchoredPosition = rect.anchoredPosition;
			newValues.m_anchorMin = rect.anchorMin;
			newValues.m_anchorMax = rect.anchorMax;
			newValues.m_sizeDelta = rect.sizeDelta;
			newValues.m_pivot = rect.pivot;
			newValues.m_rect = rect.rect;

			return newValues;
		}

		public void Apply(RectTransform rectTrans) {
			rectTrans.position = m_worldPosition;
			rectTrans.localPosition = m_localPosition;
			rectTrans.rotation = m_worldRotation;
			rectTrans.localRotation = m_localRotation;
			rectTrans.localScale = m_localScale;
			rectTrans.anchoredPosition = m_anchoredPosition;
			rectTrans.anchorMin = m_anchorMin;
			rectTrans.anchorMax = m_anchorMax;
			rectTrans.sizeDelta = m_sizeDelta;
			rectTrans.pivot = m_pivot;
			//rectは代入不可
			//そもそもレイアウト計算の結果生成されるものなので代入不要
		}
	}

}
