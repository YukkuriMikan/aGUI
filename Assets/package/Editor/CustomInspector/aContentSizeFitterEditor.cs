using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary>aContentSizeFitter のインスペクタにフィット実行ボタンを追加する。</summary>
	[CustomEditor(typeof(aContentSizeFitter), true)]
	[CanEditMultipleObjects]
	public class aContentSizeFitterEditor : UnityEditor.Editor {
		#region Unity Methods
		/// <summary>デフォルトのインスペクタにフィット実行ボタンを追加する。</summary>
		public override void OnInspectorGUI() {
			serializedObject.Update();
			DrawDefaultInspector();
			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();
			if(GUILayout.Button("Apply Fitting")) {
				foreach(var targetObject in targets) {
					if(targetObject is not aContentSizeFitter fitter) continue;
					var rectTransform = fitter.transform as RectTransform;
					if(rectTransform != null) {
						Undo.RecordObject(rectTransform, "Apply Fitting");
					}
					fitter.ApplyFitting();
					if(rectTransform != null) {
						EditorUtility.SetDirty(rectTransform);
					}
					EditorUtility.SetDirty(fitter);
				}
			}
		}
		#endregion
	}
}