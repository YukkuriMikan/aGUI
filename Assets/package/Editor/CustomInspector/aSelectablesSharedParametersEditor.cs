using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	/// <summary>aSelectablesSharedParametersのインスペクタ表示を拡張するエディタ</summary>
	[CustomEditor(typeof(aSelectablesSharedParameters))]
	public class aSelectablesSharedParametersEditor : UnityEditor.Editor {
		#region Fields
		private SerializedProperty _transitionProp;            // transition プロパティ
		private SerializedProperty _transitionColorsProp;      // transitionColors プロパティ
		private SerializedProperty _spriteStateProp;           // spriteState プロパティ
		private SerializedProperty _animationTriggersProp;     // selectableAnimationTriggers プロパティ
		private SerializedProperty _textTransitionProp;        // textTransition プロパティ
		private SerializedProperty _textColorsProp;            // textColors プロパティ
		private SerializedProperty _textSwapStateProp;         // textSwapState プロパティ
		private SerializedProperty _textAnimationTriggersProp; // textAnimationTriggers プロパティ
		private SerializedProperty _textAnimatorProp;          // textAnimator プロパティ
		#endregion

		#region Unity Methods
		/// <summary>SerializedPropertyの参照を取得する</summary>
		private void OnEnable() {
			_transitionProp = serializedObject.FindProperty("transition");
			_transitionColorsProp = serializedObject.FindProperty("transitionColors");
			_spriteStateProp = serializedObject.FindProperty("spriteState");
			_animationTriggersProp = serializedObject.FindProperty("selectableAnimationTriggers");
			_textTransitionProp = serializedObject.FindProperty("textTransition");
			_textColorsProp = serializedObject.FindProperty("textColors");
			_textSwapStateProp = serializedObject.FindProperty("textSwapState");
			_textAnimationTriggersProp = serializedObject.FindProperty("textAnimationTriggers");
			_textAnimatorProp = serializedObject.FindProperty("textAnimator");
		}

		/// <summary>インスペクタGUIを描画する</summary>
		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawTransitionSection();
			DrawTextTransitionSection();

			DrawPropertiesExcluding(serializedObject,
				"m_Script",
				"transition",
				"transitionColors",
				"spriteState",
				"selectableAnimationTriggers",
				"textTransition",
				"textColors",
				"textSwapState",
				"textAnimationTriggers",
				"textAnimator");

			serializedObject.ApplyModifiedProperties();
		}
		#endregion

		#region Inspector Helpers
		/// <summary>Selectableのトランジション設定を描画する</summary>
		private void DrawTransitionSection() {
			EditorGUILayout.LabelField("Transition", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_transitionProp);

			++EditorGUI.indentLevel;
			switch((Selectable.Transition)_transitionProp.enumValueIndex) {
				case Selectable.Transition.ColorTint:
					EditorGUILayout.PropertyField(_transitionColorsProp, new GUIContent("Transition Colors"));
					break;
				case Selectable.Transition.SpriteSwap:
					EditorGUILayout.PropertyField(_spriteStateProp, new GUIContent("Sprite State"));
					break;
				case Selectable.Transition.Animation:
					EditorGUILayout.PropertyField(_animationTriggersProp, new GUIContent("Animation Triggers"));
					break;
			}
			--EditorGUI.indentLevel;

			EditorGUILayout.Space();
		}

		/// <summary>テキストトランジション設定を描画する</summary>
		private void DrawTextTransitionSection() {
			EditorGUILayout.LabelField("Text Transition", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_textTransitionProp, new GUIContent("Transition"));

			++EditorGUI.indentLevel;
			switch((TextTransitionType)_textTransitionProp.enumValueIndex) {
				case TextTransitionType.TextColor:
					aGuiEditorUtils.DrawTextColorPresetButtons(_textColorsProp);
					EditorGUILayout.PropertyField(_textColorsProp, new GUIContent("Colors"));
					break;
				case TextTransitionType.TextSwap:
					DrawTextSwapFields();
					break;
				case TextTransitionType.TextAnimation:
					EditorGUILayout.PropertyField(_textAnimationTriggersProp, new GUIContent("Animation Triggers"));
					EditorGUILayout.PropertyField(_textAnimatorProp, new GUIContent("Text Animator"));
					break;
			}
			--EditorGUI.indentLevel;

			EditorGUILayout.Space();
		}

		/// <summary>テキスト差し替え設定の各フィールドを描画する</summary>
		private void DrawTextSwapFields() {
			SerializedProperty normal = _textSwapStateProp.FindPropertyRelative("normalText");
			SerializedProperty highlighted = _textSwapStateProp.FindPropertyRelative("highlightedText");
			SerializedProperty pressed = _textSwapStateProp.FindPropertyRelative("pressedText");
			SerializedProperty selected = _textSwapStateProp.FindPropertyRelative("selectedText");
			SerializedProperty disabled = _textSwapStateProp.FindPropertyRelative("disabledText");

			EditorGUILayout.PropertyField(normal, new GUIContent("Normal"));
			EditorGUILayout.PropertyField(highlighted, new GUIContent("Highlighted"));
			EditorGUILayout.PropertyField(pressed, new GUIContent("Pressed"));
			EditorGUILayout.PropertyField(selected, new GUIContent("Selected"));
			EditorGUILayout.PropertyField(disabled, new GUIContent("Disabled"));
		}
		#endregion
	}
}
