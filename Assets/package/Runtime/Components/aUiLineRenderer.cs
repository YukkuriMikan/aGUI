using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;

namespace ANest.UI {
	public enum aUiLineRendererSpace {
		Local,
		World
	}

 public enum CapType {
 	Default,
 	Round,
 	Square
 }

 	public enum CornerType {
 		Default,
 		Round,
 		Bevel
 	}

	/// <summary>
	/// uGUI で LineRenderer 風の描画を行うコンポーネント
	/// </summary>
	[RequireComponent(typeof(CanvasRenderer))]
	[AddComponentMenu("UI/aUiLineRenderer")]
	public class aUiLineRenderer : MaskableGraphic {
		#region SerializeFields
		[SerializeField] private float m_thickness = 2f;
		[SerializeField] private AnimationCurve m_thicknessCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
		[SerializeField] private bool m_loop;
		[SerializeField] private List<Vector2> m_points = new();
		[SerializeField] private bool m_enableCornerInterpolation = true;
		[SerializeField] private int m_cornerVertices = 8;
		[SerializeField] private CornerType m_cornerType = CornerType.Default;
		[SerializeField] private CapType m_startCap = CapType.Default;
		[SerializeField] private CapType m_endCap = CapType.Default;
		[SerializeField] private int m_startCapSegments = 8;
		[SerializeField] private int m_endCapSegments = 8;
		[SerializeField] private aUiLineRendererSpace m_space = aUiLineRendererSpace.Local;
		[SerializeField] private Sprite m_sprite;
		[SerializeField] private Vector2 m_uvTiling = Vector2.one;
		[SerializeField] private Vector2 m_uvOffset = Vector2.zero;
		[SerializeField] private bool m_swapUvAxes = false;
		#endregion

		#region Properties
		/// <summary> 線の太さ（ローカル座標） </summary>
		public float Thickness {
			get => m_thickness;
			set {
				m_thickness = Mathf.Max(0f, value);
				SetVerticesDirty();
			}
		}

		/// <summary> 線の長さに対する太さの割合を制御するカーブ（0〜1） </summary>
		public AnimationCurve ThicknessCurve {
			get => m_thicknessCurve;
			set {
				m_thicknessCurve = value;
				SetVerticesDirty();
			}
		}

		/// <summary> 始点終点をつなげるループ </summary>
		public bool Loop {
			get => m_loop;
			set {
				m_loop = value;
				SetVerticesDirty();
			}
		}

		/// <summary> 角の丸めに使用する補間頂点数 </summary>
		public int CornerVertices {
			get => m_cornerVertices;
			set {
				m_cornerVertices = Mathf.Max(0, value);
				SetVerticesDirty();
			}
		}

		/// <summary> 角の補間を行うかどうか </summary>
		public bool EnableCornerInterpolation {
			get => m_enableCornerInterpolation;
			set {
				m_enableCornerInterpolation = value;
				SetVerticesDirty();
			}
		}

		/// <summary> ポイントの座標空間 </summary>
		public aUiLineRendererSpace Space {
			get => m_space;
			set {
				m_space = value;
				SetVerticesDirty();
			}
		}

		/// <summary> 描画ポイントリスト（ローカル座標） </summary>
		public IList<Vector2> Points => m_points;

		/// <summary> 描画ポイント数 </summary>
		public int PointsCount => m_points?.Count ?? 0;

		/// <summary>
		/// ポイントをすべてクリアします。
		/// </summary>
		public void ClearPoints() {
			m_points.Clear();
			SetVerticesDirty();
		}

		/// <summary>
		/// ポイントを追加します。
		/// </summary>
		public void AddPoint(Vector2 point) {
			m_points.Add(point);
			SetVerticesDirty();
		}

		/// <summary>
		/// 複数のポイントを追加します。
		/// </summary>
		public void AddPoints(IEnumerable<Vector2> points) {
			if(points == null) return;
			m_points.AddRange(points);
			SetVerticesDirty();
		}

