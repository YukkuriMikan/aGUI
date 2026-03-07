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
		}

		/// <summary>Null選択防止が有効なコンテナの中で、優先対象かどうか</summary>
		public static bool IsHighestPriorityDisallowNullSelectionContainer(aContainerBase container) {
			if(container == null) return false;
			if(!m_addTimeDictionary.ContainsKey(container)) return false;

			//時間が最新のコンテナを取得
			var latest = m_addTimeDictionary
				.Where(pair => pair.Key != null)
				.Where(pair => pair.Key.IsVisible)
				.Where(pair => pair.Key is IDisallowNullSelectionContainer { DisallowNullSelection: true })
				.OrderByDescending(pair => pair.Value)
				.Select(pair => pair.Key)
				.FirstOrDefault();

			return latest == container;
		}

		/// <summary>管理対象からコンテナを削除する</summary>
		/// <param name="container">削除するコンテナ</param>
		public static void Remove(aContainerBase container) {
			if(container == null) return;
			if(m_containers.IndexOf(container) < 0) {
				m_containers.Remove(container);
			}
			m_containerNameDictionary.Remove(container.name);
			m_addTimeDictionary.Remove(container);
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
			m_containerNameDictionary.Clear();
			m_addTimeDictionary.Clear();
		}
		#endregion

		#region Event Handlers
		/// <summary>シーンが切り替わった際の処理。管理状態をクリアする</summary>
		private static void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
			Clear();
		}
		#endregion
	}
}
