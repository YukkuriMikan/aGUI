using UnityEditor;
using UnityEngine;

namespace ANest.UI {
	/// <summary>aScrollStopのシーン可視化を提供するカスタムエディタ。</summary>
	[CustomEditor(typeof(aScrollStop))]
	public class aScrollStopEditor : UnityEditor.Editor {
		#region Fields
		private aScrollStop scrollStop; // 対象コンポーネント参照
		#endregion

		#region Unity Methods
		private void OnEnable() {
			scrollStop = (aScrollStop)target;
		}

		public override void OnInspectorGUI() {
			DrawDefaultInspector();
		}

		private void OnSceneGUI() {
			if(scrollStop == null) return;
			var targetRect = scrollStop.TargetRect;
			if(targetRect == null) return;

			using(new Handles.DrawingScope()) {
				Handles.color = new Color(0.2f, 0.85f, 1f, 0.9f);
				if(scrollStop.Method == aScrollStop.StopMethod.Rect) {
					DrawRectRegion(targetRect);
				} else {
					DrawPolygonRegion(targetRect);
				}
			}
		}
		#endregion

		#region Private Methods
		private void DrawRectRegion(RectTransform targetRect) {
			if(!scrollStop.TryGetChildRegionRect(out Rect rect)) return;
			var corners = new Vector3[5];
			corners[0] = targetRect.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0f));
			corners[1] = targetRect.TransformPoint(new Vector3(rect.xMin, rect.yMax, 0f));
			corners[2] = targetRect.TransformPoint(new Vector3(rect.xMax, rect.yMax, 0f));
			corners[3] = targetRect.TransformPoint(new Vector3(rect.xMax, rect.yMin, 0f));
			corners[4] = corners[0];
			Handles.DrawAAPolyLine(2.5f, corners);
		}

		private void DrawPolygonRegion(RectTransform targetRect) {
			if(!scrollStop.TryGetChildRegionPolygon(out var points)) return;
			if(points.Count < 2) return;

			var worldPoints = new Vector3[points.Count + 1];
			for(int i = 0; i < points.Count; i++) {
				var point = points[i];
				worldPoints[i] = targetRect.TransformPoint(new Vector3(point.x, point.y, 0f));
			}
			worldPoints[worldPoints.Length - 1] = worldPoints[0];
			Handles.DrawAAPolyLine(2.5f, worldPoints);
		}
		#endregion
	}
}