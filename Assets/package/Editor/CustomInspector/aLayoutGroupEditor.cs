using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


namespace ANest.UI.Editor {
	/// <summary>aLayoutGroupBase系のインスペクタを拡張し、共通/派生プロパティを整理して表示するカスタムエディタ。</summary>
	[CustomEditor(typeof(aLayoutGroupBase), true)]
	[CanEditMultipleObjects]
	public class aLayoutGroupEditor : UnityEditor.Editor {
		#region Fields
		private SerializedProperty paddingProp;                    // padding プロパティへの参照
		private SerializedProperty childAlignmentProp;             // childAlignment プロパティへの参照
		private SerializedProperty reverseArrangementProp;         // reverseArrangement プロパティへの参照
		private SerializedProperty updateModeProp;                 // updateMode プロパティへの参照
		private SerializedProperty rectChildrenProp;               // rectChildren プロパティへの参照
		private SerializedProperty excludedChildrenProp;           // excludedChildren プロパティへの参照
		private SerializedProperty spacingProp;                    // spacing プロパティ（HV用）への参照
		private SerializedProperty childForceExpandCircularProp;   // Circular: childForceExpand プロパティへの参照
		private SerializedProperty childControlWidthProp;          // childControlWidth プロパティへの参照
		private SerializedProperty childControlHeightProp;         // childControlHeight プロパティへの参照
		private SerializedProperty childScaleWidthProp;            // childScaleWidth プロパティへの参照
		private SerializedProperty childScaleHeightProp;           // childScaleHeight プロパティへの参照
		private SerializedProperty childForceExpandWidthProp;      // childForceExpandWidth プロパティへの参照
		private SerializedProperty childForceExpandHeightProp;     // childForceExpandHeight プロパティへの参照
		private SerializedProperty setNavigationProp;              // setNavigation プロパティへの参照
		private SerializedProperty navigationLoopProp;             // navigationLoop プロパティへの参照
		private SerializedProperty navigationAxisRangeProp;        // navigationAxisRange プロパティへの参照
		private SerializedProperty navigationTypeProp;             // navigationType プロパティ（Circular）への参照
		private SerializedProperty useAnimationProp;               // useAnimation プロパティへの参照
		private SerializedProperty animationDurationProp;          // animationDuration プロパティへの参照
		private SerializedProperty animationDistanceThresholdProp; // animationDistanceThreshold プロパティへの参照
		private SerializedProperty useAnimationCurveProp;          // useAnimationCurve プロパティへの参照
		private SerializedProperty animationCurveProp;             // animationCurve プロパティへの参照
		private SerializedProperty animationEaseProp;              // animationEase プロパティへの参照
		private SerializedProperty useCircularMoveProp;            // Circular: useCircularMove プロパティへの参照
		private SerializedProperty circularMoveTypeProp;           // Circular: circularMoveType プロパティへの参照
		private SerializedProperty startCornerProp;                // Grid: startCorner プロパティへの参照
		private SerializedProperty startAxisProp;                  // Grid: startAxis プロパティへの参照
		private SerializedProperty cellSizeProp;                   // Grid: cellSize プロパティへの参照
		private SerializedProperty spacingXYProp;                  // Grid: spacingXY プロパティへの参照
		private SerializedProperty constraintProp;                 // Grid: constraint プロパティへの参照
		private SerializedProperty constraintCountProp;            // Grid: constraintCount プロパティへの参照

		private static readonly GUIContent childControlsLabel = new("Control Child Size");    // 子サイズ制御ラベル
		private static readonly GUIContent childControlWidthLabel = new("Width");             // 幅ラベル
		private static readonly GUIContent childControlHeightLabel = new("Height");           // 高さラベル
		private static readonly GUIContent childScaleLabel = new("Use Child Scale");          // スケール利用ラベル
		private static readonly GUIContent childForceExpandLabel = new("Child Force Expand"); // 強制拡張ラベル
		private static readonly GUIContent navigationLabel = new("Set Navigation");           // ナビ設定ラベル
		private static readonly GUIContent navigationEnableLabel = new("Enable");             // ナビ有効ラベル
		private static readonly GUIContent navigationLoopLabel = new("Loop");                 // ナビループラベル
		#endregion