		/// <summary>
		/// 指定インデックスのポイントを削除します。
		/// </summary>
		public bool RemovePoint(int index) {
			if(index < 0 || index >= m_points.Count) return false;
			m_points.RemoveAt(index);
			SetVerticesDirty();
			return true;
		}

		/// <summary>
		/// 指定したインデックス範囲のポイントを削除します（両端含む）。
		/// </summary>
		/// <param name="startIndex">削除を開始するインデックス</param>
		/// <param name="endIndex">削除を終了するインデックス（包含）</param>
		/// <returns>削除したポイント数</returns>
		public int RemovePoints(int startIndex, int endIndex) {
			if(startIndex < 0 || endIndex < startIndex || startIndex >= m_points.Count) return 0;
			endIndex = Mathf.Min(endIndex, m_points.Count - 1);
			var removeCount = endIndex - startIndex + 1;
			m_points.RemoveRange(startIndex, removeCount);
			SetVerticesDirty();
			return removeCount;
		}

		/// <summary>
		/// 指定インデックスのポイントを置き換えます。
		/// </summary>
		public bool ReplacePoint(int index, Vector2 point) {
			if(index < 0 || index >= m_points.Count) return false;
			m_points[index] = point;
			SetVerticesDirty();
			return true;
		}

		/// <summary>
		/// 指定インデックスにポイントを挿入します。
		/// </summary>
		public bool InsertPoint(int index, Vector2 point) {
			if(index < 0 || index > m_points.Count) return false;
			m_points.Insert(index, point);
			SetVerticesDirty();
			return true;
		}

		/// <summary> UV タイリング </summary>
		public Vector2 UvTiling {
			get => m_uvTiling;
			set {
				m_uvTiling = value;
				SetVerticesDirty();
			}
		}

		/// <summary> UV オフセット </summary>
		public Vector2 UvOffset {
			get => m_uvOffset;
			set {
				m_uvOffset = value;
				SetVerticesDirty();
			}
		}

		/// <summary> UV の X/Y を入れ替えるかどうか </summary>
		public bool SwapUvAxes {
			get => m_swapUvAxes;
			set {
				m_swapUvAxes = value;
				SetVerticesDirty();
			}
		}

		/// <summary> 角のメッシュタイプ </summary>
		public CornerType CornerMeshType {
			get => m_cornerType;
			set {
				m_cornerType = value;
				SetVerticesDirty();
			}
		}

		/// <summary> 始点のキャップ種別 </summary>
		public CapType StartCap {
			get => m_startCap;
			set {
				m_startCap = value;
				SetVerticesDirty();
			}
		}

		/// <summary> 始点キャップのセグメント数（Round時のみ有効） </summary>
		public int StartCapSegments {
			get => m_startCapSegments;
			set {
				m_startCapSegments = Mathf.Max(1, value);
				SetVerticesDirty();
			}
		}

		/// <summary> 終点のキャップ種別 </summary>
		public CapType EndCap {
			get => m_endCap;
			set {
				m_endCap = value;
				SetVerticesDirty();
			}
		}

		/// <summary> 終点キャップのセグメント数（Round時のみ有効） </summary>
		public int EndCapSegments {
			get => m_endCapSegments;
			set {
				m_endCapSegments = Mathf.Max(1, value);
				SetVerticesDirty();
			}
		}

		/// <summary> 描画に使用するスプライト </summary>
		public Sprite Sprite {
			get => m_sprite;
			set {
				if(m_sprite == value) return;
				m_sprite = value;
				SetVerticesDirty();
				SetMaterialDirty();
			}
		}
		#endregion

		public override Texture mainTexture => m_sprite != null ? m_sprite.texture : base.mainTexture;

