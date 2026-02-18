using UnityEditor;
using UnityEngine;
using TMPro;

namespace ANest.UI.Editor {
	/// <summary> TextMeshProUGUI を aTextMeshProUgui へ移行するコンテキストメニュー </summary>
	public static class TextMeshProUguiMigrationMenu {
		/// <summary> TextMeshProUGUI を aTextMeshProUgui に変換 </summary>
		[MenuItem("CONTEXT/TextMeshProUGUI/Migrate to aTextMeshProUgui")]
		private static void MigrateTextMeshProUgui(MenuCommand command) {
			if(command.context is not TextMeshProUGUI src) return;
			if(src is aTextMeshProUgui) return; // 既に aTextMeshProUgui の場合はスキップ
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
