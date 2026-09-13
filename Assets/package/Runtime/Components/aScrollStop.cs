using System.Collections.Generic;
using UnityEngine;

namespace ANest.UI {
	/// <summary>TargetRectの子要素領域を基準にスクロール位置を補正する。</summary>
	public class aScrollStop : MonoBehaviour {
		#region Enums
		/// <summary>子要素領域の計算方式</summary>
		public enum StopMethod {
			Rect,   // 矩形
			Polygon // ポリゴン
		}

		/// <summary>位置補正を行うタイミング</summary>
		public enum UpdateTiming {
			Update,    // Updateで補正
			LateUpdate // LateUpdateで補正
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

		private struct ChildState {
			public RectTransform Node;
			public Rect Rect;
			public Matrix4x4 Matrix;
			public static ChildState Capture(RectTransform node, Vector3 origin) {
				var matrix = node.localToWorldMatrix;
				// Targetの平行移動だけでは子の領域は変わらない。
				matrix.m03 -= origin.x;
				matrix.m13 -= origin.y;
				matrix.m23 -= origin.z;
				return new ChildState {
					Node = node, Rect = node.rect, Matrix = matrix
				};
			}
			public bool SameGeometry(ChildState other) => ReferenceEquals(Node, other.Node)
				&& Rect.Equals(other.Rect) && Matrix.Equals(other.Matrix);
		}

		private readonly List<RectTransform> m_childNodes = new();
		private readonly List<ChildState> m_childStates = new();
		private RectTransform m_cachedTarget;
		private bool m_geometryValid, m_polygonValid, m_childRectValid;
		private Vector2 m_polygonPadding, m_childRectPadding;
		private Rect m_cachedChildRect;
		private static readonly System.Comparison<Vector2> s_comparePoints = CompareVector2;
		private readonly List<Vector2> m_childPoints = new(64);         // 子要素のローカル頂点
		private readonly List<Vector2> m_polygonPoints = new(16);       // 可視化用ポリゴン
		private readonly List<Vector2> m_polygonWorkPoints = new(64);   // ポリゴン計算用
		private readonly Vector3[] m_worldCorners = new Vector3[4];     // 角取得用
		private readonly Vector3[] m_localCorners = new Vector3[4];     // ローカル角
		private readonly Vector2[] m_viewLocalCorners = new Vector2[4]; // ビュー角
		private readonly float[] m_childMaxDots = new float[8];         // 方向別の最大投影
		private Vector3 m_prevTargetLocalPosition;                      // 前回のTargetRectローカル位置
		private bool m_hasPrevTargetLocalPosition;                      // 前回位置の有無
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
		/// <summary>補正対象のRectTransform</summary>
		public RectTransform TargetRect => m_targetRect;
		/// <summary>領域計算方式</summary>
		public StopMethod Method => m_stopMethod;
		#endregion

		#region Unity Methods
		/// <summary>Update指定時に位置補正を実行</summary>
		private void Update() {
			if(m_updateTiming == UpdateTiming.Update) {
				ClampTargetPosition();
			}
		}

		/// <summary>LateUpdate指定時に位置補正を実行</summary>
		private void LateUpdate() {
			if(m_updateTiming == UpdateTiming.LateUpdate) {
				ClampTargetPosition();
			}
		}
		#endregion

		#region Public Methods
		/// <summary>子要素領域を矩形で取得する</summary>
		public bool TryGetChildRegionRect(out Rect rect) {
			rect = default;
			if(!TryCollectChildPoints()) return false;
			return TryBuildChildRect(out rect);
		}

		/// <summary>子要素領域をポリゴンで取得する</summary>
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
		/// <summary>TargetRectの位置補正を実行する</summary>
		private void ClampTargetPosition() {
			// 必須参照の欠落やRectTransform以外なら補正しない
			if(m_targetRect == null) return;
			if(transform is not RectTransform viewRect) return;
			// 子要素頂点が取得できない場合は補正不要
			if(!TryCollectChildPoints()) return;

			var currentLocalPosition = m_targetRect.localPosition;
			Vector2 movementLocal = Vector2.zero;
			if(m_hasPrevTargetLocalPosition) {
				// 前フレームとの差分をTargetRectローカルの移動量へ変換
				var parentDelta = currentLocalPosition - m_prevTargetLocalPosition;
				movementLocal = ConvertParentDeltaToTargetLocal(parentDelta);
			}

			Vector2 correctionLocal;
			switch(m_stopMethod) {
				case StopMethod.Polygon:
					// ポリゴンで補正量を算出
					correctionLocal = CalculatePolygonCorrection(viewRect, movementLocal);
					break;
				default:
					// 矩形で補正量を算出
					correctionLocal = CalculateRectCorrection(viewRect);
					break;
			}

			if(correctionLocal != Vector2.zero) {
				// 必要な補正がある場合のみ反映
				ApplyLocalCorrection(correctionLocal);
			}
			// 次回差分計算用の位置を保存
			m_prevTargetLocalPosition = m_targetRect.localPosition;
			m_hasPrevTargetLocalPosition = true;
		}

