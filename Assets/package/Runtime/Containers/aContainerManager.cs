using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ANest.UI {
	/// <summary>全てのコンテナを管理し、表示順やシーンクリアを担う静的クラス</summary>
	public static class aContainerManager {
		#region Static Constructor
		/// <summary> 静的コンストラクタ </summary>
		static aContainerManager() {
			SceneManager.activeSceneChanged += OnActiveSceneChanged;
		}
		#endregion

		#region Fields
		private static List<aContainerBase> m_containers = new();
		private static readonly List<aContainerBase> m_selectionPriority = new();
		private static Dictionary<string, aContainerBase> m_containerNameDictionary = new();
		private static Dictionary<aContainerBase, double> m_addTimeDictionary = new(); // コンテナと登録時間を保持する辞書
		#endregion

		#region Properties
		/// <summary>登録されている全てのコンテナを取得する</summary>
		public static IEnumerable<aContainerBase> Containers
			=> m_containers;

		/// <summary>管理されているコンテナの数</summary>
		public static int Count => m_containers.Count;
		#endregion

		#region Public Methods
		/// <summary>管理対象にコンテナを追加する</summary>
		/// <param name="container">追加するコンテナ</param>
		public static void Add(aContainerBase container) {
			if(container == null) return;
			if(m_containers.IndexOf(container) < 0) {
				m_containers.Add(container);
			}
			m_containerNameDictionary[container.name] = container;
			m_addTimeDictionary[container] = Time.realtimeSinceStartupAsDouble;
			m_selectionPriority.Remove(container);
			m_selectionPriority.Add(container);
		}

		/// <summary>Null選択防止が有効なコンテナの中で、優先対象かどうか</summary>
		public static bool IsHighestPriorityDisallowNullSelectionContainer(aContainerBase container) {
			if(container == null) return false;
			if(!m_addTimeDictionary.ContainsKey(container)) return false;


			// 最新の登録から調べ、有効な対象が見つかった時点で終える。
			// 有効状態は都度確認し、同一フレームの無効化・再有効化にも追従する。
			for(var i = m_selectionPriority.Count - 1; i >= 0; i--) {
				var candidate = m_selectionPriority[i];
				if(candidate == null || !candidate.isActiveAndEnabled || !candidate.IsVisible) continue;
				if(candidate is not IDisallowNullSelectionContainer { DisallowNullSelection: true }) continue;
				return candidate == container;
			}
			return false;
		}

		/// <summary>管理対象からコンテナを削除する</summary>
		/// <param name="container">削除するコンテナ</param>
		public static void Remove(aContainerBase container) {
			if(container == null) return;
			if(m_containers.IndexOf(container) >= 0) {
				m_containers.Remove(container);
			}
			m_addTimeDictionary.Remove(container);
			m_selectionPriority.Remove(container);
			// 別の同名コンテナが登録されている場合は、その登録を維持する。
			if(m_containerNameDictionary.TryGetValue(container.name, out var registered) && registered == container) {
				RestoreContainerName(container.name);
			}
		}

		private static void RestoreContainerName(string containerName) {
			aContainerBase latest = null;
			double latestTime = double.NegativeInfinity;
			foreach(var pair in m_addTimeDictionary) {
				var candidate = pair.Key;
				if(candidate == null || candidate.name != containerName) continue;
				if(pair.Value >= latestTime) {
					latestTime = pair.Value;
					latest = candidate;
				}
			}
			if(latest != null) m_containerNameDictionary[containerName] = latest;
			else m_containerNameDictionary.Remove(containerName);
		}

		/// <summary>コンテナ名から管理対象のコンテナを取得する</summary>
		/// <param name="containerName">取得するコンテナ名</param>
		/// <returns>該当するコンテナ。見つからない場合はnull</returns>
		public static aContainerBase GetContainer(string containerName) {
			if(string.IsNullOrEmpty(containerName)) return null;
			m_containerNameDictionary.TryGetValue(containerName, out var container);
			return container;
		}

		/// <summary>型引数に合ったコンテナを返す</summary>
		/// <typeparam name="T">取得するコンテナの型</typeparam>
		/// <returns>型引数に一致するコンテナ</returns>
		/// <remarks>複数の該当があった場合は順不定</remarks>
		public static T GetContainer<T>() where T : aContainerBase
			=> m_containers.OfType<T>().First();

		/// <summary>型引数に合うコンテナを列挙で返す</summary>
		/// <typeparam name="T">取得するコンテナの型</typeparam>
		/// <returns>型引数に一致するコンテナの列挙</returns>
		public static IEnumerable<T> GetContainers<T>() where T : aContainerBase
			=> m_containers.OfType<T>();

		/// <summary>全てのコンテナを管理対象から除外する</summary>
		public static void Clear() {
			m_containers.Clear();
			m_selectionPriority.Clear();
			m_containerNameDictionary.Clear();
			m_addTimeDictionary.Clear();
		}
		#endregion

		#region Event Handlers
		/// <summary>シーンが切り替わった際の処理。破棄済みのコンテナのみ管理対象から取り除く（DontDestroyOnLoadや残存シーンのコンテナは維持する）</summary>
		private static void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
			RemoveDestroyedContainers();
		}

		/// <summary>破棄済みのコンテナを管理対象から取り除く</summary>
		private static void RemoveDestroyedContainers() {
			m_containers.RemoveAll(c => c == null);
			m_selectionPriority.RemoveAll(c => c == null);

			List<string> staleNames = null;
			foreach(var pair in m_containerNameDictionary) {
				if(pair.Value == null) {
					staleNames ??= new List<string>();
					staleNames.Add(pair.Key);
				}
			}
			if(staleNames != null) {
				foreach(var key in staleNames) {
					m_containerNameDictionary.Remove(key);
				}
			}

			List<aContainerBase> staleContainers = null;
			foreach(var pair in m_addTimeDictionary) {
				if(pair.Key == null) {
					staleContainers ??= new List<aContainerBase>();
					staleContainers.Add(pair.Key);
				}
			}
			if(staleContainers != null) {
				foreach(var key in staleContainers) {
					m_addTimeDictionary.Remove(key);
				}
			}
		}
		#endregion
	}
}
