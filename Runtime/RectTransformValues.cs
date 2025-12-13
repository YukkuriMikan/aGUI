using UnityEngine;

namespace ANest.UI {

	/// <summary> トランスフォームの値の保存クラス </summary>
	public struct RectTransformValues {
		public Vector3 WorldPosition { get; private set; }    //座標のワールド値
		public Vector3 LocalPosition { get; private set; }    //座標のローカル値
		public Quaternion WorldRotation { get; private set; } //回転のワールド値
		public Quaternion LocalRotation { get; private set; } //回転のローカル値
		public Vector3 LocalScale { get; private set; }       //スケールのローカル値

		public Vector2 AnchoredPosition { get; private set; }
		public Vector2 AnchorMin { get; private set; }
		public Vector2 AnchorMax { get; private set; }
		public Vector2 SizeDelta { get; private set; }

		public Vector2 Pivot { get; private set; } //ピボットのローカル値
		public Rect Rect { get; private set; }     //TransformのRect

		public static RectTransformValues CreateValues(Transform trans) {
			var rect = trans as RectTransform;
			var newValues = new RectTransformValues();

			if(rect == null) {
#if KKRW_DEBUG
				Debug.Log("RectTransformではありません");
#endif
				return newValues;
			}

			newValues.WorldPosition = rect.position;
			newValues.LocalPosition = rect.localPosition;
			newValues.WorldRotation = rect.rotation;
			newValues.LocalRotation = rect.localRotation;
			newValues.LocalScale = rect.localScale;
			newValues.AnchoredPosition = rect.anchoredPosition;
			newValues.AnchorMin = rect.anchorMin;
			newValues.AnchorMax = rect.anchorMax;
			newValues.SizeDelta = rect.sizeDelta;
			newValues.Pivot = rect.pivot;
			newValues.Rect = rect.rect;

			return newValues;
		}

		public void Apply(RectTransform rectTrans) {
			rectTrans.position = WorldPosition;
			rectTrans.localPosition = LocalPosition;
			rectTrans.rotation = WorldRotation;
			rectTrans.localRotation = LocalRotation;
			rectTrans.localScale = LocalScale;
			rectTrans.anchoredPosition = AnchoredPosition;
			rectTrans.anchorMin = AnchorMin;
			rectTrans.anchorMax = AnchorMax;
			rectTrans.sizeDelta = SizeDelta;
			rectTrans.pivot = Pivot;
		}
	}

}
