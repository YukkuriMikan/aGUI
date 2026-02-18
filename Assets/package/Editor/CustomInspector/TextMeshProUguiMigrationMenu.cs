using UnityEditor;
using UnityEngine;
using TMPro;

namespace ANest.UI.Editor {
	/// <summary> TextMeshProUGUI を aTextMeshProUgui へ移行するコンテキストメニュー </summary>
	public static class TextMeshProUguiMigrationMenu {
		/// <summary> TextMeshProUGUI を aTextMeshProUgui に変換 </summary>
		[MenuItem("CONTEXT/TextMeshProUGUI/Migrate to aTextMeshProUgui")]
		private static void MigrateTextMeshProUgui(MenuCommand command) {
			if(command.context is aTextMeshProUgui) return;
			if(command.context is not TextMeshProUGUI src) return;
			var go = src.gameObject;
			if(PrefabUtility.IsPartOfAnyPrefab(go)) {
				Debug.LogWarning("Prefab 上の TextMeshProUGUI は移行できません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。", src);
				EditorUtility.DisplayDialog("Migrate to aTextMeshProUgui",
					"Prefab 上の TextMeshProUGUI は移行できません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。",
					"OK");
				return;
			}
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Migrate to aTextMeshProUgui");

			var script = GetScriptOf<aTextMeshProUgui>();
			if(script == null) {
				Debug.LogError("Failed to find aTextMeshProUgui script for migration.", src);
				return;
			}

			Undo.RegisterCompleteObjectUndo(src, "Migrate to aTextMeshProUgui");
			var serializedObject = new SerializedObject(src);
			var scriptProp = serializedObject.FindProperty("m_Script");
			if(scriptProp == null) {
				Debug.LogError("m_Script property not found; cannot migrate TextMeshProUGUI to aTextMeshProUgui.", src);
				return;
			}

			scriptProp.objectReferenceValue = script;
			serializedObject.ApplyModifiedProperties();

			var dst = go.GetComponent<aTextMeshProUgui>();
			if(dst != null) {
				EditorUtility.SetDirty(dst);
			} else {
				EditorUtility.SetDirty(go);
			}
			Undo.CollapseUndoOperations(group);
		}

		[MenuItem("CONTEXT/TextMeshProUGUI/Migrate to aTextMeshProUgui", true)]
		private static bool ValidateMigrateTextMeshProUgui(MenuCommand command) {
			return command.context is TextMeshProUGUI and not aTextMeshProUgui;
		}

		/// <summary> aTextMeshProUgui を TextMeshProUGUI に戻す </summary>
		[MenuItem("CONTEXT/TextMeshProUGUI/Revert to TextMeshProUGUI")]
		private static void RevertTextMeshProUgui(MenuCommand command) {
			if(command.context is not aTextMeshProUgui src) return;
			var go = src.gameObject;
			if(PrefabUtility.IsPartOfAnyPrefab(go)) {
				Debug.LogWarning("Prefab 上の aTextMeshProUgui は戻せません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。", src);
				EditorUtility.DisplayDialog("Revert to TextMeshProUGUI",
					"Prefab 上の aTextMeshProUgui は戻せません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。",
					"OK");
				return;
			}
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Revert to TextMeshProUGUI");

			var script = GetScriptOf<TextMeshProUGUI>();
			if(script == null) {
				Debug.LogError("Failed to find TextMeshProUGUI script for revert.", src);
				return;
			}

			Undo.RegisterCompleteObjectUndo(src, "Revert to TextMeshProUGUI");
			var serializedObject = new SerializedObject(src);
			var scriptProp = serializedObject.FindProperty("m_Script");
			if(scriptProp == null) {
				Debug.LogError("m_Script property not found; cannot revert aTextMeshProUgui to TextMeshProUGUI.", src);
				return;
			}

			scriptProp.objectReferenceValue = script;
			serializedObject.ApplyModifiedProperties();

			var dst = go.GetComponent<TextMeshProUGUI>();
			if(dst != null) {
				EditorUtility.SetDirty(dst);
			} else {
				EditorUtility.SetDirty(go);
			}
			Undo.CollapseUndoOperations(group);
		}

		[MenuItem("CONTEXT/TextMeshProUGUI/Revert to TextMeshProUGUI", true)]
		private static bool ValidateRevertTextMeshProUgui(MenuCommand command) {
			return command.context is aTextMeshProUgui;
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
