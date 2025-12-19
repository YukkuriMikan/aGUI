using TMPro;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	[CustomEditor(typeof(aToggle), true)]
	[CanEditMultipleObjects]
	public class aToggleEditor : SelectableEditor {
		private SerializedProperty interactableProp;
		private SerializedProperty targetGraphicProp;
		private SerializedProperty transitionProp;
		private SerializedProperty colorBlockProp;
		private SerializedProperty spriteStateProp;
		private SerializedProperty animTriggerProp;
		private SerializedProperty navigationProp;
		private SerializedProperty useSharedParametersProp;
		private SerializedProperty sharedParametersProp;

		private SerializedProperty toggleTransitionProp;
		private SerializedProperty toggleGraphicProp;
		private SerializedProperty groupProp;
		private SerializedProperty isOnProp;
		private SerializedProperty onValueChangedProp;

		private SerializedProperty useInitialGuardProp;
		private SerializedProperty initialGuardDurationProp;
		private SerializedProperty useMultipleInputGuardProp;
		private SerializedProperty multipleInputGuardIntervalProp;

		private SerializedProperty targetTextProp;
		private SerializedProperty textTransitionProp;
		private SerializedProperty textColorsProp;
		private SerializedProperty textSwapStateProp;
		private SerializedProperty textAnimationTriggersProp;
		private SerializedProperty textAnimatorProp;

		private SerializedProperty useCustomAnimationProp;
		private SerializedProperty useSharedAnimationProp;
		private SerializedProperty sharedAnimationProp;
		private SerializedProperty clickAnimationsProp;
		private SerializedProperty onAnimationsProp;
		private SerializedProperty offAnimationsProp;
		private SerializedProperty m_rectTransformProp;
		private SerializedProperty m_targetRectTransformProp;
		private SerializedProperty m_originalTargetRectTransformValuesProp;
		private SerializedProperty m_originalToggleRectTransformValuesProp;

		private static bool showNavigation;
		private const string ShowNavigationKey = "SelectableEditor.ShowNavigation";

		protected override void OnEnable() {
			base.OnEnable();

			interactableProp = serializedObject.FindProperty("m_Interactable");
			targetGraphicProp = serializedObject.FindProperty("m_TargetGraphic");
			transitionProp = serializedObject.FindProperty("m_Transition");
			colorBlockProp = serializedObject.FindProperty("m_Colors");
			spriteStateProp = serializedObject.FindProperty("m_SpriteState");
			animTriggerProp = serializedObject.FindProperty("m_AnimationTriggers");
			navigationProp = serializedObject.FindProperty("m_Navigation");
			useSharedParametersProp = serializedObject.FindProperty("useSharedParameters");
			sharedParametersProp = serializedObject.FindProperty("sharedParameters");

			toggleTransitionProp = serializedObject.FindProperty("toggleTransition");
			toggleGraphicProp = serializedObject.FindProperty("graphic");
			groupProp = serializedObject.FindProperty("m_Group");
			isOnProp = serializedObject.FindProperty("m_IsOn");
			onValueChangedProp = serializedObject.FindProperty("onValueChanged");

			useInitialGuardProp = serializedObject.FindProperty("useInitialGuard");
			initialGuardDurationProp = serializedObject.FindProperty("initialGuardDuration");
			useMultipleInputGuardProp = serializedObject.FindProperty("useMultipleInputGuard");
			multipleInputGuardIntervalProp = serializedObject.FindProperty("multipleInputGuardInterval");

			targetTextProp = serializedObject.FindProperty("targetText");
			textTransitionProp = serializedObject.FindProperty("textTransition");
			textColorsProp = serializedObject.FindProperty("textColors");
			textSwapStateProp = serializedObject.FindProperty("textSwapState");
			textAnimationTriggersProp = serializedObject.FindProperty("textAnimationTriggers");
			textAnimatorProp = serializedObject.FindProperty("textAnimator");

			useCustomAnimationProp = serializedObject.FindProperty("m_useCustomAnimation");
			useSharedAnimationProp = serializedObject.FindProperty("m_useSharedAnimation");
			sharedAnimationProp = serializedObject.FindProperty("m_sharedAnimation");
			clickAnimationsProp = serializedObject.FindProperty("m_clickAnimations");
			onAnimationsProp = serializedObject.FindProperty("m_onAnimations");
			offAnimationsProp = serializedObject.FindProperty("m_offAnimations");

			m_rectTransformProp = serializedObject.FindProperty("m_rectTransform");
			m_targetRectTransformProp = serializedObject.FindProperty("m_targetRectTransform");
			m_originalTargetRectTransformValuesProp = serializedObject.FindProperty("m_originalTargetRectTransformValues");
			m_originalToggleRectTransformValuesProp = serializedObject.FindProperty("m_originalToggleRectTransformValues");

			showNavigation = EditorPrefs.GetBool(ShowNavigationKey);

			TryAssignTargetText();

			// 初回読み込み時やコンパイル時に実行
			ValidateAndRefresh();
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			bool hasSharedAsset = sharedParametersProp.objectReferenceValue != null;
			bool isSharedEnabled = useSharedParametersProp.boolValue && hasSharedAsset;
			SerializedObject sharedSerializedObject = null;
			SerializedObject sharedAnimationSerializedObject = null;
			bool hasSharedAnimationAsset = sharedAnimationProp != null && sharedAnimationProp.objectReferenceValue != null;
			bool isSharedAnimationEnabled = useSharedAnimationProp != null && useSharedAnimationProp.boolValue && hasSharedAnimationAsset;

			if(isSharedEnabled) {
				sharedSerializedObject = new SerializedObject(sharedParametersProp.objectReferenceValue);
				sharedSerializedObject.Update();
			}

			if(hasSharedAnimationAsset) {
				sharedAnimationSerializedObject = new SerializedObject(sharedAnimationProp.objectReferenceValue);
				sharedAnimationSerializedObject.Update();
			}
			EditorGUILayout.PropertyField(interactableProp);

			EditorGUILayout.PropertyField(useSharedParametersProp, new GUIContent("Use Shared"));
			EditorGUILayout.PropertyField(sharedParametersProp, new GUIContent("Shared Parameters"));
			if(useSharedParametersProp.boolValue && !hasSharedAsset) {
				EditorGUILayout.HelpBox("Shared Parameters が設定されていません", MessageType.Warning);
			}

			EditorGUILayout.Space();

			DrawSelectableTransitionSection(isSharedEnabled, sharedSerializedObject);

			using (new EditorGUI.DisabledScope(isSharedEnabled)) {
				DrawTextTransitionSection(isSharedEnabled, sharedSerializedObject);
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Toggle", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(toggleTransitionProp);
			EditorGUILayout.PropertyField(toggleGraphicProp, new GUIContent("Target Graphic"));
			EditorGUILayout.PropertyField(groupProp);
			EditorGUILayout.PropertyField(isOnProp, new GUIContent("Is On"));

			EditorGUILayout.Space();
			DrawNavigationSection();

			EditorGUILayout.LabelField("Initial Guard", EditorStyles.boldLabel);
			{
				SerializedProperty useInitialGuardToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("useInitialGuard") : useInitialGuardProp;
				SerializedProperty guardDurationToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("initialGuardDuration") : initialGuardDurationProp;
				using (new EditorGUI.DisabledScope(isSharedEnabled)) {
					EditorGUILayout.PropertyField(useInitialGuardToShow, new GUIContent("Use Initial Guard"));
					if(useInitialGuardToShow != null && useInitialGuardToShow.boolValue) {
						EditorGUILayout.PropertyField(guardDurationToShow, new GUIContent("Guard Duration"));
					}
				}
			}

			EditorGUILayout.Space();
			{
				SerializedProperty useMultipleGuardToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("useMultipleInputGuard") : useMultipleInputGuardProp;
				SerializedProperty guardIntervalToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("multipleInputGuardInterval") : multipleInputGuardIntervalProp;
				using (new EditorGUI.DisabledScope(isSharedEnabled)) {
					EditorGUILayout.PropertyField(useMultipleGuardToShow, new GUIContent("Use Multiple Input Guard"));
					if(useMultipleGuardToShow != null && useMultipleGuardToShow.boolValue) {
						EditorGUILayout.PropertyField(guardIntervalToShow, new GUIContent("Guard Interval"));
					}
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
			{
				EditorGUILayout.PropertyField(useSharedAnimationProp, new GUIContent("Use Shared Animation"));
				EditorGUILayout.PropertyField(sharedAnimationProp, new GUIContent("Shared Animation Set"));
				if(useSharedAnimationProp.boolValue && !hasSharedAnimationAsset) {
					EditorGUILayout.HelpBox("Shared Animation Set が設定されていません", MessageType.Warning);
				}

				if(isSharedAnimationEnabled && sharedAnimationSerializedObject != null) {
					SerializedProperty clickAnimationShared = sharedAnimationSerializedObject.FindProperty("clickAnimations");
					SerializedProperty onAnimationShared = sharedAnimationSerializedObject.FindProperty("onAnimations");
					SerializedProperty offAnimationShared = sharedAnimationSerializedObject.FindProperty("offAnimations");

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Shared Animation Settings", EditorStyles.miniBoldLabel);

					if(clickAnimationShared != null) {
						EditorGUILayout.PropertyField(clickAnimationShared, new GUIContent("Click Animation"));
					} else {
						EditorGUILayout.HelpBox("Click Animation プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
					}

					if(onAnimationShared != null) {
						EditorGUILayout.PropertyField(onAnimationShared, new GUIContent("On Animation"));
					} else {
						EditorGUILayout.HelpBox("On Animation プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
					}

					if(offAnimationShared != null) {
						EditorGUILayout.PropertyField(offAnimationShared, new GUIContent("Off Animation"));
					} else {
						EditorGUILayout.HelpBox("Off Animation プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
					}

					sharedAnimationSerializedObject.ApplyModifiedProperties();
				} else {
					EditorGUILayout.Space();
					EditorGUILayout.PropertyField(useCustomAnimationProp, new GUIContent("Use Custom Animation"));
					if(useCustomAnimationProp.boolValue) {
						EditorGUILayout.PropertyField(clickAnimationsProp, new GUIContent("Click Animation"));
						EditorGUILayout.PropertyField(onAnimationsProp, new GUIContent("On Animation"));
						EditorGUILayout.PropertyField(offAnimationsProp, new GUIContent("Off Animation"));
					}
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(onValueChangedProp, new GUIContent("On Value Changed"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("RectTransform", EditorStyles.boldLabel);
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.PropertyField(m_rectTransformProp, new GUIContent("Self RectTransform"));
				EditorGUILayout.PropertyField(m_targetRectTransformProp, new GUIContent("Target RectTransform"));
				EditorGUILayout.PropertyField(m_originalTargetRectTransformValuesProp, new GUIContent("Original Target Values"));
				EditorGUILayout.PropertyField(m_originalToggleRectTransformValuesProp, new GUIContent("Original Toggle Values"));
			}

			if(EditorGUI.EndChangeCheck()) {
				serializedObject.ApplyModifiedProperties();
				ValidateAndRefresh();
			}
		}

		private void ValidateAndRefresh() {
			if(Application.isPlaying) return;

			foreach(var t in targets) {
				var toggle = t as aToggle;
				if(toggle == null) continue;

				var so = new SerializedObject(toggle);
				so.Update();

				// 共有パラメータの反映
				if(so.FindProperty("useSharedParameters").boolValue) {
					var sharedParams = so.FindProperty("sharedParameters").objectReferenceValue as aSelectablesSharedParameters;
					if(sharedParams != null) {
						so.FindProperty("m_Transition").enumValueIndex = (int)sharedParams.transition;
						// ColorBlock
						var colorsProp = so.FindProperty("m_Colors");
						colorsProp.FindPropertyRelative("m_NormalColor").colorValue = sharedParams.transitionColors.normalColor;
						colorsProp.FindPropertyRelative("m_HighlightedColor").colorValue = sharedParams.transitionColors.highlightedColor;
						colorsProp.FindPropertyRelative("m_PressedColor").colorValue = sharedParams.transitionColors.pressedColor;
						colorsProp.FindPropertyRelative("m_SelectedColor").colorValue = sharedParams.transitionColors.selectedColor;
						colorsProp.FindPropertyRelative("m_DisabledColor").colorValue = sharedParams.transitionColors.disabledColor;
						colorsProp.FindPropertyRelative("m_ColorMultiplier").floatValue = sharedParams.transitionColors.colorMultiplier;
						colorsProp.FindPropertyRelative("m_FadeDuration").floatValue = sharedParams.transitionColors.fadeDuration;

						so.FindProperty("m_SpriteState").boxedValue = sharedParams.spriteState;
						so.FindProperty("m_AnimationTriggers").boxedValue = sharedParams.selectableAnimationTriggers;

						so.FindProperty("useInitialGuard").boolValue = sharedParams.useInitialGuard;
						so.FindProperty("initialGuardDuration").floatValue = sharedParams.initialGuardDuration;
						so.FindProperty("useMultipleInputGuard").boolValue = sharedParams.useMultipleInputGuard;
						so.FindProperty("multipleInputGuardInterval").floatValue = sharedParams.multipleInputGuardInterval;

						so.FindProperty("textTransition").enumValueIndex = (int)sharedParams.textTransition;
						so.FindProperty("textColors").boxedValue = sharedParams.textColors;
						so.FindProperty("textSwapState").boxedValue = sharedParams.textSwapState;
						so.FindProperty("textAnimationTriggers").boxedValue = sharedParams.textAnimationTriggers;
					}
				}

				// 共有アニメーションの反映
				if(so.FindProperty("m_useSharedAnimation").boolValue) {
					var sharedAnim = so.FindProperty("m_sharedAnimation").objectReferenceValue as UiAnimationSet;
					if(sharedAnim != null) {
						so.FindProperty("m_clickAnimations").boxedValue = aGuiUtils.CloneAnimations(sharedAnim.clickAnimations);
						so.FindProperty("m_onAnimations").boxedValue = aGuiUtils.CloneAnimations(sharedAnim.onAnimations);
						so.FindProperty("m_offAnimations").boxedValue = aGuiUtils.CloneAnimations(sharedAnim.offAnimations);
					}
				}

				// RectTransformとGraphicのキャッシュ
				var rectProp = so.FindProperty("m_rectTransform");
				if(rectProp.objectReferenceValue == null) {
					rectProp.objectReferenceValue = toggle.GetComponent<RectTransform>();
				}

				var targetRectProp = so.FindProperty("m_targetRectTransform");
				if(targetRectProp.objectReferenceValue == null) {
					targetRectProp.objectReferenceValue = rectProp.objectReferenceValue;
				}

				var targetRect = targetRectProp.objectReferenceValue as RectTransform;
				if(targetRect != null) {
					so.FindProperty("m_originalTargetRectTransformValues").boxedValue = RectTransformValues.CreateValues(targetRect);
				}

				var graphicProp = so.FindProperty("graphic");
				if(graphicProp.objectReferenceValue == null) {
					graphicProp.objectReferenceValue = toggle.GetComponentInChildren<Graphic>();
				}

				var graphic = graphicProp.objectReferenceValue as Graphic;
				if(graphic != null) {
					so.FindProperty("m_originalToggleRectTransformValues").boxedValue = RectTransformValues.CreateValues(graphic.rectTransform);
					// 表示状態の更新
					graphic.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
				}

				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(toggle);
			}
		}

		private void DrawSelectableTransitionSection(bool isSharedEnabled, SerializedObject sharedSerializedObject) {
			EditorGUILayout.LabelField("Transition", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(targetGraphicProp);

			SerializedProperty transitionPropToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("transition") : transitionProp;
			using (new EditorGUI.DisabledScope(isSharedEnabled)) {
				EditorGUILayout.PropertyField(transitionPropToShow);
			}

			++EditorGUI.indentLevel;
			switch(GetTransition(transitionPropToShow)) {
				case Selectable.Transition.ColorTint: {
					SerializedProperty colorsToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("transitionColors") : colorBlockProp;
					using (new EditorGUI.DisabledScope(isSharedEnabled)) {
						EditorGUILayout.PropertyField(colorsToShow);
					}
					break;
				}
				case Selectable.Transition.SpriteSwap: {
					SerializedProperty spriteStateToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("spriteState") : spriteStateProp;
					using (new EditorGUI.DisabledScope(isSharedEnabled)) {
						EditorGUILayout.PropertyField(spriteStateToShow);
					}
					break;
				}
				case Selectable.Transition.Animation: {
					SerializedProperty animTriggerToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("selectableAnimationTriggers") : animTriggerProp;
					using (new EditorGUI.DisabledScope(isSharedEnabled)) {
						EditorGUILayout.PropertyField(animTriggerToShow);
					}
					break;
				}
			}
			--EditorGUI.indentLevel;

			EditorGUILayout.Space();
		}

		private void DrawNavigationSection() {
			EditorGUILayout.PropertyField(navigationProp);

			EditorGUI.BeginChangeCheck();
			Rect toggleRect = EditorGUILayout.GetControlRect();
			toggleRect.xMin += EditorGUIUtility.labelWidth;
			showNavigation = GUI.Toggle(toggleRect, showNavigation, EditorGUIUtility.TrTextContent("Visualize", "Show navigation flows between selectable UI elements."), EditorStyles.miniButton);
			if(EditorGUI.EndChangeCheck()) {
				EditorPrefs.SetBool(ShowNavigationKey, showNavigation);
				SceneView.RepaintAll();
			}
		}

		private Selectable.Transition GetTransition(SerializedProperty transitionPropToShow = null) {
			SerializedProperty source = transitionPropToShow ?? transitionProp;
			return source == null ? (Selectable.Transition)transitionProp.enumValueIndex : (Selectable.Transition)source.enumValueIndex;
		}

		private void DrawTextTransitionSection(bool isSharedEnabled, SerializedObject sharedSerializedObject) {
			SerializedProperty presetColorsProp = isSharedEnabled ? sharedSerializedObject?.FindProperty("textColors") : textColorsProp;

			EditorGUILayout.PropertyField(targetTextProp);
			if(targetTextProp.objectReferenceValue == null) return;

			SerializedProperty transitionPropToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("textTransition") : textTransitionProp;
			EditorGUILayout.PropertyField(transitionPropToShow, new GUIContent("Transition"));
			if(transitionPropToShow == null) return;

			EditorGUI.indentLevel++;
			switch((TextTransitionType)transitionPropToShow.enumValueIndex) {
				case TextTransitionType.TextColor: {
					aGuiEditorUtils.DrawTextColorPresetButtons(presetColorsProp);
					SerializedProperty colorsToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("textColors") : textColorsProp;
					EditorGUILayout.PropertyField(colorsToShow, new GUIContent("Colors"));
					break;
				}
				case TextTransitionType.TextSwap:
					DrawTextSwapFields(isSharedEnabled ? sharedSerializedObject?.FindProperty("textSwapState") : textSwapStateProp);
					break;
				case TextTransitionType.TextAnimation: {
					SerializedProperty triggersToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("textAnimationTriggers") : textAnimationTriggersProp;
					SerializedProperty animatorToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("textAnimator") : textAnimatorProp;
					EditorGUILayout.PropertyField(triggersToShow, new GUIContent("Animation Triggers"));
					EditorGUILayout.PropertyField(animatorToShow, new GUIContent("Text Animator"));
					break;
				}
			}
			EditorGUI.indentLevel--;
		}

		private void DrawTextSwapFields(SerializedProperty swapStateProp) {
			if(swapStateProp == null) return;

			SerializedProperty normal = swapStateProp.FindPropertyRelative("normalText");
			SerializedProperty highlighted = swapStateProp.FindPropertyRelative("highlightedText");
			SerializedProperty pressed = swapStateProp.FindPropertyRelative("pressedText");
			SerializedProperty selected = swapStateProp.FindPropertyRelative("selectedText");
			SerializedProperty disabled = swapStateProp.FindPropertyRelative("disabledText");

			EditorGUILayout.PropertyField(normal, new GUIContent("Normal"));
			EditorGUILayout.PropertyField(highlighted, new GUIContent("Highlighted"));
			EditorGUILayout.PropertyField(pressed, new GUIContent("Pressed"));
			EditorGUILayout.PropertyField(selected, new GUIContent("Selected"));
			EditorGUILayout.PropertyField(disabled, new GUIContent("Disabled"));
		}

		/// <summary>ターゲットテキストが未設定の場合、既存のTextをTextMeshProUGUIに置換して設定する</summary>
		private void TryAssignTargetText() {
			if(targetTextProp == null) return;
			if(targetTextProp.objectReferenceValue != null) return;

			aToggle toggle = target as aToggle;
			if(toggle == null) return;

			// 既にTextMeshProUGUIが存在するならそれを優先
			TMP_Text tmp = toggle.GetComponentInChildren<TMP_Text>(true);
			if(tmp != null) {
				targetTextProp.objectReferenceValue = tmp;
				serializedObject.ApplyModifiedProperties();
				return;
			}

			// uGUI Text があればTextMeshProUGUIへ置換
			Text legacy = toggle.GetComponentInChildren<Text>(true);
			if(legacy == null) return;

			tmp = ConvertLegacyTextToTMP(legacy);
			if(tmp == null) return;

			targetTextProp.objectReferenceValue = tmp;
			serializedObject.ApplyModifiedProperties();
		}

		private TMP_Text ConvertLegacyTextToTMP(Text legacy) {
			if(legacy == null) return null;

			GameObject go = legacy.gameObject;
			Undo.RegisterFullObjectHierarchyUndo(go, "Convert Text to TextMeshProUGUI");

			string text = legacy.text;
			Color color = legacy.color;
			int fontSize = Mathf.RoundToInt(legacy.fontSize);
			FontStyle fontStyle = legacy.fontStyle;
			TextAnchor alignment = legacy.alignment;
			bool supportRichText = legacy.supportRichText;
			bool raycastTarget = legacy.raycastTarget;

			Undo.DestroyObjectImmediate(legacy);
			TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);

			tmp.text = text;
			tmp.color = color;
			tmp.fontSize = fontSize;
			tmp.fontStyle = ConvertFontStyle(fontStyle);
			tmp.alignment = ConvertAlignment(alignment);
			tmp.richText = supportRichText;
			tmp.raycastTarget = raycastTarget;

			return tmp;
		}

		private TextAlignmentOptions ConvertAlignment(TextAnchor anchor) {
			switch(anchor) {
				case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
				case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
				case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
				case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
				case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
				case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
				case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
				case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
				case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
				default: return TextAlignmentOptions.Center;
			}
		}

		private FontStyles ConvertFontStyle(FontStyle style) {
			switch(style) {
				case FontStyle.Bold: return FontStyles.Bold;
				case FontStyle.Italic: return FontStyles.Italic;
				case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
				default: return FontStyles.Normal;
			}
		}
	}
}
