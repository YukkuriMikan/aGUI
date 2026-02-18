using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
namespace ANest.UI {
	/// <summary>Localization対応およびルビ表示を備えたTextMeshProUGUI拡張。</summary>
	public class aTextMeshProUgui : TextMeshProUGUI {
		#region Constants
		private const string RubyPrefix = "ruby:";
		private const float RubyFontSizeRatio = 0.5f;
		#endregion
		#region SerializeField
		[Tooltip("Localization用StringTableCollectionの参照")]
		[SerializeField] private LocalizedStringTable m_stringTable;
		[Tooltip("StringTable内のキー名")]
		[SerializeField] private string m_localizationKey;
		#endregion
		#region Fields
		private StringTable m_currentTable;
		private readonly List<GameObject> m_rubyObjects = new List<GameObject>();
		private bool m_isUpdatingRuby;
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
		#endregion
		#region Unity Methods
		protected override void OnEnable() {
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
		/// <summary>テキスト変更イベントのコールバック。</summary>
		private void OnTextChanged(Object obj) {
			if(obj != this) return;
			if(m_isUpdatingRuby) return;
			m_isUpdatingRuby = true;
			try {
				// TEXT_CHANGED_EVENT時点ではtextInfoがまだ古い場合があるため、
				// メッシュを強制再生成してからルビを更新する
				ForceMeshUpdate();
				UpdateRubyObjects();
			} finally {
				m_isUpdatingRuby = false;
			}
		}
		/// <summary>Localizationテーブル変更時のコールバック。</summary>
		private void OnStringTableChanged(StringTable table) {
			m_currentTable = table;
			ApplyLocalization();
			ForceMeshUpdate();
			UpdateRubyObjects();
		}
		/// <summary>現在のLocalization設定からテキストを適用する。</summary>
		private void ApplyLocalization() {
			if(m_currentTable == null) return;
			if(string.IsNullOrEmpty(m_localizationKey)) return;
			var entry = m_currentTable.GetEntry(m_localizationKey);
			if(entry == null) return;
			var rawValue = entry.Value;
			if(rawValue != null) {
				text = rawValue;
			}
		}
		/// <summary>linkInfoからルビ情報を解析し、ルビオブジェクトを更新する。</summary>
		private void UpdateRubyObjects() {
			var info = textInfo;
			if(info == null) {
				ClearRubyObjects();
				return;
			}
			int rubyCount = 0;
			for(int i = 0; i < info.linkCount; i++) {
				var linkInfo = info.linkInfo[i];
				var linkId = linkInfo.GetLinkID();
				if(!linkId.StartsWith(RubyPrefix)) continue;
				rubyCount++;
			}
			// 不要なルビオブジェクトを破棄
			while(m_rubyObjects.Count > rubyCount) {
				int last = m_rubyObjects.Count - 1;
				var obj = m_rubyObjects[last];
				m_rubyObjects.RemoveAt(last);
				if(obj != null) {
					if(Application.isPlaying) Destroy(obj);
					else DestroyImmediate(obj);
				}
			}
			int rubyIndex = 0;
			for(int i = 0; i < info.linkCount; i++) {
				var linkInfo = info.linkInfo[i];
				var linkId = linkInfo.GetLinkID();
				if(!linkId.StartsWith(RubyPrefix)) continue;
				var rubyText = linkId.Substring(RubyPrefix.Length);
				// ルビオブジェクトの取得または生成
				GameObject rubyObj;
				if(rubyIndex < m_rubyObjects.Count) {
					rubyObj = m_rubyObjects[rubyIndex];
				} else {
					rubyObj = new GameObject($"Ruby_{rubyIndex}", typeof(RectTransform), typeof(TextMeshProUGUI));
					rubyObj.transform.SetParent(transform, false);
 				rubyObj.hideFlags = HideFlags.NotEditable;
 				var rubyRect = rubyObj.GetComponent<RectTransform>();
					rubyRect.anchorMin = new Vector2(0.5f, 0.5f);
					rubyRect.anchorMax = new Vector2(0.5f, 0.5f);
					rubyRect.pivot = new Vector2(0.5f, 0.5f);
					rubyRect.sizeDelta = Vector2.zero;
					var rubyTmp = rubyObj.GetComponent<TextMeshProUGUI>();
					rubyTmp.enableWordWrapping = false;
					rubyTmp.overflowMode = TextOverflowModes.Overflow;
					rubyTmp.raycastTarget = false;
					m_rubyObjects.Add(rubyObj);
				}
				// ルビテキストの設定
				var rubyTmpComponent = rubyObj.GetComponent<TextMeshProUGUI>();
				rubyTmpComponent.font = font;
				rubyTmpComponent.fontSize = fontSize * RubyFontSizeRatio;
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
				// ベーステキストの上にルビを配置
				float centerX = (left + right) * 0.5f;
				float rubyY = top + fontSize * RubyFontSizeRatio * 0.6f;
				var rt = rubyObj.GetComponent<RectTransform>();
				rt.localPosition = new Vector3(centerX, rubyY, 0f);
				rt.sizeDelta = new Vector2(right - left + fontSize, fontSize * RubyFontSizeRatio * 1.2f);
				rubyIndex++;
			}
		}
		/// <summary>シーン再読み込み時に残存するルビ子オブジェクトをリストに回収する。</summary>
		private void CollectExistingRubyObjects() {
			m_rubyObjects.Clear();
			for(int i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild(i);
				if(child.name.StartsWith("Ruby_")) {
					m_rubyObjects.Add(child.gameObject);
				}
			}
		}
		/// <summary>全ルビオブジェクトを破棄する。</summary>
		private void ClearRubyObjects() {
			for(int i = 0; i < m_rubyObjects.Count; i++) {
				if(m_rubyObjects[i] != null) {
					if(Application.isPlaying) Destroy(m_rubyObjects[i]);
					else DestroyImmediate(m_rubyObjects[i]);
				}
			}
			m_rubyObjects.Clear();
		}
		#endregion
	}
}