		/// <summary>矩形領域で補正量を算出する</summary>
		private Vector2 CalculateRectCorrection(RectTransform viewRect) {
			// 子要素の矩形が構築できない場合は補正なし
			if(!TryBuildChildRect(out Rect childRect)) return Vector2.zero;
			// ビューのローカル角を取得して境界を計算
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			GetBounds(m_localCorners, out float minX, out float minY, out float maxX, out float maxY);

			// ビューと子要素領域の大小関係を判定
			var viewWidth = maxX - minX;
			var viewHeight = maxY - minY;
			DetermineChildParentSize(childRect.width, childRect.height, viewWidth, viewHeight, out bool childWider, out bool childTaller);
			// 軸方向に必要な補正量を返す
			return CalculateAxisAwareCorrection(childRect, minX, minY, maxX, maxY, childWider, childTaller);
		}

		/// <summary>ポリゴン領域で補正量を算出する</summary>
		private Vector2 CalculatePolygonCorrection(RectTransform viewRect, Vector2 movementLocal) {
			// 子要素の矩形が構築できない場合は補正なし
			if(!TryBuildChildRect(out Rect childRect)) return Vector2.zero;
			// ビューのローカル角を取得して境界を計算
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			GetBounds(m_localCorners, out float minX, out float minY, out float maxX, out float maxY);

			// ビューと子要素領域の大小関係を判定
			var viewWidth = maxX - minX;
			var viewHeight = maxY - minY;
			DetermineChildParentSize(childRect.width, childRect.height, viewWidth, viewHeight, out bool childWider, out bool childTaller);
			if(childWider && childTaller) {
				// 子要素がビューより大きい場合はポリゴン補正を行う
				return CalculatePolygonCorrectionChildLarger(viewRect, movementLocal);
			}

			// サイズ差がある場合は軸方向補正で対応
			return CalculateAxisAwareCorrection(childRect, minX, minY, maxX, maxY, childWider, childTaller);
		}

		/// <summary>子要素領域とビューの大小関係を判定する</summary>
		private static void DetermineChildParentSize(float childWidth, float childHeight, float viewWidth, float viewHeight, out bool childWider, out bool childTaller) {
			const float epsilon = 0.01f;
			// 微小誤差を考慮して大小判定
			childWider = childWidth + epsilon >= viewWidth;
			childTaller = childHeight + epsilon >= viewHeight;
		}

		/// <summary>軸ごとの補正量を計算する</summary>
		private static Vector2 CalculateAxisAwareCorrection(Rect childRect, float minX, float minY, float maxX, float maxY, bool childWider, bool childTaller) {
			float deltaX = 0f;
			float deltaY = 0f;

			if(childWider) {
				// 子要素が広い場合はビューが収まるように補正
				if(minX < childRect.xMin) deltaX += childRect.xMin - minX;
				if(maxX > childRect.xMax) deltaX += childRect.xMax - maxX;
			} else {
				// 子要素が狭い場合は子要素がビューに収まるように補正
				if(childRect.xMin < minX) deltaX += childRect.xMin - minX;
				if(childRect.xMax > maxX) deltaX += childRect.xMax - maxX;
			}

			if(childTaller) {
				// 縦方向も同様に補正
				if(minY < childRect.yMin) deltaY += childRect.yMin - minY;
				if(maxY > childRect.yMax) deltaY += childRect.yMax - maxY;
			} else {
				if(childRect.yMin < minY) deltaY += childRect.yMin - minY;
				if(childRect.yMax > maxY) deltaY += childRect.yMax - maxY;
			}

			return new Vector2(deltaX, deltaY);
		}

