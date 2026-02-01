using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	/// <summary>aButtonのインスペクタを拡張し、共有パラメータやガード、アニメーションを統一したUIで編集できるようにする。</summary>
	[CustomEditor(typeof(aButton), true)]
	[CanEditMultipleObjects]
	public class aButtonEditor : SelectableEditor {
		#region Fields
		private SerializedProperty interactableProp;  // interactable プロパティへの参照
		private SerializedProperty targetGraphicProp; // targetGraphic プロパティへの参照
		private SerializedProperty transitionProp;    // transition プロパティへの参照
		private SerializedProperty colorBlockProp;    // colors プロパティへの参照
		private SerializedProperty spriteStateProp;   // spriteState プロパティへの参照
		private SerializedProperty animTriggerProp;   // animationTriggers プロパティへの参照
		private SerializedProperty navigationProp;    // navigation プロパティへの参照

		private SerializedProperty onRightClickProp;               // 右クリックイベントへの参照
		private SerializedProperty useSharedParametersProp;        // 共有パラメータ使用フラグへの参照
		private SerializedProperty sharedParametersProp;           // 共有パラメータオブジェクトへの参照
		private SerializedProperty enableLongPressProp;            // 長押し有効フラグへの参照
		private SerializedProperty longPressDurationProp;          // 長押し時間への参照
		private SerializedProperty onLongPressProp;                // 長押し成立イベントへの参照
		private SerializedProperty onLongPressCancelProp;          // 長押しキャンセルイベントへの参照
		private SerializedProperty useMultipleInputGuardProp;      // 連打ガード使用フラグへの参照
		private SerializedProperty multipleInputGuardIntervalProp; // 連打ガード間隔への参照
		private SerializedProperty shortCutProp;                  // ショートカット入力への参照
		private SerializedProperty targetTextProp;                 // テキスト対象への参照
		private SerializedProperty textTransitionProp;             // テキスト遷移種別への参照
		private SerializedProperty textColorsProp;                 // テキストカラー設定への参照
		private SerializedProperty textSwapStateProp;              // テキスト差し替え設定への参照
		private SerializedProperty textAnimationTriggersProp;      // テキストアニメーショントリガーへの参照
		private SerializedProperty textAnimatorProp;               // テキスト用Animatorへの参照
		private SerializedProperty useCustomAnimationProp;         // 個別アニメーション使用フラグへの参照
		private SerializedProperty useSharedAnimationProp;         // 共有アニメーション使用フラグへの参照
		private SerializedProperty sharedAnimationProp;            // 共有アニメーションアセットへの参照
		private SerializedProperty clickAnimationProp;             // クリックアニメーション配列への参照
		private SerializedProperty longPressImageProp;             // 長押し進捗Imageへの参照
		private SerializedProperty onClickProp;                    // OnClickイベントへの参照
		private SerializedProperty guiInfoProp;                   // GUI情報の参照

		private static bool showNavigation;                                         // ナビゲーション可視化トグルの状態
		private const string ShowNavigationKey = "SelectableEditor.ShowNavigation"; // ナビゲーション可視化状態保存用キー
		private string[] excludeProps;
		#endregion

		#region Unity Methods
		/// <summary>インスペクタ描画で使用するSerializedProperty参照を初期化する。</summary>
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
			enableLongPressProp = serializedObject.FindProperty("enableLongPress");
			longPressDurationProp = serializedObject.FindProperty("longPressDuration");
			onLongPressProp = serializedObject.FindProperty("onLongPress");
			onLongPressCancelProp = serializedObject.FindProperty("onLongPressCancel");
			useMultipleInputGuardProp = serializedObject.FindProperty("useMultipleInputGuard");
			multipleInputGuardIntervalProp = serializedObject.FindProperty("multipleInputGuardInterval");
			shortCutProp = serializedObject.FindProperty("shortCut");
			targetTextProp = serializedObject.FindProperty("targetText");
			textTransitionProp = serializedObject.FindProperty("textTransition");
			textColorsProp = serializedObject.FindProperty("textColors");
			textSwapStateProp = serializedObject.FindProperty("textSwapState");
			textAnimationTriggersProp = serializedObject.FindProperty("textAnimationTriggers");
			textAnimatorProp = serializedObject.FindProperty("textAnimator");
			useCustomAnimationProp = serializedObject.FindProperty("m_useCustomAnimation");
			useSharedAnimationProp = serializedObject.FindProperty("m_useSharedAnimation");
			sharedAnimationProp = serializedObject.FindProperty("m_sharedAnimation");
			clickAnimationProp = serializedObject.FindProperty("m_clickAnimations");
			longPressImageProp = serializedObject.FindProperty("longPressImage");
			onClickProp = serializedObject.FindProperty("m_OnClick");
			guiInfoProp = serializedObject.FindProperty("m_guiInfo");

			excludeProps = new[] {
				"m_Script",
				"m_guiInfo",
				"m_Interactable",
				"m_TargetGraphic",
				"m_Transition",
				"m_Colors",
				"m_SpriteState",
				"m_AnimationTriggers",
				"m_Navigation",
				"onRightClick",
				"useSharedParameters",
				"sharedParameters",
				"enableLongPress",
				"longPressDuration",
				"onLongPress",
				"onLongPressCancel",
				"useMultipleInputGuard",
				"multipleInputGuardInterval",
				"shortCut",
				"targetText",
				"textTransition",
				"textColors",
				"textSwapState",
				"textAnimationTriggers",
				"textAnimator",
				"m_useCustomAnimation",
				"m_useSharedAnimation",
				"m_sharedAnimation",
				"m_clickAnimations",
				"longPressImage",
				"m_OnClick"
			};

			showNavigation = EditorPrefs.GetBool(ShowNavigationKey);
		}

		/// <summary>インスペクタGUIを構築し、共有設定やガード・アニメーションの編集UIを描画する。</summary>
		public override void OnInspectorGUI() {
			serializedObject.Update();

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
			DrawNavigationSection();

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
			EditorGUILayout.PropertyField(shortCutProp);

			EditorGUILayout.Space();
			{
				SerializedProperty enableLongPressToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("enableLongPress") : enableLongPressProp;
				SerializedProperty longPressDurationToShow = isSharedEnabled ? sharedSerializedObject?.FindProperty("longPressDuration") : longPressDurationProp;
				using (new EditorGUI.DisabledScope(isSharedEnabled)) {
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
				EditorGUILayout.PropertyField(useSharedAnimationProp, new GUIContent("Use Shared Animation"));
				EditorGUILayout.PropertyField(sharedAnimationProp, new GUIContent("Shared Animation Set"));
				if(useSharedAnimationProp.boolValue && !hasSharedAnimationAsset) {
					EditorGUILayout.HelpBox("Shared Animation Set が設定されていません", MessageType.Warning);
				}

				if(isSharedAnimationEnabled && sharedAnimationSerializedObject != null) {
					SerializedProperty clickAnimationShared = sharedAnimationSerializedObject.FindProperty("clickAnimations");

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Shared Animation Settings", EditorStyles.miniBoldLabel);

					if(clickAnimationShared != null) {
						EditorGUILayout.PropertyField(clickAnimationShared, new GUIContent("Click Animation"));
					} else {
						EditorGUILayout.HelpBox("Click Animation プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
					}

					sharedAnimationSerializedObject.ApplyModifiedProperties();
				} else {
					EditorGUILayout.Space();
					EditorGUILayout.PropertyField(useCustomAnimationProp, new GUIContent("Use Custom Animation"));
					if(useCustomAnimationProp.boolValue) {
						EditorGUILayout.PropertyField(clickAnimationProp, new GUIContent("Click Animation"));
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

			EditorGUILayout.PropertyField(guiInfoProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Derived Properties", EditorStyles.boldLabel);
			DrawPropertiesExcluding(serializedObject, excludeProps);

			serializedObject.ApplyModifiedProperties();
		}
		#endregion

		#region Inspector Draw Methods
		/// <summary>SelectableのTransition設定を描画し、共有設定と個別設定を切り替える。</summary>
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

		/// <summary>ナビゲーション設定と可視化トグルを描画する。</summary>
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

		/// <summary>表示対象のSerializedPropertyからTransition種別を取得する。</summary>
		private Selectable.Transition GetTransition(SerializedProperty transitionPropToShow = null) {
			SerializedProperty source = transitionPropToShow ?? transitionProp;
			return source == null ? (Selectable.Transition)transitionProp.enumValueIndex : (Selectable.Transition)source.enumValueIndex;
		}

		/// <summary>テキストの遷移設定を描画し、種類に応じた詳細設定UIを表示する。</summary>
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

		/// <summary>テキストスワップ用の各ステート文字列設定を描画する。</summary>
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
		#endregion
	}
}
