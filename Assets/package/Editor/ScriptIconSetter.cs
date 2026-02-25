using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
			RegisterIcon<aCursorBase>("d_scenepicking_pickable_hover", true);

			LoadCursorIcon();

			EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
			EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
			EditorApplication.projectWindowItemOnGUI -= OnProjectGUI;
			EditorApplication.projectWindowItemOnGUI += OnProjectGUI;
		}
		#endregion

		#region Private Methods
		/// <summary>ビルトインアイコン名を指定してMonoScriptにアイコンを登録する。</summary>
		/// <param name="builtinIconName">Unityビルトインアイコンの名前</param>
		/// <param name="includeDescendants">派生クラスにもアイコンを設定するかどうか</param>
		private static void RegisterIcon<T>(string builtinIconName, bool includeDescendants = false) where T : MonoBehaviour {
			var icon = EditorGUIUtility.IconContent(builtinIconName)?.image as Texture2D;
			if(icon == null) return;
			s_iconMap[typeof(T)] = icon;
			if(includeDescendants)
				SetMonoScriptIconWithDescendants<T>(icon);
			else
				SetMonoScriptIcon(typeof(T), icon);
		}

		/// <summary>アセットGUIDを指定してMonoScriptにアイコンを登録する。</summary>
		/// <param name="guid">アイコンアセットのGUID</param>
		/// <param name="includeDescendants">派生クラスにもアイコンを設定するかどうか</param>
		private static void RegisterIconByGuid<T>(string guid, bool includeDescendants = false) where T : MonoBehaviour {
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if(string.IsNullOrEmpty(path)) return;
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			if(icon == null) return;
			s_iconMap[typeof(T)] = icon;
			if(includeDescendants)
				SetMonoScriptIconWithDescendants<T>(icon);
			else
				SetMonoScriptIcon(typeof(T), icon);
		}

		/// <summary>指定した型のMonoScriptにアイコンを設定する。</summary>
		/// <param name="targetType">対象の型</param>
		/// <param name="icon">設定するアイコン</param>
		private static void SetMonoScriptIcon(Type targetType, Texture2D icon) {
			var scripts = MonoImporter.GetAllRuntimeMonoScripts()
				.Where(s => s.GetClass() == targetType);
			foreach (var script in scripts) {
				RegisterScriptGuid(script, icon);
				var currentIcon = EditorGUIUtility.GetIconForObject(script);
				if(currentIcon != null && currentIcon.name == icon.name) continue;
				EditorGUIUtility.SetIconForObject(script, icon);
				EditorUtility.SetDirty(script);
			}
		}

		/// <summary>指定した型とその派生クラスのMonoScriptにアイコンを設定する。</summary>
		/// <param name="icon">設定するアイコン</param>
		private static void SetMonoScriptIconWithDescendants<T>(Texture2D icon) where T : MonoBehaviour {
			var baseType = typeof(T);
			var scripts = MonoImporter.GetAllRuntimeMonoScripts()
				.Where(s => {
					var c = s.GetClass();
					return c != null && baseType.IsAssignableFrom(c);
				});
			foreach (var script in scripts) {
				RegisterScriptGuid(script, icon);
				var currentIcon = EditorGUIUtility.GetIconForObject(script);
				if(currentIcon != null && currentIcon.name == icon.name) continue;
				EditorGUIUtility.SetIconForObject(script, icon);
				EditorUtility.SetDirty(script);
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
			var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
			if(go == null) return;

			if(PrefabUtility.IsAnyPrefabInstanceRoot(go)) return;

			Texture2D icon = null;

			foreach (var pair in s_iconMap) {
				if(go.TryGetComponent(pair.Key, out _)) {
					icon = pair.Value;
					break;
				}
			}

			if(icon == null && s_cursorIcon != null) {
				if(IsCursorRectTarget(go)) {
					icon = s_cursorIcon;
				}
			}

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
			if(!s_scriptIconMap.TryGetValue(guid, out var icon)) return;
			if(icon == null) return;

			var iconRect = new Rect(selectionRect.x + 2, selectionRect.y, selectionRect.height, selectionRect.height);
			var bgColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
			EditorGUI.DrawRect(iconRect, bgColor);
			GUI.DrawTexture(iconRect, icon);
		}

		/// <summary>指定GameObjectがいずれかのaCursorBase派生コンポーネントのCursorRectの対象かどうかを判定する。</summary>
		/// <param name="go">判定対象のGameObject</param>
		private static bool IsCursorRectTarget(GameObject go) {
			var cursors = UnityEngine.Object.FindObjectsByType<aCursorBase>(FindObjectsSortMode.None);
			foreach (var cursor in cursors) {
				var so = new SerializedObject((UnityEngine.Object)cursor);
				var prop = so.FindProperty("m_cursorRect");
				if(prop != null && prop.objectReferenceValue is RectTransform rt && rt.gameObject == go) {
					return true;
				}
			}
			return false;
		}
		#endregion
	}
}
