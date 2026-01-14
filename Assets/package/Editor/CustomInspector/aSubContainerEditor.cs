using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary>aSubContainerのインスペクタに親コンテナ指定UIを追加するカスタムエディタ。</summary>
	[CustomEditor(typeof(aSubContainer), true)]
	[CanEditMultipleObjects]
	public class aSubContainerEditor : aContainerEditor {
		#region Fields
		private SerializedProperty _mainContainerProp; // 親コンテナ参照へのSerializedProperty
		#endregion

		#region Unity Methods
		/// <summary>親コンテナ参照のSerializedPropertyを初期化する。</summary>
		protected override void OnEnable() {
			base.OnEnable();
			_mainContainerProp = serializedObject.FindProperty("m_mainContainer");
		}

		/// <summary>SubContainer固有の設定と共通インスペクタを描画する。</summary>
		public override void OnInspectorGUI() {
			serializedObject.Update();

			EditorGUILayout.LabelField("Sub Container", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_mainContainerProp, new GUIContent("Main Container"));

			EditorGUILayout.Space();

			serializedObject.ApplyModifiedProperties();

			base.OnInspectorGUI();
		}
		#endregion

		#region Inspector Draw Methods
		/// <summary>SubContainer固有プロパティを除外リストへ追加する。</summary>
		protected override string[] GetExcludedProperties() {
			var excluded = new System.Collections.Generic.List<string>(base.GetExcludedProperties());
			excluded.Add("m_mainContainer");
			return excluded.ToArray();
		}
		#endregion
	}
}