		#region Unity Methods
		/// <summary>インスペクタで使用するSerializedProperty参照を初期化する。</summary>
		private void OnEnable() {
			paddingProp = serializedObject.FindProperty("padding");
			childAlignmentProp = serializedObject.FindProperty("childAlignment");
			reverseArrangementProp = serializedObject.FindProperty("reverseArrangement");
			updateModeProp = serializedObject.FindProperty("updateMode");
			rectChildrenProp = serializedObject.FindProperty("rectChildren");
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
			startCornerProp = serializedObject.FindProperty("startCorner");
			startAxisProp = serializedObject.FindProperty("startAxis");
			cellSizeProp = serializedObject.FindProperty("cellSize");
			spacingXYProp = serializedObject.FindProperty("spacingXY");
			constraintProp = serializedObject.FindProperty("constraint");
			constraintCountProp = serializedObject.FindProperty("constraintCount");
		}

		/// <summary>共通/派生プロパティのGUIを描画し、再構築ボタンを提供する。</summary>
		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawLayoutBaseProperties();
			DrawDerivedProperties();

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();
			if(GUILayout.Button("Rebuild Layout")) {
				RebuildTargets();
			}
		}

		/// <summary>RectTransformのPaddingを可視化し、ハンドルで編集できるようにする。</summary>
		private void OnSceneGUI() {
			if(target is not aLayoutGroupBase group) return;
			var rectTransform = group.transform as RectTransform;
			if(rectTransform == null) return;

			using (new Handles.DrawingScope()) {
				SerializedObject so = new SerializedObject(group);
				SerializedProperty padding = so.FindProperty("padding");
				if(padding == null) return;

				SerializedProperty leftProp = padding.FindPropertyRelative("m_Left");
				SerializedProperty rightProp = padding.FindPropertyRelative("m_Right");
				SerializedProperty topProp = padding.FindPropertyRelative("m_Top");
				SerializedProperty bottomProp = padding.FindPropertyRelative("m_Bottom");
				if(leftProp == null || rightProp == null || topProp == null || bottomProp == null) return;

				int left = leftProp.intValue;
				int right = rightProp.intValue;
				int top = topProp.intValue;
				int bottom = bottomProp.intValue;

				var outer = rectTransform.rect;
				var inner = new Rect(
					outer.xMin + left,
					outer.yMin + bottom,
					outer.width - left - right,
					outer.height - top - bottom
				);

				Vector3[] corners = new Vector3[4];
				corners[0] = rectTransform.TransformPoint(new Vector3(inner.xMin, inner.yMin, 0f));
				corners[1] = rectTransform.TransformPoint(new Vector3(inner.xMin, inner.yMax, 0f));
				corners[2] = rectTransform.TransformPoint(new Vector3(inner.xMax, inner.yMax, 0f));
				corners[3] = rectTransform.TransformPoint(new Vector3(inner.xMax, inner.yMin, 0f));

				Color prevColor = Handles.color;
				Handles.color = new Color(1f, 0.6f, 0f, 0.9f);
				Handles.DrawAAPolyLine(3f, new [] { corners[0], corners[1], corners[2], corners[3], corners[0] });

				EditorGUI.BeginChangeCheck();
				{
					Vector3 worldLeft = rectTransform.TransformPoint(new Vector3(inner.xMin, inner.center.y, 0f));
					Vector3 worldRight = rectTransform.TransformPoint(new Vector3(inner.xMax, inner.center.y, 0f));
					Vector3 worldTop = rectTransform.TransformPoint(new Vector3(inner.center.x, inner.yMax, 0f));
					Vector3 worldBottom = rectTransform.TransformPoint(new Vector3(inner.center.x, inner.yMin, 0f));

					Vector3 dirRight = rectTransform.right;
					Vector3 dirUp = rectTransform.up;

					float sizeX = HandleUtility.GetHandleSize(worldLeft) * 0.1f;
					float sizeY = HandleUtility.GetHandleSize(worldTop) * 0.1f;

					Handles.color = new Color(1f, 0.6f, 0f, 1f);
					Vector3 newLeftPos = Handles.Slider(worldLeft, dirRight, sizeX, Handles.DotHandleCap, 0f);
					Vector3 newRightPos = Handles.Slider(worldRight, dirRight, sizeX, Handles.DotHandleCap, 0f);
					Vector3 newTopPos = Handles.Slider(worldTop, dirUp, sizeY, Handles.DotHandleCap, 0f);
					Vector3 newBottomPos = Handles.Slider(worldBottom, dirUp, sizeY, Handles.DotHandleCap, 0f);
					Handles.color = prevColor;

					float deltaLeft = Vector3.Dot(newLeftPos - worldLeft, dirRight);
					float deltaRight = Vector3.Dot(newRightPos - worldRight, dirRight);
					float deltaTop = Vector3.Dot(newTopPos - worldTop, dirUp);
					float deltaBottom = Vector3.Dot(newBottomPos - worldBottom, dirUp);

					int newLeft = Mathf.RoundToInt(left + deltaLeft);
					int newRight = Mathf.RoundToInt(right - deltaRight);
					int newTop = Mathf.RoundToInt(top - deltaTop);
					int newBottom = Mathf.RoundToInt(bottom + deltaBottom);

					const float minInnerSize = 1f;
					newLeft = Mathf.RoundToInt(newLeft);
					newRight = Mathf.RoundToInt(newRight);
					newTop = Mathf.RoundToInt(newTop);
					newBottom = Mathf.RoundToInt(newBottom);

					float maxLeft = Mathf.Max(0f, outer.width - minInnerSize - newRight);
					newLeft = Mathf.Min(newLeft, Mathf.RoundToInt(maxLeft));

					float maxRight = Mathf.Max(0f, outer.width - minInnerSize - newLeft);
					newRight = Mathf.Min(newRight, Mathf.RoundToInt(maxRight));

					float maxTop = Mathf.Max(0f, outer.height - minInnerSize - newBottom);
					newTop = Mathf.Min(newTop, Mathf.RoundToInt(maxTop));

					float maxBottom = Mathf.Max(0f, outer.height - minInnerSize - newTop);
					newBottom = Mathf.Min(newBottom, Mathf.RoundToInt(maxBottom));

					if(EditorGUI.EndChangeCheck()) {
						Undo.RecordObject(group, "Change Padding");
						so.Update();
						leftProp.intValue = newLeft;
						rightProp.intValue = newRight;
						topProp.intValue = newTop;
						bottomProp.intValue = newBottom;
						so.ApplyModifiedProperties();
						EditorUtility.SetDirty(group);
						SceneView.RepaintAll();
						EditorApplication.QueuePlayerLoopUpdate();
					}
				}

				SerializedProperty childAlignment = so.FindProperty("childAlignment");
				if(childAlignment != null) {
					TextAnchor alignment = (TextAnchor)childAlignment.enumValueIndex;
					Vector2 alignment01 = GetAlignment01(alignment);
					Vector3 originLocal = new Vector3(
						Mathf.Lerp(inner.xMin, inner.xMax, alignment01.x),
						Mathf.Lerp(inner.yMax, inner.yMin, alignment01.y),
						0f
					);
					Vector3 localDirection = GetLayoutDirectionLocal(group, so, alignment);
					if(localDirection.sqrMagnitude > 0.0001f) {
						Vector3 worldDirection = rectTransform.TransformDirection(localDirection.normalized);
						float axisSizeLocal = Mathf.Abs(localDirection.x) >= Mathf.Abs(localDirection.y)
							? Mathf.Abs(inner.width)
							: Mathf.Abs(inner.height);
						Vector3 centerLocal = new Vector3(inner.center.x, inner.center.y, 0f);
						float baseSize = axisSizeLocal;
						float arrowSizeLocal = baseSize * 0.2f;
						float paddingRatio = Mathf.Abs(localDirection.x) >= Mathf.Abs(localDirection.y) ? 0.05f : 0.08f;
						float arrowPaddingLocal = baseSize * paddingRatio;
						float insetLocal = baseSize * 0.05f;
						Vector3 insetDirection = centerLocal - originLocal;
						if(insetDirection.sqrMagnitude > 0.0001f) {
							originLocal += insetDirection.normalized * insetLocal;
						}
						Vector3 originWorld = rectTransform.TransformPoint(originLocal);
						if(baseSize <= 0.001f) {
							baseSize = HandleUtility.GetHandleSize(originWorld);
							arrowSizeLocal = baseSize * 0.2f;
							arrowPaddingLocal = baseSize * paddingRatio;
						}
						float arrowSize = rectTransform.TransformVector(localDirection.normalized * arrowSizeLocal).magnitude;
						Vector3 arrowPadding = rectTransform.TransformVector(localDirection.normalized * arrowPaddingLocal);
						Vector3 arrowOrigin = originWorld + arrowPadding;
						Handles.color = new Color(0.2f, 0.9f, 1f, 0.9f);
						Handles.ArrowHandleCap(0, arrowOrigin, Quaternion.LookRotation(worldDirection), arrowSize, EventType.Repaint);
					}
				}

				Handles.color = prevColor;
			}
		}

