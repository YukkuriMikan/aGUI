using System.Collections.Generic;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>aUiLineRenderer のメッシュ生成を検証するテスト</summary>
public class aUiLineRendererTests {
	[TestCase(CornerType.Default, false, false)]
	[TestCase(CornerType.Default, true, false)]
	[TestCase(CornerType.Round, false, false)]
	[TestCase(CornerType.Bevel, false, false)]
	[TestCase(CornerType.Default, false, true)]
	[TestCase(CornerType.Default, true, true)]
	[TestCase(CornerType.Round, false, true)]
	[TestCase(CornerType.Bevel, false, true)]
	public void SharpAndShortCornersKeepConsistentTriangleFacing(CornerType corner, bool interpolate, bool loop) {
		var root = new GameObject("Line corners", typeof(RectTransform), typeof(CanvasRenderer));
		try {
			var line = root.AddComponent<aUiLineRenderer>();
			line.Thickness = 20;
			line.CornerMeshType = corner;
			line.EnableCornerInterpolation = interpolate;
			line.Loop = loop;
			foreach(var length in new[] { 5f, 15f, 100f }) {
				foreach(var angle in new[] { -179f, -150f, -90f, -45f, 0f, 45f, 90f, 150f, 179f, 180f }) {
					line.ClearPoints();
					line.AddPoints(new[] { new Vector2(-length, 0), Vector2.zero,
						new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * length });
					AssertClockwise(BuildMesh(line), $"{corner}, interpolation={interpolate}, loop={loop}, length={length}, angle={angle}");
				}
			}
		} finally { Object.DestroyImmediate(root); }
	}

	[TestCase(CapType.Round)]
	[TestCase(CapType.Square)]
	public void BothCapsFaceTheSameDirectionAsTheLine(CapType cap) {
		var root = new GameObject("Line caps", typeof(RectTransform), typeof(CanvasRenderer));
		try {
			var line = root.AddComponent<aUiLineRenderer>();
			line.Thickness = 10;
			line.StartCap = line.EndCap = cap;
			line.AddPoints(new[] { Vector2.zero, new Vector2(100, 0) });
			AssertClockwise(BuildMesh(line), cap.ToString());
		} finally { Object.DestroyImmediate(root); }
	}

	[TestCase(CornerType.Round)]
	[TestCase(CornerType.Bevel)]
	public void ShortSegmentsRetainFullWidthNearOpenEndpoints(CornerType corner) {
		var root = new GameObject("Short thick line", typeof(RectTransform), typeof(CanvasRenderer));
		try {
			var line = root.AddComponent<aUiLineRenderer>();
			line.EnableCornerInterpolation = false;
			line.CornerMeshType = corner;
			line.Thickness = 20;
			line.AddPoints(new[] { Vector2.zero, new Vector2(5, 0), new Vector2(5, 5) });
			var mesh = BuildMesh(line);
			// 角の丸め区間外にある始点・終点付近は、線幅の両側まで描画される。
			foreach(var sample in new[] { new Vector2(0.5f, -9), new Vector2(0.5f, 9), new Vector2(-4, 4.5f), new Vector2(14, 4.5f) }) {
				Assert.That(Covers(mesh, sample), Is.True, $"Missing stroke near endpoint: {sample}");
			}
		} finally { Object.DestroyImmediate(root); }
	}

	[TestCase(CornerType.Round)]
	[TestCase(CornerType.Bevel)]
	public void ReversingDirectionDoesNotCutOffTheTurningPoint(CornerType corner) {
		var root = new GameObject("Line reversal", typeof(RectTransform), typeof(CanvasRenderer));
		try {
			var line = root.AddComponent<aUiLineRenderer>();
			line.EnableCornerInterpolation = false;
			line.CornerMeshType = corner;
			line.Thickness = 20;
			line.AddPoints(new[] { Vector2.zero, new Vector2(5, 0), Vector2.zero });
			var mesh = BuildMesh(line);
			Assert.That(Covers(mesh, new Vector2(4.5f, 9)), Is.True);
			Assert.That(Covers(mesh, new Vector2(4.5f, -9)), Is.True);
			AssertClockwise(mesh, "Reversal");
		} finally { Object.DestroyImmediate(root); }
	}

