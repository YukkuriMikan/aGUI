using System.Collections.Generic;
using UnityEngine;

namespace ANest.UI {
	/// <summary>TargetRectの子要素領域を基準にスクロール位置を補正する。</summary>
	public class aScrollStop : MonoBehaviour {
		#region Enums
		public enum StopMethod {
			Rect,
			Polygon
		}

		public enum UpdateTiming {
			Update,
			LateUpdate
		}
		#endregion

		#region SerializeField
		[Tooltip("位置補正対象のRectTransform")]
		[SerializeField] private RectTransform m_targetRect; // 補正対象のRectTransform
		[Tooltip("子要素領域の計算方式")]
		[SerializeField] private StopMethod m_stopMethod = StopMethod.Rect; // 領域計算方式
		[Tooltip("子要素領域のパディング")]
		[SerializeField] private Vector2 m_padding = Vector2.zero; // 領域パディング
		[Tooltip("位置補正を行うタイミング")]
		[SerializeField] private UpdateTiming m_updateTiming = UpdateTiming.LateUpdate; // 補正タイミング
		#endregion

		#region Fields
		private readonly List<Vector2> m_childPoints = new List<Vector2>(64); // 子要素のローカル頂点
		private readonly List<Vector2> m_polygonPoints = new List<Vector2>(16); // 可視化用ポリゴン
		private readonly Vector3[] m_worldCorners = new Vector3[4]; // 角取得用
		private readonly Vector3[] m_localCorners = new Vector3[4]; // ローカル角
		private readonly Vector2[] m_viewLocalCorners = new Vector2[4]; // ビュー角
		private readonly float[] m_childMaxDots = new float[8]; // 方向別の最大投影
		private readonly Vector2[] m_childExtremePoints = new Vector2[8]; // 方向別の極値点
		private static readonly Vector2[] s_polygonDirections = {
			new Vector2(1f, 0f),
			new Vector2(1f, 1f).normalized,
			new Vector2(0f, 1f),
			new Vector2(-1f, 1f).normalized,
			new Vector2(-1f, 0f),
			new Vector2(-1f, -1f).normalized,
			new Vector2(0f, -1f),
			new Vector2(1f, -1f).normalized
		};
		#endregion

		#region Properties
		public RectTransform TargetRect => m_targetRect;
		public StopMethod Method => m_stopMethod;
		#endregion

		#region Unity Methods
		private void Update() {
			if(m_updateTiming == UpdateTiming.Update) {
				ClampTargetPosition();
			}
		}

		private void LateUpdate() {
			if(m_updateTiming == UpdateTiming.LateUpdate) {
				ClampTargetPosition();
			}
		}
		#endregion

		#region Public Methods
		public bool TryGetChildRegionRect(out Rect rect) {
			rect = default;
			if(!TryCollectChildPoints()) return false;
			return TryBuildChildRect(out rect);
		}

		public bool TryGetChildRegionPolygon(out IReadOnlyList<Vector2> polygonPoints) {
			polygonPoints = null;
			if(!TryCollectChildPoints()) return false;
			BuildPolygonFromChildPoints();
			if(m_polygonPoints.Count < 3) return false;
			polygonPoints = m_polygonPoints;
			return true;
		}
		#endregion

		#region Private Methods
		private void ClampTargetPosition() {
			if(m_targetRect == null) return;
			if(transform is not RectTransform viewRect) return;
			if(!TryCollectChildPoints()) return;

			Vector2 correctionLocal;
			switch(m_stopMethod) {
				case StopMethod.Polygon:
					correctionLocal = CalculatePolygonCorrection(viewRect);
					break;
				default:
					correctionLocal = CalculateRectCorrection(viewRect);
					break;
			}

			if(correctionLocal == Vector2.zero) return;
			ApplyLocalCorrection(correctionLocal);
		}

		private Vector2 CalculateRectCorrection(RectTransform viewRect) {
			if(!TryBuildChildRect(out Rect childRect)) return Vector2.zero;
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			GetBounds(m_localCorners, out float minX, out float minY, out float maxX, out float maxY);

			float deltaX = 0f;
			float deltaY = 0f;

			if(minX < childRect.xMin) deltaX += childRect.xMin - minX;
			if(maxX > childRect.xMax) deltaX += childRect.xMax - maxX;
			if(minY < childRect.yMin) deltaY += childRect.yMin - minY;
			if(maxY > childRect.yMax) deltaY += childRect.yMax - maxY;

			return new Vector2(deltaX, deltaY);
		}

		private Vector2 CalculatePolygonCorrection(RectTransform viewRect) {
			BuildPolygonFromChildPoints();
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			FillViewLocalCorners();

			Vector2 correction = Vector2.zero;
			for(int i = 0; i < s_polygonDirections.Length; i++) {
				var dir = s_polygonDirections[i];
				float maxDot = float.NegativeInfinity;
				for(int j = 0; j < m_viewLocalCorners.Length; j++) {
					var shifted = m_viewLocalCorners[j] + correction;
					maxDot = Mathf.Max(maxDot, Vector2.Dot(shifted, dir));
				}
				float limit = m_childMaxDots[i];
				if(maxDot > limit) {
					correction += (limit - maxDot) * dir;
				}
			}

			return correction;
		}

