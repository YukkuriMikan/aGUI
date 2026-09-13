using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace ANest.UI {
	/// <summary>ルビのサイズモード。</summary>
	public enum RubySizeMode {
		Auto,  // 本文の横幅にルビの横幅を合わせる
		Scale, // 本文を基準とした割合で大きさを決める
		Size,  // サイズを直接指定する
	}

	/// <summary>Localization対応およびルビ表示を備えたTextMeshProUGUI拡張。</summary>
	public class aTextMeshProUgui : TextMeshProUGUI {
		#region Constants
		private const string RubyPrefix = "ruby:";    // linkタグのルビ識別プレフィックス
		#endregion

		#region SerializeField
		[Tooltip("Localization用StringTableCollectionの参照")]
		[SerializeField] private LocalizedStringTable m_stringTable; // Localization用テーブル参照
		[Tooltip("StringTable内のキー名")]
		[SerializeField] private string m_localizationKey; // ローカライズキー
		[Tooltip("ルビのサイズモード")]
		[SerializeField] private RubySizeMode m_rubySizeMode = RubySizeMode.Auto; // ルビサイズモード
		[Tooltip("Scaleモード時の本文に対するルビの割合 (0.0〜1.0)")]
		[SerializeField] private float m_rubyScale = 0.5f; // Scaleモード時の割合
		[Tooltip("Sizeモード時のルビのフォントサイズ")]
		[SerializeField] private float m_rubySize = 8f; // Sizeモード時のフォントサイズ
		[Tooltip("ルビと本文の間隔（現在の配置位置からの相対値）")]
		[SerializeField] private float m_rubyOffset = 0f; // ルビと本文の間隔
		#endregion

		#region Fields
		private StringTable m_currentTable;                                       // 現在のStringTable
		private aRubyMeshLayout m_rubyMesh;
		private readonly List<string> m_rubyTextByLink = new();
		private string m_rubySource;
		private bool m_hasRubySource;
		private bool m_isUpdatingRuby;                                            // ルビ更新中の再帰防止フラグ
		private aRubyTextPreprocessor m_rubyPreprocessor;
		private bool m_preparingRubyInput;
		#endregion

		#region Properties
		/// <summary>Localization用StringTableCollectionの参照</summary>
		public LocalizedStringTable StringTable => m_stringTable;

		/// <summary>ルビのサイズモード</summary>
		public RubySizeMode RubySizeMode {
			get => m_rubySizeMode;
			set {
				m_rubySizeMode = value;
				ForceMeshUpdate();
			}
		}

		/// <summary>Scaleモード時の本文に対するルビの割合</summary>
		public float RubyScale {
			get => m_rubyScale;
			set {
				m_rubyScale = value;
				ForceMeshUpdate();
			}
		}

		/// <summary>Sizeモード時のルビのフォントサイズ</summary>
		public float RubySize {
			get => m_rubySize;
			set {
				m_rubySize = value;
				ForceMeshUpdate();
			}
		}

		/// <summary>ルビと本文の間隔（現在の配置位置からの相対値）</summary>
		public float RubyOffset {
			get => m_rubyOffset;
			set {
				m_rubyOffset = value;
				ForceMeshUpdate();
			}
		}

		/// <summary>StringTable内のキー名</summary>
		public string LocalizationKey {
			get => m_localizationKey;
			set {
				m_localizationKey = value;
				ApplyLocalization();
			}
		}
		#endregion

		#region Unity Methods
		/// <summary>有効化時にイベント購読とLocalization適用を行う</summary>
		protected override void OnEnable() {
			EnsureRubyPreprocessor();
			base.OnEnable();
			// シーン再読み込み時に残存するルビオブジェクトを回収
			RemoveLegacyRubyObjects();
			base.OnPreRenderText += ApplyRubyMesh;
			TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
			if(m_stringTable != null) {
				m_stringTable.TableChanged += OnStringTableChanged;
			}
			ApplyLocalization();
			ForceMeshUpdate();
		}

		/// <summary>無効化時にイベント購読とレイアウトキャッシュを解除する</summary>
		protected override void OnDisable() {
			TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
			base.OnPreRenderText -= ApplyRubyMesh;
			m_rubyPreprocessor?.InvalidateLayout();
			if(m_stringTable != null) {
				m_stringTable.TableChanged -= OnStringTableChanged;
			}
			base.OnDisable();
		}
		#endregion

		#region Private Methods
		private void EnsureRubyPreprocessor() {
			m_rubyPreprocessor ??= new aRubyTextPreprocessor(this);
			m_rubyMesh ??= new aRubyMeshLayout();
			if(ReferenceEquals(m_TextPreprocessor, m_rubyPreprocessor)) return;
			m_rubyPreprocessor.Input = m_TextPreprocessor;
			m_TextPreprocessor = m_rubyPreprocessor;
		}

		public override void SetVerticesDirty() {
			EnsureRubyPreprocessor();
			if(m_generatingRuby) m_rubyInputDirty = true;
			if(!m_preparingRubyInput && !m_isUpdatingRuby && !m_generatingRuby) {
				m_rubyPreprocessor.InvalidateLayout();
				// 配列・数値書式のSetTextはTMPのプリプロセッサを通らないため、ルビ入力のみ文字列経路へ戻す。
				var source = base.text;
				if(aRubyTextPreprocessor.MayContainRuby(source)) {
					m_preparingRubyInput = true;
					try { base.SetText(source); }
					finally { m_preparingRubyInput = false; }
				}
			}
			base.SetVerticesDirty();
		}

		public override void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false) {
			EnsureRubyPreprocessor();
			if(m_generatingRuby) { m_rubyInputDirty = m_havePropertiesChanged = true; return; }
			base.ForceMeshUpdate(ignoreActiveState, forceTextReparsing);
		}

		public override void Rebuild(UnityEngine.UI.CanvasUpdate update) {
			EnsureRubyPreprocessor();
			base.Rebuild(update);
		}

		internal bool CalculatingRubyPreferredValues => m_isCalculatingPreferredValues;
		private bool m_generatingRuby;
		private float m_rubyLossyScale;
		private bool m_bodyTruncated;
		private int m_bodyOverflowIndex;
		private bool m_rubyInputDirty;
		private bool m_restoreLayoutSettings, m_restoreRenderSettings;
		private TextWrappingModes m_savedWrapping;
		private TextRenderFlags m_savedRenderMode;
		private RubyRenderSettings m_savedRenderSettings;
		public override event System.Action<TMP_TextInfo> OnPreRenderText;

		private struct RubyRenderSettings {
			internal TextOverflowModes Overflow;
			internal bool AutoSize;
			internal float FontSize;
			internal int First, Characters, Words, Lines;
		}

		protected override void GenerateTextMesh() {
			if(m_generatingRuby) { base.GenerateTextMesh(); return; }
			EnsureRubyPreprocessor();
			if(m_rubyPreprocessor.ParsedMesh && m_rubyLossyScale != transform.lossyScale.y) {
				m_rubyPreprocessor.InvalidateLayout();
				ParseInputText();
			}
			m_savedWrapping = m_TextWrappingMode;
			m_savedRenderMode = m_renderMode;
			m_restoreLayoutSettings = true;
			m_rubyInputDirty = false;
			m_generatingRuby = true;
			try {
				if(!m_rubyPreprocessor.ParsedMesh) {
					if(m_rubyPreprocessor.UseManualWrapping) m_TextWrappingMode = TextWrappingModes.NoWrap;
					// 本文は通常のTMPレイアウトで計算する。描画用の追記文字は折り返し・AutoSizeに参加させない。
					bool mayHaveRuby = richText && aRubyTextPreprocessor.MayContainRuby(m_rubyPreprocessor.Output);
					if(mayHaveRuby) m_renderMode = TextRenderFlags.DontRender;
					base.GenerateTextMesh();
					if(!mayHaveRuby) { m_rubyMesh.Clear(); return; }
					UpdateRubyTextCache(textInfo);
					if(m_rubyPreprocessor.TryCreateOverflowLayout(textInfo, m_rubyTextByLink)) {
						Debug.LogWarning("[aTextMeshProUgui] ルビ本文が1行の幅を超えるため、途中で分割せず横にはみ出して表示します。", this);
						m_TextWrappingMode = TextWrappingModes.NoWrap;
						ParseInputText();
						base.GenerateTextMesh();
					}
					m_rubyMesh.Capture(textInfo, m_rubyTextByLink, m_rubyPreprocessor.Output, m_fontSize);
					m_rubyLossyScale = transform.lossyScale.y;
					m_bodyTruncated = m_isTextTruncated;
					m_bodyOverflowIndex = m_firstOverflowCharacterIndex;
					m_renderMode = m_savedRenderMode;
					if(m_rubyMesh.Count == 0) {
						// Ellipsis / Truncateは生成中に解析バッファを書き換えるため、元の本文から再解析する。
						ParseInputText();
						base.GenerateTextMesh();
						return;
					}
					m_rubyPreprocessor.MeshOutput = m_rubyMesh.Output;
					ParseInputText();
				}
				RenderRubyMesh();
			} finally {
				RestoreRubySettings();
				m_generatingRuby = false;
				// コールバックによる変更を次の本文計算へ反映する。生成途中のキャッシュは壊さない。
				if(m_rubyInputDirty) SetVerticesDirty();
			}
		}

		private void RenderRubyMesh() {
			m_savedRenderSettings = new RubyRenderSettings {
				Overflow = m_overflowMode, AutoSize = m_enableAutoSizing, FontSize = m_fontSize,
				First = m_firstVisibleCharacter, Characters = m_maxVisibleCharacters,
				Words = m_maxVisibleWords, Lines = m_maxVisibleLines,
			};
			m_restoreRenderSettings = true;
			m_TextWrappingMode = TextWrappingModes.NoWrap;
			m_overflowMode = TextOverflowModes.Overflow;
			m_enableAutoSizing = false;
			m_fontSize = m_rubyMesh.BodyFontSize;
			m_firstVisibleCharacter = 0;
			m_maxVisibleCharacters = m_maxVisibleWords = m_maxVisibleLines = int.MaxValue;
			base.GenerateTextMesh();
			// DontRender / inactive ForceMeshUpdateでは描画イベントが呼ばれない。
			if(m_restoreRenderSettings)
				m_rubyMesh.Apply(textInfo, m_rubySizeMode, m_rubyScale, m_rubySize, m_rubyOffset);
			m_characterCount = textInfo.characterCount;
		}

		private void RestoreRubySettings() {
			if(m_restoreRenderSettings) {
				m_restoreRenderSettings = false;
				m_overflowMode = m_savedRenderSettings.Overflow;
				m_enableAutoSizing = m_savedRenderSettings.AutoSize;
				m_fontSize = m_savedRenderSettings.AutoSize ? m_rubyMesh.BodyFontSize : m_savedRenderSettings.FontSize;
				m_firstVisibleCharacter = m_savedRenderSettings.First;
				m_maxVisibleCharacters = m_savedRenderSettings.Characters;
				m_maxVisibleWords = m_savedRenderSettings.Words;
				m_maxVisibleLines = m_savedRenderSettings.Lines;
				m_isTextTruncated = m_bodyTruncated;
				m_firstOverflowCharacterIndex = m_bodyOverflowIndex;
			}
			if(m_restoreLayoutSettings) {
				m_restoreLayoutSettings = false;
				m_renderMode = m_savedRenderMode;
				m_TextWrappingMode = m_savedWrapping;
			}
		}

		private void ApplyRubyMesh(TMP_TextInfo info) {
			if(m_rubyPreprocessor.ParsedMesh) m_rubyMesh.Apply(info, m_rubySizeMode, m_rubyScale, m_rubySize, m_rubyOffset);
			// 公開イベントには元の設定を見せる。その後の変更をfinallyで上書きしない。
			RestoreRubySettings();
			OnPreRenderText?.Invoke(info);
		}

		protected override Vector2 CalculatePreferredValues(ref float fontSize, Vector2 marginSize, bool isTextAutoSizingEnabled, TextWrappingModes textWrapMode) {
			if(m_rubyPreprocessor != null && m_rubyPreprocessor.UseManualWrapping) textWrapMode = TextWrappingModes.NoWrap;
			return base.CalculatePreferredValues(ref fontSize, marginSize, isTextAutoSizingEnabled, textWrapMode);
		}

		/// <summary>テキスト変更イベントのコールバック</summary>
		private void OnTextChanged(Object obj) {
			if(obj != this || m_generatingRuby || m_isUpdatingRuby || !havePropertiesChanged) return;
			m_isUpdatingRuby = true;
			try { ForceMeshUpdate(); }
			finally { m_isUpdatingRuby = false; }
		}

		/// <summary>Localizationテーブル変更時のコールバック</summary>
		private void OnStringTableChanged(StringTable table) {
			m_currentTable = table;
			ApplyLocalization();
			ForceMeshUpdate();
		}

		/// <summary>現在のLocalization設定からテキストを適用する</summary>
		private void ApplyLocalization() {
			if(m_currentTable == null) return;
			if(string.IsNullOrEmpty(m_localizationKey)) return;
			var entry = m_currentTable.GetEntry(m_localizationKey);
			if(entry == null) return;
			// タグを含む未加工の生データを取得
			var rawValue = entry.Value;
			if(rawValue != null) {
				text = rawValue;
			}
		}

		private void UpdateRubyTextCache(TMP_TextInfo info) {
			var source = m_rubyPreprocessor?.Output ?? text;
			// 外部プリプロセッサの結果も含め、実際に解析された文字列で判定する。
			if(m_hasRubySource && m_rubySource == source && m_rubyTextByLink.Count == info.linkCount) return;
			m_rubySource = source;
			m_hasRubySource = true;
			m_rubyTextByLink.Clear();
			for(var i = 0; i < info.linkCount; i++) {
				var id = info.linkInfo[i].GetLinkID();
				m_rubyTextByLink.Add(id.StartsWith(RubyPrefix, System.StringComparison.Ordinal) ? id.Substring(RubyPrefix.Length) : null);
			}
		}


		// 旧版がシーンに保存した生成物だけを除去する。新方式はルビ用GameObjectを生成しない。
		private void RemoveLegacyRubyObjects() {
			for(var i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild(i).gameObject;
				if(!child.name.StartsWith("Ruby_", System.StringComparison.Ordinal)
					|| (child.hideFlags & HideFlags.NotEditable) == 0 || !child.TryGetComponent<TextMeshProUGUI>(out _)) continue;
				if(Application.isPlaying) {
					child.name = "Retired ruby";
					child.SetActive(false);
					Destroy(child);
				} else DestroyImmediate(child);
			}
		}
		#endregion
	}
}
