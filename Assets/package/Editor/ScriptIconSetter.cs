using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary>スクリプトアイコンをビルトインアイコンやアセットGUIDで設定し、Hierarchyにも反映する。</summary>
	[InitializeOnLoad]
	internal static class ScriptIconSetter {
		#region Fields
		private static readonly Dictionary<Type, Texture2D> s_iconMap = new(); // 型とアイコンの対応マップ
		private static Texture2D s_cursorIcon; // aCursorBase用アイコン
		#endregion

		#region Constructor
		/// <summary>アイコンの登録とHierarchyコールバックの設定を行う。</summary>
		static ScriptIconSetter() {
			RegisterIconByGuid<aContainerBase>("429a97a9e532b3a45a079b775dc39ed3", true);
			RegisterIcon<aButton>("d_Button Icon");
			RegisterIcon<aToggle>("d_Toggle Icon");
			RegisterIcon<aLayoutGroupVertical>("d_VerticalLayoutGroup Icon");
			RegisterIcon<aLayoutGroupHorizontal>("d_HorizontalLayoutGroup Icon");
			RegisterIcon<aLayoutGroupGrid>("d_GridLayoutGroup Icon");
			RegisterIconByGuid<aLayoutGroupCircular>("d66d360f8ee5409489c4eb4c449951bc");

			LoadCursorIcon("1f4e28ed76cb8b040aeef8ee98d3420d");

			EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
			EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
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
			foreach(var script in scripts) {
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
			foreach(var script in scripts) {
				var currentIcon = EditorGUIUtility.GetIconForObject(script);
				if(currentIcon != null && currentIcon.name == icon.name) continue;
				EditorGUIUtility.SetIconForObject(script, icon);
				EditorUtility.SetDirty(script);
			}
		}

		/// <summary>aCursorBase用アイコンをGUIDから読み込む。</summary>
		/// <param name="guid">aCursor.pngアセットのGUID</param>
		private static void LoadCursorIcon(string guid) {
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if(string.IsNullOrEmpty(path)) return;
			s_cursorIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		/// <summary>Hierarchyウィンドウの各行にアイコンを描画する。</summary>
		/// <param name="instanceID">対象オブジェクトのインスタンスID</param>
		/// <param name="selectionRect">Hierarchy行の描画領域</param>
		private static void OnHierarchyGUI(int instanceID, Rect selectionRect) {
			var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
			if(go == null) return;

			Texture2D icon = null;

			foreach(var pair in s_iconMap) {
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
				var iconRect = new Rect(selectionRect.x - 2, selectionRect.y, selectionRect.height, selectionRect.height);
				var bgColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
				EditorGUI.DrawRect(iconRect, bgColor);
				GUI.DrawTexture(iconRect, icon);
			}
		}
		/// <summary>指定GameObjectがいずれかのaCursorBase派生コンポーネントのCursorRectの対象かどうかを判定する。</summary>
		/// <param name="go">判定対象のGameObject</param>
		private static bool IsCursorRectTarget(GameObject go) {
			var cursors = UnityEngine.Object.FindObjectsByType<aCursorBase>(FindObjectsSortMode.None);
			foreach(var cursor in cursors) {
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
