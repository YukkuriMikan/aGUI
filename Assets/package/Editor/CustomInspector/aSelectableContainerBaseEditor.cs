using System.Collections.Generic;
using UnityEditor;

namespace ANest.UI.Editor {
	/// <summary>aSelectableContainerBase のインスペクタ表示をカスタマイズする。</summary>
	[CustomEditor(typeof(aSelectableContainerBase<>), true)]
	[CanEditMultipleObjects]
	public class aSelectableContainerBaseEditor : aContainerEditor {
		#region Fields
		private SerializedProperty _childSelectableListProp;            // 子要素Selectableリスト
		private SerializedProperty _initialSelectableProp;              // 初期選択Selectable
		private SerializedProperty _currentSelectableProp;              // 現在選択中Selectable
		private SerializedProperty _defaultResumeSelectionOnShowProp;    // 表示時に前回選択を優先するか
		private SerializedProperty _indexModeProp;                      // インデックス範囲外指定時の挙動
		private SerializedProperty _initialGuardProp;                   // 初期ガード
		private SerializedProperty _initialGuardDurationProp;           // 初期ガード時間
		private SerializedProperty _onSelectChangedProp;                // 選択変更イベント
		private SerializedProperty _disallowNullSelectionProp;          // Null選択禁止
		private SerializedProperty _selectOnHoverProp;                  // ホバー選択
		private SerializedProperty _skipNonInteractableNavigationProp;  // 非Interactableスキップ
		#endregion

		#region Unity Methods
		/// <summary>SerializedProperty参照を初期化する。</summary>
		protected override void OnEnable() {
			base.OnEnable();
			_childSelectableListProp = serializedObject.FindProperty("m_childSelectableList");
			_initialSelectableProp = serializedObject.FindProperty("m_initialSelectable");
			_currentSelectableProp = serializedObject.FindProperty("m_currentSelectable");
			_defaultResumeSelectionOnShowProp = serializedObject.FindProperty("m_defaultResumeSelectionOnShow");
			_indexModeProp = serializedObject.FindProperty("m_indexMode");
			_initialGuardProp = serializedObject.FindProperty("m_initialGuard");
			_initialGuardDurationProp = serializedObject.FindProperty("m_initialGuardDuration");
			_onSelectChangedProp = serializedObject.FindProperty("m_onSelectChanged");
			_disallowNullSelectionProp = serializedObject.FindProperty("m_disallowNullSelection");
			_selectOnHoverProp = serializedObject.FindProperty("m_selectOnHover");
			_skipNonInteractableNavigationProp = serializedObject.FindProperty("m_skipNonInteractableNavigation");
		}
		#endregion

		#region Inspector Draw Methods
		/// <summary>aSelectableContainerBase 固有プロパティを除外リストへ追加する。</summary>
		protected override string[] GetExcludedProperties() {
			var excluded = new List<string>(base.GetExcludedProperties()) {
				"m_childSelectableList",
				"m_initialSelectable",
				"m_currentSelectable",
				"m_defaultResumeSelectionOnShow",
				"m_indexMode",
				"m_initialGuard",
				"m_initialGuardDuration",
				"m_onSelectChanged",
				"m_disallowNullSelection",
				"m_selectOnHover",
				"m_skipNonInteractableNavigation"
			};
			return excluded.ToArray();
		}

		/// <summary>Selection設定を描画する。</summary>
		protected virtual void DrawSelectionSection() {
			if(_childSelectableListProp != null) {
				EditorGUILayout.PropertyField(_childSelectableListProp);
			}
			if(_initialSelectableProp != null) {
				EditorGUILayout.PropertyField(_initialSelectableProp);
			}
			if(_currentSelectableProp != null) {
				EditorGUILayout.PropertyField(_currentSelectableProp);
			}
			if(_defaultResumeSelectionOnShowProp != null) {
				EditorGUILayout.PropertyField(_defaultResumeSelectionOnShowProp);
			}
			if(_indexModeProp != null) {
				EditorGUILayout.PropertyField(_indexModeProp);
			}
			if(_selectOnHoverProp != null) {
				EditorGUILayout.PropertyField(_selectOnHoverProp);
			}
			if(_skipNonInteractableNavigationProp != null) {
				EditorGUILayout.PropertyField(_skipNonInteractableNavigationProp);
			}
			if(_disallowNullSelectionProp != null) {
				EditorGUILayout.PropertyField(_disallowNullSelectionProp);
			}
			DrawAdditionalSelectionFields();
		}

		/// <summary>Selection設定に追加で表示するフィールドを描画する。</summary>
		protected virtual void DrawAdditionalSelectionFields() {
		}

		/// <summary>Guard設定を描画する。</summary>
		private void DrawGuardSection() {
			if(_initialGuardProp != null) {
				EditorGUILayout.PropertyField(_initialGuardProp);
			}
			if(_initialGuardDurationProp != null) {
				EditorGUILayout.PropertyField(_initialGuardDurationProp);
			}
		}

		/// <summary>Event設定を描画する。</summary>
		protected override void DrawEventSection() {
			base.DrawEventSection();
			if(_onSelectChangedProp != null) {
				EditorGUILayout.PropertyField(_onSelectChangedProp);
			}
		}

		/// <summary>aSelectableContainerBase 固有のカスタムセクションを描画する。</summary>
		protected override void DrawCustomSection() {
			DrawSelectionSection();
			DrawGuardSection();
		}
		#endregion
	}
}