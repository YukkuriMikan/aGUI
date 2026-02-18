using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
namespace ANest.UI {
	/// <summary>aTextMeshProUguiのLocalization対応カスタムインスペクタ。</summary>
	[CustomEditor(typeof(aTextMeshProUgui))]
	public class aTextMeshProUguiEditor : UnityEditor.Editor {
		#region Fields
		private SerializedProperty m_stringTableProp;    // StringTable 参照プロパティ
		private SerializedProperty m_localizationKeyProp; // Localizationキープロパティ
		private StringTableCollection[] m_tableCollections; // 全StringTableCollection
		private string[] m_tableNames;                      // テーブル名一覧
		private int m_selectedTableIndex;                   // 選択中テーブルインデックス
		private string[] m_keys;                            // 選択テーブルのキー一覧
		private int m_selectedKeyIndex;                     // 選択中キーインデックス
		private string m_keySearchFilter = "";              // キー検索フィルタ
		private string[] m_filteredKeys;                    // フィルタ済みキー一覧
		private UnityEditor.Editor m_tmpEditor;             // TMP標準エディタ
		private static Type s_tmpEditorType;                // TMP_EditorPanelUI型キャッシュ
		#endregion
		#region Unity Methods
		private void OnEnable() {
			m_stringTableProp = serializedObject.FindProperty("m_stringTable");
			m_localizationKeyProp = serializedObject.FindProperty("m_localizationKey");
			RefreshTableCollections();
			SyncSelectedTableIndex();
			RefreshKeys();
			SyncSelectedKeyIndex();
			CreateTmpEditor();
		}
		private void OnDisable() {
			if(m_tmpEditor != null) {
				DestroyImmediate(m_tmpEditor);
				m_tmpEditor = null;
			}
		}
		public override void OnInspectorGUI() {
			serializedObject.Update();
			EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
			DrawTableDropdown();
			DrawKeySearchField();
			DrawKeyDropdown();
			if(serializedObject.ApplyModifiedProperties()) {
				foreach(var t in targets) {
					var tmp = (aTextMeshProUgui)t;
					tmp.LocalizationKey = tmp.LocalizationKey;
				}
			}
			EditorGUILayout.Space();
			if(m_tmpEditor != null) {
				m_tmpEditor.OnInspectorGUI();
			}
		}
		#endregion
		#region Private Methods
		/// <summary>TMP標準エディタを生成する。</summary>
		private void CreateTmpEditor() {
			if(s_tmpEditorType == null) {
				s_tmpEditorType = Type.GetType("TMPro.EditorUtilities.TMP_EditorPanelUI, Unity.TextMeshPro.Editor");
			}
			if(s_tmpEditorType != null) {
				m_tmpEditor = CreateEditor(targets, s_tmpEditorType);
			}
		}
		/// <summary>StringTableCollectionの一覧を取得する。</summary>
		private void RefreshTableCollections() {
			m_tableCollections = LocalizationEditorSettings.GetStringTableCollections().ToArray();
			var names = new List<string> { "(None)" };
			names.AddRange(m_tableCollections.Select(c => c.TableCollectionName));
			m_tableNames = names.ToArray();
		}
		/// <summary>現在のSerializedPropertyからテーブル選択インデックスを同期する。</summary>
		private void SyncSelectedTableIndex() {
			m_selectedTableIndex = 0;
			var tableRefProp = m_stringTableProp.FindPropertyRelative("m_TableReference");
			if(tableRefProp == null) return;
			var tableNameProp = tableRefProp.FindPropertyRelative("m_TableCollectionName");
			if(tableNameProp == null || string.IsNullOrEmpty(tableNameProp.stringValue)) return;
			for(int i = 0; i < m_tableCollections.Length; i++) {
				if(m_tableCollections[i].TableCollectionName == tableNameProp.stringValue) {
					m_selectedTableIndex = i + 1;
					break;
				}
			}
		}
		/// <summary>選択テーブルのキー一覧を取得する。</summary>
		private void RefreshKeys() {
			if(m_selectedTableIndex <= 0 || m_selectedTableIndex > m_tableCollections.Length) {
				m_keys = new string[0];
				m_filteredKeys = new string[0];
				return;
			}
			var collection = m_tableCollections[m_selectedTableIndex - 1];
			m_keys = collection.SharedData.Entries.Select(e => e.Key).OrderBy(k => k).ToArray();
			ApplyKeyFilter();
		}
		/// <summary>現在のLocalizationKeyからキー選択インデックスを同期する。</summary>
		private void SyncSelectedKeyIndex() {
			m_selectedKeyIndex = 0;
			if(m_filteredKeys == null || m_filteredKeys.Length == 0) return;
			var currentKey = m_localizationKeyProp.stringValue;
			if(string.IsNullOrEmpty(currentKey)) return;
			for(int i = 0; i < m_filteredKeys.Length; i++) {
				if(m_filteredKeys[i] == currentKey) {
					m_selectedKeyIndex = i;
					break;
				}
			}
		}
		/// <summary>検索フィルタをキー一覧に適用する。</summary>
		private void ApplyKeyFilter() {
			if(string.IsNullOrEmpty(m_keySearchFilter)) {
				m_filteredKeys = m_keys;
			} else {
				var lower = m_keySearchFilter.ToLowerInvariant();
				m_filteredKeys = m_keys.Where(k => k.ToLowerInvariant().Contains(lower)).ToArray();
			}
		}
		/// <summary>StringTableドロップダウンを描画する。</summary>
		private void DrawTableDropdown() {
			var newIndex = EditorGUILayout.Popup("String Table", m_selectedTableIndex, m_tableNames);
			if(newIndex != m_selectedTableIndex) {
				m_selectedTableIndex = newIndex;
				ApplySelectedTable();
				RefreshKeys();
				m_selectedKeyIndex = 0;
				m_localizationKeyProp.stringValue = "";
			}
		}
		/// <summary>キー検索フィールドを描画する。</summary>
		private void DrawKeySearchField() {
			var newFilter = EditorGUILayout.TextField("Key Search", m_keySearchFilter);
			if(newFilter != m_keySearchFilter) {
				m_keySearchFilter = newFilter;
				ApplyKeyFilter();
				SyncSelectedKeyIndex();
			}
		}
		/// <summary>キードロップダウンを描画する。</summary>
		private void DrawKeyDropdown() {
			if(m_filteredKeys == null || m_filteredKeys.Length == 0) {
				EditorGUILayout.LabelField("Key", "No keys available");
				return;
			}
			var newKeyIndex = EditorGUILayout.Popup("Key", m_selectedKeyIndex, m_filteredKeys);
			if(newKeyIndex != m_selectedKeyIndex || m_localizationKeyProp.stringValue != m_filteredKeys[newKeyIndex]) {
				m_selectedKeyIndex = newKeyIndex;
				m_localizationKeyProp.stringValue = m_filteredKeys[newKeyIndex];
			}
		}
		/// <summary>選択されたテーブルをSerializedPropertyに反映する。</summary>
		private void ApplySelectedTable() {
			var tableRefProp = m_stringTableProp.FindPropertyRelative("m_TableReference");
			if(tableRefProp == null) return;
			var tableNameProp = tableRefProp.FindPropertyRelative("m_TableCollectionName");
			if(tableNameProp == null) return;
			if(m_selectedTableIndex <= 0 || m_selectedTableIndex > m_tableCollections.Length) {
				tableNameProp.stringValue = "";
			} else {
				tableNameProp.stringValue = m_tableCollections[m_selectedTableIndex - 1].TableCollectionName;
			}
		}
		#endregion
	}
}
