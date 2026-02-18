using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
namespace ANest.UI {
	/// <summary>Localization対応を備えたTextMeshProUGUI拡張。</summary>
	public class aTextMeshProUgui : TextMeshProUGUI {
		#region SerializeField
		[Tooltip("Localization用StringTableCollectionの参照")]
		[SerializeField] private LocalizedStringTable m_stringTable;
		[Tooltip("StringTable内のキー名")]
		[SerializeField] private string m_localizationKey;
		#endregion
		#region Fields
		private StringTable m_currentTable; // 現在のStringTable
		#endregion
		#region Properties
		/// <summary>Localization用StringTableCollectionの参照</summary>
		public LocalizedStringTable StringTable => m_stringTable;
		/// <summary>StringTable内のキー名</summary>
		public string LocalizationKey {
			get => m_localizationKey;
			set {
				m_localizationKey = value;
				ApplyLocalization();
			}
		}
		#endregion
		#region Unity Methods
		protected override void OnEnable() {
			base.OnEnable();
			if(m_stringTable != null) {
				m_stringTable.TableChanged += OnStringTableChanged;
			}
			ApplyLocalization();
		}
		protected override void OnDisable() {
			if(m_stringTable != null) {
				m_stringTable.TableChanged -= OnStringTableChanged;
			}
			base.OnDisable();
		}
		#endregion
		#region Private Methods
		/// <summary>Localizationテーブル変更時のコールバック。</summary>
		private void OnStringTableChanged(StringTable table) {
			m_currentTable = table;
			ApplyLocalization();
		}
		/// <summary>現在のLocalization設定からテキストを適用する。</summary>
		private void ApplyLocalization() {
			if(m_currentTable == null) return;
			if(string.IsNullOrEmpty(m_localizationKey)) return;
			var entry = m_currentTable.GetEntry(m_localizationKey);
			if(entry == null) return;
			// タグを含む生のデータを取得する（加工しない）
			var rawValue = entry.Value;
			if(rawValue != null) {
				text = rawValue;
			}
		}
		#endregion
	}
}
