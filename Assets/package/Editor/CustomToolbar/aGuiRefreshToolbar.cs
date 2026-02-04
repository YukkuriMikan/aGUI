using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ANest.UI.Editor {
	public static class aGuiRefreshAll {
		public const string Id = "ANest/Refresh aGuiInfo";

		/// <summary>現在のシーン内のaGuiInfoを更新する</summary>
		[MenuItem(Id)]
		private static void RefreshAll() {
			var activeScene = SceneManager.GetActiveScene();
			if(!activeScene.IsValid()) return;

			var refreshedCount = 0;
			var rootObjects = activeScene.GetRootGameObjects();
			foreach(var rootObject in rootObjects) {
				var guiInfos = rootObject.GetComponentsInChildren<aGuiInfo>(true);

				foreach(var guiInfo in guiInfos) {
					if(guiInfo == null) continue;
					if(EditorUtility.IsPersistent(guiInfo)) continue;

					Undo.RecordObject(guiInfo, "Refresh aGuiInfo");
					guiInfo.Refresh();
					EditorUtility.SetDirty(guiInfo);
					refreshedCount++;
				}
			}

			if(refreshedCount > 0) {
				EditorSceneManager.MarkSceneDirty(activeScene);
			}
		}
	}
}
