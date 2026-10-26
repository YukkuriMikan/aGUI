using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary>スクリプトアイコンをビルトインアイコンやアセットGUIDで設定し、HierarchyとProjectビューにも反映する。</summary>
	[InitializeOnLoad]
	internal static class ScriptIconSetter {
		private const int ICON_PADDING_X = 0;
		private const int ICON_PADDING_Y = 0;

		#region Fields
		private static readonly Dictionary<Type, Texture2D> s_iconMap = new();         // 型とアイコンの対応マップ
		private static readonly Dictionary<string, Texture2D> s_scriptIconMap = new(); // MonoScriptのGUIDとアイコンの対応マップ
		private static readonly Dictionary<Type, Texture2D> s_scriptTypeIcons = new();
		private static readonly Dictionary<string, Texture2D> s_scriptNameIcons = new();
		private static readonly Dictionary<int, Texture2D> s_hierarchyIcons = new();   // アイコンがない行もキャッシュする
		private static readonly HashSet<int> s_cursorRectTargetIds = new();            // CursorRect対象GameObjectのインスタンスIDキャッシュ
		private static readonly HashSet<int> s_nextCursorRectTargetIds = new();
		private static readonly HashSet<int> s_cursorInstanceIds = new();
		private static bool s_cursorTargetsDirty = true;                               // CursorRectキャッシュの再構築が必要か
		private static bool s_cursorRefreshQueued;
		private static double s_nextPlayModeRefresh;
		private static Texture2D s_cursorIcon;                                         // aCursorBase用アイコン
		#endregion

		#region Constructor
		/// <summary>アイコンの登録とHierarchy・Projectビューコールバックの設定を行う。</summary>
		static ScriptIconSetter() {
			RegisterIcon<aContainerBase>("d_LODGroup Icon", true);
			RegisterIcon<aButton>("d_Button Icon", true);
			RegisterIcon<aToggle>("d_Toggle Icon", true);
			RegisterIcon<aLayoutGroupVertical>("d_VerticalLayoutGroup Icon");
			RegisterIcon<aLayoutGroupHorizontal>("d_HorizontalLayoutGroup Icon");
			RegisterIcon<aLayoutGroupGrid>("d_GridLayoutGroup Icon");
			RegisterIcon<aLayoutGroupCircular>("d_Refresh");
			RegisterIcon<aContentSizeFitter>("d_ContentSizeFitter Icon");
			RegisterIcon<aUiLineRenderer>("d_LineRenderer Icon");
			RegisterIcon<aCursorBase>("d_scenepicking_pickable_hover", true);

			LoadCursorIcon();
			// アセット操作は初期化コンストラクタから分離し、全スクリプトを一度だけ列挙する。
			EditorApplication.delayCall += InitializeScriptIcons;

			EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
			EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
			EditorApplication.projectWindowItemOnGUI -= OnProjectGUI;
			EditorApplication.projectWindowItemOnGUI += OnProjectGUI;

			// CursorRect対象キャッシュの無効化トリガーを登録
			EditorApplication.hierarchyChanged -= OnHierarchyChanged;
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
			ObjectChangeEvents.changesPublished -= OnObjectChanged;
			ObjectChangeEvents.changesPublished += OnObjectChanged;
			PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
			PrefabStage.prefabStageOpened += OnPrefabStageChanged;
			PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
			PrefabStage.prefabStageClosing += OnPrefabStageChanged;
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}
		#endregion

		#region Private Methods
		/// <summary>ビルトインアイコン名を指定してMonoScriptにアイコンを登録する。</summary>
		/// <param name="builtinIconName">Unityビルトインアイコンの名前</param>
		/// <param name="includeDescendants">派生クラスにもアイコンを設定するかどうか</param>
		private static void RegisterIcon<T>(string builtinIconName, bool includeDescendants = false) where T : MonoBehaviour {
			var icon = EditorGUIUtility.IconContent(builtinIconName)?.image as Texture2D;
			if(icon == null) return;
			RegisterIconType<T>(icon, includeDescendants);
		}

		/// <summary>アセットGUIDを指定してMonoScriptにアイコンを登録する。</summary>
		/// <param name="guid">アイコンアセットのGUID</param>
		/// <param name="includeDescendants">派生クラスにもアイコンを設定するかどうか</param>
		private static void RegisterIconByGuid<T>(string guid, bool includeDescendants = false) where T : MonoBehaviour {
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if(string.IsNullOrEmpty(path)) return;
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			if(icon == null) return;
			RegisterIconType<T>(icon, includeDescendants);
		}

		/// <summary>Unityの型キャッシュを使い、スクリプト列挙前に型とアイコンの対応を作る。</summary>
		private static void RegisterIconType<T>(Texture2D icon, bool includeDescendants) where T : MonoBehaviour {
			s_iconMap[typeof(T)] = icon;
			s_scriptTypeIcons[typeof(T)] = icon;
			if(!includeDescendants) return;
			RegisterScriptName(typeof(T), icon);
			foreach(var type in TypeCache.GetTypesDerivedFrom<T>()) {
				s_scriptTypeIcons[type] = icon;
				RegisterScriptName(type, icon);
			}
		}

		/// <summary>GetClassがnullになるジェネリック型定義もスクリプト名で解決できるようにする。</summary>
		private static void RegisterScriptName(Type type, Texture2D icon) {
			s_scriptNameIcons[type.Name] = icon;
			int backtickIndex = type.Name.IndexOf('`');
			if(backtickIndex > 0) s_scriptNameIcons[type.Name.Substring(0, backtickIndex)] = icon;
		}

		/// <summary>全スクリプトを一巡してアイコンを設定する。</summary>
		private static void InitializeScriptIcons() {
			if(EditorApplication.isCompiling || EditorApplication.isUpdating) {
				EditorApplication.delayCall += InitializeScriptIcons;
				return;
			}
			foreach(var script in MonoImporter.GetAllRuntimeMonoScripts()) {
				var type = script.GetClass();
				Texture2D icon;
				if(type != null) {
					if(!s_scriptTypeIcons.TryGetValue(type, out icon)) continue;
				} else if(!s_scriptNameIcons.TryGetValue(script.name, out icon)) continue;
				RegisterScriptGuid(script, icon);
				var currentIcon = EditorGUIUtility.GetIconForObject(script);
				if(currentIcon != null && currentIcon.name == icon.name) continue;
				// SetDirtyはスクリプトの.metaへの書き込み（差分ノイズ）になるため行わない。アイコンはドメインリロード毎に再設定される
				EditorGUIUtility.SetIconForObject(script, icon);
			}
		}

		/// <summary>MonoScriptのGUIDをs_scriptIconMapに登録する。</summary>
		/// <param name="script">対象のMonoScript</param>
		/// <param name="icon">設定するアイコン</param>
		private static void RegisterScriptGuid(MonoScript script, Texture2D icon) {
			var path = AssetDatabase.GetAssetPath(script);
			if(string.IsNullOrEmpty(path)) return;
			var guid = AssetDatabase.AssetPathToGUID(path);
			if(!string.IsNullOrEmpty(guid)) {
				s_scriptIconMap[guid] = icon;
			}
		}

		/// <summary>aCursorBase用アイコン（CursorRect対象のHierarchy描画用）を読み込む。</summary>
		private static void LoadCursorIcon() {
			s_cursorIcon = EditorGUIUtility.IconContent("d_scenepicking_pickable_hover")?.image as Texture2D;
		}

		/// <summary>Hierarchyウィンドウの各行にアイコンを描画する。</summary>
		/// <param name="instanceID">対象オブジェクトのインスタンスID</param>
		/// <param name="selectionRect">Hierarchy行の描画領域</param>
		private static void OnHierarchyGUI(int instanceID, Rect selectionRect) {
			var icon = GetHierarchyIcon(instanceID, Event.current.type);

			if(icon != null) {
				//PADDINGに合わせて位置を自分で微調整する
				var iconRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.height - ICON_PADDING_X, selectionRect.height - ICON_PADDING_Y);
				var bgColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
				EditorGUI.DrawRect(iconRect, bgColor);
				GUI.DrawTexture(iconRect, icon);
			}
		}
		/// <summary>Projectウィンドウの各行にアイコンを描画する。</summary>
		/// <param name="guid">対象アセットのGUID</param>
		/// <param name="selectionRect">Project行の描画領域</param>
		private static void OnProjectGUI(string guid, Rect selectionRect) {
			if(Event.current.type != EventType.Repaint) return;
			if(!s_scriptIconMap.TryGetValue(guid, out var icon)) return;
			if(icon == null) return;

			// List表示時のみ左側に追加描画する。
			// Grid(アイコン)表示では既定のアイコン描画に任せないと、ラベル領域に重なってファイル名が隠れる。
			bool isListMode = selectionRect.width > selectionRect.height * 2f;
			Rect iconRect;
			if(isListMode) {
				iconRect = new Rect(selectionRect.x + 2, selectionRect.y, selectionRect.height, selectionRect.height);
			} else {
				// Grid表示時は下部ラベル領域を避けて、可能な範囲で拡大表示する
				const float labelReservedHeight = 18f;
				const float margin = 2f;
				float maxWidth = Mathf.Max(0f, selectionRect.width - margin * 2f);
				float maxHeight = Mathf.Max(0f, selectionRect.height - labelReservedHeight - margin);
				float size = Mathf.Min(maxWidth, maxHeight);
				if(size <= 0f) return;

				float x = selectionRect.x + (selectionRect.width - size) * 0.5f;
				float y = selectionRect.y + margin;
				iconRect = new Rect(x, y, size, size);
			}

			var bgColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
			EditorGUI.DrawRect(iconRect, bgColor);
			GUI.DrawTexture(iconRect, icon);
		}

		/// <summary>アイコンがない行も含め、初回だけコンポーネントを検索する。</summary>
		private static Texture2D GetHierarchyIcon(int instanceID, EventType eventType) {
			if(eventType != EventType.Repaint) return null;
			// Play中のUndoを伴わないAddComponent・参照変更も、表示中だけ最大毎秒1回反映する。
			if(EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= s_nextPlayModeRefresh) {
				s_nextPlayModeRefresh = EditorApplication.timeSinceStartup + 1d;
				OnHierarchyChanged();
			}
			// Hierarchyが表示されていない間は、変更通知だけではシーンを検索しない。
			if(s_cursorTargetsDirty) QueueCursorTargetRefresh();
			if(s_hierarchyIcons.TryGetValue(instanceID, out var icon)) return icon;
			var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
			if(go != null && !PrefabUtility.IsAnyPrefabInstanceRoot(go)) {
				foreach(var pair in s_iconMap) {
					if(!go.TryGetComponent(pair.Key, out _)) continue;
					icon = pair.Value;
					break;
				}
				if(icon == null && s_cursorRectTargetIds.Contains(instanceID)) icon = s_cursorIcon;
			}
			s_hierarchyIcons[instanceID] = icon;
			return icon;
		}

		/// <summary>構造変更時だけ行とCursorRect対象のキャッシュを無効化する。</summary>
		private static void OnHierarchyChanged() {
			s_hierarchyIcons.Clear();
			MarkCursorTargetsDirty();
		}

		/// <summary>CursorRect対象を無効化し、表示中のHierarchyに再描画を要求する。</summary>
		private static void MarkCursorTargetsDirty() {
			if(s_cursorTargetsDirty) return;
			s_cursorTargetsDirty = true;
			EditorApplication.RepaintHierarchyWindow();
		}

		/// <summary>行描画の外で再検索する。複数行・複数の変更通知を一つの予約にまとめる。</summary>
		private static void QueueCursorTargetRefresh() {
			if(s_cursorRefreshQueued) return;
			s_cursorRefreshQueued = true;
			EditorApplication.delayCall += RefreshCursorTargets;
		}

		private static void RefreshCursorTargets() {
			EditorApplication.delayCall -= RefreshCursorTargets;
			s_cursorRefreshQueued = false;
			if(!s_cursorTargetsDirty) return;
			if(EditorApplication.isCompiling || EditorApplication.isUpdating) {
				QueueCursorTargetRefresh();
				return;
			}
			RebuildCursorTargetCache();
			s_cursorTargetsDirty = false;
		}

		/// <summary>カーソル参照・構造に関係する変更だけを拾い、移動や無関係なアセット変更は無視する。</summary>
		private static void OnObjectChanged(ref ObjectChangeEventStream stream) {
			for(var i = 0; i < stream.length; i++) {
				switch(stream.GetEventType(i)) {
					case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
						stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var properties);
						// 無関係な変更ではInstanceIDToObjectなどのネイティブ呼び出しも行わない。
						if(s_cursorInstanceIds.Contains(properties.instanceId)) MarkCursorTargetsDirty();
						break;
					case ObjectChangeKind.ChangeGameObjectStructure:
						stream.GetChangeGameObjectStructureEvent(i, out var structure);
						s_hierarchyIcons.Remove(structure.instanceId);
						MarkCursorTargetsDirty();
						break;
					case ObjectChangeKind.ChangeScene:
					case ObjectChangeKind.CreateGameObjectHierarchy:
					case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
					case ObjectChangeKind.ChangeGameObjectParent:
					case ObjectChangeKind.DestroyGameObjectHierarchy:
					case ObjectChangeKind.UpdatePrefabInstances:
						OnHierarchyChanged();
						return;
				}
			}
		}

		/// <summary>プレハブステージの開閉時にキャッシュを無効化する。</summary>
		private static void OnPrefabStageChanged(PrefabStage stage) {
			OnHierarchyChanged();
		}

		private static void OnPlayModeChanged(PlayModeStateChange state) {
			s_nextPlayModeRefresh = 0;
			OnHierarchyChanged();
		}

		/// <summary>CursorRect対象のGameObjectインスタンスIDキャッシュを再構築する。</summary>
		private static void RebuildCursorTargetCache() {
			s_nextCursorRectTargetIds.Clear();
			s_cursorInstanceIds.Clear();
			var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
			aCursorBase[] cursors;
			if(prefabStage != null) {
				cursors = prefabStage.prefabContentsRoot.GetComponentsInChildren<aCursorBase>(true);
			} else {
				cursors = UnityEngine.Object.FindObjectsByType<aCursorBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			}
			foreach (var cursor in cursors) {
				s_cursorInstanceIds.Add(cursor.GetInstanceID());
				using var so = new SerializedObject(cursor);
				using var prop = so.FindProperty("m_cursorRect");
				if(prop != null && prop.objectReferenceValue is RectTransform rt) {
					s_nextCursorRectTargetIds.Add(rt.gameObject.GetInstanceID());
				}
			}
			if(s_cursorRectTargetIds.SetEquals(s_nextCursorRectTargetIds)) return;
			// 参照変更で影響を受ける行だけ再評価し、他の行のコンポーネント検索結果は残す。
			foreach(var id in s_cursorRectTargetIds) s_hierarchyIcons.Remove(id);
			foreach(var id in s_nextCursorRectTargetIds) s_hierarchyIcons.Remove(id);
			s_cursorRectTargetIds.Clear();
			s_cursorRectTargetIds.UnionWith(s_nextCursorRectTargetIds);
			EditorApplication.RepaintHierarchyWindow();
		}
		#endregion
	}
}
