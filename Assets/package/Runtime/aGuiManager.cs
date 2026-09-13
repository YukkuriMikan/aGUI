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
		private static readonly LinkedList<Selectable> m_freeHistoryNodes = new();
		private static double m_nextEventSystemSearch;
		private static Selectable m_currentSelectable; // 現在選択されている Selectable
		private static int m_maxHistorySize = 10; // 履歴の最大保持数
		#endregion

		#region Properties
		/// <summary> 現在アクティブな EventSystem を取得する </summary>
		public static EventSystem EventSystem {
			get {
				if(m_eventSystem == null) {
					// 新設された有効なEventSystemは、検索の待機時間中でも即座に利用できる。
					if(UnityEngine.EventSystems.EventSystem.current != null || Time.realtimeSinceStartupAsDouble >= m_nextEventSystemSearch)
						UpdateEventSystem();
				}
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
				LinkedListNode<Selectable> node;
				if(m_selectionHistory.Count >= m_maxHistorySize) {
					node = m_selectionHistory.First;
					m_selectionHistory.Remove(node);
				} else if(m_freeHistoryNodes.Count > 0) {
					node = m_freeHistoryNodes.First;
					m_freeHistoryNodes.Remove(node);
				} else {
					node = new LinkedListNode<Selectable>(null);
				}
				node.Value = m_currentSelectable;
				m_selectionHistory.AddLast(node);
				while(m_selectionHistory.Count > m_maxHistorySize) {
					RecycleHistoryNode(m_selectionHistory.First);
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
				RecycleHistoryNode(m_selectionHistory.Last);
				if(previous != null) {
					m_currentSelectable = previous;
					return previous;
				}
			}

			m_currentSelectable = null;
			return null;
		}

		/// <summary>対象コンテナの履歴を遡り、選択可能な項目へ戻る</summary>
		internal static T GoBack<T>(IReadOnlyList<T> candidates) where T : Selectable {
			if(candidates == null) return null;
			var node = m_selectionHistory.Last;
			while(node != null) {
				var previousNode = node.Previous;
				var selectable = node.Value;
				if(selectable == null) {
					RecycleHistoryNode(node);
				} else if(selectable is T candidate && Contains(candidates, candidate)) {
					RecycleHistoryNode(node);
					if(candidate.IsActive() && candidate.IsInteractable() && aGuiSelectableUtils.CanReceiveFocus(candidate)) {
						m_currentSelectable = candidate;
						return candidate;
					}
				}
				// 別のコンテナの履歴は消費しない。
				node = previousNode;
			}
			return null;
		}

		private static bool Contains<T>(IReadOnlyList<T> candidates, T target) where T : Selectable {
			for(var i = 0; i < candidates.Count; i++) {
				if(candidates[i] == target) return true;
			}
			return false;
		}

		/// <summary> 選択履歴をクリアする </summary>
		public static void ClearSelectionHistory() {
			while(m_selectionHistory.Count > 0) RecycleHistoryNode(m_selectionHistory.First);
			m_currentSelectable = null;
		}


		private static void RecycleHistoryNode(LinkedListNode<Selectable> node) {
			m_selectionHistory.Remove(node);
			node.Value = null;
			if(m_freeHistoryNodes.Count < m_maxHistorySize) m_freeHistoryNodes.AddLast(node);
		}

		/// <summary> シーン内の EventSystem を検索し、キャッシュを更新する </summary>
		public static void UpdateEventSystem() {

			m_eventSystem = null;
			m_nextEventSystemSearch = Time.realtimeSinceStartupAsDouble + 0.5;
			// シーンの取得だけのためにDontDestroyOnLoadオブジェクトを生成しない。
			var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach(var es in systems) {
				var scene = es.gameObject.scene;
				if(scene.IsValid() && scene.isLoaded && scene.name == "DontDestroyOnLoad" && es.transform.parent == null) {
					m_eventSystem = es;
					return;
				}
				if(es.gameObject.activeInHierarchy) m_eventSystem = es;
			}
		}
		#endregion
	}
}
