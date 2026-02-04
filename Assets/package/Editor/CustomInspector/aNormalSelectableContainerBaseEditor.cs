using System.Collections.Generic;
using UnityEditor;

namespace ANest.UI.Editor {
	/// <summary>aNormalSelectableContainerBase のインスペクタ表示をカスタマイズする。</summary>
	[CustomEditor(typeof(aNormalSelectableContainerBase<>), true)]
	[CanEditMultipleObjects]
	public class aNormalSelectableContainerBaseEditor : aSelectableContainerBaseEditor {
		#region Fields
		private SerializedProperty _selectOnHoverProp;                  // ホバーで選択するか
		#endregion

		#region Unity Methods
		/// <summary>SerializedProperty参照を初期化する。</summary>
		protected override void OnEnable() {
			base.OnEnable();
			_selectOnHoverProp = serializedObject.FindProperty("m_selectOnHover");
		}
		#endregion

		#region Inspector Draw Methods
		/// <summary>aNormalSelectableContainerBase 固有プロパティを除外リストへ追加する。</summary>
		protected override string[] GetExcludedProperties() {
			var excluded = new List<string>(base.GetExcludedProperties()) {
				"m_selectOnHover"
			};
			return excluded.ToArray();
		}

		/// <summary>aNormalSelectableContainerBase 固有のカスタムセクションを描画する。</summary>
		protected override void DrawCustomSection() {
			base.DrawCustomSection();
		}

		/// <summary>Selection設定に追加で表示するフィールドを描画する。</summary>
		protected override void DrawAdditionalSelectionFields() {
			if(_selectOnHoverProp != null) {
				EditorGUILayout.PropertyField(_selectOnHoverProp);
			}
		}
		#endregion
	}
}