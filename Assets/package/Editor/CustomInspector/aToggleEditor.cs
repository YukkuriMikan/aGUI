using TMPro;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;


namespace ANest.UI.Editor {
	/// <summary>aToggleのインスペクタを拡張し、共有設定・ガード・アニメーション・RectTransform情報を整理して編集できるようにする。</summary>
	[CustomEditor(typeof(aToggle), true)]
	[CanEditMultipleObjects]
	public class aToggleEditor : SelectableEditor {
		#region Fields
		private SerializedProperty interactableProp;                   // interactable プロパティへの参照
		private SerializedProperty targetGraphicProp;                  // targetGraphic プロパティへの参照
		private SerializedProperty transitionProp;                     // transition プロパティへの参照
		private SerializedProperty colorBlockProp;                     // colors プロパティへの参照
		private SerializedProperty spriteStateProp;                    // spriteState プロパティへの参照
		private SerializedProperty animTriggerProp;                    // animationTriggers プロパティへの参照
		private SerializedProperty navigationProp;                     // navigation プロパティへの参照
		private SerializedProperty useSharedParametersProp;            // 共有パラメータ使用フラグへの参照
		private SerializedProperty sharedParametersProp;               // 共有パラメータオブジェクトへの参照

		private SerializedProperty toggleTransitionProp;               // トグルTransition種別への参照
		private SerializedProperty toggleGraphicProp;                  // チェックマークGraphicへの参照
		private SerializedProperty groupProp;                          // ToggleGroup参照への参照
		private SerializedProperty isOnProp;                           // 初期状態フラグへの参照
		private SerializedProperty onValueChangedProp;                 // 値変更イベントへの参照

		private SerializedProperty useMultipleInputGuardProp;          // 連打ガード使用フラグへの参照
		private SerializedProperty multipleInputGuardIntervalProp;     // 連打ガード間隔への参照
		private SerializedProperty shortCutProp;                      // ショートカット入力への参照

		private SerializedProperty targetTextProp;                    // テキスト対象への参照
		private SerializedProperty textTransitionProp;                // テキスト遷移種別への参照
		private SerializedProperty textColorsProp;                    // テキストカラー設定への参照
		private SerializedProperty textSwapStateProp;                 // テキスト差し替え設定への参照
		private SerializedProperty textAnimationTriggersProp;         // テキストアニメーショントリガーへの参照
		private SerializedProperty textAnimatorProp;                  // テキスト用Animatorへの参照

		private SerializedProperty useCustomAnimationProp;            // 個別アニメーション使用フラグへの参照
		private SerializedProperty useSharedAnimationProp;            // 共有アニメーション使用フラグへの参照
		private SerializedProperty sharedAnimationProp;               // 共有アニメーションアセットへの参照
		private SerializedProperty clickAnimationsProp;               // クリックアニメーション配列への参照
		private SerializedProperty onAnimationsProp;                  // ONアニメーション配列への参照
		private SerializedProperty offAnimationsProp;                 // OFFアニメーション配列への参照

		private static bool showNavigation;                                         // ナビゲーション可視化トグルの状態
		private const string ShowNavigationKey = "SelectableEditor.ShowNavigation"; // ナビゲーション可視化状態の保存キー
		private string[] excludeProps;
		#endregion

		#region Unity Methods
		/// <summary>インスペクタ描画に用いるSerializedProperty参照を初期化し、初期状態を補正する。</summary>
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
			clickAnimationsProp = serializedObject.FindProperty("m_clickAnimations");
			onAnimationsProp = serializedObject.FindProperty("m_onAnimations");
			offAnimationsProp = serializedObject.FindProperty("m_offAnimations");

			excludeProps = new[] {
				"m_Script",
				"m_Interactable",
				"m_TargetGraphic",
				"m_Transition",
				"m_Colors",
				"m_SpriteState",
				"m_AnimationTriggers",
				"m_Navigation",
				"useSharedParameters",
				"sharedParameters",
				"toggleTransition",
				"graphic",
				"m_Group",
				"m_IsOn",
				"onValueChanged",
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
				"m_onAnimations",
				"m_offAnimations"
			};

			showNavigation = EditorPrefs.GetBool(ShowNavigationKey);

			TryAssignTargetText();

			// 初回読み込み時やコンパイル時に実行
			ValidateAndRefresh();
		}

		/// <summary>トグルの共有設定/ガード/アニメーション/イベントをまとめて描画する。</summary>
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
			EditorGUILayout.LabelField("ShortCut", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(shortCutProp);

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

			if(EditorGUI.EndChangeCheck()) {
				serializedObject.ApplyModifiedProperties();
				ValidateAndRefresh();
			}

			EditorGUILayout.Space();
			DrawPropertiesExcluding(serializedObject, excludeProps);

			serializedObject.ApplyModifiedProperties();
		}
		#endregion

		#region Validation
		/// <summary>共有設定やアニメーションの反映、RectTransformキャッシュを更新する。</summary>
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
				var targetRectProp = so.FindProperty("m_targetRectTransform");
				var originalTargetValuesProp = so.FindProperty("m_originalTargetRectTransformValues");
				var originalToggleValuesProp = so.FindProperty("m_originalToggleRectTransformValues");
				var graphicProp = so.FindProperty("graphic");

				bool hasRectProps = rectProp != null && targetRectProp != null && originalTargetValuesProp != null && originalToggleValuesProp != null;

				if(hasRectProps) {
					if(rectProp.objectReferenceValue == null) {
						rectProp.objectReferenceValue = toggle.GetComponent<RectTransform>();
					}

					if(targetRectProp.objectReferenceValue == null) {
						targetRectProp.objectReferenceValue = rectProp.objectReferenceValue ?? toggle.GetComponent<RectTransform>();
					}

					var targetRect = targetRectProp.objectReferenceValue as RectTransform;
					if(targetRect != null) {
						originalTargetValuesProp.boxedValue = RectTransformValues.CreateValues(targetRect);
					}

					if(graphicProp != null) {
						if(graphicProp.objectReferenceValue == null) {
							graphicProp.objectReferenceValue = toggle.GetComponentInChildren<Graphic>();
						}

						var graphic = graphicProp.objectReferenceValue as Graphic;
						if(graphic != null) {
							originalToggleValuesProp.boxedValue = RectTransformValues.CreateValues(graphic.rectTransform);
							// 表示状態の更新
							graphic.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
						}
					}
				}

				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(toggle);
			}
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

		/// <summary>テキスト遷移設定を描画し、種類に応じた詳細設定UIを表示する。</summary>
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

		/// <summary>テキスト差し替え用の各ステート文字列設定を描画する。</summary>
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
		#endregion

		#region Utilities
		/// <summary>ターゲットテキストが未設定の場合、既存のTextをTextMeshProUGUIに置換して設定する。</summary>
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

		/// <summary>uGUI TextをTextMeshProUGUIへ置き換え、主要な表示設定を引き継ぐ。</summary>
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

		/// <summary>uGUIのTextAnchorをTMPのTextAlignmentOptionsへ変換する。</summary>
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

		/// <summary>uGUIのFontStyleをTMPのFontStylesへ変換する。</summary>
		private FontStyles ConvertFontStyle(FontStyle style) {
			switch(style) {
				case FontStyle.Bold: return FontStyles.Bold;
				case FontStyle.Italic: return FontStyles.Italic;
				case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
				default: return FontStyles.Normal;
			}
		}
		#endregion
	}
}
