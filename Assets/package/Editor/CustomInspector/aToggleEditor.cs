using TMPro;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	[CustomEditor(typeof(aToggle), true)]
	[CanEditMultipleObjects]
	public class aToggleEditor : SelectableEditor {
		private static readonly ColorBlock TextColorPresetWhite = new() {
			normalColor = Color.white,
			highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f),
			pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f),
			selectedColor = Color.white,
			disabledColor = new Color(1f, 1f, 1f, 0.5f),
			colorMultiplier = 1f,
			fadeDuration = 0.1f
		};

		private static readonly ColorBlock TextColorPresetBlack = new() {
			normalColor = Color.black,
			highlightedColor = new Color(0.2f, 0.2f, 0.2f, 1f),
			pressedColor = new Color(0.3f, 0.3f, 0.3f, 1f),
			selectedColor = Color.black,
			disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f),
			colorMultiplier = 1f,
			fadeDuration = 0.1f
		};

		private SerializedProperty interactableProp;
		private SerializedProperty targetGraphicProp;
		private SerializedProperty transitionProp;
		private SerializedProperty colorBlockProp;
		private SerializedProperty spriteStateProp;
		private SerializedProperty animTriggerProp;
		private SerializedProperty navigationProp;

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
		private SerializedProperty clickAnimationsProp;
		private SerializedProperty onAnimationsProp;
		private SerializedProperty offAnimationsProp;

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
			clickAnimationsProp = serializedObject.FindProperty("m_clickAnimations");
			onAnimationsProp = serializedObject.FindProperty("m_onAnimations");
			offAnimationsProp = serializedObject.FindProperty("m_offAnimations");

			showNavigation = EditorPrefs.GetBool(ShowNavigationKey);

			TryAssignTargetText();
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawSelectableTransitionSection();

			EditorGUILayout.Space();
			DrawTextTransitionSection();
			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Toggle", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(toggleTransitionProp);
			EditorGUILayout.PropertyField(toggleGraphicProp, new GUIContent("Target Graphic"));
			EditorGUILayout.PropertyField(groupProp);
			EditorGUILayout.PropertyField(isOnProp, new GUIContent("Is On"));

			EditorGUILayout.Space();
			DrawNavigationSection();

			EditorGUILayout.PropertyField(useInitialGuardProp, new GUIContent("Use Initial Guard"));
			if(useInitialGuardProp.boolValue) {
				EditorGUILayout.PropertyField(initialGuardDurationProp, new GUIContent("Guard Duration"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(useMultipleInputGuardProp, new GUIContent("Use Multiple Input Guard"));
			if(useMultipleInputGuardProp.boolValue) {
				EditorGUILayout.PropertyField(multipleInputGuardIntervalProp, new GUIContent("Guard Interval"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(useCustomAnimationProp, new GUIContent("Use Custom Animation"));
			if(useCustomAnimationProp.boolValue) {
				EditorGUILayout.PropertyField(clickAnimationsProp, new GUIContent("Click Animation"));
				EditorGUILayout.PropertyField(onAnimationsProp, new GUIContent("On Animation"));
				EditorGUILayout.PropertyField(offAnimationsProp, new GUIContent("Off Animation"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(onValueChangedProp, new GUIContent("On Value Changed"));

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
				case TextTransitionType.TextColor: {
					DrawTextColorPresetButtons(textColorsProp);
					EditorGUILayout.PropertyField(textColorsProp, new GUIContent("Colors"));
					break;
				}
				case TextTransitionType.TextSwap:
					DrawTextSwapFields();
					break;
				case TextTransitionType.TextAnimation: {
					EditorGUILayout.PropertyField(textAnimationTriggersProp, new GUIContent("Animation Triggers"));
					EditorGUILayout.PropertyField(textAnimatorProp, new GUIContent("Text Animator"));
					break;
				}
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

		private static void DrawTextColorPresetButtons(SerializedProperty colorsProperty) {
			using(new EditorGUILayout.HorizontalScope()) {
				GUI.enabled &= colorsProperty != null;
				if(GUILayout.Button("White Text Colors")) {
					ApplyColorBlockPreset(colorsProperty, TextColorPresetWhite);
				}
				if(GUILayout.Button("Black Text Colors")) {
					ApplyColorBlockPreset(colorsProperty, TextColorPresetBlack);
				}
				GUI.enabled = true;
			}
		}

		private static void ApplyColorBlockPreset(SerializedProperty colorsProperty, ColorBlock preset) {
			if(colorsProperty == null) return;

			SetColorProperty(colorsProperty, "m_NormalColor", preset.normalColor);
			SetColorProperty(colorsProperty, "m_HighlightedColor", preset.highlightedColor);
			SetColorProperty(colorsProperty, "m_PressedColor", preset.pressedColor);
			SetColorProperty(colorsProperty, "m_SelectedColor", preset.selectedColor);
			SetColorProperty(colorsProperty, "m_DisabledColor", preset.disabledColor);

			SerializedProperty colorMultiplier = colorsProperty.FindPropertyRelative("m_ColorMultiplier");
			if(colorMultiplier != null) {
				colorMultiplier.floatValue = preset.colorMultiplier;
			}

			SerializedProperty fadeDuration = colorsProperty.FindPropertyRelative("m_FadeDuration");
			if(fadeDuration != null) {
				fadeDuration.floatValue = preset.fadeDuration;
			}
		}

		private static void SetColorProperty(SerializedProperty parent, string name, Color value) {
			SerializedProperty prop = parent.FindPropertyRelative(name);
			if(prop != null) {
				prop.colorValue = value;
			}
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