		private void ApplyLocalCorrection(Vector2 correctionLocal) {
			var worldDelta = m_targetRect.TransformVector(new Vector3(correctionLocal.x, correctionLocal.y, 0f));
			Vector3 parentDelta = worldDelta;
			if(m_targetRect.parent != null) {
				parentDelta = m_targetRect.parent.InverseTransformVector(worldDelta);
			}
			m_targetRect.localPosition += new Vector3(-parentDelta.x, -parentDelta.y, 0f);
		}

		private bool TryCollectChildPoints() {
			m_childPoints.Clear();
			if(m_targetRect == null) return false;
			var childRects = m_targetRect.GetComponentsInChildren<RectTransform>(false);
			if(childRects == null || childRects.Length == 0) return false;

			foreach(var child in childRects) {
				if(child == m_targetRect) continue;
				child.GetWorldCorners(m_worldCorners);
				for(int i = 0; i < 4; i++) {
					var localCorner = m_targetRect.InverseTransformPoint(m_worldCorners[i]);
					m_childPoints.Add(new Vector2(localCorner.x, localCorner.y));
				}
			}

			return m_childPoints.Count > 0;
		}

		private void BuildPolygonFromChildPoints() {
			m_polygonPoints.Clear();
			for(int i = 0; i < s_polygonDirections.Length; i++) {
				m_childMaxDots[i] = float.NegativeInfinity;
				m_childExtremePoints[i] = Vector2.zero;
			}

			for(int i = 0; i < m_childPoints.Count; i++) {
				var point = m_childPoints[i];
				for(int d = 0; d < s_polygonDirections.Length; d++) {
					var dir = s_polygonDirections[d];
					float dot = Vector2.Dot(point, dir);
					if(dot > m_childMaxDots[d]) {
						m_childMaxDots[d] = dot;
						m_childExtremePoints[d] = point;
					}
				}
			}

			const float epsilon = 0.01f;
			for(int i = 0; i < m_childExtremePoints.Length; i++) {
				var dir = s_polygonDirections[i];
				var padding = Mathf.Abs(dir.x) * m_padding.x + Mathf.Abs(dir.y) * m_padding.y;
				var point = m_childExtremePoints[i] + dir * padding;
				m_childMaxDots[i] += padding;
				bool isDuplicate = false;
				for(int j = 0; j < m_polygonPoints.Count; j++) {
					if(Vector2.Distance(m_polygonPoints[j], point) < epsilon) {
						isDuplicate = true;
						break;
					}
				}
				if(!isDuplicate) {
					m_polygonPoints.Add(point);
				}
			}
		}

		private bool TryBuildChildRect(out Rect rect) {
			rect = default;
			if(m_childPoints.Count == 0) return false;

			float minX = float.PositiveInfinity;
			float minY = float.PositiveInfinity;
			float maxX = float.NegativeInfinity;
			float maxY = float.NegativeInfinity;

			for(int i = 0; i < m_childPoints.Count; i++) {
				var point = m_childPoints[i];
				minX = Mathf.Min(minX, point.x);
				minY = Mathf.Min(minY, point.y);
				maxX = Mathf.Max(maxX, point.x);
				maxY = Mathf.Max(maxY, point.y);
			}

			minX -= m_padding.x;
			maxX += m_padding.x;
			minY -= m_padding.y;
			maxY += m_padding.y;

			rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
			return rect.width > 0f || rect.height > 0f;
		}

		private void FillViewLocalCorners() {
			for(int i = 0; i < m_localCorners.Length; i++) {
				var corner = m_localCorners[i];
				m_viewLocalCorners[i] = new Vector2(corner.x, corner.y);
			}
		}

		private static void GetLocalCorners(RectTransform rect, RectTransform relativeTo, Vector3[] corners) {
			rect.GetWorldCorners(corners);
			for(int i = 0; i < corners.Length; i++) {
				corners[i] = relativeTo.InverseTransformPoint(corners[i]);
			}
		}

		private static void GetBounds(Vector3[] corners, out float minX, out float minY, out float maxX, out float maxY) {
			minX = float.PositiveInfinity;
			minY = float.PositiveInfinity;
			maxX = float.NegativeInfinity;
			maxY = float.NegativeInfinity;
			for(int i = 0; i < corners.Length; i++) {
				var corner = corners[i];
				minX = Mathf.Min(minX, corner.x);
				minY = Mathf.Min(minY, corner.y);
				maxX = Mathf.Max(maxX, corner.x);
				maxY = Mathf.Max(maxY, corner.y);
			}
		}
		#endregion
	}
}