		private Vector2 TransformUV(Vector2 uv) {
			if(m_swapUvAxes) uv = new Vector2(uv.y, uv.x);
			if(m_sprite == null) return uv;
			var outer = DataUtility.GetOuterUV(m_sprite);
			var size = new Vector2(outer.z - outer.x, outer.w - outer.y);
			return new Vector2(outer.x + uv.x * size.x, outer.y + uv.y * size.y);
		}

		#region Unity Overrides
		#if UNITY_EDITOR
		protected override void OnValidate() {
			base.OnValidate();
			SetVerticesDirty();
		}
		#endif

		protected override void OnPopulateMesh(VertexHelper vh) {
			vh.Clear();

			if(m_points == null || m_points.Count < 2 || color.a <= 0f || m_thickness <= 0f) return;

			var localPoints = ConvertToLocalPoints(m_points);
			var drawablePoints = BuildDrawablePoints(localPoints);
			if(drawablePoints.Count < 2) return;

			BuildStripMesh(vh, drawablePoints);
		}
		#endregion

		#region Private Methods
		private CornerType EffectiveCornerType => m_enableCornerInterpolation ? CornerType.Default : m_cornerType;

		private void BuildStripMesh(VertexHelper vh, IReadOnlyList<Vector2> points) {
			var baseCount = points.Count;
			var isLoop = m_loop && baseCount > 2;

			var lengths = new float[baseCount];
			for (var i = 1; i < baseCount; i++) {
				lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
			}

			var closingLength = isLoop ? Vector2.Distance(points[baseCount - 1], points[0]) : 0f;
			var totalLength = lengths[baseCount - 1] + closingLength;
			if(totalLength <= Mathf.Epsilon) return;

			var cornerType = EffectiveCornerType;
			if(cornerType != CornerType.Default) {
				var normalsForCaps = CalculateNormals(points, isLoop);
				BuildSegmentedStrip(vh, points, lengths, totalLength, isLoop);
				AddCornerMeshes(vh, points, lengths, totalLength, isLoop, cornerType);
				if(!isLoop) {
					AddCaps(vh, points, normalsForCaps, lengths, totalLength);
				}
				return;
			}

			var normals = CalculateNormals(points, isLoop);

			// メインストリップ
			for (var i = 0; i < baseCount; i++) {
				AddStripVertices(vh, points[i], normals[i], lengths[i], totalLength);
			}

			if(isLoop) {
				AddStripVertices(vh, points[0], normals[0], totalLength, totalLength);
			}

			var vertPairCount = baseCount + (isLoop ? 1 : 0);
			for (var i = 0; i < vertPairCount - 1; i++) {
				var baseIndex = i * 2;
				vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 3);
				vh.AddTriangle(baseIndex, baseIndex + 3, baseIndex + 2);
			}


			if(!isLoop) {
				AddCaps(vh, points, normals, lengths, totalLength);
			}
		}

		private void BuildSegmentedStrip(VertexHelper vh, IReadOnlyList<Vector2> points, IReadOnlyList<float> lengths, float totalLength, bool isLoop) {
			var count = points.Count;
			if(count < 2) return;

			for (var i = 0; i < count - 1; i++) {
				var trimStart = isLoop || i > 0;
				var trimEnd = isLoop || i < count - 2;
				AddSegmentStrip(vh, points[i], points[i + 1], lengths[i], lengths[i + 1], totalLength, trimStart, trimEnd);
			}

			if(isLoop) {
				AddSegmentStrip(vh, points[count - 1], points[0], lengths[count - 1], totalLength, totalLength, true, true);
			}
		}

