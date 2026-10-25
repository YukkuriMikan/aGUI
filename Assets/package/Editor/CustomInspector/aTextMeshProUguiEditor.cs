using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
namespace ANest.UI {
	/// <summary>aTextMeshProUguiのLocalization対応カスタムインスペクタ。</summary>
	[CustomEditor(typeof(aTextMeshProUgui))]
	[CanEditMultipleObjects]
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
		private string[] m_keyNames;                        // 未選択を含むキー表示名
		private SerializedProperty m_rubySizeModeProp;     // ルビサイズモード
		private SerializedProperty m_rubyScaleProp;         // ルビスケール
		private SerializedProperty m_rubySizeProp;          // ルビサイズ
		private SerializedProperty m_rubyOffsetProp;        // ルビオフセット
		private SerializedProperty m_shrinkWidthProp;
		private aShrinkWidthTmpEditor m_tmpEditor;          // TMP標準エディタ
		#endregion
		#region Unity Methods
		private void OnEnable() {
			m_stringTableProp = serializedObject.FindProperty("m_stringTable");
			m_localizationKeyProp = serializedObject.FindProperty("m_localizationKey");
			RefreshTableCollections();
			SyncSelectedTableIndex();
			RefreshKeys();
			SyncSelectedKeyIndex();
			m_rubySizeModeProp = serializedObject.FindProperty("m_rubySizeMode");
			m_rubyScaleProp = serializedObject.FindProperty("m_rubyScale");
			m_rubySizeProp = serializedObject.FindProperty("m_rubySize");
			m_rubyOffsetProp = serializedObject.FindProperty("m_rubyOffset");
			m_shrinkWidthProp = serializedObject.FindProperty("m_shrinkWidth");
			CreateTmpEditor();
		}
		private void OnDisable() {
			if(m_tmpEditor != null) {
				DestroyImmediate(m_tmpEditor);
				m_tmpEditor = null;
			}
		}
		private void OnSceneGUI() {
			// TMP標準のマージン枠と編集ハンドルも描画する。
			m_tmpEditor?.OnSceneGUI();
		}
		public override void OnInspectorGUI() {
			serializedObject.Update();
			int previousTableIndex = m_selectedTableIndex;
			SyncSelectedTableIndex();
			if(previousTableIndex != m_selectedTableIndex) RefreshKeys();
			SyncSelectedKeyIndex();
			EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
			DrawTableDropdown();
			DrawKeySearchField();
			DrawKeyDropdown();
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Ruby", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(m_rubySizeModeProp, new GUIContent("Size Mode"));
			var mode = (RubySizeMode)m_rubySizeModeProp.enumValueIndex;
			if(mode == RubySizeMode.Scale || m_rubySizeModeProp.hasMultipleDifferentValues) {
				EditorGUILayout.PropertyField(m_rubyScaleProp, new GUIContent("Scale"));
			}
			if(mode == RubySizeMode.Size || m_rubySizeModeProp.hasMultipleDifferentValues) {
				EditorGUILayout.PropertyField(m_rubySizeProp, new GUIContent("Font Size"));
			}
			EditorGUILayout.PropertyField(m_rubyOffsetProp, new GUIContent("Offset"));
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(m_shrinkWidthProp, new GUIContent("Shrink Width"));
			if(m_shrinkWidthProp.boolValue || m_shrinkWidthProp.hasMultipleDifferentValues)
				EditorGUILayout.HelpBox("横にはみ出す場合、本文とルビの高さを維持して横幅を縮小します。明示改行は維持します。OFFにすると元のWrap Mode・Overflow・Auto Size設定に戻ります。", MessageType.Info);
			if(serializedObject.ApplyModifiedProperties()) {
				foreach(var t in targets) {
					var tmp = (aTextMeshProUgui)t;
					tmp.LocalizationKey = tmp.LocalizationKey;
					tmp.ForceMeshUpdate();
				}
			}
			EditorGUILayout.Space();
			if(m_tmpEditor != null) {
				EditorGUI.BeginChangeCheck();
				m_tmpEditor.OnInspectorGUI();
				if(EditorGUI.EndChangeCheck())
					foreach(var t in targets) ((aTextMeshProUgui)t).ForceMeshUpdate();
			}
		}
		#endregion
		#region Private Methods
		/// <summary>TMP標準エディタを生成する。</summary>
		private void CreateTmpEditor() {
			m_tmpEditor = (aShrinkWidthTmpEditor)CreateEditor(targets, typeof(aShrinkWidthTmpEditor));
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
				m_keyNames = new[] { "(None)" };
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
					m_selectedKeyIndex = i + 1;
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
			m_keyNames = new[] { "(None)" }.Concat(m_filteredKeys).ToArray();
		}
		/// <summary>StringTableドロップダウンを描画する。</summary>
		private void DrawTableDropdown() {
			bool mixed = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = m_stringTableProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUILayout.Popup("String Table", m_selectedTableIndex, m_tableNames);
			if(EditorGUI.EndChangeCheck()) {
				m_selectedTableIndex = newIndex;
				ApplySelectedTable();
				RefreshKeys();
				m_selectedKeyIndex = 0;
				m_localizationKeyProp.stringValue = "";
			}
			EditorGUI.showMixedValue = mixed;
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
			bool mixed = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = m_localizationKeyProp.hasMultipleDifferentValues;
			using(new EditorGUI.DisabledScope(m_stringTableProp.hasMultipleDifferentValues)) {
				EditorGUI.BeginChangeCheck();
				var newKeyIndex = EditorGUILayout.Popup("Key", m_selectedKeyIndex, m_keyNames);
				if(EditorGUI.EndChangeCheck()) {
					m_selectedKeyIndex = newKeyIndex;
					m_localizationKeyProp.stringValue = newKeyIndex > 0 ? m_filteredKeys[newKeyIndex - 1] : "";
				}
			}
			EditorGUI.showMixedValue = mixed;
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
