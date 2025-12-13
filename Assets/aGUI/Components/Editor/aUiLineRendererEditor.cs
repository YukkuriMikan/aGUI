using UnityEditor;
using UnityEngine;

namespace ANest.UI {
	[CustomEditor(typeof(aUiLineRenderer))]
	public class aUiLineRendererEditor : UnityEditor.Editor {
		private aUiLineRenderer line;
		private SerializedProperty pointsProp;
		private SerializedProperty spaceProp;
		private SerializedProperty enableCornerInterpolationProp;
		private SerializedProperty cornerTypeProp;

		private void OnEnable() {
			line = (aUiLineRenderer)target;
			pointsProp = serializedObject.FindProperty("m_points");
			spaceProp = serializedObject.FindProperty("m_space");
			enableCornerInterpolationProp = serializedObject.FindProperty("m_enableCornerInterpolation");
			cornerTypeProp = serializedObject.FindProperty("m_cornerType");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();
			
			var cornerSettingsDrawn = false;
			var iterator = serializedObject.GetIterator();
			var enterChildren = true;
			while(iterator.NextVisible(enterChildren)) {
				enterChildren = false;
				if(iterator.propertyPath == "m_enableCornerInterpolation" || iterator.propertyPath == "m_cornerType") continue;

				if(!cornerSettingsDrawn && iterator.propertyPath == "m_cornerVertices") {
					EditorGUILayout.PropertyField(enableCornerInterpolationProp);
					if(!enableCornerInterpolationProp.boolValue) {
						EditorGUILayout.PropertyField(cornerTypeProp);
					}
					EditorGUILayout.Space();
					cornerSettingsDrawn = true;
				}

				EditorGUILayout.PropertyField(iterator, true);
			}
			serializedObject.ApplyModifiedProperties();
		}

		private void OnSceneGUI() {
			if(pointsProp == null || spaceProp == null) return;
			serializedObject.Update();

			// ギズモの視認性を高めるため、明るいワイヤーカラーで表示
			var gizmoColor = new Color(1f, 0.45f, 0f, 0.9f); // 濃いオレンジ系
			Handles.color = gizmoColor;

			var worldPoints = new Vector3[pointsProp.arraySize];
			for (int i = 0; i < pointsProp.arraySize; i++) {
				var pointProp = pointsProp.GetArrayElementAtIndex(i);
				var current = pointProp.vector2Value;

				Vector3 worldPos;
				if(line.Space == aUiLineRendererSpace.World) {
					worldPos = new Vector3(current.x, current.y, 0f);
				} else {
					var rt = line.rectTransform;
					worldPos = rt.TransformPoint(new Vector3(current.x, current.y, 0f));
				}
				worldPoints[i] = worldPos;

				EditorGUI.BeginChangeCheck();
				var fmh_41_56_639011357143497637 = Quaternion.identity;
				var newWorldPos = Handles.FreeMoveHandle(worldPos, HandleUtility.GetHandleSize(worldPos) * 0.08f, Vector3.zero, Handles.DotHandleCap);
				if(EditorGUI.EndChangeCheck()) {
					Undo.RecordObject(line, "Move Line Point");
					var newLocal = line.Space == aUiLineRendererSpace.World
						? new Vector2(newWorldPos.x, newWorldPos.y)
						: (Vector2)line.rectTransform.InverseTransformPoint(newWorldPos);
					pointProp.vector2Value = newLocal;
					EditorUtility.SetDirty(line);
				}
			}

			// ワイヤー表示
			if(worldPoints.Length >= 2) {
				Handles.DrawAAPolyLine(2.5f, worldPoints);
				if(line.Loop) {
					Handles.DrawAAPolyLine(2.5f, worldPoints[worldPoints.Length - 1], worldPoints[0]);
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
