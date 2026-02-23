using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>
	/// GUI全体の管理を行う静的クラス。
	/// EventSystem の一元管理などを行う。
	/// </summary>
	public static class aGuiManager {
		#region Fields
		private static EventSystem m_eventSystem; // キャッシュされた EventSystem
		private static readonly LinkedList<Selectable> m_selectionHistory = new(); // 選択履歴
		private static Selectable m_currentSelectable; // 現在選択されている Selectable
		private static int m_maxHistorySize = 10; // 履歴の最大保持数
		#endregion

		#region Properties
		/// <summary> 現在アクティブな EventSystem を取得する </summary>
		public static EventSystem EventSystem {
			get {
				if(m_eventSystem == null) UpdateEventSystem();
				return m_eventSystem;
			}
		}

		/// <summary> 現在選択されている Selectable を取得する </summary>
		public static Selectable CurrentSelectable => m_currentSelectable;

		/// <summary> 選択履歴の件数を取得する </summary>
		public static int SelectionHistoryCount => m_selectionHistory.Count;

		/// <summary> 前の選択に戻れるかどうかを取得する </summary>
		public static bool CanGoBack => m_selectionHistory.Count > 0;

		/// <summary> 履歴の最大保持数を取得・設定する（1以上） </summary>
		public static int MaxHistorySize {
			get => m_maxHistorySize;
			set => m_maxHistorySize = Mathf.Max(1, value);
		}
		#endregion

		#region Lifecycle
		/// <summary> シーンロード後に EventSystem を初期化する </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Init() {
			UpdateEventSystem();
			ClearSelectionHistory();
		}
		#endregion

		#region Public Methods
		/// <summary> 選択された Selectable を履歴に記録する </summary>
		/// <param name="selectable">選択された Selectable</param>
		public static void SetSelectedSelectable(Selectable selectable) {
			if(selectable == null) return;
			if(m_currentSelectable == selectable) return;

			if(m_currentSelectable != null) {
				m_selectionHistory.AddLast(m_currentSelectable);
				while(m_selectionHistory.Count > m_maxHistorySize) {
					m_selectionHistory.RemoveFirst();
				}
			}
			m_currentSelectable = selectable;
		}

		/// <summary> 前の選択に戻る </summary>
		/// <returns>戻り先の Selectable。履歴がない場合は null</returns>
		public static Selectable GoBack() {
			if(m_selectionHistory.Count == 0) return null;

			// 破棄済みの要素をスキップする
			while(m_selectionHistory.Count > 0) {
				var previous = m_selectionHistory.Last.Value;
				m_selectionHistory.RemoveLast();
				if(previous != null) {
					m_currentSelectable = previous;
					return previous;
				}
			}

			m_currentSelectable = null;
			return null;
		}

		/// <summary> 選択履歴をクリアする </summary>
		public static void ClearSelectionHistory() {
			m_selectionHistory.Clear();
			m_currentSelectable = null;
		}

		/// <summary> シーン内の EventSystem を検索し、キャッシュを更新する </summary>
		public static void UpdateEventSystem() {
			m_eventSystem = null;

			// DontDestroyシーンの取得用にオブジェクトを生成する
			var tempDontDestroyObject = new GameObject("DontDestroyTemp");
			Object.DontDestroyOnLoad(tempDontDestroyObject);

			var dontDestroyScene = tempDontDestroyObject.scene;

			Object.Destroy(tempDontDestroyObject);

			// 最初にDontDestroyシーンを走査
			if(dontDestroyScene.IsValid() && dontDestroyScene.isLoaded) {
				var dontDestroyGameObjects = dontDestroyScene.GetRootGameObjects();

				for (int i = 0; i < dontDestroyGameObjects.Length; i++) {
					var es = dontDestroyGameObjects[i].GetComponent<EventSystem>();
					if(es != null) {
						m_eventSystem = es;
						return;
					}
				}
			}

			if(m_eventSystem != null) return;

			// 全てのEventSystemを取得
			var allEventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

			foreach (var es in allEventSystems) {
				m_eventSystem = es;
			}
		}
		#endregion
	}
}
