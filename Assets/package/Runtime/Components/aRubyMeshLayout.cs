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
		private sealed class Vertices {
			internal Vector3[] Positions = Array.Empty<Vector3>();
			internal Vector4[] UV = Array.Empty<Vector4>();
			internal Vector2[] UV2 = Array.Empty<Vector2>();
			internal Color32[] Colors = Array.Empty<Color32>();
			internal int Count;
			internal void Ensure(int count) {
				if(Positions.Length >= count) return;
				int capacity = Mathf.NextPowerOfTwo(Mathf.Max(count, 16));
				Array.Resize(ref Positions, capacity);
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
			}
			internal void CopyTo(ref TMP_MeshInfo mesh, int offset) {
				Array.Copy(Positions, 0, mesh.vertices, offset, Count);
				Array.Copy(UV, 0, mesh.uvs0, offset, Count);
				Array.Copy(UV2, 0, mesh.uvs2, offset, Count);
				Array.Copy(Colors, 0, mesh.colors32, offset, Count);
			}
		}

		private readonly TMP_TextInfo m_body = new();
		private readonly List<Vertices> m_bodyVertices = new();
		private readonly List<Vertices> m_readingVertices = new();
		private readonly List<Run> m_runs = new();
		private readonly StringBuilder m_buffer = new();
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

		internal void Apply(TMP_TextInfo info, RubySizeMode mode, float rubyScale, float rubySize, float offset) {
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
			for(var i = 0; i < info.materialCount; i++) {
				ref var mesh = ref info.meshInfo[i];
				var body = i < m_body.materialCount ? m_bodyVertices[i] : null;
				var reading = m_readingVertices[i];
				int bodyCount = body?.Count ?? 0, count = bodyCount + reading.Count;
				if(mesh.vertices.Length < count) mesh.ResizeMeshInfo((count + 3) / 4);
				body?.CopyTo(ref mesh, 0);
				reading.CopyTo(ref mesh, bodyCount);
				mesh.vertexCount = count;
				mesh.ClearUnusedVertices();
			}
			RestoreTextInfo(info);
		}

		internal void RestoreTextInfo(TMP_TextInfo info) => CopyTextInfo(m_body, info);

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
