using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	/// <summary> uGUI Toggle を aToggle へ移行するコンテキストメニュー </summary>
	public static class ToggleMigrationMenu {
		/// <summary> Toggle を aToggle に変換 </summary>
		[MenuItem("CONTEXT/Toggle/Migrate to aToggle")]
		private static void MigrateToggle(MenuCommand command) {
			if(command.context is ANest.UI.aToggle) return;
			if(command.context is not Toggle src) return;
			var go = src.gameObject;
			if(PrefabUtility.IsPartOfAnyPrefab(go)) {
				Debug.LogWarning("Prefab 上の Toggle は移行できません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。", src);
				EditorUtility.DisplayDialog("Migrate to aToggle",
					"Prefab 上の Toggle は移行できません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。",
					"OK");
				return;
			}

			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Migrate to aToggle");

			var script = GetScriptOf<ANest.UI.aToggle>();
			if(script == null) {
				Debug.LogError("Failed to find aToggle script for migration.", src);
				return;
			}

			Undo.RegisterCompleteObjectUndo(src, "Migrate to aToggle");
			var serializedObject = new SerializedObject(src);
			var scriptProp = serializedObject.FindProperty("m_Script");
			if(scriptProp == null) {
				Debug.LogError("m_Script property not found; cannot migrate Toggle to aToggle.", src);
				return;
			}

			scriptProp.objectReferenceValue = script;
			serializedObject.ApplyModifiedProperties();

			AssignTargetToggleGraphicIfPossible(serializedObject, src);

			var dst = go.GetComponent<ANest.UI.aToggle>();
			if(dst != null) {
				EditorUtility.SetDirty(dst);
			} else {
				EditorUtility.SetDirty(go);
			}
			Undo.CollapseUndoOperations(group);
		}

		[MenuItem("CONTEXT/Toggle/Migrate to aToggle", true)]
		private static bool ValidateMigrateToggle(MenuCommand command) {
			return command.context is Toggle and not ANest.UI.aToggle;
		}

		/// <summary> aToggle を Toggle に戻す </summary>
		[MenuItem("CONTEXT/Toggle/Revert to Toggle")]
		private static void RevertToggle(MenuCommand command) {
			if(command.context is not ANest.UI.aToggle src) return;
			var go = src.gameObject;
			if(PrefabUtility.IsPartOfAnyPrefab(go)) {
				Debug.LogWarning("Prefab 上の aToggle は戻せません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。", src);
				EditorUtility.DisplayDialog("Revert to Toggle",
					"Prefab 上の aToggle は戻せません。プレハブエディタを使用するか、シーン上のインスタンスに対して実行してください。",
					"OK");
				return;
			}
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Revert to Toggle");

			var script = GetScriptOf<Toggle>();
			if(script == null) {
				Debug.LogError("Failed to find Toggle script for revert.", src);
				return;
			}

			Undo.RegisterCompleteObjectUndo(src, "Revert to Toggle");
			var serializedObject = new SerializedObject(src);
			var scriptProp = serializedObject.FindProperty("m_Script");
			if(scriptProp == null) {
				Debug.LogError("m_Script property not found; cannot revert aToggle to Toggle.", src);
				return;
			}

			scriptProp.objectReferenceValue = script;
			serializedObject.ApplyModifiedProperties();

			var dst = go.GetComponent<Toggle>();
			if(dst != null) {
				EditorUtility.SetDirty(dst);
			} else {
				EditorUtility.SetDirty(go);
			}
			Undo.CollapseUndoOperations(group);
		}

		[MenuItem("CONTEXT/Toggle/Revert to Toggle", true)]
		private static bool ValidateRevertToggle(MenuCommand command) {
			return command.context is ANest.UI.aToggle;
		}

		/// <summary> Toggle の graphic を TargetToggleGraphic に設定 </summary>
		private static void AssignTargetToggleGraphicIfPossible(SerializedObject serializedObject, Toggle src) {
			if(serializedObject == null || src == null) return;

			serializedObject.Update();
			var targetToggleGraphicProp = serializedObject.FindProperty("m_TargetToggleGraphic");
			if(targetToggleGraphicProp == null) return;

			if(targetToggleGraphicProp.objectReferenceValue == null && src.graphic != null) {
				targetToggleGraphicProp.objectReferenceValue = src.graphic;
			}
			serializedObject.ApplyModifiedProperties();
		}

		/// <summary> 指定したコンポーネントの MonoScript を取得 </summary>
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