		/// <summary>子要素がビューより大きい場合のポリゴン補正を算出する</summary>
		private Vector2 CalculatePolygonCorrectionChildLarger(RectTransform viewRect, Vector2 movementLocal) {
			// 子要素頂点からポリゴンを構築
			BuildPolygonFromChildPoints();
			// ビューの角をTargetRectローカルで取得
			GetLocalCorners(viewRect, m_targetRect, m_localCorners);
			FillViewLocalCorners();

			const float movementEpsilon = 0.0001f;
			const float correctionEpsilon = 0.001f;
			// 移動がほぼない場合は全方向から補正
			if(movementLocal.sqrMagnitude <= movementEpsilon) {
				return CalculatePolygonCorrectionAllDirections();
			}

			// 移動方向と逆向きに補正を寄せる
			var correctionDir = -movementLocal.normalized;
			float requiredShift = 0f;
			for (int i = 0; i < s_polygonDirections.Length; i++) {
				var dir = s_polygonDirections[i];
				float maxDot = float.NegativeInfinity;
				for (int j = 0; j < m_viewLocalCorners.Length; j++) {
					maxDot = Mathf.Max(maxDot, Vector2.Dot(m_viewLocalCorners[j], dir));
				}
				float limit = m_childMaxDots[i];
				if(maxDot <= limit + correctionEpsilon) continue;
				// 移動方向で補正できない場合は全方向補正へ切り替え
				float dirDot = Vector2.Dot(correctionDir, dir);
				if(dirDot <= 0f) {
					return CalculatePolygonCorrectionAllDirections();
				}
				float shift = (maxDot - limit) / dirDot;
				if(shift > requiredShift) {
					requiredShift = shift;
				}
			}

			// 必要量が閾値以下なら補正なし
			if(requiredShift <= correctionEpsilon) return Vector2.zero;
			return correctionDir * requiredShift;
		}

		/// <summary>全方向から必要な補正量を算出する</summary>
		private Vector2 CalculatePolygonCorrectionAllDirections() {
			Vector2 correction = Vector2.zero;
			const float correctionEpsilon = 0.001f;
			for (int i = 0; i < s_polygonDirections.Length; i++) {
				var dir = s_polygonDirections[i];
				float maxDot = float.NegativeInfinity;
				for (int j = 0; j < m_viewLocalCorners.Length; j++) {
					// 既存補正を加味して投影量を計算
					var shifted = m_viewLocalCorners[j] + correction;
					maxDot = Mathf.Max(maxDot, Vector2.Dot(shifted, dir));
				}
				float limit = m_childMaxDots[i];
				if(maxDot > limit + correctionEpsilon) {
					// 上限を超えた分だけ補正量を足す
					correction += (limit - maxDot) * dir;
				}
			}

			return correction;
		}

		/// <summary>親座標系の移動量をTargetRectローカルに変換する</summary>
		private Vector2 ConvertParentDeltaToTargetLocal(Vector3 parentDelta) {
			Vector3 worldDelta = parentDelta;
			if(m_targetRect.parent != null) {
				// 親の回転/スケールを考慮してワールドに変換
				worldDelta = m_targetRect.parent.TransformVector(parentDelta);
			}
			// TargetRectローカルに変換して2D成分に落とす
			var localDelta = m_targetRect.InverseTransformVector(worldDelta);
			return new Vector2(localDelta.x, localDelta.y);
		}

		/// <summary>ローカル補正量をTargetRectに適用する</summary>
		private void ApplyLocalCorrection(Vector2 correctionLocal) {
			// TargetRectローカル補正をワールドに変換
			var worldDelta = m_targetRect.TransformVector(new Vector3(correctionLocal.x, correctionLocal.y, 0f));
			Vector3 parentDelta = worldDelta;
			if(m_targetRect.parent != null) {
				// 親ローカルへ戻して位置補正に使う
				parentDelta = m_targetRect.parent.InverseTransformVector(worldDelta);
			}
			// 親ローカル座標系の補正を反映
			m_targetRect.localPosition += new Vector3(-parentDelta.x, -parentDelta.y, 0f);
		}

