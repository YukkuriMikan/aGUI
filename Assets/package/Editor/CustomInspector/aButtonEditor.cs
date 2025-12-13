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
		private SerializedProperty useInitialGuardProp;
		private SerializedProperty initialGuardDurationProp;
		private SerializedProperty enableLongPressProp;
		private SerializedProperty longPressDurationProp;
		private SerializedProperty onLongPressProp;
		private SerializedProperty onLongPressCancelProp;
		private SerializedProperty useMultipleInputGuardProp;
		private SerializedProperty clickDebounceIntervalProp;
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
			useInitialGuardProp = serializedObject.FindProperty("useInitialGuard");
			initialGuardDurationProp = serializedObject.FindProperty("initialGuardDuration");
			enableLongPressProp = serializedObject.FindProperty("enableLongPress");
			longPressDurationProp = serializedObject.FindProperty("longPressDuration");
			onLongPressProp = serializedObject.FindProperty("onLongPress");
			onLongPressCancelProp = serializedObject.FindProperty("onLongPressCancel");
			useMultipleInputGuardProp = serializedObject.FindProperty("useMultipleInputGuard");
			clickDebounceIntervalProp = serializedObject.FindProperty("clickDebounceInterval");
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

			DrawSelectableTransitionSection();

			EditorGUILayout.Space();
			DrawTextTransitionSection();

			EditorGUILayout.Space();
			DrawNavigationSection();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Initial Guard", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(useInitialGuardProp, new GUIContent("Use Initial Guard"));
			if(useInitialGuardProp.boolValue) {
				EditorGUILayout.PropertyField(initialGuardDurationProp, new GUIContent("Guard Duration"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(useMultipleInputGuardProp, new GUIContent("Use Multiple Input Guard"));
			if(useMultipleInputGuardProp.boolValue) {
				EditorGUILayout.PropertyField(clickDebounceIntervalProp, new GUIContent("Guard Interval"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(enableLongPressProp);
			if(enableLongPressProp.boolValue) {
				EditorGUILayout.PropertyField(longPressDurationProp);
				EditorGUILayout.PropertyField(longPressImageProp, new GUIContent("Long Press Image"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(useCustomAnimationProp, new GUIContent("Use Custom Animation"));
			if(useCustomAnimationProp.boolValue) {
				EditorGUILayout.PropertyField(clickAnimationProp, new GUIContent("Click Animation"));
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

		private void DrawTextTransitionSection() {
			EditorGUILayout.PropertyField(targetTextProp);
			if(targetTextProp.objectReferenceValue == null) return;

			EditorGUILayout.PropertyField(textTransitionProp, new GUIContent("Transition"));

			EditorGUI.indentLevel++;
			switch((TextTransitionType)textTransitionProp.enumValueIndex) {
				case TextTransitionType.TextColor:
					EditorGUILayout.PropertyField(textColorsProp, new GUIContent("Colors"));
					break;
				case TextTransitionType.TextSwap:
					DrawTextSwapFields();
					break;
				case TextTransitionType.TextAnimation:
					EditorGUILayout.PropertyField(textAnimationTriggersProp, new GUIContent("Animation Triggers"));
					EditorGUILayout.PropertyField(textAnimatorProp, new GUIContent("Text Animator"));
					break;
			}
			EditorGUI.indentLevel--;
		}

		private void DrawTextSwapFields() {
			SerializedProperty normal = textSwapStateProp.FindPropertyRelative("normalText");
			SerializedProperty highlighted = textSwapStateProp.FindPropertyRelative("highlightedText");
			SerializedProperty pressed = textSwapStateProp.FindPropertyRelative("pressedText");
			SerializedProperty selected = textSwapStateProp.FindPropertyRelative("selectedText");
			SerializedProperty disabled = textSwapStateProp.FindPropertyRelative("disabledText");

			EditorGUILayout.PropertyField(normal, new GUIContent("Normal"));
			EditorGUILayout.PropertyField(highlighted, new GUIContent("Highlighted"));
			EditorGUILayout.PropertyField(pressed, new GUIContent("Pressed"));
			EditorGUILayout.PropertyField(selected, new GUIContent("Selected"));
			EditorGUILayout.PropertyField(disabled, new GUIContent("Disabled"));
		}
	}
}
