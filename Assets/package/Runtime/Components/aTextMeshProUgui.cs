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
		private const float RubyFontSizeRatio = 0.5f; // デフォルトのルビフォントサイズ比率
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
		private readonly List<GameObject> m_rubyObjects = new List<GameObject>(); // ルビ用子オブジェクト
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
			CollectExistingRubyObjects();
			TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
			if(m_stringTable != null) {
				m_stringTable.TableChanged += OnStringTableChanged;
			}
			ApplyLocalization();
			ForceMeshUpdate();
		}

		/// <summary>無効化時にイベント解除とルビオブジェクト破棄を行う</summary>
		protected override void OnDisable() {
			TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
			ClearRubyObjects();
			if(m_stringTable != null) {
				m_stringTable.TableChanged -= OnStringTableChanged;
			}
			base.OnDisable();
		}
		#endregion

		#region Private Methods
		private void EnsureRubyPreprocessor() {
			m_rubyPreprocessor ??= new aRubyTextPreprocessor(this);
			if(ReferenceEquals(m_TextPreprocessor, m_rubyPreprocessor)) return;
			m_rubyPreprocessor.Input = m_TextPreprocessor;
			m_TextPreprocessor = m_rubyPreprocessor;
		}

		public override void SetVerticesDirty() {
			EnsureRubyPreprocessor();
			if(!m_preparingRubyInput && !m_isUpdatingRuby) {
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
			base.ForceMeshUpdate(ignoreActiveState, forceTextReparsing);
		}

		public override void Rebuild(UnityEngine.UI.CanvasUpdate update) {
			EnsureRubyPreprocessor();
			base.Rebuild(update);
		}

		protected override void GenerateTextMesh() {
			var wrapping = m_TextWrappingMode;
			if(m_rubyPreprocessor != null && m_rubyPreprocessor.UseManualWrapping) m_TextWrappingMode = TextWrappingModes.NoWrap;
			try { base.GenerateTextMesh(); }
			finally { m_TextWrappingMode = wrapping; }
		}

		protected override Vector2 CalculatePreferredValues(ref float fontSize, Vector2 marginSize, bool isTextAutoSizingEnabled, TextWrappingModes textWrapMode) {
			if(m_rubyPreprocessor != null && m_rubyPreprocessor.UseManualWrapping) textWrapMode = TextWrappingModes.NoWrap;
			return base.CalculatePreferredValues(ref fontSize, marginSize, isTextAutoSizingEnabled, textWrapMode);
		}

		/// <summary>テキスト変更イベントのコールバック</summary>
		private void OnTextChanged(Object obj) {
			if(obj != this) return;
			if(m_isUpdatingRuby) return;
			m_isUpdatingRuby = true;
			try {
				// TMP自身の通知はメッシュ生成完了後。外部からの未反映通知だけ更新する。
				if(havePropertiesChanged) ForceMeshUpdate();
				UpdateRubyTextCache(textInfo);
				if(m_rubyPreprocessor.TryCreateOverflowLayout(textInfo, m_rubyTextByLink)) {
					Debug.LogWarning("[aTextMeshProUgui] ルビ本文が1行の幅を超えるため、途中で分割せず横にはみ出して表示します。", this);
					ForceMeshUpdate();
				}
				UpdateRubyObjects();
			} finally {
				m_isUpdatingRuby = false;
			}
		}

		/// <summary>Localizationテーブル変更時のコールバック</summary>
		private void OnStringTableChanged(StringTable table) {
			m_currentTable = table;
			ApplyLocalization();
			ForceMeshUpdate();
			UpdateRubyObjects();
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

		/// <summary>linkInfoからルビ情報を解析し、ルビオブジェクトを更新する</summary>
		private void UpdateRubyObjects() {
			var info = textInfo;
			if(info == null) {
				ClearRubyObjects();
				return;
			}

			UpdateRubyTextCache(info);

			// ルビ用linkの数を集計
			int rubyCount = 0;
			for (int i = 0; i < info.linkCount; i++) {
				if(m_rubyTextByLink[i] != null && HasRubyBody(info, info.linkInfo[i])) rubyCount++;
			}

			// 不要なルビオブジェクトを破棄
			while (m_rubyObjects.Count > rubyCount) {
				int last = m_rubyObjects.Count - 1;
				var obj = m_rubyObjects[last];
				m_rubyObjects.RemoveAt(last);
				DestroyRubyObject(obj);
			}

			int rubyIndex = 0;
			for (int i = 0; i < info.linkCount; i++) {
				var linkInfo = info.linkInfo[i];
				var rubyText = m_rubyTextByLink[i];
				if(rubyText == null || !HasRubyBody(info, linkInfo)) continue;

				// ルビオブジェクトの取得または生成
				GameObject rubyObj;
				if(rubyIndex < m_rubyObjects.Count && m_rubyObjects[rubyIndex] != null) {
					rubyObj = m_rubyObjects[rubyIndex];
				} else {
					rubyObj = new GameObject($"Ruby_{rubyIndex}", typeof(RectTransform), typeof(TextMeshProUGUI));
					rubyObj.transform.SetParent(transform, false);
					rubyObj.hideFlags = HideFlags.NotEditable;
					var rubyRect = rubyObj.GetComponent<RectTransform>();
					rubyRect.anchorMin = new Vector2(0.5f, 0.5f);
					rubyRect.anchorMax = new Vector2(0.5f, 0.5f);
					rubyRect.pivot = new Vector2(0.5f, 0.5f);
					rubyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
					rubyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
					var rubyTmp = rubyObj.GetComponent<TextMeshProUGUI>();
					rubyTmp.textWrappingMode = TextWrappingModes.NoWrap;
					rubyTmp.overflowMode = TextOverflowModes.Overflow;
					rubyTmp.raycastTarget = false;
					if(rubyIndex < m_rubyObjects.Count) m_rubyObjects[rubyIndex] = rubyObj;
					else m_rubyObjects.Add(rubyObj);
				}

				// ルビテキストの設定
				var rubyTmpComponent = rubyObj.GetComponent<TextMeshProUGUI>();
				rubyTmpComponent.font = font;
				rubyTmpComponent.color = color;
				rubyTmpComponent.alignment = TextAlignmentOptions.Center;
				rubyTmpComponent.text = rubyText;

				// ベーステキストの文字位置からルビの配置位置を計算
				int firstCharIdx = linkInfo.linkTextfirstCharacterIndex;
				int lastCharIdx = firstCharIdx + linkInfo.linkTextLength - 1;
				if(firstCharIdx >= info.characterInfo.Length || lastCharIdx >= info.characterInfo.Length) {
					rubyObj.SetActive(false);
					rubyIndex++;
					continue;
				}

				var firstCharInfo = info.characterInfo[firstCharIdx];
				var lastCharInfo = info.characterInfo[lastCharIdx];
				if(!firstCharInfo.isVisible || !lastCharInfo.isVisible) {
					rubyObj.SetActive(false);
					rubyIndex++;
					continue;
				}
				rubyObj.SetActive(true);

				// characterInfoの座標は親RectTransformのpivot基準ローカル座標
				float left = firstCharInfo.topLeft.x;
				float right = lastCharInfo.topRight.x;
				float top = firstCharInfo.topLeft.y;
				float baseWidth = right - left;

				// ルビサイズモードに応じたフォントサイズ計算
				float rubyFontSize;
				switch(m_rubySizeMode) {
					case RubySizeMode.Auto:
						// ルビ文字数と本文幅から、ルビが本文幅に収まるサイズを算出
						rubyFontSize = rubyText.Length > 0 ? baseWidth / rubyText.Length : fontSize * RubyFontSizeRatio;
						break;
					case RubySizeMode.Scale:
						// 本文フォントサイズに対する割合で算出
						rubyFontSize = fontSize * m_rubyScale;
						break;
					case RubySizeMode.Size:
						// 直接指定
						rubyFontSize = m_rubySize;
						break;
					default:
						rubyFontSize = fontSize * RubyFontSizeRatio;
						break;
				}
				rubyTmpComponent.fontSize = rubyFontSize;

				// ベーステキストの上にルビを配置
				float centerX = (left + right) * 0.5f;
				float rubyY = top + rubyFontSize * 0.6f + m_rubyOffset;
				var rt = rubyObj.GetComponent<RectTransform>();
				rt.localPosition = new Vector3(centerX, rubyY, 0f);
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseWidth + fontSize);
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rubyFontSize * 1.2f);
				rubyIndex++;
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


		private static bool HasRubyBody(TMP_TextInfo info, TMP_LinkInfo link) {
			return link.linkTextLength > 0 && link.linkTextfirstCharacterIndex >= 0
				&& link.linkTextfirstCharacterIndex < info.characterCount
				&& link.linkTextLength <= info.characterCount - link.linkTextfirstCharacterIndex;
		}

		private void DestroyRubyObject(GameObject obj) {
			if(obj == null) return;
			if(Application.isPlaying) {
				// Destroyはフレーム末まで遅延する。再有効化時の回収対象から即座に外す。
				obj.name = "Retired ruby";
				obj.SetActive(false);
				Destroy(obj);
			} else DestroyImmediate(obj);
		}

		/// <summary>シーン再読み込み時に残存するルビ子オブジェクトをリストに回収する</summary>
		private void CollectExistingRubyObjects() {
			m_rubyObjects.Clear();
			// Ruby_Nの連番と再利用時の対応が崩れないよう、子の並び順のまま回収する
			for (int i = 0; i < transform.childCount; i++) {
				var child = transform.GetChild(i);
				if(child.name.StartsWith("Ruby_")) {
					// 名前がRuby_で始まる子オブジェクトをルビとして回収
					m_rubyObjects.Add(child.gameObject);
				}
			}
		}

		/// <summary>全ルビオブジェクトを破棄する</summary>
		private void ClearRubyObjects() {
			m_rubyTextByLink.Clear();
			m_rubySource = null;
			m_hasRubySource = false;
			for (int i = 0; i < m_rubyObjects.Count; i++) {
				DestroyRubyObject(m_rubyObjects[i]);
			}
			m_rubyObjects.Clear();
		}
		#endregion
	}
}
