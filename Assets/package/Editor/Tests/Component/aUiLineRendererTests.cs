using System.Collections.Generic;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>aUiLineRenderer のメッシュ生成を検証するテスト</summary>
public class aUiLineRendererTests {
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
