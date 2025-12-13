using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	/// <summary> aLayoutGroup 系のインスペクタを拡張するカスタムエディタ </summary>
	[CustomEditor(typeof(aLayoutGroupBase), true)]
	[CanEditMultipleObjects]
	public class aLayoutGroupEditor : UnityEditor.Editor {
		#region Fields
		private SerializedProperty paddingProp;                    // padding プロパティ
		private SerializedProperty childAlignmentProp;             // childAlignment プロパティ
		private SerializedProperty reverseArrangementProp;         // reverseArrangement プロパティ
		private SerializedProperty updateModeProp;                 // updateMode プロパティ
		private SerializedProperty excludedChildrenProp;           // excludedChildren プロパティ
		private SerializedProperty spacingProp;                    // spacing プロパティ（HV用）
		private SerializedProperty childForceExpandCircularProp;   // Circular: childForceExpand プロパティ
		private SerializedProperty childControlWidthProp;          // childControlWidth プロパティ
		private SerializedProperty childControlHeightProp;         // childControlHeight プロパティ
		private SerializedProperty childScaleWidthProp;            // childScaleWidth プロパティ
		private SerializedProperty childScaleHeightProp;           // childScaleHeight プロパティ
		private SerializedProperty childForceExpandWidthProp;      // childForceExpandWidth プロパティ
		private SerializedProperty childForceExpandHeightProp;     // childForceExpandHeight プロパティ
		private SerializedProperty setNavigationProp;              // setNavigation プロパティ
		private SerializedProperty navigationLoopProp;             // navigationLoop プロパティ
		private SerializedProperty navigationAxisRangeProp;        // navigationAxisRange プロパティ
		private SerializedProperty navigationTypeProp;             // navigationType プロパティ（Circular）
		private SerializedProperty headStartTargetProp;            // Circular: headStartTarget プロパティ
		private SerializedProperty useAnimationProp;               // useAnimation プロパティ
		private SerializedProperty animationDurationProp;          // animationDuration プロパティ
		private SerializedProperty animationDistanceThresholdProp; // animationDistanceThreshold プロパティ
		private SerializedProperty useAnimationCurveProp;          // useAnimationCurve プロパティ
		private SerializedProperty animationCurveProp;             // animationCurve プロパティ
		private SerializedProperty animationEaseProp;              // animationEase プロパティ
		private SerializedProperty useCircularMoveProp;            // Circular: useCircularMove プロパティ
		private SerializedProperty circularMoveTypeProp;           // Circular: circularMoveType プロパティ
		private SerializedProperty startCornerProp;                // Grid: startCorner プロパティ
		private SerializedProperty startAxisProp;                  // Grid: startAxis プロパティ
		private SerializedProperty cellSizeProp;                   // Grid: cellSize プロパティ
		private SerializedProperty spacingXYProp;                  // Grid: spacingXY プロパティ
		private SerializedProperty constraintProp;                 // Grid: constraint プロパティ
		private SerializedProperty constraintCountProp;            // Grid: constraintCount プロパティ

		private static readonly GUIContent childControlsLabel = new("Control Child Size");    // 子サイズ制御ラベル
		private static readonly GUIContent childControlWidthLabel = new("Width");             // 幅ラベル
		private static readonly GUIContent childControlHeightLabel = new("Height");           // 高さラベル
		private static readonly GUIContent childScaleLabel = new("Use Child Scale");          // スケール利用ラベル
		private static readonly GUIContent childForceExpandLabel = new("Child Force Expand"); // 強制拡張ラベル
		private static readonly GUIContent navigationLabel = new("Set Navigation");           // ナビ設定ラベル
		private static readonly GUIContent navigationEnableLabel = new("Enable");             // ナビ有効ラベル
		private static readonly GUIContent navigationLoopLabel = new("Loop");                 // ナビループラベル
		#endregion

		private void OnEnable() {
			paddingProp = serializedObject.FindProperty("padding");
			childAlignmentProp = serializedObject.FindProperty("childAlignment");
			reverseArrangementProp = serializedObject.FindProperty("reverseArrangement");
			updateModeProp = serializedObject.FindProperty("updateMode");
			excludedChildrenProp = serializedObject.FindProperty("excludedChildren");
			spacingProp = serializedObject.FindProperty("spacing");
			childForceExpandCircularProp = serializedObject.FindProperty("childForceExpand");
			childControlWidthProp = serializedObject.FindProperty("childControlWidth");
			childControlHeightProp = serializedObject.FindProperty("childControlHeight");
			childScaleWidthProp = serializedObject.FindProperty("childScaleWidth");
			childScaleHeightProp = serializedObject.FindProperty("childScaleHeight");
			childForceExpandWidthProp = serializedObject.FindProperty("childForceExpandWidth");
			childForceExpandHeightProp = serializedObject.FindProperty("childForceExpandHeight");
			setNavigationProp = serializedObject.FindProperty("setNavigation");
			navigationLoopProp = serializedObject.FindProperty("navigationLoop");
			navigationAxisRangeProp = serializedObject.FindProperty("navigationAxisRange");
			navigationTypeProp = serializedObject.FindProperty("navigationType");
			useAnimationProp = serializedObject.FindProperty("useAnimation");
			animationDurationProp = serializedObject.FindProperty("animationDuration");
			animationDistanceThresholdProp = serializedObject.FindProperty("animationDistanceThreshold");
			useAnimationCurveProp = serializedObject.FindProperty("useAnimationCurve");
			animationCurveProp = serializedObject.FindProperty("animationCurve");
			animationEaseProp = serializedObject.FindProperty("animationEase");
			useCircularMoveProp = serializedObject.FindProperty("useCircularMove");
			circularMoveTypeProp = serializedObject.FindProperty("circularMoveType");
			headStartTargetProp = serializedObject.FindProperty("headStartTarget");
			startCornerProp = serializedObject.FindProperty("startCorner");
			startAxisProp = serializedObject.FindProperty("startAxis");
			cellSizeProp = serializedObject.FindProperty("cellSize");
			spacingXYProp = serializedObject.FindProperty("spacingXY");
			constraintProp = serializedObject.FindProperty("constraint");
			constraintCountProp = serializedObject.FindProperty("constraintCount");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawLayoutBaseProperties();
			DrawDerivedProperties();

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();
			if(GUILayout.Button("Rebuild Layout")) {
				RebuildTargets();
			}
			if(target is aLayoutGroupCircular) {
				if(GUILayout.Button("Head Start")) {
					InvokeHeadStartButton();
				}
			}
		}

		private void DrawLayoutBaseProperties() {
			DrawScriptField();
			PropertyFieldSafe(updateModeProp);
			PropertyFieldSafe(excludedChildrenProp, true);
			PropertyFieldSafe(paddingProp, true);

			if(target is aLayoutGroupGrid) {
				PropertyFieldSafe(cellSizeProp);
				PropertyFieldSafe(spacingXYProp, new GUIContent("Spacing"));
				PropertyFieldSafe(startCornerProp);
				PropertyFieldSafe(startAxisProp);
				PropertyFieldSafe(childAlignmentProp);
				PropertyFieldSafe(constraintProp);
				if(constraintProp != null && constraintProp.enumValueIndex != 0) {
					PropertyFieldSafe(constraintCountProp);
				}
				PropertyFieldSafe(reverseArrangementProp);
				DrawChildControlsSection();
				DrawAnimationSection();
			} else {
				PropertyFieldSafe(spacingProp);
				PropertyFieldSafe(childAlignmentProp);
				PropertyFieldSafe(reverseArrangementProp);
				DrawChildControlsSection();
				DrawAnimationSection();
				if(target is aLayoutGroupCircular) {
					PropertyFieldSafe(headStartTargetProp, new GUIContent("Head Start Target"));
				}
			}
		}

		private void InvokeHeadStartButton() {
			foreach (var obj in targets) {
				if(obj is aLayoutGroupCircular circular) {
					Undo.RecordObject(circular, "Head Start");
					circular.HeadStart();
					EditorUtility.SetDirty(circular);
				}
			}
		}

		private void DrawChildControlsSection() {
			bool isCircular = target is aLayoutGroupCircular;
			if(!isCircular) {
				DrawToggleRow(childControlsLabel, childControlWidthProp, childControlHeightProp);
				DrawToggleRow(childScaleLabel, childScaleWidthProp, childScaleHeightProp);
			}
			if(isCircular) {
				PropertyFieldSafe(childForceExpandCircularProp, childForceExpandLabel);
			} else {
				DrawToggleRow(childForceExpandLabel, childForceExpandWidthProp, childForceExpandHeightProp);
			}
			DrawToggleRow(navigationLabel, setNavigationProp, navigationLoopProp, navigationEnableLabel, navigationLoopLabel);
			if(setNavigationProp != null && setNavigationProp.boolValue) {
				EditorGUI.indentLevel++;
				PropertyFieldSafe(navigationAxisRangeProp, new GUIContent("Navigation Axis Range"));
				if(target is aLayoutGroupCircular) {
					PropertyFieldSafe(navigationTypeProp, new GUIContent("Navigation Type"));
				}
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.Space();
		}

		private void DrawAnimationSection() {
			if(useAnimationProp == null) return;
			PropertyFieldSafe(useAnimationProp, new GUIContent("Use Animation"));
			if(useAnimationProp.boolValue) {
				EditorGUI.indentLevel++;
				PropertyFieldSafe(animationDurationProp, new GUIContent("Animation Duration"));
				PropertyFieldSafe(animationDistanceThresholdProp, new GUIContent("Distance Threshold"));
				PropertyFieldSafe(useAnimationCurveProp, new GUIContent("Use Animation Curve"));
				if(useAnimationCurveProp.boolValue) {
					PropertyFieldSafe(animationCurveProp, new GUIContent("Animation Curve"));
				} else {
					PropertyFieldSafe(animationEaseProp, new GUIContent("Animation Ease"));
				}
				if(target is aLayoutGroupCircular) {
					PropertyFieldSafe(useCircularMoveProp, new GUIContent("Use Circular Move"));
					if(useCircularMoveProp.boolValue) {
						PropertyFieldSafe(circularMoveTypeProp, new GUIContent("Circular Move Type"));
					}
				}
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.Space();
		}

		private void DrawToggleRow(GUIContent rowLabel, SerializedProperty leftProp, SerializedProperty rightProp) {
			DrawToggleRow(rowLabel, leftProp, rightProp, childControlWidthLabel, childControlHeightLabel);
		}

		private void DrawToggleRow(GUIContent rowLabel, SerializedProperty leftProp, SerializedProperty rightProp, GUIContent leftLabel, GUIContent rightLabel) {
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(rowLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
			float oldLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 50f;
			PropertyFieldSafe(leftProp, leftLabel);
			PropertyFieldSafe(rightProp, rightLabel);
			EditorGUIUtility.labelWidth = oldLabelWidth;
			EditorGUILayout.EndHorizontal();
		}

		private void DrawDerivedProperties() {
			SerializedProperty iterator = serializedObject.GetIterator();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren)) {
				enterChildren = false;
				if(IsBaseProperty(iterator.propertyPath)) continue;
				EditorGUILayout.PropertyField(iterator, true);
			}
		}

		private void DrawScriptField() {
			SerializedProperty script = serializedObject.FindProperty("m_Script");
			if(script == null) return;
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.PropertyField(script);
			}
		}

		private static void PropertyFieldSafe(SerializedProperty prop, bool includeChildren = false) {
			if(prop != null) EditorGUILayout.PropertyField(prop, includeChildren);
		}

		private static void PropertyFieldSafe(SerializedProperty prop, GUIContent label) {
			if(prop != null) EditorGUILayout.PropertyField(prop, label);
		}

		private static bool IsBaseProperty(string propertyPath) {
			switch(propertyPath) {
				case "m_Script":
				case "padding":
				case "updateMode":
				case "excludedChildren":
				case "childAlignment":
				case "reverseArrangement":
				case "spacing":
				case "childForceExpand":
				case "spacingXY":
				case "startCorner":
				case "startAxis":
				case "cellSize":
				case "constraint":
				case "constraintCount":
				case "childControlWidth":
				case "childControlHeight":
				case "childScaleWidth":
				case "childScaleHeight":
				case "childForceExpandWidth":
				case "childForceExpandHeight":
				case "setNavigation":
				case "navigationLoop":
				case "useAnimation":
				case "animationDuration":
				case "animationDistanceThreshold":
				case "useAnimationCurve":
				case "animationCurve":
				case "animationEase":
				case "useCircularMove":
				case "circularMoveType":
				case "navigationAxisRange":
				case "navigationType":
				case "headStartTarget":
					return true;
				default:
					return false;
			}
		}

		private void RebuildTargets() {
			Undo.IncrementCurrentGroup();
			int undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Rebuild Layout");

			foreach (var targetObject in targets) {
				if(targetObject is not aLayoutGroupBase group) continue;

				List<Object> undoTargets = CollectUndoTargets(group);
				if(undoTargets.Count > 0) {
					Undo.RecordObjects(undoTargets.ToArray(), "Rebuild Layout");
				}

				if(Application.isPlaying) {
					group.PerformLayout();
				} else {
					group.PerformLayoutEditor();
				}

				foreach (var obj in undoTargets) {
					if(obj != null) EditorUtility.SetDirty(obj);
				}
			}

			Undo.CollapseUndoOperations(undoGroup);
			SceneView.RepaintAll();
			EditorApplication.QueuePlayerLoopUpdate();
		}

		private List<Object> CollectUndoTargets(aLayoutGroupBase group) {
			var result = new List<Object>();
			var seen = new HashSet<Object>();

			void AddTarget(Object obj) {
				if(obj == null) return;
				if(seen.Add(obj)) result.Add(obj);
			}

			AddTarget(group);
			AddTarget(group.transform as RectTransform);

			// Collect layout target children (same criteria as aLayoutGroupBase: active & not ignored)
			Transform parent = group.transform;
			int childCount = parent.childCount;
			for (int i = 0; i < childCount; i++) {
				if(parent.GetChild(i) is not RectTransform child) continue;
				if(!child.gameObject.activeInHierarchy) continue;
				var ignorer = child.GetComponent<ILayoutIgnorer>();
				if(ignorer != null && ignorer.ignoreLayout) continue;
				AddTarget(child);
			}

			return result;
		}
	}
}
