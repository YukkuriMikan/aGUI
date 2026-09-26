using TMPro;
using UnityEngine;

namespace ANest.UI {
	/// <summary>本文・ルビ・装飾の合成メッシュを高さを変えずに横幅へ収める。</summary>
	internal static class aTextWidthLayout {
		internal static void Apply(TMP_TextInfo info, Rect rect, Vector4 margin,
			HorizontalAlignmentOptions alignment, out float scale, out float offset) {
			scale = 1;
			offset = 0;
			if(info == null || info.characterCount == 0) return;
			float min = float.PositiveInfinity, max = float.NegativeInfinity;
			// 非表示文字も含む本文幅を使い、文字送り中の縮小率の変化を抑える。
			for(int i = 0; i < info.characterCount; i++) {
				var c = info.characterInfo[i];
				if(char.IsWhiteSpace(c.character) || c.character == '\u200B' || c.character == '\u0003') continue;
				min = Mathf.Min(min, Mathf.Min(c.bottomLeft.x, c.topLeft.x));
				max = Mathf.Max(max, Mathf.Max(c.bottomRight.x, c.topRight.x));
			}
			// 全マテリアルを対象に、ルビ・スプライト・下線等も幅の判定へ含める。
			for(int m = 0; m < info.materialCount; m++) {
				var mesh = info.meshInfo[m];
				for(int v = 0; v + 3 < mesh.vertexCount; v += 4) {
					if(IsEmptyQuad(mesh.vertices, v)) continue;
					for(int c = 0; c < 4; c++) {
						min = Mathf.Min(min, mesh.vertices[v + c].x);
						max = Mathf.Max(max, mesh.vertices[v + c].x);
					}
				}
			}
			if(float.IsInfinity(min) || max <= min) return;
			float left = rect.xMin + margin.x;
			float right = Mathf.Max(left, rect.xMax - margin.z);
			if(min >= left && max <= right) return;
			scale = Mathf.Min(1, (right - left) / (max - min));
			float pivot = alignment == HorizontalAlignmentOptions.Right ? right
				: alignment == HorizontalAlignmentOptions.Center || alignment == HorizontalAlignmentOptions.Geometry ? (left + right) * .5f : left;
			offset = pivot * (1 - scale);
			// ルビや斜体の張り出しも含めて枠内へ平行移動する。
			offset = Mathf.Clamp(offset, left - min * scale, right - max * scale);
			for(int m = 0; m < info.materialCount; m++) {
				var mesh = info.meshInfo[m];
				for(int v = 0; v + 3 < mesh.vertexCount; v += 4) {
					if(IsEmptyQuad(mesh.vertices, v)) continue;
					for(int c = 0; c < 4; c++) mesh.vertices[v + c].x = mesh.vertices[v + c].x * scale + offset;
				}
			}
			// TMPのリンク・文字のヒットテストも、描画と同じ座標へ変換する。
			for(int i = 0; i < info.characterCount; i++) {
				ref var c = ref info.characterInfo[i];
				c.bottomLeft.x = c.bottomLeft.x * scale + offset;
				c.topLeft.x = c.topLeft.x * scale + offset;
				c.topRight.x = c.topRight.x * scale + offset;
				c.bottomRight.x = c.bottomRight.x * scale + offset;
				c.vertex_BL.position.x = c.vertex_BL.position.x * scale + offset;
				c.vertex_TL.position.x = c.vertex_TL.position.x * scale + offset;
				c.vertex_TR.position.x = c.vertex_TR.position.x * scale + offset;
				c.vertex_BR.position.x = c.vertex_BR.position.x * scale + offset;
				c.origin = c.origin * scale + offset;
				c.xAdvance = c.xAdvance * scale + offset;
				c.aspectRatio *= scale;
			}
			for(int i = 0; i < info.lineCount; i++) {
				ref var line = ref info.lineInfo[i];
				line.lineExtents.min.x = line.lineExtents.min.x * scale + offset;
				line.lineExtents.max.x = line.lineExtents.max.x * scale + offset;
				line.length *= scale;
				line.maxAdvance *= scale;
			}
		}

		private static bool IsEmptyQuad(Vector3[] vertices, int start) => vertices[start] == vertices[start + 1]
			&& vertices[start] == vertices[start + 2] && vertices[start] == vertices[start + 3];
	}
}
