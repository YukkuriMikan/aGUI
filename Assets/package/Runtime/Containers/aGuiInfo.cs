using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary> aGUIの情報取得用クラス </summary>
	public class aGuiInfo : MonoBehaviour {
		[SerializeField]
		private RectTransform m_rectTransform;
		[SerializeField]
		private RectTransformValues m_originalRectTransformValues;
		[SerializeField]
		private Graphic m_targetGraphic;

		public RectTransform RectTransform => m_rectTransform;
		public RectTransformValues OriginalRectTransformValues => m_originalRectTransformValues;
		public Graphic TargetGraphic => m_targetGraphic;

		#if UNITY_EDITOR
		private void Reset() {
			m_rectTransform = transform as RectTransform;
			m_originalRectTransformValues = RectTransformValues.CreateValues(m_rectTransform);
			m_targetGraphic = GetComponent<Graphic>();
		}

		private void OnValidate() {
			if(m_rectTransform != null) {
				var currentValues = RectTransformValues.CreateValues(m_rectTransform);

				if(!currentValues.Equals(m_originalRectTransformValues)) {
					m_originalRectTransformValues = currentValues;
				}
			}

		}
		#endif
	}
}
