using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class UnityBuiltinIconLister : EditorWindow {
	private Vector2 _scroll;
	private string _filter = "";
	private List<Texture2D> _icons = new List<Texture2D>();
	private List<IconInfo> _iconInfos = new List<IconInfo>();
	private bool _includeDarkSkinVariants = true;
	private bool _sortByName = true;

	[Serializable]
	private class IconInfo {
		public Texture2D Texture;
		public string Name;
		public int Width;
		public int Height;
		public string AssetPath;
		public bool IsProbablyBuiltin;
	}

	[MenuItem("Tools/Unity Builtin Icons/Lister")]
	public static void Open() {
		var window = GetWindow<UnityBuiltinIconLister>("Builtin Icons");
		window.minSize = new Vector2(700, 500);
		window.Refresh();
	}

	private void OnGUI() {
		EditorGUILayout.Space();

		using (new EditorGUILayout.HorizontalScope()) {
			if(GUILayout.Button("Refresh", GUILayout.Width(100))) {
				Refresh();
			}

			if(GUILayout.Button("Export TSV", GUILayout.Width(100))) {
				ExportTSV();
			}

			if(GUILayout.Button("Export PNGs", GUILayout.Width(100))) {
				ExportPNGs();
			}

			GUILayout.FlexibleSpace();
		}

		EditorGUILayout.Space();

		using (new EditorGUILayout.HorizontalScope()) {
			_filter = EditorGUILayout.TextField("Filter", _filter);

			_includeDarkSkinVariants = EditorGUILayout.ToggleLeft("Include d_ variants", _includeDarkSkinVariants, GUILayout.Width(150));
			_sortByName = EditorGUILayout.ToggleLeft("Sort by name", _sortByName, GUILayout.Width(110));
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField($"Count: {GetFiltered().Count}", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		_scroll = EditorGUILayout.BeginScrollView(_scroll);

		const int previewSize = 36;
		foreach (var info in GetFiltered()) {
			using (new EditorGUILayout.HorizontalScope()) {
				GUILayout.Label(info.Texture, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
				EditorGUILayout.SelectableLabel(info.Name, GUILayout.Height(18));

				GUILayout.Label($"{info.Width}x{info.Height}", GUILayout.Width(60));

				// 必要ならパス表示（長いので省略気味）
				if(!string.IsNullOrEmpty(info.AssetPath)) {
					GUILayout.Label(info.AssetPath, EditorStyles.miniLabel);
				} else {
					GUILayout.Label("(builtin/internal)", EditorStyles.miniLabel);
				}
			}
		}

		EditorGUILayout.EndScrollView();
	}

	private void Refresh() {
		_icons.Clear();
		_iconInfos.Clear();

		// Editor 上にロードされている Texture2D を総当たり
		// 内蔵アイコンもここに多数含まれます
		var textures = Resources.FindObjectsOfTypeAll<Texture2D>();

		var seen = new HashSet<IntPtr>();
		foreach (var tex in textures) {
			if(tex == null) continue;

			// 同一インスタンス重複回避
			var ptr = tex.GetNativeTexturePtr();
			if(ptr != IntPtr.Zero && !seen.Add(ptr))
				continue;

			if(!IsLikelyIcon(tex))
				continue;

			var path = AssetDatabase.GetAssetPath(tex); // 内蔵/内部は空になることが多い

			var info = new IconInfo {
				Texture = tex,
				Name = tex.name ?? "",
				Width = tex.width,
				Height = tex.height,
				AssetPath = path,
				IsProbablyBuiltin = IsProbablyBuiltin(tex, path)
			};

			_iconInfos.Add(info);
		}

		// 名前で重複しやすいので、名前＋サイズ＋パスでユニーク化
		_iconInfos = _iconInfos
			.GroupBy(i => $"{i.Name}|{i.Width}x{i.Height}|{i.AssetPath}")
			.Select(g => g.First())
			.ToList();

		// 内蔵っぽいもの優先
		_iconInfos = _iconInfos
			.OrderByDescending(i => i.IsProbablyBuiltin)
			.ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();

		Repaint();
		Debug.Log($"[UnityBuiltinIconLister] Found {_iconInfos.Count} icon-like textures.");
	}

	private List<IconInfo> GetFiltered() {
		IEnumerable<IconInfo> query = _iconInfos;

		if(!_includeDarkSkinVariants) {
			query = query.Where(i => !i.Name.StartsWith("d_", StringComparison.Ordinal));
		}

		if(!string.IsNullOrWhiteSpace(_filter)) {
			query = query.Where(i =>
				i.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
				(!string.IsNullOrEmpty(i.AssetPath) && i.AssetPath.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0));
		}

		if(_sortByName) {
			query = query.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
				.ThenBy(i => i.Width)
				.ThenBy(i => i.Height);
		}

		return query.ToList();
	}

	private static bool IsLikelyIcon(Texture2D tex) {
		if(tex == null) return false;

		// 名前なし除外
		if(string.IsNullOrEmpty(tex.name))
			return false;

		// 極端に大きいものは除外（スプラッシュ等）
		if(tex.width > 256 || tex.height > 256)
			return false;

		// 0サイズ除外
		if(tex.width <= 0 || tex.height <= 0)
			return false;

		// 一般的なアイコンサイズを優先
		bool sizeLooksLikeIcon =
			tex.width == tex.height ||
			tex.width <= 64 && tex.height <= 64;

		if(!sizeLooksLikeIcon) return false;

		// Unity内部でよくある名前パターン
		string n = tex.name;
		bool nameLooksLikeIcon =
			n.Contains("Icon", StringComparison.OrdinalIgnoreCase) ||
			n.Contains("icon", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("d_", StringComparison.Ordinal) ||
			n.StartsWith("Toolbar", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("sv_", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("Avatar", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("console", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("Profiler", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("Animation", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("Scene", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("PlayButton", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("PauseButton", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("StepButton", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("TreeEditor", StringComparison.OrdinalIgnoreCase) ||
			n.StartsWith("BuildSettings", StringComparison.OrdinalIgnoreCase);

		// 名前パターンに一致しなくても小さいテクスチャは拾いたい
		return nameLooksLikeIcon || (tex.width <= 64 && tex.height <= 64);
	}

	private static bool IsProbablyBuiltin(Texture2D tex, string assetPath) {
		// AssetDatabase パスが空 → 内部リソースの可能性が高い
		if(string.IsNullOrEmpty(assetPath))
			return true;

		// Unity内蔵テーマ/エディタリソース系
		if(assetPath.IndexOf("Library/unity editor resources", StringComparison.OrdinalIgnoreCase) >= 0)
			return true;

		// プロジェクトアセットのアイコンではない場合を優先したいので、
		// Assets/Packages 外を built-in 寄り扱い
		if(!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
			!assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
			return true;

		return false;
	}

	private void ExportTSV() {
		var list = GetFiltered();
		if(list.Count == 0) {
			EditorUtility.DisplayDialog("Export TSV", "No icons to export.", "OK");
			return;
		}

		var path = EditorUtility.SaveFilePanel(
			"Export Builtin Icon List (TSV)",
			Application.dataPath,
			$"UnityBuiltinIcons_{Application.unityVersion}",
			"tsv");

		if(string.IsNullOrEmpty(path)) return;

		using (var sw = new StreamWriter(path, false, new System.Text.UTF8Encoding(true))) {
			sw.WriteLine("Name\tWidth\tHeight\tAssetPath\tBuiltinLike");
			foreach (var i in list) {
				sw.WriteLine($"{EscapeTsv(i.Name)}\t{i.Width}\t{i.Height}\t{EscapeTsv(i.AssetPath)}\t{i.IsProbablyBuiltin}");
			}
		}

		EditorUtility.RevealInFinder(path);
		Debug.Log($"[UnityBuiltinIconLister] Exported TSV: {path}");
	}

	private void ExportPNGs() {
		var list = GetFiltered();
		if(list.Count == 0) {
			EditorUtility.DisplayDialog("Export PNGs", "No icons to export.", "OK");
			return;
		}

		var folder = EditorUtility.SaveFolderPanel(
			"Export Builtin Icons as PNG",
			Application.dataPath,
			$"UnityBuiltinIcons_{Application.unityVersion}");

		if(string.IsNullOrEmpty(folder)) return;

		int exported = 0;
		foreach (var info in list) {
			if(info.Texture == null) continue;

			try {
				byte[] png = info.Texture.EncodeToPNG();
				if(png == null || png.Length == 0) continue;

				string safeName = MakeSafeFileName(info.Name);
				string fileName = $"{safeName}_{info.Width}x{info.Height}.png";
				string path = Path.Combine(folder, fileName);

				// 同名回避
				int suffix = 1;
				while (File.Exists(path)) {
					fileName = $"{safeName}_{info.Width}x{info.Height}_{suffix}.png";
					path = Path.Combine(folder, fileName);
					suffix++;
				}

				File.WriteAllBytes(path, png);
				exported++;
			} catch {
				// 読み取り不可テクスチャ等はスキップ
			}
		}

		EditorUtility.DisplayDialog("Export PNGs", $"Exported {exported} PNG files.", "OK");
		EditorUtility.RevealInFinder(folder);
		Debug.Log($"[UnityBuiltinIconLister] Exported {exported} PNGs to: {folder}");
	}

	private static string EscapeTsv(string s) {
		if(string.IsNullOrEmpty(s)) return "";
		return s.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
	}

	private static string MakeSafeFileName(string s) {
		if(string.IsNullOrEmpty(s)) return "unnamed";
		foreach (char c in Path.GetInvalidFileNameChars())
			s = s.Replace(c, '_');
		return s;
	}
}