		/// <summary>子要素のローカル頂点を収集する</summary>
		private bool TryCollectChildPoints() {
			if(m_targetRect == null) {
				ClearGeometryCache();
				return false;
			}
			var origin = m_targetRect.position;
			// 一括収集は個々の親・子数・有効状態を問い合わせるより軽く、
			// 深い階層への追加や非アクティブ化もその呼び出し中に検出できる。
			m_targetRect.GetComponentsInChildren(false, m_childNodes);
			var geometryChanged = m_cachedTarget != m_targetRect || !m_geometryValid
				|| m_childNodes.Count != m_childStates.Count;
			for(var i = 0; i < m_childNodes.Count; i++) {
				var state = ChildState.Capture(m_childNodes[i], origin);
				if(i >= m_childStates.Count) m_childStates.Add(state);
				else {
					if(!m_childStates[i].SameGeometry(state)) geometryChanged = true;
					m_childStates[i] = state;
				}
			}
			if(m_childStates.Count > m_childNodes.Count)
				m_childStates.RemoveRange(m_childNodes.Count, m_childStates.Count - m_childNodes.Count);
			if(!geometryChanged) return m_childPoints.Count > 0;
			m_cachedTarget = m_targetRect;
			m_childPoints.Clear();
			m_geometryValid = true;
			m_polygonValid = m_childRectValid = false;
			for(var n = 0; n < m_childNodes.Count; n++) {
				var child = m_childNodes[n];
				if(child == m_targetRect) continue;
				child.GetWorldCorners(m_worldCorners);
				for(var i = 0; i < 4; i++) {
					var corner = m_targetRect.InverseTransformPoint(m_worldCorners[i]);
					m_childPoints.Add(new Vector2(corner.x, corner.y));
				}
			}
			return m_childPoints.Count > 0;
		}


		private void OnDisable() => ClearGeometryCache();

		private void ClearGeometryCache() {
			m_cachedTarget = null;
			m_geometryValid = m_polygonValid = m_childRectValid = false;
			m_childNodes.Clear();
			m_childStates.Clear();
			m_childPoints.Clear();
			m_polygonPoints.Clear();
		}

		/// <summary>子要素頂点からポリゴンを構築する</summary>
		private void BuildPolygonFromChildPoints() {
			if(m_polygonValid && m_polygonPadding.Equals(m_padding)) return;
			m_polygonPadding = m_padding;
			m_polygonValid = true;
			// 作業用リストと投影値を初期化
			m_polygonPoints.Clear();
			m_polygonWorkPoints.Clear();
			for (int i = 0; i < s_polygonDirections.Length; i++) {
				m_childMaxDots[i] = float.NegativeInfinity;
			}

			var hasPadding = m_padding != Vector2.zero;
			for (int i = 0; i < m_childPoints.Count; i++) {
				var point = m_childPoints[i];
				if(hasPadding) {
					// パディング分だけ四隅を展開
					m_polygonWorkPoints.Add(new Vector2(point.x - m_padding.x, point.y - m_padding.y));
					m_polygonWorkPoints.Add(new Vector2(point.x - m_padding.x, point.y + m_padding.y));
					m_polygonWorkPoints.Add(new Vector2(point.x + m_padding.x, point.y + m_padding.y));
					m_polygonWorkPoints.Add(new Vector2(point.x + m_padding.x, point.y - m_padding.y));
				} else {
					m_polygonWorkPoints.Add(point);
				}
			}

			if(m_polygonWorkPoints.Count < 3) {
				// 点/線分しかない場合はそのまま使用
				for (int i = 0; i < m_polygonWorkPoints.Count; i++) {
					var point = m_polygonWorkPoints[i];
					for (int d = 0; d < s_polygonDirections.Length; d++) {
						float dot = Vector2.Dot(point, s_polygonDirections[d]);
						if(dot > m_childMaxDots[d]) m_childMaxDots[d] = dot;
					}
					m_polygonPoints.Add(point);
				}
				return;
			}

			// 凸包作成のため座標をソート
			m_polygonWorkPoints.Sort(s_comparePoints);

			for (int i = 0; i < m_polygonWorkPoints.Count; i++) {
				var point = m_polygonWorkPoints[i];
				// 上側の凸包を構築
				while (m_polygonPoints.Count >= 2 && Cross(m_polygonPoints[m_polygonPoints.Count - 2], m_polygonPoints[m_polygonPoints.Count - 1], point) <= 0f) {
					m_polygonPoints.RemoveAt(m_polygonPoints.Count - 1);
				}
				m_polygonPoints.Add(point);
			}

			int lowerCount = m_polygonPoints.Count;
			for (int i = m_polygonWorkPoints.Count - 2; i >= 0; i--) {
				var point = m_polygonWorkPoints[i];
				// 下側の凸包を構築
				while (m_polygonPoints.Count > lowerCount && Cross(m_polygonPoints[m_polygonPoints.Count - 2], m_polygonPoints[m_polygonPoints.Count - 1], point) <= 0f) {
					m_polygonPoints.RemoveAt(m_polygonPoints.Count - 1);
				}
				m_polygonPoints.Add(point);
			}

			if(m_polygonPoints.Count > 1) {
				// 始点重複を除去
				m_polygonPoints.RemoveAt(m_polygonPoints.Count - 1);
			}

			for (int i = 0; i < m_polygonPoints.Count; i++) {
				var point = m_polygonPoints[i];
				for (int d = 0; d < s_polygonDirections.Length; d++) {
					// 各方向への最大投影値を更新
					float dot = Vector2.Dot(point, s_polygonDirections[d]);
					if(dot > m_childMaxDots[d]) m_childMaxDots[d] = dot;
				}
			}
		}

