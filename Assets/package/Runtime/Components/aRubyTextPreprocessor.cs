using System;
using System.Text;
using System.Collections.Generic;
using TMPro;

namespace ANest.UI {
	/// <summary>ルビ本文を一つの改行単位にする。著者の入力文字列は変更しない。</summary>
	internal sealed class aRubyTextPreprocessor : ITextPreprocessor {
		private readonly aTextMeshProUgui m_owner;
		private readonly StringBuilder m_buffer = new();
		private string m_source;
		private bool m_richText;
		private bool m_parseEscapes;
		private bool m_hasSource;
		private readonly SortedSet<int> m_lineBreaks = new();
		internal ITextPreprocessor Input;
		internal string Output { get; private set; }
		internal bool UseManualWrapping { get; private set; }
		internal bool ParsedMesh { get; private set; }
		internal string MeshOutput;

		internal aRubyTextPreprocessor(aTextMeshProUgui owner) => m_owner = owner;

		internal static bool MayContainRuby(string source) => source != null
			&& (source.IndexOf("ruby:", StringComparison.Ordinal) >= 0 || source.IndexOf("<ruby=", StringComparison.OrdinalIgnoreCase) >= 0);

		internal void InvalidateLayout() {
			m_hasSource = false;
			UseManualWrapping = false;
			MeshOutput = null;
		}

		// TMPはnobrでも長すぎる単語を分割する。その場合のみ、決定済みの改行位置を固定し、
		// ルビの途中の改行を直前・直後へ移す。NoWrapで再描画すれば他の行幅を変えずにはみ出せる。
		internal bool TryCreateOverflowLayout(TMP_TextInfo info, List<string> rubyTextByLink) {
			if(UseManualWrapping || string.IsNullOrEmpty(Output)) return false;
			bool needsOverflow = false;
			for(var i = 0; i < info.linkCount; i++) {
				var link = info.linkInfo[i];
				if(rubyTextByLink[i] == null || link.linkTextLength <= 0) continue;
				int first = link.linkTextfirstCharacterIndex, last = first + link.linkTextLength - 1;
				if(first < 0 || last >= info.characterCount) continue;
				if(info.characterInfo[first].lineNumber != info.characterInfo[last].lineNumber) needsOverflow = true;
			}
			if(!needsOverflow) return false;

			m_lineBreaks.Clear();
			for(var i = 1; i < info.characterCount; i++) {
				var previous = info.characterInfo[i - 1];
				var current = info.characterInfo[i];
				if(current.lineNumber != previous.lineNumber && previous.character != '\n' && previous.character != '\r')
					m_lineBreaks.Add(current.index);
			}
			for(var i = 0; i < info.linkCount; i++) {
				var link = info.linkInfo[i];
				if(rubyTextByLink[i] == null || link.linkTextLength <= 0) continue;
				int first = link.linkTextfirstCharacterIndex, last = first + link.linkTextLength - 1;
				if(first < 0 || last >= info.characterCount || info.characterInfo[first].lineNumber == info.characterInfo[last].lineNumber) continue;
				for(var j = first; j <= last; j++) m_lineBreaks.Remove(info.characterInfo[j].index);
				var start = Output.LastIndexOf("<link", info.characterInfo[first].index, StringComparison.OrdinalIgnoreCase);
				var end = Output.IndexOf("</link>", info.characterInfo[last].index, StringComparison.OrdinalIgnoreCase);
				// 追加したゼロ幅スペースや既存の明示改行と重複して空行を作らない。
				int previous = first - 1, next = last + 1;
				while(previous >= 0 && !info.characterInfo[previous].isVisible && info.characterInfo[previous].character != '\n') {
					m_lineBreaks.Remove(info.characterInfo[previous--].index);
				}
				while(next < info.characterCount && !info.characterInfo[next].isVisible && info.characterInfo[next].character != '\n') {
					m_lineBreaks.Remove(info.characterInfo[next++].index);
				}
				if(start >= 0 && previous >= 0 && info.characterInfo[previous].character != '\n'
					&& !HasBreakBetween(info.characterInfo[previous].index, start)) m_lineBreaks.Add(start);
				if(end >= 0 && next < info.characterCount && info.characterInfo[next].character != '\n') {
					m_lineBreaks.Remove(info.characterInfo[next].index);
					m_lineBreaks.Add(end + 7);
				}
			}
			m_buffer.Clear();
			var copied = 0;
			foreach(var index in m_lineBreaks) {
				if(index < copied || index > Output.Length) continue;
				m_buffer.Append(Output, copied, index - copied).Append('\n');
				copied = index;
			}
			m_buffer.Append(Output, copied, Output.Length - copied);
			Output = m_buffer.ToString();
			UseManualWrapping = true;
			return true;
		}

		private bool HasBreakBetween(int after, int through) {
			foreach(var index in m_lineBreaks) {
				if(index > through) break;
				if(index > after) return true;
			}
			return false;
		}

		public string PreprocessText(string text) {
			var body = PreprocessBody(text);
			ParsedMesh = !m_owner.CalculatingRubyPreferredValues && MeshOutput != null;
			return ParsedMesh ? MeshOutput : body;
		}

