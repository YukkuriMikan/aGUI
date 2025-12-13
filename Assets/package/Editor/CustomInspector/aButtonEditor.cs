using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	[CustomEditor(typeof(aButton), true)]
	[CanEditMultipleObjects]
	public class aButtonEditor : SelectableEditor {
		private SerializedProperty interactableProp;
		private SerializedProperty targetGraphicProp;
		private SerializedProperty transitionProp;
		private SerializedProperty colorBlockProp;
		private SerializedProperty spriteStateProp;
		private SerializedProperty animTriggerProp;
		private SerializedProperty navigationProp;

		private SerializedProperty onRightClickProp;
		private SerializedProperty useSharedParametersProp;
		private SerializedProperty sharedParametersProp;
		private SerializedProperty useInitialGuardProp;
		private SerializedProperty initialGuardDurationProp;
		private SerializedProperty enableLongPressProp;
		private SerializedProperty longPressDurationProp;
		private SerializedProperty onLongPressProp;
		private SerializedProperty onLongPressCancelProp;
		private SerializedProperty useMultipleInputGuardProp;
		private SerializedProperty multipleInputGuardIntervalProp;
		private SerializedProperty targetTextProp;
		private SerializedProperty textTransitionProp;
		private SerializedProperty textColorsProp;
		private SerializedProperty textSwapStateProp;
		private SerializedProperty textAnimationTriggersProp;
		private SerializedProperty textAnimatorProp;
		private SerializedProperty useCustomAnimationProp;
		private SerializedProperty clickAnimationProp;
		private SerializedProperty longPressImageProp;
		private SerializedProperty onClickProp;

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
			onRightClickProp = serializedObject.FindProperty("onRightClick");
			useSharedParametersProp = serializedObject.FindProperty("useSharedParameters");
			sharedParametersProp = serializedObject.FindProperty("sharedParameters");
			useInitialGuardProp = serializedObject.FindProperty("useInitialGuard");
			initialGuardDurationProp = serializedObject.FindProperty("initialGuardDuration");
			enableLongPressProp = serializedObject.FindProperty("enableLongPress");
			longPressDurationProp = serializedObject.FindProperty("longPressDuration");
			onLongPressProp = serializedObject.FindProperty("onLongPress");
			onLongPressCancelProp = serializedObject.FindProperty("onLongPressCancel");
			useMultipleInputGuardProp = serializedObject.FindProperty("useMultipleInputGuard");
			multipleInputGuardIntervalProp = serializedObject.FindProperty("multipleInputGuardInterval");
			targetTextProp = serializedObject.FindProperty("targetText");
			textTransitionProp = serializedObject.FindProperty("textTransition");
			textColorsProp = serializedObject.FindProperty("textColors");
			textSwapStateProp = serializedObject.FindProperty("textSwapState");
			textAnimationTriggersProp = serializedObject.FindProperty("textAnimationTriggers");
			textAnimatorProp = serializedObject.FindProperty("textAnimator");
			useCustomAnimationProp = serializedObject.FindProperty("m_useCustomAnimation");
			clickAnimationProp = serializedObject.FindProperty("m_clickAnimations");
			longPressImageProp = serializedObject.FindProperty("longPressImage");
			onClickProp = serializedObject.FindProperty("m_OnClick");

			showNavigation = EditorPrefs.GetBool(ShowNavigationKey);
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			bool hasSharedAsset = sharedParametersProp.objectReferenceValue != null;
			bool isSharedEnabled = useSharedParametersProp.boolValue && hasSharedAsset;
			SerializedObject sharedSerializedObject = null;
			if(isSharedEnabled) {
				sharedSerializedObject = new SerializedObject(sharedParametersProp.objectReferenceValue);
				sharedSerializedObject.Update();
			}

			DrawSelectableTransitionSection();
			
			EditorGUILayout.PropertyField(useSharedParametersProp, new GUIContent("Use Shared"));
			EditorGUILayout.PropertyField(sharedParametersProp, new GUIContent("Shared Parameters"));
			if(useSharedParametersProp.boolValue && !hasSharedAsset) {
				EditorGUILayout.HelpBox("Shared Parameters が設定されていません", MessageType.Warning);
			}

			EditorGUILayout.Space();
			using(new EditorGUI.DisabledScope(isSharedEnabled)) {
				DrawTextTransitionSection(isSharedEnabled, sharedSerializedObject);
			}

			EditorGUILayout.Space();
			DrawNavigationSection();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Initial Guard", EditorStyles.boldLabel);
			{
				SerializedProperty useInitialGuardToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("useInitialGuard") : useInitialGuardProp;
				SerializedProperty guardDurationToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("initialGuardDuration") : initialGuardDurationProp;
				using(new EditorGUI.DisabledScope(isSharedEnabled)) {
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
				using(new EditorGUI.DisabledScope(isSharedEnabled)) {
					EditorGUILayout.PropertyField(useMultipleGuardToShow, new GUIContent("Use Multiple Input Guard"));
					if(useMultipleGuardToShow != null && useMultipleGuardToShow.boolValue) {
						EditorGUILayout.PropertyField(guardIntervalToShow, new GUIContent("Guard Interval"));
					}
				}
			}

			EditorGUILayout.Space();
			{
				SerializedProperty enableLongPressToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("enableLongPress") : enableLongPressProp;
				SerializedProperty longPressDurationToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("longPressDuration") : longPressDurationProp;
				using(new EditorGUI.DisabledScope(isSharedEnabled)) {
					EditorGUILayout.PropertyField(enableLongPressToShow, new GUIContent(enableLongPressProp.displayName));
					if(enableLongPressToShow != null && enableLongPressToShow.boolValue) {
						EditorGUILayout.PropertyField(longPressDurationToShow, new GUIContent(longPressDurationProp.displayName));
					}
				}
			}
			// 画像は個別設定できるよう共有時も編集可（共有設定には含めない）
			if(enableLongPressProp.boolValue) {
				EditorGUILayout.PropertyField(longPressImageProp, new GUIContent("Long Press Image"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
			{
				SerializedProperty useCustomAnimationToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("useCustomAnimation") : useCustomAnimationProp;
				SerializedProperty clickAnimationToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("clickAnimations") : clickAnimationProp;
				using(new EditorGUI.DisabledScope(isSharedEnabled)) {
					EditorGUILayout.PropertyField(useCustomAnimationToShow, new GUIContent("Use Custom Animation"));
					if(useCustomAnimationToShow != null && useCustomAnimationToShow.boolValue) {
						EditorGUILayout.PropertyField(clickAnimationToShow, new GUIContent("Click Animation"));
					}
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Click Events", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(onClickProp);
			EditorGUILayout.PropertyField(onRightClickProp);

			if(enableLongPressProp.boolValue) {
				EditorGUILayout.PropertyField(onLongPressProp);
				EditorGUILayout.PropertyField(onLongPressCancelProp, new GUIContent("On Long Press Cancel"));
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawSelectableTransitionSection() {
			EditorGUILayout.PropertyField(interactableProp);
			EditorGUILayout.PropertyField(targetGraphicProp);
			EditorGUILayout.PropertyField(transitionProp);

			++EditorGUI.indentLevel;
			switch(GetTransition()) {
				case Selectable.Transition.ColorTint:
					EditorGUILayout.PropertyField(colorBlockProp);
					break;
				case Selectable.Transition.SpriteSwap:
					EditorGUILayout.PropertyField(spriteStateProp);
					break;
				case Selectable.Transition.Animation:
					EditorGUILayout.PropertyField(animTriggerProp);
					break;
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

		private Selectable.Transition GetTransition() {
			return (Selectable.Transition)transitionProp.enumValueIndex;
		}

		private void DrawTextTransitionSection(bool isSharedEnabled, SerializedObject sharedSerializedObject) {
			EditorGUILayout.PropertyField(targetTextProp);
			if(targetTextProp.objectReferenceValue == null) return;

			SerializedProperty transitionPropToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("textTransition") : textTransitionProp;
			EditorGUILayout.PropertyField(transitionPropToShow, new GUIContent("Transition"));

			EditorGUI.indentLevel++;
			switch((TextTransitionType)transitionPropToShow.enumValueIndex) {
				case TextTransitionType.TextColor: {
					SerializedProperty colorsToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("textColors") : textColorsProp;
					EditorGUILayout.PropertyField(colorsToShow, new GUIContent("Colors"));
					break;
				}
				case TextTransitionType.TextSwap:
					DrawTextSwapFields(isSharedEnabled, sharedSerializedObject);
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

		private void DrawTextSwapFields(bool isSharedEnabled, SerializedObject sharedSerializedObject) {
			SerializedProperty swapStateProp = isSharedEnabled ? sharedSerializedObject?.FindProperty("textSwapState") : textSwapStateProp;
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
	}
}