		private static Vector2 GetAlignment01(TextAnchor alignment) {
			int column = (int)alignment % 3;
			int row = (int)alignment / 3;
			return new Vector2(column * 0.5f, row * 0.5f);
		}

		private static float GetCircularAlignmentAngleOffset(TextAnchor alignment) {
			switch(alignment) {
				case TextAnchor.UpperLeft: return -45f;
				case TextAnchor.UpperCenter: return 0f;
				case TextAnchor.UpperRight: return 45f;
				case TextAnchor.MiddleLeft: return 270f;
				case TextAnchor.MiddleCenter: return 0f;
				case TextAnchor.MiddleRight: return 90f;
				case TextAnchor.LowerLeft: return 225f;
				case TextAnchor.LowerCenter: return 180f;
				case TextAnchor.LowerRight: return 135f;
				default: return 0f;
			}
		}

		private static Vector3 GetLayoutDirectionLocal(aLayoutGroupBase group, SerializedObject so, TextAnchor alignment) {
			if(group is aLayoutGroupHorizontal) {
				return Vector3.right;
			}
			if(group is aLayoutGroupVertical) {
				return Vector3.down;
			}
			if(group is aLayoutGroupGrid) {
				SerializedProperty startCorner = so.FindProperty("startCorner");
				SerializedProperty startAxis = so.FindProperty("startAxis");
				int corner = startCorner != null ? startCorner.enumValueIndex : 0;
				int axis = startAxis != null ? startAxis.enumValueIndex : 0;
				int cornerX = corner % 2;
				int cornerY = corner / 2;
				if(axis == 0) {
					return cornerX == 0 ? Vector3.right : Vector3.left;
				}
				return cornerY == 0 ? Vector3.down : Vector3.up;
			}
			if(group is aLayoutGroupCircular) {
				SerializedProperty startAngle = so.FindProperty("startAngle");
				SerializedProperty angleOffset = so.FindProperty("angleOffset");
				float baseAngle = startAngle != null ? startAngle.floatValue : 0f;
				float offsetAngle = angleOffset != null ? angleOffset.floatValue : 0f;
				float alignmentOffset = GetCircularAlignmentAngleOffset(alignment);
				float angle = baseAngle + offsetAngle + alignmentOffset;
				float rad = (90f - angle) * Mathf.Deg2Rad;
				return new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
			}
			if(group is aLayoutGroupLinear) {
				return Vector3.right;
			}
			return Vector3.right;
		}
		#endregion