		private void AddSegmentStrip(VertexHelper vh, Vector2 start, Vector2 end, float startLength, float endLength, float totalLength, bool trimStart, bool trimEnd) {
			var dir = end - start;
			if(dir.sqrMagnitude <= Mathf.Epsilon) return;

			dir.Normalize();
			var normal = new Vector2(-dir.y, dir.x);

			var startNormalized = totalLength <= Mathf.Epsilon ? 0f : Mathf.Clamp01(startLength / totalLength);
			var endNormalized = totalLength <= Mathf.Epsilon ? 0f : Mathf.Clamp01(endLength / totalLength);

			var startHalf = EvaluateThickness(startNormalized) * 0.5f;
			var endHalf = EvaluateThickness(endNormalized) * 0.5f;
			if(startHalf <= Mathf.Epsilon && endHalf <= Mathf.Epsilon) return;

			var uvBottom = m_uvOffset.y;
			var uvTop = (1f * m_uvTiling.y) + m_uvOffset.y;
			var uvXStart = (startNormalized * m_uvTiling.x) + m_uvOffset.x;
			var uvXEnd = (endNormalized * m_uvTiling.x) + m_uvOffset.x;


			var startCut = trimStart ? startHalf : 0f;
			var endCut = trimEnd ? endHalf : 0f;
			var segmentLength = Vector2.Distance(start, end);
			if(segmentLength <= startCut + endCut) return;

			var startPos = start + dir * startCut;
			var endPos = end - dir * endCut;

			var startOffset = normal * startHalf;
			var endOffset = normal * endHalf;

			var baseIndex = vh.currentVertCount;
			AddVertex(vh, startPos - startOffset, TransformUV(new Vector2(uvXStart, uvBottom)));
			AddVertex(vh, startPos + startOffset, TransformUV(new Vector2(uvXStart, uvTop)));
			AddVertex(vh, endPos - endOffset, TransformUV(new Vector2(uvXEnd, uvBottom)));
			AddVertex(vh, endPos + endOffset, TransformUV(new Vector2(uvXEnd, uvTop)));

			vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 3);
			vh.AddTriangle(baseIndex, baseIndex + 3, baseIndex + 2);
		}

		private void AddCornerMeshes(VertexHelper vh, IReadOnlyList<Vector2> points, IReadOnlyList<float> lengths, float totalLength, bool isLoop, CornerType cornerType) {
			var count = points.Count;
			if(count < 2) return;

			for (var i = 0; i < count; i++) {
				if(!isLoop && (i == 0 || i == count - 1)) continue;

				var prev = points[(i - 1 + count) % count];
				var current = points[i];
				var next = points[(i + 1) % count];

				var dirPrev = current - prev;
				var dirNext = next - current;

				if(dirPrev.sqrMagnitude <= Mathf.Epsilon || dirNext.sqrMagnitude <= Mathf.Epsilon) continue;

				dirPrev.Normalize();
				dirNext.Normalize();

				var cross = (dirPrev.x * dirNext.y) - (dirPrev.y * dirNext.x);
				var leftTurn = cross > 0f;

				var normalPrev = new Vector2(-dirPrev.y, dirPrev.x);
				var normalNext = new Vector2(-dirNext.y, dirNext.x);
				// 外側（凸側）の法線を使用する。左回り（cross>0）の場合は右側の法線、右回りの場合は左側の法線を選ぶ。
				var fromNormal = leftTurn ? -normalPrev : normalPrev;
				var toNormal = leftTurn ? -normalNext : normalNext;

 			var lengthAt = lengths[Mathf.Clamp(i, 0, lengths.Count - 1)];
 			var normalized = totalLength <= Mathf.Epsilon ? 0f : Mathf.Clamp01(lengthAt / totalLength);
				var halfThickness = EvaluateThickness(normalized) * 0.5f;
				if(halfThickness <= Mathf.Epsilon) continue;

 			var uvX = (normalized * m_uvTiling.x) + m_uvOffset.x;
 			var uvBottom = m_uvOffset.y;
 			var uvTop = (1f * m_uvTiling.y) + m_uvOffset.y;

				switch(cornerType) {
					case CornerType.Bevel:
						AddBevelCornerMesh(vh, current, fromNormal, toNormal, halfThickness, uvX, uvBottom, uvTop, leftTurn);
						break;
					case CornerType.Round:
						AddRoundCornerMesh(vh, current, fromNormal, toNormal, halfThickness, uvX, uvBottom, uvTop, leftTurn);
						break;
				}
			}
		}

		private void AddBevelCornerMesh(VertexHelper vh, Vector2 center, Vector2 fromNormal, Vector2 toNormal, float halfThickness, float uvX, float uvBottom, float uvTop, bool leftTurn) {
			// セグメントの進行方向を復元（法線の外向き符号を考慮）
			var baseFromDir = new Vector2(fromNormal.y, -fromNormal.x);
			var baseToDir = new Vector2(toNormal.y, -toNormal.x);
			var dirPrev = leftTurn ? -baseFromDir : baseFromDir;
			var dirNext = leftTurn ? -baseToDir : baseToDir;

			// トリム済みストリップ端点（外側／内側）を算出
			var outerPrev = center - dirPrev * halfThickness + fromNormal * halfThickness;
			var innerPrev = center - dirPrev * halfThickness - fromNormal * halfThickness;
			var outerNext = center + dirNext * halfThickness + toNormal * halfThickness;
			var innerNext = center + dirNext * halfThickness - toNormal * halfThickness;

			var baseIndex = vh.currentVertCount;
			AddVertex(vh, outerPrev, TransformUV(new Vector2(uvX, uvTop)));
			AddVertex(vh, outerNext, TransformUV(new Vector2(uvX, uvTop)));
			AddVertex(vh, innerPrev, TransformUV(new Vector2(uvX, uvBottom)));
			AddVertex(vh, innerNext, TransformUV(new Vector2(uvX, uvBottom)));

			if(leftTurn) {
				vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 3);
				vh.AddTriangle(baseIndex, baseIndex + 3, baseIndex + 2);
			} else {
				vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
				vh.AddTriangle(baseIndex + 1, baseIndex + 3, baseIndex + 2);
			}
		}

		private void AddRoundCornerMesh(VertexHelper vh, Vector2 center, Vector2 fromNormal, Vector2 toNormal, float halfThickness, float uvX, float uvBottom, float uvTop, bool leftTurn) {
			var segments = Mathf.Max(1, m_cornerVertices);
			var baseIndex = vh.currentVertCount;

			// from/to の法線から元のセグメント方向を復元（法線の外向き符号を考慮）
			var baseFromDir = new Vector2(fromNormal.y, -fromNormal.x);
			var baseToDir = new Vector2(toNormal.y, -toNormal.x);
			var dirPrev = leftTurn ? -baseFromDir : baseFromDir;
			var dirNext = leftTurn ? -baseToDir : baseToDir;

			// トリム後の外側／内側端点（メインストリップ端に対応）
			var outerStart = center - dirPrev * halfThickness + fromNormal * halfThickness;
			var outerEnd = center + dirNext * halfThickness + toNormal * halfThickness;
			var innerStart = center - dirPrev * halfThickness - fromNormal * halfThickness;
			var innerEnd = center + dirNext * halfThickness - toNormal * halfThickness;

			// 内外オフセットラインの交点を制御点として使用（平行時は二等分線で代替）
			bool TryIntersect(Vector2 p, Vector2 dir, Vector2 q, Vector2 dirQ, out Vector2 hit) {
				hit = Vector2.zero;
				var d = (dir.x * dirQ.y) - (dir.y * dirQ.x);
				if(Mathf.Abs(d) < 1e-5f) return false;
				var diff = q - p;
				var t = (diff.x * dirQ.y - diff.y * dirQ.x) / d;
				hit = p + dir * t;
				return true;
			}

			Vector2 outerCtrl;
			if(!TryIntersect(outerStart, dirPrev, outerEnd, dirNext, out outerCtrl)) {
				var bisector = (fromNormal + toNormal).normalized;
				outerCtrl = center + bisector * halfThickness;
			}

			Vector2 innerCtrl;
			if(!TryIntersect(innerStart, dirPrev, innerEnd, dirNext, out innerCtrl)) {
				var bisector = (fromNormal + toNormal).normalized;
				innerCtrl = center - bisector * halfThickness;
			}

			// スプライン（ここでは二次ベジェ）で内外それぞれを接続し、同一tでストリップ化
			for (var i = 0; i <= segments; i++) {
				var t = (float)i / segments;
				var innerPos = QuadraticBezier(innerStart, innerCtrl, innerEnd, t);
				var outerPos = QuadraticBezier(outerStart, outerCtrl, outerEnd, t);

				AddVertex(vh, innerPos, TransformUV(new Vector2(uvX, uvBottom)));
				AddVertex(vh, outerPos, TransformUV(new Vector2(uvX, uvTop)));
			}

			for (var i = 0; i < segments; i++) {
				var idxInner0 = baseIndex + i * 2;
				var idxOuter0 = idxInner0 + 1;
				var idxInner1 = idxInner0 + 2;
				var idxOuter1 = idxInner0 + 3;
				if(leftTurn) {
					vh.AddTriangle(idxInner0, idxOuter0, idxOuter1);
					vh.AddTriangle(idxInner0, idxOuter1, idxInner1);
				} else {
					vh.AddTriangle(idxInner0, idxOuter1, idxOuter0);
					vh.AddTriangle(idxInner0, idxInner1, idxOuter1);
				}
			}
		}

		private void AddCaps(VertexHelper vh, IReadOnlyList<Vector2> points, IReadOnlyList<Vector2> normals, IReadOnlyList<float> lengths, float totalLength) {
			// 始点キャップ
			if(m_startCap != CapType.Default) {
				var normal = normals[0];
				var point = points[0];
				var dir = (points[1] - points[0]).normalized;
				AddCapMesh(vh, point, dir, normal, 0f, totalLength, m_startCap, m_startCapSegments, true);
			}

			// 終点キャップ
			if(m_endCap != CapType.Default) {
				var count = points.Count;
				var normal = normals[count - 1];
				var point = points[count - 1];
				var dir = (points[count - 1] - points[count - 2]).normalized;
				AddCapMesh(vh, point, dir, normal, lengths[count - 1], totalLength, m_endCap, m_endCapSegments, false);
			}
		}

		private void AddCapMesh(VertexHelper vh, Vector2 point, Vector2 dir, Vector2 normal, float length, float totalLength, CapType cap, int segments, bool isStart) {
			if(cap == CapType.Default) return;
			segments = Mathf.Max(1, segments);
			var normalized = totalLength <= Mathf.Epsilon ? 0f : Mathf.Clamp01(length / totalLength);
			var halfThickness = EvaluateThickness(normalized) * 0.5f;
			var uvX = (normalized * m_uvTiling.x) + m_uvOffset.x;
			var uvBottom = m_uvOffset.y;
			var uvTop = (1f * m_uvTiling.y) + m_uvOffset.y;

			if(cap == CapType.Square) {
				var offset = normal * halfThickness;
				var advance = dir * halfThickness * (isStart ? -1f : 1f);
				var basePos = point + advance;
				var p0 = basePos - offset;
				var p1 = basePos + offset;
				var p2 = point + offset;
				var p3 = point - offset;

				var baseIndex = vh.currentVertCount;
				AddVertex(vh, p0, TransformUV(new Vector2(uvX, uvBottom)));
				AddVertex(vh, p1, TransformUV(new Vector2(uvX, uvTop)));
				AddVertex(vh, p2, TransformUV(new Vector2(uvX, uvTop)));
				AddVertex(vh, p3, TransformUV(new Vector2(uvX, uvBottom)));

				vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
				vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
				return;
			}

			if(cap == CapType.Round) {
				var baseIndex = vh.currentVertCount;
				var center = point;                // 端点を中心に半円を描く
				var capDir = isStart ? -dir : dir; // 始点は進行方向の逆側へ半円を向ける
				var baseAngle = Mathf.Atan2(capDir.y, capDir.x);
				var startAngle = baseAngle - Mathf.PI * 0.5f;
				var endAngle = baseAngle + Mathf.PI * 0.5f;
				var uvCenterY = Mathf.Lerp(uvBottom, uvTop, 0.5f);
				var uvOuterY = uvTop;

				AddVertex(vh, center, TransformUV(new Vector2(uvX, uvCenterY)));
				for (var i = 0; i <= segments; i++) {
					var t = (float)i / segments;
					var angle = Mathf.Lerp(startAngle, endAngle, t);
					var dirCircle = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
					var pos = center + dirCircle * halfThickness;
					var radial = Mathf.Clamp01((pos - center).magnitude / halfThickness);
					var uvY = Mathf.Lerp(uvCenterY, uvOuterY, radial);
					AddVertex(vh, pos, TransformUV(new Vector2(uvX, uvY)));
				}

				for (var i = 0; i < segments; i++) {
					if(isStart) vh.AddTriangle(baseIndex, baseIndex + i + 2, baseIndex + i + 1);
					else vh.AddTriangle(baseIndex, baseIndex + i + 1, baseIndex + i + 2);
				}
			}
		}

		private void AddStripVertices(VertexHelper vh, Vector2 point, Vector2 normal, float length, float totalLength) {
			var normalized = totalLength <= Mathf.Epsilon ? 0f : Mathf.Clamp01(length / totalLength);
			var halfThickness = EvaluateThickness(normalized) * 0.5f;
			var offset = normal * halfThickness;

			var uvX = (normalized * m_uvTiling.x) + m_uvOffset.x;
			var uvBottom = m_uvOffset.y;
			var uvTop = (1f * m_uvTiling.y) + m_uvOffset.y;

			AddVertex(vh, point - offset, TransformUV(new Vector2(uvX, uvBottom)));
			AddVertex(vh, point + offset, TransformUV(new Vector2(uvX, uvTop)));
		}

		private void AddVertex(VertexHelper vh, Vector2 point, Vector2 uv) {
			var pos = new Vector3(point.x, point.y, 0f);
			vh.AddVert(pos, color, uv);
		}

		private float EvaluateThickness(float normalizedLength) {
			var curveValue = m_thicknessCurve != null ? m_thicknessCurve.Evaluate(Mathf.Clamp01(normalizedLength)) : 1f;
			var scale = Mathf.Max(0f, curveValue);
			return m_thickness * scale;
		}

		private Vector2 ToLocalPoint(Vector2 point) {
			if(m_space == aUiLineRendererSpace.Local) return point;

			var worldPoint = new Vector3(point.x, point.y, 0f);
			return rectTransform.InverseTransformPoint(worldPoint);
		}

		private List<Vector2> ConvertToLocalPoints(IReadOnlyList<Vector2> source) {
			var result = new List<Vector2>(source.Count);
			for (var i = 0; i < source.Count; i++) {
				result.Add(ToLocalPoint(source[i]));
			}
			return result;
		}

		private List<Vector2> BuildDrawablePoints(IReadOnlyList<Vector2> localPoints) {
			if(localPoints.Count < 2) return new List<Vector2>(localPoints);

			// CornerType が指定されている場合はポイント列を加工せず、そのまま角メッシュで処理する
			if(EffectiveCornerType != CornerType.Default) return new List<Vector2>(localPoints);

			// 補間が無効の場合はそのまま返す
			if(!m_enableCornerInterpolation || m_cornerVertices <= 0) return new List<Vector2>(localPoints);

			var result = new List<Vector2>();
			var count = localPoints.Count;

			if(!m_loop) {
				result.Add(localPoints[0]);
				for (var i = 1; i < count - 1; i++) {
					AppendRoundedCorner(result, localPoints[i - 1], localPoints[i], localPoints[i + 1]);
				}
				result.Add(localPoints[count - 1]);
			} else {
				for (var i = 0; i < count; i++) {
					var prev = localPoints[(i - 1 + count) % count];
					var current = localPoints[i];
					var next = localPoints[(i + 1) % count];
					AppendRoundedCorner(result, prev, current, next, i == 0);
				}
			}

			return result;
		}

		private void AppendRoundedCorner(List<Vector2> dst, Vector2 prev, Vector2 current, Vector2 next, bool addStart = false) {
			var dirPrev = current - prev;
			var dirNext = next - current;

			var lenPrev = dirPrev.magnitude;
			var lenNext = dirNext.magnitude;

			if(lenPrev <= Mathf.Epsilon || lenNext <= Mathf.Epsilon) {
				if(addStart) dst.Add(current);
				dst.Add(current);
				return;
			}

			dirPrev /= lenPrev;
			dirNext /= lenNext;

			var cut = Mathf.Min(lenPrev, lenNext) * 0.5f;
			var start = current - dirPrev * cut;
			var end = current + dirNext * cut;

			if(addStart) dst.Add(start);
			else dst.Add(start);

			for (var i = 1; i <= m_cornerVertices; i++) {
				var t = (float)i / (m_cornerVertices + 1);
				dst.Add(QuadraticBezier(start, current, end, t));
			}

			dst.Add(end);
		}

		private Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t) {
			var u = 1f - t;
			return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
		}

		private List<Vector2> CalculateNormals(IReadOnlyList<Vector2> points, bool isLoop) {
			var count = points.Count;
			var normals = new List<Vector2>(count);

			// CornerType が指定されている場合は、各頂点をセグメント法線でオフセットし、角メッシュ側で処理する
			if(EffectiveCornerType != CornerType.Default) {
				for (var i = 0; i < count; i++) {
					var current = points[i];
					Vector2 dir;

					if(!isLoop && i == count - 1) {
						// 非ループ終端は前方セグメントの法線を使用
						dir = current - points[Mathf.Max(0, i - 1)];
					} else {
						var next = points[(i + 1) % count];
						dir = next - current;
					}

					if(dir.sqrMagnitude <= Mathf.Epsilon) {
						normals.Add(Vector2.zero);
						continue;
					}

					dir.Normalize();
					normals.Add(new Vector2(-dir.y, dir.x));
				}

				return normals;
			}

			for (var i = 0; i < count; i++) {
				var prev = isLoop ? points[(i - 1 + count) % count] : points[Mathf.Max(0, i - 1)];
				var current = points[i];
				var next = isLoop ? points[(i + 1) % count] : points[Mathf.Min(count - 1, i + 1)];

				var dirPrev = (current - prev);
				var dirNext = (next - current);

				if(dirPrev.sqrMagnitude <= Mathf.Epsilon) dirPrev = dirNext;
				if(dirNext.sqrMagnitude <= Mathf.Epsilon) dirNext = dirPrev;

				dirPrev.Normalize();
				dirNext.Normalize();

				var normalPrev = new Vector2(-dirPrev.y, dirPrev.x);
				var normalNext = new Vector2(-dirNext.y, dirNext.x);
				var blended = normalPrev + normalNext;
				if(blended.sqrMagnitude <= Mathf.Epsilon) blended = normalPrev;

				blended.Normalize();

				var denom = Vector2.Dot(blended, normalPrev);

				Vector2 finalNormal;
				if(Mathf.Abs(denom) < 0.001f) {
					finalNormal = normalPrev;
				} else {
					finalNormal = blended / denom;
				}

				var maxScale = 4f;
				if(finalNormal.magnitude > maxScale) finalNormal = finalNormal.normalized * maxScale;

				normals.Add(finalNormal);
			}

			return normals;
		}
		#endregion
	}
}