		private string PreprocessBody(string text) {
			var source = Input != null ? Input.PreprocessText(text) : text;
			if(m_hasSource && source == m_source && m_richText == m_owner.richText && m_parseEscapes == m_owner.parseCtrlCharacters) return Output;
			m_hasSource = true;
			MeshOutput = null;
			UseManualWrapping = false;
			m_source = source;
			m_richText = m_owner.richText;
			m_parseEscapes = m_owner.parseCtrlCharacters;
			if(!m_richText || !MayContainRuby(source)) return Output = source;

			m_buffer.Clear();
			bool noParse = false, noBreak = false, ruby = false, addedNoBreak = false;
			int customRubyEnd = -1;
			for(var i = 0; i < source.Length; i++) {
				if(source[i] == '<') {
					var end = TagEnd(source, i);
					if(end >= 0) {
						if(TagIs(source, i, end, "/noparse")) noParse = false;
						else if(noParse) { m_buffer.Append(source, i, end - i + 1); i = end; continue; }
						else if(TagIs(source, i, end, "noparse")) noParse = true;
						else if(TagIs(source, i, end, "nobr")) noBreak = true;
						else if(TagIs(source, i, end, "/nobr")) noBreak = false;
						else if(!ruby && TryReadRubyTag(source, i, end, out var readingStart, out var readingLength, out var closingTag)) {
							// シリアライズされたTextは触らず、描画へ渡す文字列だけを標準linkへ変換する。
							ruby = true;
							customRubyEnd = closingTag;
							addedNoBreak = !noBreak;
							if(addedNoBreak) m_buffer.Append('\u200B').Append("<nobr>");
							m_buffer.Append("<link=\"ruby:").Append(source, readingStart, readingLength).Append("\">");
							i = end;
							continue;
						}
						else if(IsRubyLink(source, i, end)) {
							ruby = true;
							addedNoBreak = !noBreak;
							if(addedNoBreak) m_buffer.Append('\u200B').Append("<nobr>");
						} else if(ruby && (i == customRubyEnd || (customRubyEnd < 0 && TagIs(source, i, end, "/link")))) {
							if(customRubyEnd >= 0) m_buffer.Append("</link>");
							else m_buffer.Append(source, i, end - i + 1);
							if(addedNoBreak) m_buffer.Append("</nobr>").Append('\u200B');
							ruby = false;
							customRubyEnd = -1;
							i = end;
							continue;
						} else if(ruby && (TagIs(source, i, end, "br") || TagIs(source, i, end, "br/"))) {
							i = end;
							continue;
						}
						m_buffer.Append(source, i, end - i + 1);
						i = end;
						continue;
					}
				}
				if(ruby && !noParse) {
					if(source[i] == '\n' || source[i] == '\r' || source[i] == '\u2028' || source[i] == '\u2029') continue;
					if(m_parseEscapes && source[i] == '\\' && i + 1 < source.Length && (source[i + 1] == 'n' || source[i + 1] == 'r')) { i++; continue; }
				}
				m_buffer.Append(source[i]);
			}
			return Output = m_buffer.ToString();
		}

		private static int TagEnd(string source, int start) {
			char quote = '\0';
			for(var i = start + 1; i < source.Length; i++) {
				var c = source[i];
				if(quote != '\0') { if(c == quote) quote = '\0'; }
				else if(c == '"' || c == '\'') quote = c;
				else if(c == '>') return i;
			}
			return -1;
		}

		private static bool TagIs(string source, int start, int end, string tag) => end - start - 1 == tag.Length
			&& string.Compare(source, start + 1, tag, 0, tag.Length, StringComparison.OrdinalIgnoreCase) == 0;

		private static bool TryReadRubyTag(string source, int start, int end, out int readingStart, out int readingLength, out int closingTag) {
			readingStart = readingLength = 0;
			closingTag = -1;
			if(end - start < 6 || string.Compare(source, start + 1, "ruby=", 0, 5, StringComparison.OrdinalIgnoreCase) != 0) return false;
			readingStart = start + 6;
			readingLength = end - readingStart;
			if(readingLength > 0 && (source[readingStart] == '\'' || source[readingStart] == '"')) {
				if(readingLength < 2 || source[end - 1] != source[readingStart]) return false;
				readingStart++;
				readingLength -= 2;
			}
			// linkの引用符を壊す入力は変換しない。入力途中の原文もそのまま保持する。
			for(var i = readingStart; i < readingStart + readingLength; i++) if(source[i] == '"' || source[i] == '<' || source[i] == '\n' || source[i] == '\r') return false;
			bool noParse = false;
			for(var i = end + 1; i < source.Length; i++) {
				if(source[i] != '<') continue;
				var tagEnd = TagEnd(source, i);
				if(tagEnd < 0) return false;
				if(TagIs(source, i, tagEnd, "/noparse")) noParse = false;
				else if(!noParse) {
					if(TagIs(source, i, tagEnd, "noparse")) noParse = true;
					else if(TagIs(source, i, tagEnd, "/ruby")) { closingTag = i; return true; }
					else if(tagEnd - i >= 6 && string.Compare(source, i + 1, "ruby=", 0, 5, StringComparison.OrdinalIgnoreCase) == 0) return false;
				}
				i = tagEnd;
			}
			return false;
		}

		private static bool IsRubyLink(string source, int start, int end) {
			if(end - start < 11 || string.Compare(source, start + 1, "link=", 0, 5, StringComparison.OrdinalIgnoreCase) != 0) return false;
			var value = start + 6;
			if(source[value] == '"' || source[value] == '\'') value++;
			return end - value >= 5 && string.Compare(source, value, "ruby:", 0, 5, StringComparison.Ordinal) == 0;
		}
	}
}
