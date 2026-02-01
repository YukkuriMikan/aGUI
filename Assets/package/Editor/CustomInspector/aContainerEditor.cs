using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary>aContainerBaseのインスペクタにアニメーション設定と表示状態操作UIを追加するカスタムエディタ。</summary>
	[CustomEditor(typeof(aContainerBase), true)]
	[CanEditMultipleObjects]
	public class aContainerEditor : UnityEditor.Editor {
		#region Fields
		private SerializedProperty _guiInfoProp;            // GUI情報の参照
		private SerializedProperty _canvasGroupProp;        // CanvasGroup参照
		protected SerializedProperty _onShowProp;             // Show完了イベント参照
		protected SerializedProperty _onHideProp;             // Hide完了イベント参照
		private SerializedProperty _debugModeProp;          // デバッグモード参照
		private SerializedProperty _useCustomAnimationsProp; // 個別アニメーション使用フラグへの参照
		private SerializedProperty _useSharedAnimationProp;  // 共有アニメーション使用フラグへの参照
		private SerializedProperty _sharedAnimationProp;     // 共有アニメーションアセットへの参照
		private SerializedProperty _showAnimationsProp;      // Show用アニメーション配列への参照
		private SerializedProperty _hideAnimationsProp;      // Hide用アニメーション配列への参照
		private SerializedProperty _isVisibleProp;           // 表示状態フラグへの参照
		#endregion

		#region Unity Methods
		/// <summary>インスペクタ描画に使用するSerializedProperty参照を初期化する。</summary>
		protected virtual void OnEnable() {
			_guiInfoProp = serializedObject.FindProperty("m_guiInfo");
			_canvasGroupProp = serializedObject.FindProperty("m_canvasGroup");
			_onShowProp = serializedObject.FindProperty("m_onShow");
			_onHideProp = serializedObject.FindProperty("m_onHide");
			_debugModeProp = serializedObject.FindProperty("m_debugMode");
			_useCustomAnimationsProp = serializedObject.FindProperty("m_useCustomAnimations");
			_useSharedAnimationProp = serializedObject.FindProperty("m_useSharedAnimation");
			_sharedAnimationProp = serializedObject.FindProperty("m_sharedAnimation");
			_showAnimationsProp = serializedObject.FindProperty("m_showAnimations");
			_hideAnimationsProp = serializedObject.FindProperty("m_hideAnimations");
			_isVisibleProp = serializedObject.FindProperty("m_isVisible");
		}

		/// <summary>アニメーション/状態の設定UIを描画し、その他のプロパティを表示する。</summary>
		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawAnimationSection();

			EditorGUILayout.Space();
			DrawStateSection();

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_canvasGroupProp);
			EditorGUILayout.PropertyField(_guiInfoProp);

			DrawEventSection();

			DrawCustomSection();
			
			EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_debugModeProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Derived Properties", EditorStyles.boldLabel);
			DrawPropertiesExcluding(serializedObject, GetExcludedProperties());

			serializedObject.ApplyModifiedProperties();

			DrawPlayButtons();
		}
		#endregion

		#region Inspector Draw Methods
		/// <summary>インスペクタから除外するプロパティ名の配列を返す。</summary>
		protected virtual string[] GetExcludedProperties() {
			return new[] {
				"m_Script",
				"m_guiInfo",
				"m_useCustomAnimations",
				"m_useSharedAnimation",
				"m_sharedAnimation",
				"m_showAnimations",
				"m_hideAnimations",
				"m_isVisible",
				"m_canvasGroup",
				"m_onShow",
				"m_onHide",
				"m_debugMode"
			};
		}

		/// <summary>表示状態のトグルを描画し、再生中は即座にShow/Hideへ反映する。</summary>
		private void DrawStateSection() {
			EditorGUILayout.LabelField("State", EditorStyles.boldLabel);

			EditorGUI.showMixedValue = _isVisibleProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			bool isVisible = EditorGUILayout.Toggle("Is Visible", _isVisibleProp.boolValue);
			if(EditorGUI.EndChangeCheck()) {
				if(Application.isPlaying) {
					Undo.RecordObjects(targets, "Change Container Visibility");
					foreach (var obj in targets) {
						if(obj is aContainerBase container) {
							container.IsVisible = isVisible;
							EditorUtility.SetDirty(container);
						}
					}
				} else {
					_isVisibleProp.boolValue = isVisible;
				}
			}
			EditorGUI.showMixedValue = false;
		}

		/// <summary>イベント設定を描画する。</summary>
		protected virtual void DrawEventSection() {
			EditorGUILayout.PropertyField(_onShowProp);
			EditorGUILayout.PropertyField(_onHideProp);
		}

		/// <summary>派生クラス固有のカスタムセクションを描画する。</summary>
		protected virtual void DrawCustomSection() {
		}

		/// <summary>共有/個別アニメーション設定を描画し、適切な警告とプロパティ表示を行う。</summary>
		private void DrawAnimationSection() {
			bool hasSharedAsset = _sharedAnimationProp != null && _sharedAnimationProp.objectReferenceValue != null;
			bool isSharedEnabled = _useSharedAnimationProp != null && _useSharedAnimationProp.boolValue && hasSharedAsset;
			SerializedObject sharedAnimationSerializedObject = null;

			if(hasSharedAsset) {
				sharedAnimationSerializedObject = new SerializedObject(_sharedAnimationProp.objectReferenceValue);
				sharedAnimationSerializedObject.Update();
			}

			EditorGUILayout.PropertyField(_useSharedAnimationProp, new GUIContent("Use Shared Animation"));
			EditorGUILayout.PropertyField(_sharedAnimationProp, new GUIContent("Shared Animation Set"));
			if(_useSharedAnimationProp.boolValue && !hasSharedAsset) {
				EditorGUILayout.HelpBox("Shared Animation Set が設定されていません", MessageType.Warning);
			}

			if(isSharedEnabled && sharedAnimationSerializedObject != null) {
				SerializedProperty showAnimationsShared = sharedAnimationSerializedObject.FindProperty("showAnimations");
				SerializedProperty hideAnimationsShared = sharedAnimationSerializedObject.FindProperty("hideAnimations");

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Shared Animation Settings", EditorStyles.miniBoldLabel);

				if(showAnimationsShared != null) {
					EditorGUILayout.PropertyField(showAnimationsShared, new GUIContent("Show Animations"));
				} else {
					EditorGUILayout.HelpBox("Show Animations プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
				}

				if(hideAnimationsShared != null) {
					EditorGUILayout.PropertyField(hideAnimationsShared, new GUIContent("Hide Animations"));
				} else {
					EditorGUILayout.HelpBox("Hide Animations プロパティが見つかりません。アセットを再作成してください。", MessageType.Warning);
				}

				sharedAnimationSerializedObject.ApplyModifiedProperties();
			} else {
				EditorGUILayout.Space();
				EditorGUILayout.PropertyField(_useCustomAnimationsProp, new GUIContent("Use Custom Animations"));
				if(_useCustomAnimationsProp.boolValue) {
					EditorGUILayout.PropertyField(_showAnimationsProp, new GUIContent("Show Animations"));
					EditorGUILayout.PropertyField(_hideAnimationsProp, new GUIContent("Hide Animations"));
				}
			}
		}

		/// <summary>再生中にShow/Hideを試行するテスト用ボタンを描画する。</summary>
		private void DrawPlayButtons() {
			EditorGUILayout.Space();

			bool isPlaying = Application.isPlaying;
			using (new EditorGUI.DisabledScope(!isPlaying)) {
				EditorGUILayout.BeginHorizontal();
				if(GUILayout.Button("Show")) {
					foreach (var obj in targets) {
						if(obj is aContainerBase container) {
							container.Show();
						}
					}
				}
				if(GUILayout.Button("Hide")) {
					foreach (var obj in targets) {
						if(obj is aContainerBase container) {
							container.Hide();
						}
					}
				}
				EditorGUILayout.EndHorizontal();
			}

			if(!isPlaying) {
				EditorGUILayout.HelpBox("Show/Hide試験実行ボタンはエディタ再生中のみ使用できます。", MessageType.Info);
			}
		}
		#endregion
	}
}
