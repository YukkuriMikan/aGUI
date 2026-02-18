using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace ANest.UI {
	/// <summary>ルビ表示とLocalization対応を備えたTextMeshProUGUI拡張。</summary>
	public class aTextMeshProUgui : TextMeshProUGUI {
		#region Structs
		/// <summary>ルビ情報を保持する構造体。</summary>
		private struct RubyInfo {
			public int startIndex;  // ルビ対象の開始インデックス
			public int length;      // ルビ対象の文字数
			public string rubyText; // ルビ文字列
		}
		#endregion

		#region SerializeField
		[Tooltip("Localization用StringTableCollectionの参照")]
		[SerializeField] private LocalizedStringTable m_stringTable;
		[Tooltip("StringTable内のキー名")]
		[SerializeField] private string m_localizationKey;
		#endregion

		#region Fields
		private static readonly Regex s_rubyRegex = new(@"<ruby=""([^""]+)"">([^<]+)</ruby>", RegexOptions.Compiled); // rubyタグ解析用正規表現
		private readonly List<RubyInfo> m_rubyInfos = new();                                                          // 解析済みルビ情報
		[SerializeField] private string m_rawText;                                                                    // ルビタグを含む元テキスト
		private string m_baseText;                                                                                    // ルビタグ除去後の本文
		private StringTable m_currentTable;                                                                           // 現在のStringTable
		#endregion

	    #region Properties
		/// <summary>Localization用StringTableCollectionの参照</summary>
		public LocalizedStringTable StringTable => m_stringTable;
		/// <summary>StringTable内のキー名</summary>
		public string LocalizationKey {
			get => m_localizationKey;
			set {
				m_localizationKey = value;
				ApplyLocalization();
			}
		}

		/// <summary>ルビタグを含む元テキスト</summary>
		public string RawText => m_rawText;

		/// <summary>テキストを設定する。rubyタグが含まれる場合は自動的に解析する。</summary>
		public override string text {
			get => m_rawText ?? base.text;
			set {
				m_rawText = value;
				if(s_rubyRegex.IsMatch(value ?? string.Empty)) {
					ParseRubyTags(value);
					base.text = m_baseText;
				} else {
					m_rubyInfos.Clear();
					base.text = value;
				}
			}
		}
		#endregion

		#region Unity Methods
		protected override void OnEnable() {
			base.OnEnable();
			if(m_stringTable != null) {
				m_stringTable.TableChanged += OnStringTableChanged;
			}
			// 保存されたルビタグ付きテキストを再解析
			if(!string.IsNullOrEmpty(m_rawText) && s_rubyRegex.IsMatch(m_rawText)) {
				ParseRubyTags(m_rawText);
				base.text = m_baseText;
			}
			ApplyLocalization();
		}

		protected override void OnDisable() {
			if(m_stringTable != null) {
				m_stringTable.TableChanged -= OnStringTableChanged;
			}
			base.OnDisable();
		}
		#endregion

		#region Public Methods
		/// <summary>ルビタグを含むテキストを設定し、ルビ付きで表示する。</summary>
		public void SetTextWithRuby(string rubyTaggedText) {
			m_rawText = rubyTaggedText;
			ParseRubyTags(rubyTaggedText);
			base.text = m_baseText;
		}
		#endregion

		#region Private Methods
		/// <summary>Localizationテーブル変更時のコールバック。</summary>
		private void OnStringTableChanged(StringTable table) {
			m_currentTable = table;
			ApplyLocalization();
		}

		/// <summary>現在のLocalization設定からテキストを適用する。</summary>
		private void ApplyLocalization() {

		}

		/// <summary>rubyタグを解析してルビ情報と本文を分離する。</summary>
		private void ParseRubyTags(string input) {
			m_rubyInfos.Clear();
			if(string.IsNullOrEmpty(input)) {
				m_baseText = string.Empty;
				return;
			}

			var matches = s_rubyRegex.Matches(input);
			if(matches.Count == 0) {
				m_baseText = input;
				return;
			}

			var builder = new System.Text.StringBuilder(input.Length);
			int lastIndex = 0;
			foreach (Match match in matches) {
				// タグ前のテキストを追加
				builder.Append(input, lastIndex, match.Index - lastIndex);
				var rubyText = match.Groups[1].Value;
				var bodyText = match.Groups[2].Value;
				m_rubyInfos.Add(new RubyInfo {
					startIndex = builder.Length,
					length = bodyText.Length,
					rubyText = rubyText
				});
				builder.Append(bodyText);
				lastIndex = match.Index + match.Length;
			}
			// 残りのテキストを追加
			builder.Append(input, lastIndex, input.Length - lastIndex);
			m_baseText = builder.ToString();
		}

		/// <summary>メッシュ生成後にルビテキストを上部に配置する。</summary>
		protected override void GenerateTextMesh() {
			base.GenerateTextMesh();
			if(m_rubyInfos.Count == 0) return;
			if(textInfo == null || textInfo.characterCount == 0) return;

			var rubyFontSize = fontSize * 0.5f;
			foreach (var ruby in m_rubyInfos) {
				if(ruby.startIndex >= textInfo.characterCount) continue;
				int endIndex = Mathf.Min(ruby.startIndex + ruby.length - 1, textInfo.characterCount - 1);
				var firstCharInfo = textInfo.characterInfo[ruby.startIndex];
				var lastCharInfo = textInfo.characterInfo[endIndex];
				if(!firstCharInfo.isVisible || !lastCharInfo.isVisible) continue;

				// ルビ対象の中央上部を算出
				float centerX = (firstCharInfo.topLeft.x + lastCharInfo.topRight.x) * 0.5f;
				float topY = Mathf.Max(firstCharInfo.topLeft.y, lastCharInfo.topRight.y);
				float rubyY = topY + rubyFontSize * 0.2f;

				// ルビ用のサブテキストオブジェクトを生成
				var rubyObj = new GameObject($"Ruby_{ruby.startIndex}");
				rubyObj.transform.SetParent(transform, false);
				var rubyTmp = rubyObj.AddComponent<TextMeshProUGUI>();
				rubyTmp.text = ruby.rubyText;
				rubyTmp.font = font;
				rubyTmp.fontSize = rubyFontSize;
				rubyTmp.color = color;
				rubyTmp.alignment = TextAlignmentOptions.Center;
				rubyTmp.enableWordWrapping = false;
				rubyTmp.overflowMode = TextOverflowModes.Overflow;
				rubyTmp.raycastTarget = false;

				var rubyRect = rubyTmp.rectTransform;
				rubyRect.anchorMin = new Vector2(0.5f, 0.5f);
				rubyRect.anchorMax = new Vector2(0.5f, 0.5f);
				rubyRect.pivot = new Vector2(0.5f, 0f);
				rubyRect.anchoredPosition = new Vector2(centerX, rubyY);
				rubyRect.sizeDelta = new Vector2(ruby.rubyText.Length * rubyFontSize, rubyFontSize * 1.2f);
			}
		}
		#endregion
	}
}
