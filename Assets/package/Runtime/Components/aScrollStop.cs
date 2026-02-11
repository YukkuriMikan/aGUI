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
		private readonly List<Vector2> m_polygonWorkPoints = new List<Vector2>(64); // ポリゴン計算用
		private readonly Vector3[] m_worldCorners = new Vector3[4]; // 角取得用
		private readonly Vector3[] m_localCorners = new Vector3[4]; // ローカル角
		private readonly Vector2[] m_viewLocalCorners = new Vector2[4]; // ビュー角
		private readonly float[] m_childMaxDots = new float[8]; // 方向別の最大投影
		private Vector3 m_prevTargetLocalPosition; // 前回のTargetRectローカル位置
		private bool m_hasPrevTargetLocalPosition; // 前回位置の有無
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

			var currentLocalPosition = m_targetRect.localPosition;
			Vector2 movementLocal = Vector2.zero;
			if(m_hasPrevTargetLocalPosition) {
				var parentDelta = currentLocalPosition - m_prevTargetLocalPosition;
				movementLocal = ConvertParentDeltaToTargetLocal(parentDelta);
			}

			Vector2 correctionLocal;
			switch(m_stopMethod) {
				case StopMethod.Polygon:
					correctionLocal = CalculatePolygonCorrection(viewRect, movementLocal);
					break;
				default:
					correctionLocal = CalculateRectCorrection(viewRect);
					break;
			}

			if(correctionLocal != Vector2.zero) {
				ApplyLocalCorrection(correctionLocal);
			}
			m_prevTargetLocalPosition = m_targetRect.localPosition;
			m_hasPrevTargetLocalPosition = true;
		}

		private Vector2 CalculateRectCorrection(RectTransform viewRect) {
			if(!TryBuildChildRect(out Rect childRect)) return Vector2.zero;
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			GetBounds(m_localCorners, out float minX, out float minY, out float maxX, out float maxY);

			var viewWidth = maxX - minX;
			var viewHeight = maxY - minY;
			DetermineChildParentSize(childRect.width, childRect.height, viewWidth, viewHeight, out bool childWider, out bool childTaller);
			return CalculateAxisAwareCorrection(childRect, minX, minY, maxX, maxY, childWider, childTaller);
		}

		private Vector2 CalculatePolygonCorrection(RectTransform viewRect, Vector2 movementLocal) {
			if(!TryBuildChildRect(out Rect childRect)) return Vector2.zero;
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			GetBounds(m_localCorners, out float minX, out float minY, out float maxX, out float maxY);

			var viewWidth = maxX - minX;
			var viewHeight = maxY - minY;
			DetermineChildParentSize(childRect.width, childRect.height, viewWidth, viewHeight, out bool childWider, out bool childTaller);
			if(childWider && childTaller) {
				return CalculatePolygonCorrectionChildLarger(viewRect, movementLocal);
			}

			return CalculateAxisAwareCorrection(childRect, minX, minY, maxX, maxY, childWider, childTaller);
		}

		private static void DetermineChildParentSize(float childWidth, float childHeight, float viewWidth, float viewHeight, out bool childWider, out bool childTaller) {
			const float epsilon = 0.01f;
			childWider = childWidth + epsilon >= viewWidth;
			childTaller = childHeight + epsilon >= viewHeight;
		}

		private static Vector2 CalculateAxisAwareCorrection(Rect childRect, float minX, float minY, float maxX, float maxY, bool childWider, bool childTaller) {
			float deltaX = 0f;
			float deltaY = 0f;

			if(childWider) {
				if(minX < childRect.xMin) deltaX += childRect.xMin - minX;
				if(maxX > childRect.xMax) deltaX += childRect.xMax - maxX;
			} else {
				if(childRect.xMin < minX) deltaX += childRect.xMin - minX;
				if(childRect.xMax > maxX) deltaX += childRect.xMax - maxX;
			}

			if(childTaller) {
				if(minY < childRect.yMin) deltaY += childRect.yMin - minY;
				if(maxY > childRect.yMax) deltaY += childRect.yMax - maxY;
			} else {
				if(childRect.yMin < minY) deltaY += childRect.yMin - minY;
				if(childRect.yMax > maxY) deltaY += childRect.yMax - maxY;
			}

			return new Vector2(deltaX, deltaY);
		}

		private Vector2 CalculatePolygonCorrectionChildLarger(RectTransform viewRect, Vector2 movementLocal) {
			BuildPolygonFromChildPoints();
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			FillViewLocalCorners();

			const float movementEpsilon = 0.0001f;
			const float correctionEpsilon = 0.001f;
			if(movementLocal.sqrMagnitude <= movementEpsilon) {
				return CalculatePolygonCorrectionAllDirections();
			}

			var correctionDir = -movementLocal.normalized;
			float requiredShift = 0f;
			for(int i = 0; i < s_polygonDirections.Length; i++) {
				var dir = s_polygonDirections[i];
				float maxDot = float.NegativeInfinity;
				for(int j = 0; j < m_viewLocalCorners.Length; j++) {
					maxDot = Mathf.Max(maxDot, Vector2.Dot(m_viewLocalCorners[j], dir));
				}
				float limit = m_childMaxDots[i];
				if(maxDot <= limit + correctionEpsilon) continue;
				float dirDot = Vector2.Dot(correctionDir, dir);
				if(dirDot <= 0f) {
					return CalculatePolygonCorrectionAllDirections();
				}
				float shift = (maxDot - limit) / dirDot;
				if(shift > requiredShift) {
					requiredShift = shift;
				}
			}

			if(requiredShift <= correctionEpsilon) return Vector2.zero;
			return correctionDir * requiredShift;
		}

		private Vector2 CalculatePolygonCorrectionAllDirections() {
			Vector2 correction = Vector2.zero;
			const float correctionEpsilon = 0.001f;
			for(int i = 0; i < s_polygonDirections.Length; i++) {
				var dir = s_polygonDirections[i];
				float maxDot = float.NegativeInfinity;
				for(int j = 0; j < m_viewLocalCorners.Length; j++) {
					var shifted = m_viewLocalCorners[j] + correction;
					maxDot = Mathf.Max(maxDot, Vector2.Dot(shifted, dir));
				}
				float limit = m_childMaxDots[i];
				if(maxDot > limit + correctionEpsilon) {
					correction += (limit - maxDot) * dir;
				}
			}

			return correction;
		}

		private Vector2 ConvertParentDeltaToTargetLocal(Vector3 parentDelta) {
			Vector3 worldDelta = parentDelta;
			if(m_targetRect.parent != null) {
				worldDelta = m_targetRect.parent.TransformVector(parentDelta);
			}
			var localDelta = m_targetRect.InverseTransformVector(worldDelta);
			return new Vector2(localDelta.x, localDelta.y);
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
			m_polygonWorkPoints.Clear();
			for(int i = 0; i < s_polygonDirections.Length; i++) {
				m_childMaxDots[i] = float.NegativeInfinity;
			}

			var hasPadding = m_padding != Vector2.zero;
			for(int i = 0; i < m_childPoints.Count; i++) {
				var point = m_childPoints[i];
				if(hasPadding) {
					m_polygonWorkPoints.Add(new Vector2(point.x - m_padding.x, point.y - m_padding.y));
					m_polygonWorkPoints.Add(new Vector2(point.x - m_padding.x, point.y + m_padding.y));
					m_polygonWorkPoints.Add(new Vector2(point.x + m_padding.x, point.y + m_padding.y));
					m_polygonWorkPoints.Add(new Vector2(point.x + m_padding.x, point.y - m_padding.y));
				} else {
					m_polygonWorkPoints.Add(point);
				}
			}

			if(m_polygonWorkPoints.Count < 3) {
				for(int i = 0; i < m_polygonWorkPoints.Count; i++) {
					var point = m_polygonWorkPoints[i];
					for(int d = 0; d < s_polygonDirections.Length; d++) {
						float dot = Vector2.Dot(point, s_polygonDirections[d]);
						if(dot > m_childMaxDots[d]) m_childMaxDots[d] = dot;
					}
					m_polygonPoints.Add(point);
				}
				return;
			}

			m_polygonWorkPoints.Sort(CompareVector2);

			for(int i = 0; i < m_polygonWorkPoints.Count; i++) {
				var point = m_polygonWorkPoints[i];
				while(m_polygonPoints.Count >= 2 && Cross(m_polygonPoints[m_polygonPoints.Count - 2], m_polygonPoints[m_polygonPoints.Count - 1], point) <= 0f) {
					m_polygonPoints.RemoveAt(m_polygonPoints.Count - 1);
				}
				m_polygonPoints.Add(point);
			}

			int lowerCount = m_polygonPoints.Count;
			for(int i = m_polygonWorkPoints.Count - 2; i >= 0; i--) {
				var point = m_polygonWorkPoints[i];
				while(m_polygonPoints.Count > lowerCount && Cross(m_polygonPoints[m_polygonPoints.Count - 2], m_polygonPoints[m_polygonPoints.Count - 1], point) <= 0f) {
					m_polygonPoints.RemoveAt(m_polygonPoints.Count - 1);
				}
				m_polygonPoints.Add(point);
			}

			if(m_polygonPoints.Count > 1) {
				m_polygonPoints.RemoveAt(m_polygonPoints.Count - 1);
			}

			for(int i = 0; i < m_polygonPoints.Count; i++) {
				var point = m_polygonPoints[i];
				for(int d = 0; d < s_polygonDirections.Length; d++) {
					float dot = Vector2.Dot(point, s_polygonDirections[d]);
					if(dot > m_childMaxDots[d]) m_childMaxDots[d] = dot;
				}
			}
		}

		private static int CompareVector2(Vector2 a, Vector2 b) {
			int compare = a.x.CompareTo(b.x);
			return compare != 0 ? compare : a.y.CompareTo(b.y);
		}

		private static float Cross(Vector2 origin, Vector2 a, Vector2 b) {
			return (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);
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
