using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary>Material Inspector の変更を Image に通知する。通常描画時の監視は追加しない。</summary>
	[InitializeOnLoad]
	internal static class aImageMaterialChangeWatcher {
		private static readonly HashSet<Material> ChangedMaterials = new();

		static aImageMaterialChangeWatcher() {
			ObjectChangeEvents.changesPublished += OnObjectsChanged;
		}

		private static void OnObjectsChanged(ref ObjectChangeEventStream stream) {
			for(var i = 0; i < stream.length; i++) {
				if(stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
				stream.GetChangeAssetObjectPropertiesEvent(i, out var change);
				if(EditorUtility.InstanceIDToObject(change.instanceId) is Material material) ChangedMaterials.Add(material);
			}
			if(ChangedMaterials.Count == 0) return;
			try {
				foreach(var image in Object.FindObjectsByType<aImage>(FindObjectsSortMode.None)) {
					if(image.isActiveAndEnabled && ChangedMaterials.Contains(image.material)) image.SetMaterialDirty();
				}
			} finally { ChangedMaterials.Clear(); }
		}
	}
}
