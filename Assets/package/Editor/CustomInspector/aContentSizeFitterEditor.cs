using System.Collections.Generic;
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
			var fitWidth = serializedObject.FindProperty("m_fitWidth");
			var fitHeight = serializedObject.FindProperty("m_fitHeight");
			var canFit = fitWidth.boolValue || fitWidth.hasMultipleDifferentValues ||
				fitHeight.boolValue || fitHeight.hasMultipleDifferentValues;
			if(!canFit) {
				EditorGUILayout.HelpBox("Fit Width または Fit Height を有効にしてください。", MessageType.Info);
			}
			using(new EditorGUI.DisabledScope(!canFit)) {
				if(GUILayout.Button("Apply Fitting")) {
					foreach(var targetObject in targets) {
						if(targetObject is aContentSizeFitter fitter) ApplyFittingWithUndo(fitter);
					}
				}
			}
		}
		#endregion

		/// <summary>最新の子要素で即時にレイアウトを更新してからフィットする。</summary>
		private static void ApplyFittingWithUndo(aContentSizeFitter fitter) {
			using var fitterObject = new SerializedObject(fitter);
			if(!fitterObject.FindProperty("m_fitWidth").boolValue && !fitterObject.FindProperty("m_fitHeight").boolValue) return;
			var layoutGroup = fitterObject.FindProperty("m_layoutGroup").objectReferenceValue as aLayoutGroupBase;
			if(layoutGroup == null) layoutGroup = fitter.GetComponent<aLayoutGroupBase>();
			if(layoutGroup == null) return;
			var modifiedObjects = GetFittingObjects(fitter, layoutGroup);
			Undo.RecordObjects(modifiedObjects.ToArray(), "Apply Fitting");
			if(!fitter.PreserveChildPositions) layoutGroup.AlignNonAnimate(fitter.CollectChildrenEveryTime);
			fitter.ApplyFitting();
			foreach(var modifiedObject in modifiedObjects) {
				EditorUtility.SetDirty(modifiedObject);
				PrefabUtility.RecordPrefabInstancePropertyModifications(modifiedObject);
			}
		}

		/// <summary>フィットで変更する親と再配置対象の子をUndo・Prefab差分に含める。</summary>
		private static List<Object> GetFittingObjects(aContentSizeFitter fitter, aLayoutGroupBase layoutGroup) {
			var objects = new List<Object> { fitter, fitter.transform, layoutGroup };
			// 監視対象が別オブジェクトでも、フィッター直下の子は位置維持の対象となる。
			foreach(Transform child in fitter.transform) {
				if(!objects.Contains(child)) objects.Add(child);
			}
			// 再収集で追加される子も、変更前にUndoへ登録する。
			foreach(Transform child in layoutGroup.transform) {
				if(child is RectTransform && !objects.Contains(child)) objects.Add(child);
			}
			using var layoutObject = new SerializedObject(layoutGroup);
			var children = layoutObject.FindProperty("rectChildren");
			for(var i = 0; i < children.arraySize; i++) {
				var child = children.GetArrayElementAtIndex(i).objectReferenceValue;
				if(child != null && !objects.Contains(child)) objects.Add(child);
			}
			return objects;
		}
	}
}
