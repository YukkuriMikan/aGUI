using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary>aGuiInfoのインスペクタを拡張し、リフレッシュボタンを追加する。</summary>
	[CustomEditor(typeof(aGuiInfo))]
	[CanEditMultipleObjects]
	public class aGuiInfoEditor : UnityEditor.Editor {
		/// <summary>インスペクタGUIを描画する。</summary>
		public override void OnInspectorGUI() {
			DrawDefaultInspector();

			EditorGUILayout.Space();

			if(GUILayout.Button("Refresh")) {
				foreach(var t in targets) {
					var guiInfo = t as aGuiInfo;
					if(guiInfo != null) {
						guiInfo.Refresh();
						EditorUtility.SetDirty(guiInfo);
					}
				}
			}
		}
	}
}
