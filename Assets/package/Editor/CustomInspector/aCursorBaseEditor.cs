using UnityEditor;

namespace ANest.UI.Editor {
	/// <summary>aCursorBase のインスペクタ表示をカスタマイズする。</summary>
	[CustomEditor(typeof(aCursorBase), true)]
	[CanEditMultipleObjects]
	public class aCursorBaseEditor : UnityEditor.Editor {
		#region Fields
		private SerializedProperty _cursorRectProp; // カーソルRectTransformへの参照
		private SerializedProperty _cursorImageProp; // カーソルImageへの参照
		private SerializedProperty _updateModeProp; // 更新モードへの参照
		private SerializedProperty _moveModeProp; // 移動モードへの参照
		private SerializedProperty _moveDurationProp; // 移動時間への参照
		private SerializedProperty _moveEaseProp; // 移動イージングへの参照
		private SerializedProperty _sizeModeProp; // サイズ変更モードへの参照
		private SerializedProperty _paddingProp; // パディングへの参照
		private SerializedProperty _sizeChangeDurationProp; // サイズ変更時間への参照
		private SerializedProperty _sizeChangeEaseProp; // サイズ変更イージングへの参照
		#endregion

		#region Unity Methods
		/// <summary>SerializedProperty参照を初期化する。</summary>
		protected virtual void OnEnable() {
			_cursorRectProp = serializedObject.FindProperty("m_cursorRect");
			_cursorImageProp = serializedObject.FindProperty("m_cursorImage");
			_updateModeProp = serializedObject.FindProperty("m_updateMode");
			_moveModeProp = serializedObject.FindProperty("m_moveMode");
			_moveDurationProp = serializedObject.FindProperty("m_moveDuration");
			_moveEaseProp = serializedObject.FindProperty("m_moveEase");
			_sizeModeProp = serializedObject.FindProperty("m_sizeMode");
			_paddingProp = serializedObject.FindProperty("m_padding");
			_sizeChangeDurationProp = serializedObject.FindProperty("m_sizeChangeDuration");
			_sizeChangeEaseProp = serializedObject.FindProperty("m_sizeChangeEase");
		}

		/// <summary>カーソル設定を条件付きで描画する。</summary>
		public override void OnInspectorGUI() {
			serializedObject.Update();

			EditorGUILayout.PropertyField(_cursorRectProp);
			EditorGUILayout.PropertyField(_cursorImageProp);
			EditorGUILayout.PropertyField(_updateModeProp);
			EditorGUILayout.PropertyField(_moveModeProp);
			if(ShouldShowMoveAnimationFields()) {
				EditorGUILayout.PropertyField(_moveDurationProp);
				EditorGUILayout.PropertyField(_moveEaseProp);
			}
			EditorGUILayout.PropertyField(_sizeModeProp);
			if(ShouldShowSizeAnimationFields()) {
				EditorGUILayout.PropertyField(_paddingProp);
				EditorGUILayout.PropertyField(_sizeChangeDurationProp);
				EditorGUILayout.PropertyField(_sizeChangeEaseProp);
			}

			serializedObject.ApplyModifiedProperties();
		}
		#endregion

		#region Internal Methods
		/// <summary>移動アニメーション設定を表示すべきか判定する。</summary>
		private bool ShouldShowMoveAnimationFields() {
			if(_moveModeProp == null) {
				return true;
			}
			if(_moveModeProp.hasMultipleDifferentValues) {
				return true;
			}
			return _moveModeProp.enumValueIndex == (int)aCursorBase.MoveMode.Animation;
		}

		/// <summary>サイズ変更アニメーション設定を表示すべきか判定する。</summary>
		private bool ShouldShowSizeAnimationFields() {
			if(_sizeModeProp == null) {
				return true;
			}
			if(_sizeModeProp.hasMultipleDifferentValues) {
				return true;
			}
			return _sizeModeProp.enumValueIndex == (int)aCursorBase.SizeMode.MatchSelectable;
		}
		#endregion
	}
}