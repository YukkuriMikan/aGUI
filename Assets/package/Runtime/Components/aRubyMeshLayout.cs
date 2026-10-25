using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace ANest.UI {
	/// <summary>TMP自身が生成したルビの頂点を本文へ合成する。フォントの字形・UV・マテリアル生成はTMPに任せる。</summary>
	internal sealed class aRubyMeshLayout {
		private const float ReadingPointSize = 32f;
		internal sealed class Run {
			internal string Text;
			internal int First, Last;
			internal float FontSize;
			internal Bounds Bounds;
		}
		private struct LineBounds {
			internal float BodyTop, BodyBottom, Top, Bottom, Offset;
			internal int Page;
		}
		private sealed class Vertices {
			internal Vector3[] Positions = Array.Empty<Vector3>();
			internal int[] Lines = Array.Empty<int>();
			internal Vector4[] UV = Array.Empty<Vector4>();
			internal Vector2[] UV2 = Array.Empty<Vector2>();
			internal Color32[] Colors = Array.Empty<Color32>();
			internal int Count;
			internal void Ensure(int count) {
				if(Positions.Length >= count) return;
				int capacity = Mathf.NextPowerOfTwo(Mathf.Max(count, 16));
				Array.Resize(ref Positions, capacity);
				Array.Resize(ref Lines, capacity / 4);
				Array.Resize(ref UV, capacity);
				Array.Resize(ref UV2, capacity);
				Array.Resize(ref Colors, capacity);
			}
			internal void CopyFrom(TMP_MeshInfo mesh) {
				Count = mesh.vertexCount;
				Ensure(Count);
				Array.Copy(mesh.vertices, Positions, Count);
				Array.Copy(mesh.uvs0, UV, Count);
				Array.Copy(mesh.uvs2, UV2, Count);
				Array.Copy(mesh.colors32, Colors, Count);
				Array.Fill(Lines, -1, 0, Count / 4);
			}
			internal void CopyTo(ref TMP_MeshInfo mesh, int offset, LineBounds[] lines) {
				Array.Copy(Positions, 0, mesh.vertices, offset, Count);
				Array.Copy(UV, 0, mesh.uvs0, offset, Count);
				Array.Copy(UV2, 0, mesh.uvs2, offset, Count);
				Array.Copy(Colors, 0, mesh.colors32, offset, Count);
				if(lines == null) return;
				for(int i = 0; i < Count; i += 4) {
					int line = Lines[i / 4];
					if(line < 0) continue;
					for(int corner = 0; corner < 4; corner++) mesh.vertices[offset + i + corner].y += lines[line].Offset;
				}
			}
		}

		private readonly TMP_TextInfo m_body = new();
		private readonly List<Vertices> m_bodyVertices = new();
		private readonly List<Vertices> m_readingVertices = new();
		private readonly List<Run> m_runs = new();
		private readonly StringBuilder m_buffer = new();
		private LineBounds[] m_lines = Array.Empty<LineBounds>();
		private float[] m_pageSpacing = Array.Empty<float>();
		internal int Count { get; private set; }
		internal string Output { get; private set; }
		internal float BodyFontSize { get; private set; }
		internal void Clear() => Count = 0;

		internal void Capture(TMP_TextInfo info, List<string> readings, string body, float fontSize) {
			CopyTextInfo(info, m_body);
			m_body.materialCount = info.materialCount;
			BodyFontSize = fontSize;
			for(var i = 0; i < info.materialCount; i++) {
				if(i == m_bodyVertices.Count) m_bodyVertices.Add(new Vertices());
				m_bodyVertices[i].CopyFrom(info.meshInfo[i]);
			}
			CaptureVertexLines(info);
			Count = 0;
			m_buffer.Clear().Append(body);
			for(var i = 0; i < info.linkCount; i++) {
				var link = info.linkInfo[i];
				int first = link.linkTextfirstCharacterIndex, last = first + link.linkTextLength - 1;
				if(string.IsNullOrEmpty(readings[i]) || link.linkTextLength <= 0 || first < 0 || last >= info.characterCount) continue;
				if(Count == m_runs.Count) m_runs.Add(new Run());
				var run = m_runs[Count++];
				run.Text = readings[i];
				run.First = first;
				run.Last = last;
				run.Bounds = default;
				// 追記部分は描画専用。本文のレイアウト・公開textInfoには含めない。
				m_buffer.Append("\n<size=32><voffset=0><align=left><link=\"aGUI-reading\">")
					.Append(run.Text).Append("</link></align></voffset></size>");
			}
			Output = Count == 0 ? body : m_buffer.ToString();
		}

		internal void Apply(TMP_TextInfo info, RubySizeMode mode, float rubyScale, float rubySize, float offset, VerticalAlignmentOptions alignment) {
			for(var i = 0; i < info.materialCount; i++) {
				if(i == m_readingVertices.Count) m_readingVertices.Add(new Vertices());
				m_readingVertices[i].Count = 0;
			}
			for(var i = 0; i < Count; i++) {
				var run = m_runs[i];
				var bodyFirst = m_body.characterInfo[run.First];
				var bodyLast = m_body.characterInfo[run.Last];
				run.Bounds = default;
				float width = bodyLast.topRight.x - bodyFirst.topLeft.x;
				run.FontSize = Mathf.Max(0, mode == RubySizeMode.Auto ? width / run.Text.Length
					: mode == RubySizeMode.Size ? rubySize : BodyFontSize * rubyScale);
				if(!bodyFirst.isVisible || !bodyLast.isVisible || run.FontSize <= 0 || m_body.linkCount + i >= info.linkCount) continue;
				var link = info.linkInfo[m_body.linkCount + i];
				int start = link.linkTextfirstCharacterIndex, end = start + link.linkTextLength;
				if(start < 0 || start >= end || end > info.characterCount) continue;
				float ascent = float.NegativeInfinity, descent = float.PositiveInfinity;
				for(var j = start; j < end; j++) {
					ascent = Mathf.Max(ascent, info.characterInfo[j].ascender);
					descent = Mathf.Min(descent, info.characterInfo[j].descender);
				}
				var center = new Vector3((info.characterInfo[start].origin + info.characterInfo[end - 1].xAdvance) * .5f, (ascent + descent) * .5f);
				var target = new Vector3((bodyFirst.topLeft.x + bodyLast.topRight.x) * .5f, bodyFirst.topLeft.y + run.FontSize * .6f + offset);
				float scale = run.FontSize / ReadingPointSize;
				bool hasBounds = false;
				for(var j = start; j < end; j++) {
					var character = info.characterInfo[j];
					if(!character.isVisible) continue;
					var mesh = info.meshInfo[character.materialReferenceIndex];
					var vertices = m_readingVertices[character.materialReferenceIndex];
					vertices.Ensure(vertices.Count + 4);
					vertices.Lines[vertices.Count / 4] = bodyFirst.lineNumber;
					for(var corner = 0; corner < 4; corner++) {
						int source = character.vertexIndex + corner, destination = vertices.Count++;
						var point = (mesh.vertices[source] - center) * scale + target;
						vertices.Positions[destination] = point;
						vertices.UV[destination] = mesh.uvs0[source];
						// Unity 6 TMPのSDFスケール。頂点の拡縮に合わせないと文字の太さが変わる。
						vertices.UV[destination].w *= scale;
						vertices.UV2[destination] = mesh.uvs2[source];
						vertices.Colors[destination] = mesh.colors32[source];
						if(hasBounds) run.Bounds.Encapsulate(point);
						else { run.Bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
					}
				}
			}
			bool multiline = m_body.lineCount > 1;
			if(multiline) CalculateLineOffsets(alignment);
			for(var i = 0; i < info.materialCount; i++) {
				ref var mesh = ref info.meshInfo[i];
				var body = i < m_body.materialCount ? m_bodyVertices[i] : null;
				var reading = m_readingVertices[i];
				int bodyCount = body?.Count ?? 0, count = bodyCount + reading.Count;
				if(mesh.vertices.Length < count) mesh.ResizeMeshInfo((count + 3) / 4);
				body?.CopyTo(ref mesh, 0, multiline ? m_lines : null);
				reading.CopyTo(ref mesh, bodyCount, multiline ? m_lines : null);
				mesh.vertexCount = count;
				mesh.ClearUnusedVertices();
			}
			RestoreTextInfo(info);
			if(multiline) ApplyLineOffsets(info);
		}

		private void CaptureVertexLines(TMP_TextInfo info) {
			int visiblePage = 0;
			for(int i = 0; i < info.characterCount; i++) {
				var character = info.characterInfo[i];
				if(!character.isVisible) continue;
				visiblePage = character.pageNumber;
				m_bodyVertices[character.materialReferenceIndex].Lines[character.vertexIndex / 4] = character.lineNumber;
			}
			// 下線・打ち消し線・ハイライトなど、文字に直接対応しない四角形も同じ行へ移す。
			for(int material = 0; material < info.materialCount; material++) {
				var mesh = m_bodyVertices[material];
				for(int vertex = 0; vertex < mesh.Count; vertex += 4) {
					if(mesh.Lines[vertex / 4] >= 0) continue;
					float center = (mesh.Positions[vertex].y + mesh.Positions[vertex + 2].y) * .5f;
					float nearest = float.PositiveInfinity;
					for(int line = 0; line < info.lineCount; line++) {
						var bounds = info.lineInfo[line];
						int first = Mathf.Clamp(bounds.firstCharacterIndex, 0, info.characterCount - 1);
						if(info.characterInfo[first].pageNumber != visiblePage) continue;
						float distance = Mathf.Abs(center - (bounds.ascender + bounds.descender) * .5f);
						if(distance >= nearest) continue;
						nearest = distance;
						mesh.Lines[vertex / 4] = line;
					}
				}
			}
		}

		private void CalculateLineOffsets(VerticalAlignmentOptions alignment) {
			if(m_lines.Length < m_body.lineCount) Array.Resize(ref m_lines, Mathf.NextPowerOfTwo(m_body.lineCount));
			if(m_pageSpacing.Length < m_body.pageCount) Array.Resize(ref m_pageSpacing, Mathf.NextPowerOfTwo(m_body.pageCount));
			Array.Clear(m_pageSpacing, 0, m_body.pageCount);
			for(int i = 0; i < m_body.lineCount; i++) {
				var line = m_body.lineInfo[i];
				m_lines[i] = new LineBounds {
					BodyTop = line.ascender, BodyBottom = line.descender,
					Page = m_body.characterInfo[Mathf.Clamp(line.firstCharacterIndex, 0, m_body.characterCount - 1)].pageNumber,
				};
			}
			for(int i = 0; i < m_body.characterCount; i++) {
				var character = m_body.characterInfo[i];
				if(!character.isVisible) continue;
				ref var line = ref m_lines[character.lineNumber];
				line.BodyTop = Mathf.Max(line.BodyTop, Mathf.Max(character.topLeft.y, character.topRight.y));
				line.BodyBottom = Mathf.Min(line.BodyBottom, Mathf.Min(character.bottomLeft.y, character.bottomRight.y));
			}
			for(int i = 0; i < m_body.lineCount; i++) {
				m_lines[i].Top = m_lines[i].BodyTop;
				m_lines[i].Bottom = m_lines[i].BodyBottom;
			}
			for(int i = 0; i < Count; i++) {
				var run = m_runs[i];
				if(run.Bounds.size == Vector3.zero) continue;
				ref var line = ref m_lines[m_body.characterInfo[run.First].lineNumber];
				line.Top = Mathf.Max(line.Top, run.Bounds.max.y);
				line.Bottom = Mathf.Min(line.Bottom, run.Bounds.min.y);
			}
			float spacing = 0;
			for(int i = 0; i < m_body.lineCount; i++) {
				ref var line = ref m_lines[i];
				if(i == 0 || line.Page != m_lines[i - 1].Page) spacing = 0;
				else {
					var previous = m_lines[i - 1];
					if(line.Top > line.BodyTop || previous.Bottom < previous.BodyBottom) {
						float gap = Mathf.Max(0, previous.BodyBottom - line.BodyTop);
						spacing += Mathf.Max(0, line.Top - previous.Bottom + gap);
					}
				}
				line.Offset = -spacing;
				m_pageSpacing[line.Page] = spacing;
			}
			float anchor = alignment == VerticalAlignmentOptions.Bottom ? 1
				: alignment == VerticalAlignmentOptions.Middle || alignment == VerticalAlignmentOptions.Geometry ? .5f : 0;
			for(int i = 0; i < m_body.lineCount; i++) m_lines[i].Offset += m_pageSpacing[m_lines[i].Page] * anchor;
		}

		private void ApplyLineOffsets(TMP_TextInfo info) {
			for(int i = 0; i < Count; i++) {
				var run = m_runs[i];
				if(run.Bounds.size == Vector3.zero) continue;
				var bounds = run.Bounds;
				bounds.center += Vector3.up * m_lines[m_body.characterInfo[run.First].lineNumber].Offset;
				run.Bounds = bounds;
			}
			for(int i = 0; i < info.characterCount; i++) {
				ref var character = ref info.characterInfo[i];
				float offset = m_lines[character.lineNumber].Offset;
				var delta = Vector3.up * offset;
				character.bottomLeft += delta;
				character.topLeft += delta;
				character.topRight += delta;
				character.bottomRight += delta;
				character.vertex_BL.position += delta;
				character.vertex_TL.position += delta;
				character.vertex_TR.position += delta;
				character.vertex_BR.position += delta;
				character.baseLine += offset;
				character.ascender += offset;
				character.descender += offset;
			}
			for(int i = 0; i < info.lineCount; i++) {
				ref var line = ref info.lineInfo[i];
				var bounds = m_lines[i];
				line.baseline += bounds.Offset;
				line.ascender = bounds.Top + bounds.Offset;
				line.descender = bounds.Bottom + bounds.Offset;
				line.lineHeight += bounds.Top - bounds.BodyTop + bounds.BodyBottom - bounds.Bottom;
				line.lineExtents.min.y = line.descender;
				line.lineExtents.max.y = line.ascender;
			}
			for(int page = 0; page < info.pageCount; page++) {
				float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
				for(int i = 0; i < info.lineCount; i++) {
					if(m_lines[i].Page != page) continue;
					top = Mathf.Max(top, info.lineInfo[i].ascender);
					bottom = Mathf.Min(bottom, info.lineInfo[i].descender);
				}
				if(!float.IsInfinity(top)) {
					info.pageInfo[page].ascender = top;
					info.pageInfo[page].descender = bottom;
				}
			}
		}

		internal void RestoreTextInfo(TMP_TextInfo info) => CopyTextInfo(m_body, info);

		internal void TransformBoundsX(float scale, float offset) {
			for(int i = 0; i < Count; i++) {
				var run = m_runs[i];
				if(run.Bounds.size == Vector3.zero) continue;
				var center = run.Bounds.center;
				var size = run.Bounds.size;
				center.x = center.x * scale + offset;
				size.x *= scale;
				run.Bounds = new Bounds(center, size);
			}
		}

		private static void CopyTextInfo(TMP_TextInfo source, TMP_TextInfo destination) {
			destination.characterCount = source.characterCount;
			destination.spriteCount = source.spriteCount;
			destination.spaceCount = source.spaceCount;
			destination.wordCount = source.wordCount;
			destination.linkCount = source.linkCount;
			destination.lineCount = source.lineCount;
			destination.pageCount = source.pageCount;
			Copy(source.characterInfo, ref destination.characterInfo, source.characterCount);
			Copy(source.wordInfo, ref destination.wordInfo, source.wordCount);
			Copy(source.linkInfo, ref destination.linkInfo, source.linkCount);
			Copy(source.lineInfo, ref destination.lineInfo, source.lineCount);
			Copy(source.pageInfo, ref destination.pageInfo, source.pageCount);
		}

		private static void Copy<T>(T[] source, ref T[] destination, int count) {
			if(destination.Length < count) Array.Resize(ref destination, Mathf.NextPowerOfTwo(count));
			Array.Copy(source, destination, count);
		}
	}
}
