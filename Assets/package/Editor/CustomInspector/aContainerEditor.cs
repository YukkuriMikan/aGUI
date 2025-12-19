using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	[CustomEditor(typeof(aContainerBase), true)]
	[CanEditMultipleObjects]
	public class aContainerEditor : UnityEditor.Editor {
		private SerializedProperty _useCustomAnimationsProp;
		private SerializedProperty _useSharedAnimationProp;
		private SerializedProperty _sharedAnimationProp;
		private SerializedProperty _showAnimationsProp;
		private SerializedProperty _hideAnimationsProp;
		private SerializedProperty _isVisibleProp;

		protected virtual void OnEnable() {
			_useCustomAnimationsProp = serializedObject.FindProperty("m_useCustomAnimations");
			_useSharedAnimationProp = serializedObject.FindProperty("m_useSharedAnimation");
			_sharedAnimationProp = serializedObject.FindProperty("m_sharedAnimation");
			_showAnimationsProp = serializedObject.FindProperty("m_showAnimations");
			_hideAnimationsProp = serializedObject.FindProperty("m_hideAnimations");
			_isVisibleProp = serializedObject.FindProperty("m_isVisible");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawAnimationSection();

			EditorGUILayout.Space();
			DrawStateSection();

			EditorGUILayout.Space();
			
			DrawPropertiesExcluding(serializedObject, GetExcludedProperties());

			serializedObject.ApplyModifiedProperties();

			DrawPlayButtons();
		}

		protected virtual string[] GetExcludedProperties() {
			return new[] {
				"m_Script",
				"m_useCustomAnimations",
				"m_useSharedAnimation",
				"m_sharedAnimation",
				"m_showAnimations",
				"m_hideAnimations",
				"m_isVisible"
			};
		}

		private void DrawStateSection() {
			EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
			
			EditorGUI.showMixedValue = _isVisibleProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			bool isVisible = EditorGUILayout.Toggle("Is Visible", _isVisibleProp.boolValue);
			if (EditorGUI.EndChangeCheck()) {
				if (Application.isPlaying) {
					Undo.RecordObjects(targets, "Change Container Visibility");
					foreach (var obj in targets) {
						if (obj is aContainerBase container) {
							container.IsVisible = isVisible;
							EditorUtility.SetDirty(container);
						}
					}
				} else {
					_isVisibleProp.boolValue = isVisible;
				}
			}
			EditorGUI.showMixedValue = false;
		}

		private void DrawAnimationSection() {
			bool hasSharedAsset = _sharedAnimationProp != null && _sharedAnimationProp.objectReferenceValue != null;
			bool isSharedEnabled = _useSharedAnimationProp != null && _useSharedAnimationProp.boolValue && hasSharedAsset;
			SerializedObject sharedAnimationSerializedObject = null;

			if(hasSharedAsset) {
				sharedAnimationSerializedObject = new SerializedObject(_sharedAnimationProp.objectReferenceValue);
				sharedAnimationSerializedObject.Update();
			}

			EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_useSharedAnimationProp, new GUIContent("Use Shared Animation"));
			EditorGUILayout.PropertyField(_sharedAnimationProp, new GUIContent("Shared Animation Set"));
			if(_useSharedAnimationProp.boolValue && !hasSharedAsset) {
				EditorGUILayout.HelpBox("Shared Animation Set が設定されていません", MessageType.Warning);
			}

			if(isSharedEnabled && sharedAnimationSerializedObject != null) {
				SerializedProperty showAnimationsShared = sharedAnimationSerializedObject.FindProperty("showAnimations");
				SerializedProperty hideAnimationsShared = sharedAnimationSerializedObject.FindProperty("hideAnimations");

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Shared Animation Settings", EditorStyles.miniBoldLabel);

				if(showAnimationsShared != null) {
					EditorGUILayout.PropertyField(showAnimationsShared, new GUIContent("Show Animations"));
				} else {
					EditorGUILayout.HelpBox("Show Animations プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
				}

				if(hideAnimationsShared != null) {
					EditorGUILayout.PropertyField(hideAnimationsShared, new GUIContent("Hide Animations"));
				} else {
					EditorGUILayout.HelpBox("Hide Animations プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
				}

				sharedAnimationSerializedObject.ApplyModifiedProperties();
			} else {
				EditorGUILayout.Space();
				EditorGUILayout.PropertyField(_useCustomAnimationsProp, new GUIContent("Use Custom Animations"));
				if(_useCustomAnimationsProp.boolValue) {
					EditorGUILayout.PropertyField(_showAnimationsProp, new GUIContent("Show Animations"));
					EditorGUILayout.PropertyField(_hideAnimationsProp, new GUIContent("Hide Animations"));
				}
			}
		}

		private void DrawPlayButtons() {
			EditorGUILayout.Space();

			bool isPlaying = Application.isPlaying;
			using (new EditorGUI.DisabledScope(!isPlaying)) {
				EditorGUILayout.BeginHorizontal();
				if(GUILayout.Button("Show")) {
					foreach (var obj in targets) {
						if(obj is aContainerBase container) {
							container.Show();
						}
					}
				}
				if(GUILayout.Button("Hide")) {
					foreach (var obj in targets) {
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
