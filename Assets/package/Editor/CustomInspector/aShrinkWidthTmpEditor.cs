using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace ANest.UI {
	/// <summary>Shrink Width中だけ、競合する設定を無効表示するTMP Inspector。</summary>
	// TMPのDrawFont/DrawWrappingOverflowはprivateのため、ON時のMain Settingsをここで描画する。
	// Text InputとExtra Settings、OFF時のInspectorはTMP標準の実装を使用する。
	internal sealed class aShrinkWidthTmpEditor : TMP_EditorPanelUI {
		private static readonly GUIContent SpacingLabel = new("Spacing Options (em)", "Spacing adjustments between characters, words, lines and paragraphs.");
		private static readonly GUIContent HorizontalScaleLabel = new("Character Horizontal Scale", "The horizontal scale of characters. A value of 1 is normal width.");
		private static readonly string[] SpacingProperties = { "m_characterSpacing", "m_wordSpacing", "m_lineSpacing", "m_paragraphSpacing" };
		private static readonly GUIContent[] SpacingLabels = { new("Character"), new("Word"), new("Line"), new("Paragraph") };
		private readonly System.Collections.Generic.Dictionary<string, SerializedProperty> m_properties = new();
		private readonly System.Collections.Generic.Dictionary<string, GUIContent> m_labels = new();

		public override void OnInspectorGUI() {
			bool shrinkWidth = System.Array.Exists(targets, item => ((aTextMeshProUgui)item).ShrinkWidth);
			if(!shrinkWidth) { base.OnInspectorGUI(); return; }
			if(IsMixSelectionTypes()) return;
			serializedObject.Update();
			DrawTextInput();
			EditorGUILayout.LabelField("Main Settings", EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			Field("m_fontAsset", "Font Asset");
			if(EditorGUI.EndChangeCheck()) {
				Property("m_hasFontAssetChanged").boolValue = true;
				m_MaterialPresetNames = GetMaterialPresets();
			}
			DrawMaterialPreset();
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
				DrawGradient();
			}
			Field("m_overrideHtmlColors", "Override Tags");
			DrawSpacing();
			// TMPのアラインメントenumはFlagsなので、複数選択できないPopupで表示する。
			DrawAlignment("m_HorizontalAlignment", "Horizontal Alignment", true);
			DrawAlignment("m_VerticalAlignment", "Vertical Alignment", false);
			if(System.Array.Exists(targets, item => {
				var alignment = ((TMP_Text)item).horizontalAlignment;
				return alignment == HorizontalAlignmentOptions.Justified || alignment == HorizontalAlignmentOptions.Flush;
			})) DrawPropertySlider(new GUIContent("Wrap Mix (W <-> C)"), m_WordWrappingRatiosProp);
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

		private void DrawSpacing() {
			EditorGUILayout.Space();
			float labelWidth = EditorGUIUtility.labelWidth;
			int indent = EditorGUI.indentLevel;
			try {
				var row = EditorGUILayout.GetControlRect();
				float shortLabelWidth = EditorStyles.label.CalcSize(SpacingLabels[3]).x + 4;
				float minCellWidth = shortLabelWidth + 50;
				bool inlineLabel = row.width >= labelWidth + minCellWidth * 2 + 3;
				if(inlineLabel) EditorGUI.PrefixLabel(row, SpacingLabel);
				else {
					EditorGUI.LabelField(row, SpacingLabel);
					row = EditorGUILayout.GetControlRect(false);
				}
				EditorGUI.indentLevel = 0;
				EditorGUIUtility.labelWidth = shortLabelWidth;
				float offset = inlineLabel ? labelWidth : 0;
				int columns = row.width - offset >= minCellWidth * 2 + 3 ? 2 : 1;
				for(int i = 0; i < SpacingProperties.Length; i++) {
					if(i > 0 && i % columns == 0) row = EditorGUILayout.GetControlRect(false);
					float cellWidth = (row.width - offset - (columns - 1) * 3) / columns;
					var cell = new Rect(row.x + offset + i % columns * (cellWidth + 3), row.y, cellWidth, row.height);
					EditorGUI.PropertyField(cell, Property(SpacingProperties[i]), SpacingLabels[i]);
				}
				EditorGUI.indentLevel = indent;
				// 長い横倍率ラベルにも必要な幅を確保し、数値欄はTMP標準と同じコンパクトな幅にする。
				EditorGUIUtility.labelWidth = Mathf.Max(labelWidth, EditorStyles.label.CalcSize(HorizontalScaleLabel).x + 4);
				EditorGUILayout.PropertyField(Property("m_characterHorizontalScale"), HorizontalScaleLabel,
					GUILayout.MaxWidth(EditorGUIUtility.labelWidth + 50));
			} finally {
				EditorGUIUtility.labelWidth = labelWidth;
				EditorGUI.indentLevel = indent;
			}
			EditorGUILayout.Space();
		}

		private void DrawMaterialPreset() {
			if(m_MaterialPresetNames == null || m_IsPresetListDirty) m_MaterialPresetNames = GetMaterialPresets();
			if(m_MaterialPresetNames == null || m_MaterialPresetNames.Length == 0 || m_FontAssetProp.hasMultipleDifferentValues) {
				Field("m_sharedMaterial", "Material");
				return;
			}
			int index = -1;
			if(m_FontSharedMaterialProp.objectReferenceValue != null)
				if(!m_MaterialPresetIndexLookup.TryGetValue(m_FontSharedMaterialProp.objectReferenceValue.GetInstanceID(), out index)) index = -1;
			bool mixed = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = m_FontSharedMaterialProp.hasMultipleDifferentValues;
			var rect = EditorGUILayout.GetControlRect();
			EditorGUI.BeginProperty(rect, new GUIContent("Material Preset"), m_FontSharedMaterialProp);
			EditorGUI.BeginChangeCheck();
			int selected = EditorGUI.Popup(rect, new GUIContent("Material Preset"), index, m_MaterialPresetNames);
			if(EditorGUI.EndChangeCheck()) m_FontSharedMaterialProp.objectReferenceValue = m_MaterialPresets[selected];
			EditorGUI.EndProperty();
			EditorGUI.showMixedValue = mixed;
		}

		private void DrawGradient() {
			// プリセットを共有している場合は、TMP標準と同じくアセットをその場で編集する。
			if(m_FontColorGradientPresetProp.hasMultipleDifferentValues) return;
			var preset = m_FontColorGradientPresetProp.objectReferenceValue as TMP_ColorGradient;
			using(var presetObject = preset != null ? new SerializedObject(preset) : null) {
				presetObject?.Update();
				var mode = presetObject != null ? presetObject.FindProperty("colorMode") : m_ColorModeProp;
				var topLeft = presetObject != null ? presetObject.FindProperty("topLeft") : m_FontColorGradientProp.FindPropertyRelative("topLeft");
				var topRight = presetObject != null ? presetObject.FindProperty("topRight") : m_FontColorGradientProp.FindPropertyRelative("topRight");
				var bottomLeft = presetObject != null ? presetObject.FindProperty("bottomLeft") : m_FontColorGradientProp.FindPropertyRelative("bottomLeft");
				var bottomRight = presetObject != null ? presetObject.FindProperty("bottomRight") : m_FontColorGradientProp.FindPropertyRelative("bottomRight");
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField(mode, new GUIContent("Color Mode"));
				EditorGUILayout.PropertyField(topLeft, new GUIContent("Top Left"));
				if(mode.hasMultipleDifferentValues || (ColorMode)mode.enumValueIndex == ColorMode.FourCornersGradient) {
					EditorGUILayout.PropertyField(topRight, new GUIContent("Top Right"));
					EditorGUILayout.PropertyField(bottomLeft, new GUIContent("Bottom Left"));
					EditorGUILayout.PropertyField(bottomRight, new GUIContent("Bottom Right"));
				} else {
					if((ColorMode)mode.enumValueIndex == ColorMode.HorizontalGradient)
						EditorGUILayout.PropertyField(topRight, new GUIContent("Top Right"));
					if((ColorMode)mode.enumValueIndex == ColorMode.VerticalGradient)
						EditorGUILayout.PropertyField(bottomLeft, new GUIContent("Bottom Left"));
				}
				if(EditorGUI.EndChangeCheck()) {
					m_HavePropertiesChanged = true;
					if(presetObject != null) {
						SyncGradientColors(presetObject, true);
						if(presetObject.ApplyModifiedProperties()) TMPro_EventManager.ON_COLOR_GRADIENT_PROPERTY_CHANGED(preset);
					} else {
						serializedObject.ApplyModifiedProperties();
						// 各オブジェクト自身の色を使い、未編集の混在値を先頭の値で上書きしない。
						foreach(var item in targets) {
							using(var textObject = new SerializedObject(item)) {
								SyncGradientColors(textObject, false);
								textObject.ApplyModifiedProperties();
							}
						}
						serializedObject.Update();
					}
				}
			}
		}

		private static void SyncGradientColors(SerializedObject obj, bool preset) {
			var mode = obj.FindProperty(preset ? "colorMode" : "m_colorMode");
			var gradient = preset ? null : obj.FindProperty("m_fontColorGradient");
			var topLeft = preset ? obj.FindProperty("topLeft") : gradient.FindPropertyRelative("topLeft");
			var topRight = preset ? obj.FindProperty("topRight") : gradient.FindPropertyRelative("topRight");
			var bottomLeft = preset ? obj.FindProperty("bottomLeft") : gradient.FindPropertyRelative("bottomLeft");
			var bottomRight = preset ? obj.FindProperty("bottomRight") : gradient.FindPropertyRelative("bottomRight");
			switch((ColorMode)mode.enumValueIndex) {
				case ColorMode.Single:
					topRight.colorValue = bottomLeft.colorValue = bottomRight.colorValue = topLeft.colorValue;
					break;
				case ColorMode.HorizontalGradient:
					bottomLeft.colorValue = topLeft.colorValue;
					bottomRight.colorValue = topRight.colorValue;
					break;
				case ColorMode.VerticalGradient:
					topRight.colorValue = topLeft.colorValue;
					bottomRight.colorValue = bottomLeft.colorValue;
					break;
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
			bool mixed = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			int value = horizontal
				? (int)(HorizontalAlignmentOptions)EditorGUILayout.EnumPopup(label, (HorizontalAlignmentOptions)property.intValue)
				: (int)(VerticalAlignmentOptions)EditorGUILayout.EnumPopup(label, (VerticalAlignmentOptions)property.intValue);
			if(EditorGUI.EndChangeCheck()) property.intValue = value;
			EditorGUI.showMixedValue = mixed;
		}
	}
}
