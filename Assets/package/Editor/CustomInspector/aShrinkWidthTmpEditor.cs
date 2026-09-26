using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace ANest.UI {
	/// <summary>Shrink Width中だけ、競合する設定を無効表示するTMP Inspector。</summary>
	// TMPのDrawFont/DrawWrappingOverflowはprivateのため、ON時のMain Settingsをここで描画する。
	// Text InputとExtra Settings、OFF時のInspectorはTMP標準の実装を使用する。
	internal sealed class aShrinkWidthTmpEditor : TMP_EditorPanelUI {
		private readonly System.Collections.Generic.Dictionary<string, SerializedProperty> m_properties = new();
		private readonly System.Collections.Generic.Dictionary<string, GUIContent> m_labels = new();

		public override void OnInspectorGUI() {
			if(!((aTextMeshProUgui)target).ShrinkWidth) { base.OnInspectorGUI(); return; }
			serializedObject.Update();
			DrawTextInput();
			EditorGUILayout.LabelField("Main Settings", EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			Field("m_fontAsset", "Font Asset");
			if(EditorGUI.EndChangeCheck()) Property("m_hasFontAssetChanged").boolValue = true;
			Field("m_sharedMaterial", "Material");
			Field("m_fontStyle", "Font Style");
			EditorGUI.BeginChangeCheck();
			Field("m_fontSize", "Font Size");
			if(EditorGUI.EndChangeCheck()) {
				float size = Mathf.Clamp(Property("m_fontSize").floatValue, 0, 32767);
				Property("m_fontSize").floatValue = Property("m_fontSizeBase").floatValue = size;
			}
			using(new EditorGUI.DisabledScope(true)) Field("m_enableAutoSizing", "Auto Size");
			Field("m_fontColor", "Vertex Color");
			Field("m_enableVertexGradient", "Color Gradient");
			if(Property("m_enableVertexGradient").boolValue) {
				Field("m_fontColorGradientPreset", "Gradient Preset");
				Field("m_fontColorGradient", "Gradient");
			}
			Field("m_overrideHtmlColors", "Override Tags");
			Field("m_characterSpacing", "Character Spacing");
			Field("m_wordSpacing", "Word Spacing");
			Field("m_lineSpacing", "Line Spacing");
			Field("m_paragraphSpacing", "Paragraph Spacing");
			// TMPのアラインメントenumはFlagsなので、複数選択できないPopupで表示する。
			DrawAlignment("m_HorizontalAlignment", "Horizontal Alignment", true);
			DrawAlignment("m_VerticalAlignment", "Vertical Alignment", false);
			using(new EditorGUI.DisabledScope(true)) {
				Field("m_TextWrappingMode", "Wrap Mode");
				Field("m_overflowMode", "Overflow");
			}
			Field("m_horizontalMapping", "Horizontal Mapping");
			Field("m_verticalMapping", "Vertical Mapping");
			Field("m_uvLineOffset", "UV Line Offset");
			DrawExtraSettings();
			if(serializedObject.ApplyModifiedProperties() || m_HavePropertiesChanged) {
				foreach(var item in targets) ((TMP_Text)item).havePropertiesChanged = true;
				m_HavePropertiesChanged = false;
			}
		}

		private SerializedProperty Property(string name) {
			if(!m_properties.TryGetValue(name, out var property)) m_properties[name] = property = serializedObject.FindProperty(name);
			return property;
		}

		private void Field(string name, string label) {
			if(!m_labels.TryGetValue(name, out var content)) m_labels[name] = content = new GUIContent(label);
			EditorGUILayout.PropertyField(Property(name), content, true);
		}

		private void DrawAlignment(string name, string label, bool horizontal) {
			var property = Property(name);
			EditorGUI.BeginChangeCheck();
			int value = horizontal
				? (int)(HorizontalAlignmentOptions)EditorGUILayout.EnumPopup(label, (HorizontalAlignmentOptions)property.intValue)
				: (int)(VerticalAlignmentOptions)EditorGUILayout.EnumPopup(label, (VerticalAlignmentOptions)property.intValue);
			if(EditorGUI.EndChangeCheck()) property.intValue = value;
		}
	}
}
