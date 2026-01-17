using System;
using UnityEngine;

namespace ANest.UI {
	/// <summary>トランスフォームの値の保存クラス</summary>
	[Serializable]
	public struct RectTransformValues {
		#region Field
		[Tooltip("座標のワールド値")]
		[SerializeField]
		private Vector3 m_worldPosition; // 座標のワールド値
		[Tooltip("座標のローカル値")]
		[SerializeField]
		private Vector3 m_localPosition; // 座標のローカル値
		[Tooltip("回転のワールド値")]
		[SerializeField]
		private Quaternion m_worldRotation; // 回転のワールド値
		[Tooltip("回転のローカル値")]
		[SerializeField]
		private Quaternion m_localRotation; // 回転のローカル値
		[Tooltip("スケールのローカル値")]
		[SerializeField]
		private Vector3 m_localScale; // スケールのローカル値
		[Tooltip("アンカー基準の座標")]
		[SerializeField]
		private Vector2 m_anchoredPosition; // アンカー基準の座標
		[Tooltip("最小アンカー")]
		[SerializeField]
		private Vector2 m_anchorMin; // 最小アンカー
		[Tooltip("最大アンカー")]
		[SerializeField]
		private Vector2 m_anchorMax; // 最大アンカー
		[Tooltip("サイズデルタ")]
		[SerializeField]
		private Vector2 m_sizeDelta; // サイズデルタ
		[Tooltip("ピボットのローカル値")]
		[SerializeField]
		private Vector2 m_pivot; // ピボットのローカル値
		[Tooltip("TransformのRect")]
		[SerializeField]
		private Rect m_rect; // TransformのRect
		#endregion

		#region Property
		/// <summary>座標のワールド値</summary>
		public Vector3 WorldPosition => m_worldPosition;

		/// <summary>座標のローカル値</summary>
		public Vector3 LocalPosition => m_localPosition;

		/// <summary>回転のワールド値</summary>
		public Quaternion WorldRotation => m_worldRotation;

		/// <summary>回転のローカル値</summary>
		public Quaternion LocalRotation => m_localRotation;

		/// <summary>スケールのローカル値</summary>
		public Vector3 LocalScale => m_localScale;

		/// <summary>アンカー基準の座標</summary>
		public Vector2 AnchoredPosition => m_anchoredPosition;

		/// <summary>最小アンカー</summary>
		public Vector2 AnchorMin => m_anchorMin;

		/// <summary>最大アンカー</summary>
		public Vector2 AnchorMax => m_anchorMax;

		/// <summary>サイズデルタ</summary>
		public Vector2 SizeDelta => m_sizeDelta;

		/// <summary>ピボットのローカル値</summary>
		public Vector2 Pivot => m_pivot;

		/// <summary>TransformのRect</summary>
		public Rect Rect => m_rect;
		#endregion

		#region Public Method
		/// <summary>指定されたTransformから値を作成する</summary>
		public static RectTransformValues CreateValues(Transform trans) {
			var rect = trans as RectTransform;
			var newValues = new RectTransformValues();

			if(rect == null) {
#if UNITY_EDITOR
				Debug.Log("RectTransformではありません", trans.gameObject);
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

		/// <summary>RectTransformに値を適用する</summary>
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
			// rectは代入不可
			// そもそもレイアウト計算の結果生成されるものなので代入不要
		}

		/// <summary>同一性の判定</summary>
		public override bool Equals(object obj) {
			if(!(obj is RectTransformValues other)) return false;

			return m_worldPosition == other.m_worldPosition &&
				m_localPosition == other.m_localPosition &&
				m_worldRotation == other.m_worldRotation &&
				m_localRotation == other.m_localRotation &&
				m_localScale == other.m_localScale &&
				m_anchoredPosition == other.m_anchoredPosition &&
				m_anchorMin == other.m_anchorMin &&
				m_anchorMax == other.m_anchorMax &&
				m_sizeDelta == other.m_sizeDelta &&
				m_pivot == other.m_pivot;
		}

		/// <summary>ハッシュコードの取得</summary>
		public override int GetHashCode() {
			return m_worldPosition.GetHashCode(); // 簡易的
		}
		#endregion
	}

}
