using TMPro;
using UnityEngine;

namespace ANest.UI {
	public class TestTextToggle : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI m_text;

		[SerializeField]
		private bool m_isToggled;

		public void ToggleText() {
			m_isToggled = !m_isToggled;
			m_text.text = m_isToggled ? "ON" : "OFF";
		}
	}
}