		/// <summary>頂点ソート用の比較</summary>
		private static int CompareVector2(Vector2 a, Vector2 b) {
			int compare = a.x.CompareTo(b.x);
			return compare != 0 ? compare : a.y.CompareTo(b.y);
		}

		/// <summary>外積（符号付き面積）を計算する</summary>
		private static float Cross(Vector2 origin, Vector2 a, Vector2 b) {
			return (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);
		}

		/// <summary>子要素頂点から矩形領域を構築する</summary>
		private bool TryBuildChildRect(out Rect rect) {
			if(m_childRectValid && m_childRectPadding.Equals(m_padding)) {
				rect = m_cachedChildRect;
				return rect.width > 0f || rect.height > 0f;
			}
			rect = default;
			if(m_childPoints.Count == 0) return false;

			float minX = float.PositiveInfinity;
			float minY = float.PositiveInfinity;
			float maxX = float.NegativeInfinity;
			float maxY = float.NegativeInfinity;

			for (int i = 0; i < m_childPoints.Count; i++) {
				var point = m_childPoints[i];
				// 子要素頂点の範囲を集計
				minX = Mathf.Min(minX, point.x);
				minY = Mathf.Min(minY, point.y);
				maxX = Mathf.Max(maxX, point.x);
				maxY = Mathf.Max(maxY, point.y);
			}

			// パディングを反映
			minX -= m_padding.x;
			maxX += m_padding.x;
			minY -= m_padding.y;
			maxY += m_padding.y;

			// 最小/最大で矩形を生成
			rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
			m_cachedChildRect = rect;
			m_childRectPadding = m_padding;
			m_childRectValid = true;
			return rect.width > 0f || rect.height > 0f;
		}

		/// <summary>ビューのローカル角を2D配列へ詰める</summary>
		private void FillViewLocalCorners() {
			for (int i = 0; i < m_localCorners.Length; i++) {
				// Vector3 -> Vector2へ変換
				var corner = m_localCorners[i];
				m_viewLocalCorners[i] = new Vector2(corner.x, corner.y);
			}
		}

		/// <summary>指定RectTransformの角を相対座標で取得する</summary>
		private static void GetLocalCorners(RectTransform rect, RectTransform relativeTo, Vector3[] corners) {
			// ワールド角を取得して相対座標に変換
			rect.GetWorldCorners(corners);
			for (int i = 0; i < corners.Length; i++) {
				corners[i] = relativeTo.InverseTransformPoint(corners[i]);
			}
		}

		/// <summary>角配列から境界を取得する</summary>
		private static void GetBounds(Vector3[] corners, out float minX, out float minY, out float maxX, out float maxY) {
			minX = float.PositiveInfinity;
			minY = float.PositiveInfinity;
			maxX = float.NegativeInfinity;
			maxY = float.NegativeInfinity;
			for (int i = 0; i < corners.Length; i++) {
				var corner = corners[i];
				// 角の範囲を集計
				minX = Mathf.Min(minX, corner.x);
				minY = Mathf.Min(minY, corner.y);
				maxX = Mathf.Max(maxX, corner.x);
				maxY = Mathf.Max(maxY, corner.y);
			}
		}
		#endregion
	}
}