	private static List<UIVertex> BuildMesh(aUiLineRenderer line) {
		using var vh = new VertexHelper();
		typeof(aUiLineRenderer).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
			null, new[] { typeof(VertexHelper) }, null).Invoke(line, new object[] { vh });
		var vertices = new List<UIVertex>();
		vh.GetUIVertexStream(vertices);
		return vertices;
	}

	private static void AssertClockwise(List<UIVertex> vertices, string context) {
		Assert.That(vertices.Count, Is.GreaterThan(0), context);
		for(var i = 0; i < vertices.Count; i += 3) {
			var area = Cross(vertices[i].position, vertices[i + 1].position, vertices[i + 2].position);
			Assert.That(float.IsNaN(area) || float.IsInfinity(area), Is.False, context);
			Assert.That(area, Is.LessThanOrEqualTo(0.001f), $"Back-facing triangle {i / 3}: {context}");
		}
	}

	private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

	private static bool Covers(List<UIVertex> vertices, Vector2 point) {
		for(var i = 0; i < vertices.Count; i += 3) {
			Vector2 a = vertices[i].position, b = vertices[i + 1].position, c = vertices[i + 2].position;
			if(Mathf.Abs(Cross(a, b, c)) < 0.0001f) continue;
			var ab = Cross(a, b, point); var bc = Cross(b, c, point); var ca = Cross(c, a, point);
			if((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
		}
		return false;
	}

	/// <summary>隣接する丸め区間が同じ中点で接する場合も法線が反転しないことを確認する</summary>
	[Test]
	public void RoundedInterpolationDoesNotFlipAtNearlyDuplicateMidpoint() {
		var gameObject = new GameObject("aUiLineRenderer test", typeof(RectTransform), typeof(CanvasRenderer));
		var line = gameObject.AddComponent<aUiLineRenderer>();

		try {
			line.Thickness = 10f;
			line.EnableCornerInterpolation = true;
			line.CornerVertices = 8;
			line.AddPoints(new[] {
				new Vector2(-422.36734f, -459.199768f),
				new Vector2(-276.887634f, -444.2287f),
				new Vector2(-131.553284f, -316.35376f),
				new Vector2(7.4782486f, -180.5565f),
				new Vector2(144.784332f, -43.3079872f),
				new Vector2(283.815857f, 92.48933f),
				new Vector2(429.15033f, 220.364334f),
				new Vector2(574.63f, 235.335327f)
			});

			using var vertexHelper = new VertexHelper();
			var populateMesh = typeof(aUiLineRenderer).GetMethod(
				"OnPopulateMesh",
				BindingFlags.Instance | BindingFlags.NonPublic,
				null,
				new[] {typeof(VertexHelper)},
				null
			);
			Assert.NotNull(populateMesh);
			populateMesh.Invoke(line, new object[] {vertexHelper});
			var vertices = new List<UIVertex>();
			vertexHelper.GetUIVertexStream(vertices);

			Assert.Greater(vertices.Count, 0);
			Assert.AreEqual(0, vertices.Count % 6, "Each strip section must contain two triangles.");

			Vector2? previousWidthDirection = null;
			for (var triangle = 0; triangle < vertices.Count; triangle += 6) {
				// AddStripVertices が追加した始点側の二頂点。GetUIVertexStream では
				// 最初の三角形の先頭二頂点として取得できる。
				var widthDirection = ((Vector2)vertices[triangle + 1].position -
				                      (Vector2)vertices[triangle].position).normalized;

				if(previousWidthDirection.HasValue) {
					Assert.Greater(
						Vector2.Dot(previousWidthDirection.Value, widthDirection),
						0f,
						$"Strip width direction flipped at triangle pair {triangle / 6}."
					);
				}

				previousWidthDirection = widthDirection;
			}
		} finally {
			Object.DestroyImmediate(gameObject);
		}
	}
}
