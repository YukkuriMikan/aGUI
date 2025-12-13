using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ANest.UI.Editor {
	/// <summary> uGUI Button を aButton へ移行するコンテキストメニュー </summary>
	public static class ButtonMigrationMenu {
		/// <summary> Button を aButton に変換 </summary>
		[MenuItem("CONTEXT/Button/Migrate to aButton")]
		private static void MigrateButton(MenuCommand command) {
			if(command.context is not Button src) return;
			var go = src.gameObject;
			if(PrefabUtility.IsPartOfAnyPrefab(go)) {
				Debug.LogWarning("Prefab 上の Button は移行できません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。", src);
				EditorUtility.DisplayDialog("Migrate to aButton",
					"Prefab 上の Button は移行できません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。",
					"OK");
				return;
			}
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Migrate to aButton");

			var script = GetScriptOf<aButton>();
			if(script == null) {
				Debug.LogError("Failed to find aButton script for migration.", src);
				return;
			}

			Undo.RegisterCompleteObjectUndo(src, "Migrate to aButton");
			var serializedObject = new SerializedObject(src);
			var scriptProp = serializedObject.FindProperty("m_Script");
			if(scriptProp == null) {
				Debug.LogError("m_Script property not found; cannot migrate Button to aButton.", src);
				return;
			}

			scriptProp.objectReferenceValue = script;
			serializedObject.ApplyModifiedProperties();

			AssignTargetTextIfPossible(serializedObject, go);

			var dst = go.GetComponent<aButton>();
			if(dst != null) {
				EditorUtility.SetDirty(dst);
			} else {
				EditorUtility.SetDirty(go);
			}
			Undo.CollapseUndoOperations(group);
		}

		private static void AssignTargetTextIfPossible(SerializedObject serializedObject, GameObject go) {
			if(serializedObject == null || go == null) return;

			serializedObject.Update();
			var targetTextProp = serializedObject.FindProperty("targetText");
			if(targetTextProp == null) return;

			var text = go.GetComponentInChildren<TMP_Text>(true);
			if(text == null) return;

			targetTextProp.objectReferenceValue = (Object)text;

			var textColorsProp = serializedObject.FindProperty("textColors");
			if(textColorsProp != null) {
				var palette = BuildTextColorBlock(text.color, IsDarkColor(text.color));
				ApplyColorBlock(textColorsProp, palette);
			}
			serializedObject.ApplyModifiedProperties();
		}

		private static bool IsDarkColor(Color color) {
			Color linear = color.linear;
			float luminance = linear.r * 0.2126f + linear.g * 0.7152f + linear.b * 0.0722f;
			return luminance < 0.5f;
		}

		private static void ApplyColorBlock(SerializedProperty prop, ColorBlock block) {
			SetColor(prop, "m_NormalColor", block.normalColor);
			SetColor(prop, "m_HighlightedColor", block.highlightedColor);
			SetColor(prop, "m_PressedColor", block.pressedColor);
			SetColor(prop, "m_SelectedColor", block.selectedColor);
			SetColor(prop, "m_DisabledColor", block.disabledColor);
			SetFloat(prop, "m_ColorMultiplier", block.colorMultiplier);
			SetFloat(prop, "m_FadeDuration", block.fadeDuration);
		}

		private static ColorBlock BuildTextColorBlock(Color baseColor, bool isDark) {
			var block = new ColorBlock {
				normalColor = baseColor,
				colorMultiplier = 1f,
				fadeDuration = 0.1f
			};

			if(isDark) {
				block.highlightedColor = Color.Lerp(baseColor, Color.white, 0.18f);
				block.pressedColor = Color.Lerp(baseColor, Color.white, 0.28f);
				block.selectedColor = Color.Lerp(baseColor, Color.white, 0.14f);
				block.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * 0.5f);
			} else {
				block.highlightedColor = MultiplyColor(baseColor, 0.93f);
				block.pressedColor = MultiplyColor(baseColor, 0.85f);
				block.selectedColor = baseColor;
				block.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * 0.5f);
			}

			return block;
		}

		private static Color MultiplyColor(Color color, float factor) {
			return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
		}

		private static void SetColor(SerializedProperty parent, string name, Color value) {
			var p = parent.FindPropertyRelative(name);
			if(p == null) return;
			p.colorValue = value;
		}

		private static void SetFloat(SerializedProperty parent, string name, float value) {
			var p = parent.FindPropertyRelative(name);
			if(p == null) return;
			p.floatValue = value;
		}

		private static MonoScript GetScriptOf<T>() where T : MonoBehaviour {
			GameObject temp = null;
			try {
				temp = new GameObject("__TempComponentHolder__") {
					hideFlags = HideFlags.HideAndDontSave
				};
				var component = temp.AddComponent<T>();
				return MonoScript.FromMonoBehaviour(component);
			} finally {
				if(temp != null) Object.DestroyImmediate(temp);
			}
		}
	}
}
