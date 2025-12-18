using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	[CustomEditor(typeof(aSubContainer), true)]
	[CanEditMultipleObjects]
	public class aSubContainerEditor : aContainerEditor {
		private SerializedProperty _mainContainerProp;

		protected override void OnEnable() {
			base.OnEnable();
			_mainContainerProp = serializedObject.FindProperty("m_mainContainer");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			EditorGUILayout.LabelField("Sub Container", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_mainContainerProp, new GUIContent("Main Container"));

			EditorGUILayout.Space();
			
			serializedObject.ApplyModifiedProperties();

			base.OnInspectorGUI();
		}
	}
}