		#region Inspector Draw Methods
		/// <summary>共通のレイアウト設定と派生クラスの分岐を描画する。</summary>
		private void DrawLayoutBaseProperties() {
			DrawScriptField();
			PropertyFieldSafe(updateModeProp);
			PropertyFieldSafe(rectChildrenProp, true);
			PropertyFieldSafe(excludedChildrenProp, true);
			PropertyFieldSafe(paddingProp, true);
			if (target is aLayoutGroupCircular) {
				EditorGUILayout.HelpBox("Radiusが自動計算される際の制限範囲、およびScene上でのガイド枠として機能します。", MessageType.Info);
			}

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
				if (target is aLayoutGroupCircular) {
					EditorGUILayout.HelpBox("要素間の角度。Child Force Expandが無効な場合に使用されます。", MessageType.Info);
				}
				PropertyFieldSafe(childAlignmentProp);
				if (target is aLayoutGroupCircular) {
					EditorGUILayout.HelpBox("円形配置全体の基準回転を決定します（例: LowerCenterで180度回転）。", MessageType.Info);
				}
				PropertyFieldSafe(reverseArrangementProp);
				DrawChildControlsSection();
				DrawAnimationSection();
			}
		}
	
		/// <summary>子要素のサイズ制御やナビゲーション設定を描画する。</summary>
		private void DrawChildControlsSection() {
			bool isCircular = target is aLayoutGroupCircular;
			if(!isCircular) {
				DrawToggleRow(childControlsLabel, childControlWidthProp, childControlHeightProp);
				DrawToggleRow(childScaleLabel, childScaleWidthProp, childScaleHeightProp);
			}
			if(isCircular) {
				PropertyFieldSafe(childForceExpandCircularProp, childForceExpandLabel);
				EditorGUILayout.HelpBox("有効な場合、Start-Endの範囲内に要素を均等に広げます。", MessageType.Info);
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

		/// <summary>アニメーション有効時の各種パラメータを描画する。</summary>
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

		/// <summary>ラベル付きで2つのトグルを横並びに描画する。</summary>
		private void DrawToggleRow(GUIContent rowLabel, SerializedProperty leftProp, SerializedProperty rightProp) {
			DrawToggleRow(rowLabel, leftProp, rightProp, childControlWidthLabel, childControlHeightLabel);
		}

		/// <summary>カスタムラベルを指定して2つのトグルを横並びに描画する。</summary>
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

		/// <summary>派生クラス固有プロパティを描画し、Circular向け補足情報を表示する。</summary>
		private void DrawDerivedProperties() {
			bool isCircular = target is aLayoutGroupCircular;
			SerializedProperty iterator = serializedObject.GetIterator();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren)) {
				enterChildren = false;
				if(IsBaseProperty(iterator.propertyPath)) continue;
				EditorGUILayout.PropertyField(iterator, true);

				if (isCircular) {
					switch (iterator.propertyPath) {
						case "radius":
							EditorGUILayout.HelpBox("円の半径。0以下の場合は親のRectサイズ（Padding考慮）に合わせて自動計算されます。", MessageType.Info);
							break;
						case "startAngle":
							EditorGUILayout.HelpBox("配置の開始角度。0度は真上(12時)で、時計回りに増加します。", MessageType.Info);
							break;
						case "endAngle":
							EditorGUILayout.HelpBox("配置の終了角度。0度は真上(12時)で、時計回りに増加します。", MessageType.Info);
							break;
						case "angleOffset":
							EditorGUILayout.HelpBox("配置全体の角度を回転させます。", MessageType.Info);
							break;
						case "centerOffset":
							EditorGUILayout.HelpBox("配置中心を親のピボット位置からずらします。", MessageType.Info);
							break;
					}
				}
			}
		}

		/// <summary>m_Scriptを読み取り専用で表示する。</summary>
		private void DrawScriptField() {
			SerializedProperty script = serializedObject.FindProperty("m_Script");
			if(script == null) return;
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.PropertyField(script);
			}
		}
		#endregion

		#region Utilities
		/// <summary>SerializedPropertyがnullでない場合のみ描画する（子要素を含めるか選択可能）。</summary>
		private static void PropertyFieldSafe(SerializedProperty prop, bool includeChildren = false) {
			if(prop != null) EditorGUILayout.PropertyField(prop, includeChildren);
		}

		/// <summary>SerializedPropertyがnullでない場合のみ指定ラベルで描画する。</summary>
		private static void PropertyFieldSafe(SerializedProperty prop, GUIContent label) {
			if(prop != null) EditorGUILayout.PropertyField(prop, label);
		}

		/// <summary>基底クラス側で処理するプロパティかどうか判定する。</summary>
		private static bool IsBaseProperty(string propertyPath) {
			switch(propertyPath) {
				case "m_Script":
				case "rectChildren":
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
					return true;
				default:
					return false;
			}
		}

		/// <summary>選択中のレイアウトを再構築し、Undoを記録する。</summary>
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
					group.AlignWithCollection();
				} else {
					group.AlignEditor();
				}

				foreach (var obj in undoTargets) {
					if(obj != null) EditorUtility.SetDirty(obj);
				}
			}

			Undo.CollapseUndoOperations(undoGroup);
			SceneView.RepaintAll();
			EditorApplication.QueuePlayerLoopUpdate();
		}

		/// <summary>Undo対象に含めるオブジェクトを重複なく収集する。</summary>
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
		#endregion
	}
}
