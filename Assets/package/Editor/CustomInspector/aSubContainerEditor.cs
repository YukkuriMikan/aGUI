using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	[CustomEditor(typeof(aSubContainer), true)]
	[CanEditMultipleObjects]
	public class aSubContainerEditor : aContainerEditor {
		public override void OnInspectorGUI() {
			serializedObject.Update();

			EditorGUILayout.PropertyField(serializedObject.FindProperty("m_mainContainer"));
			DrawPropertiesExcluding(serializedObject, "m_Script", "m_mainContainer");

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();

			bool isPlaying = Application.isPlaying;
			using(new EditorGUI.DisabledScope(!isPlaying)) {
				EditorGUILayout.BeginHorizontal();
				if(GUILayout.Button("Show")) {
					foreach(var obj in targets) {
						if(obj is aContainerBase container) {
							container.Show();
						}
					}
				}
				if(GUILayout.Button("Hide")) {
					foreach(var obj in targets) {
						if(obj is aContainerBase container) {
							container.Hide();
						}
					}
				}
				EditorGUILayout.EndHorizontal();
			}

			if(!isPlaying) {
				EditorGUILayout.HelpBox("Show/Hide試験実行ボタンはエディタ再生中のみ使用できます。", MessageType.Info);
			}
		}
	}
}