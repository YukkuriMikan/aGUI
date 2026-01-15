using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aGUIの情報取得用クラス</summary>
	public class aGuiInfo : MonoBehaviour {
		#region SerializeField
		[Tooltip("自身のRectTransform")]
		[SerializeField] private RectTransform m_rectTransform; // 自身のRectTransform
		[Tooltip("RectTransformの初期値キャッシュ")]
		[SerializeField] private RectTransformValues m_originalRectTransformValues; // RectTransformの初期値
		[Tooltip("対象グラフィック")]
		[SerializeField] private Graphic m_targetGraphic; // 対象グラフィック
		#endregion

		#region Properties
		/// <summary>自身のRectTransform</summary>
		public RectTransform RectTransform => m_rectTransform;

		/// <summary>RectTransformの初期値</summary>
		public RectTransformValues OriginalRectTransformValues => m_originalRectTransformValues;

		/// <summary>対象のGraphic</summary>
		public Graphic TargetGraphic => m_targetGraphic;
		#endregion

		/// <summary> 情報の更新を行う </summary>
		public void Refresh() {
			m_originalRectTransformValues = RectTransformValues.CreateValues(m_rectTransform);
		}

		#if UNITY_EDITOR
		/// <summary>コンポーネント追加・リセット時に参照を取得</summary>
		private void Reset() {
			m_rectTransform = transform as RectTransform;
			m_originalRectTransformValues = RectTransformValues.CreateValues(m_rectTransform);
			m_targetGraphic = GetComponent<Graphic>();
		}

		/// <summary>エディタ上の値変更時に初期値キャッシュを更新</summary>
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
