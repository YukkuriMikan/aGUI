using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	/// <summary>aGUIのエディタ機能で共通利用するユーティリティ</summary>
	public static class aGuiEditorUtils {
		#region Fields
		public static readonly ColorBlock TextColorPresetWhite = new() {
			normalColor = Color.white,
			highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f),
			pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f),
			selectedColor = Color.white,
			disabledColor = new Color(1f, 1f, 1f, 0.5f),
			colorMultiplier = 1f,
			fadeDuration = 0.1f
		}; // 白系のテキストカラープリセット

		public static readonly ColorBlock TextColorPresetBlack = new() {
			normalColor = Color.black,
			highlightedColor = new Color(0.2f, 0.2f, 0.2f, 1f),
			pressedColor = new Color(0.3f, 0.3f, 0.3f, 1f),
			selectedColor = Color.black,
			disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f),
			colorMultiplier = 1f,
			fadeDuration = 0.1f
		}; // 黒系のテキストカラープリセット
		#endregion

		#region Methods
		/// <summary>テキストカラーのプリセットボタンを描画し選択された色を適用する</summary>
		public static void DrawTextColorPresetButtons(SerializedProperty colorsProperty) {
			using (new EditorGUILayout.HorizontalScope()) {
				GUI.enabled &= colorsProperty != null;
				if(GUILayout.Button("White Text Colors")) {
					ApplyColorBlockPreset(colorsProperty, TextColorPresetWhite);
				}
				if(GUILayout.Button("Black Text Colors")) {
					ApplyColorBlockPreset(colorsProperty, TextColorPresetBlack);
				}
				GUI.enabled = true;
			}
		}

		/// <summary>ColorBlockプリセットの値をSerializedPropertyへ反映する</summary>
		public static void ApplyColorBlockPreset(SerializedProperty colorsProperty, ColorBlock preset) {
			if(colorsProperty == null) return;

			SetColorProperty(colorsProperty, "m_NormalColor", preset.normalColor);
			SetColorProperty(colorsProperty, "m_HighlightedColor", preset.highlightedColor);
			SetColorProperty(colorsProperty, "m_PressedColor", preset.pressedColor);
			SetColorProperty(colorsProperty, "m_SelectedColor", preset.selectedColor);
			SetColorProperty(colorsProperty, "m_DisabledColor", preset.disabledColor);

			SerializedProperty colorMultiplier = colorsProperty.FindPropertyRelative("m_ColorMultiplier");
			if(colorMultiplier != null) {
				colorMultiplier.floatValue = preset.colorMultiplier;
			}

			SerializedProperty fadeDuration = colorsProperty.FindPropertyRelative("m_FadeDuration");
			if(fadeDuration != null) {
				fadeDuration.floatValue = preset.fadeDuration;
			}
		}

		/// <summary>ColorBlock内の特定色プロパティへ値を設定する</summary>
		public static void SetColorProperty(SerializedProperty parent, string name, Color value) {
			SerializedProperty prop = parent.FindPropertyRelative(name);
			if(prop != null) {
				prop.colorValue = value;
			}
		}
		#endregion
	}
}
