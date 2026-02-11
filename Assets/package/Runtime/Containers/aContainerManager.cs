using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ANest.UI {
	/// <summary>全てのコンテナを管理し、表示順やシーンクリアを担う静的クラス</summary>
	public static class aContainerManager {
		#region Fields
		private static Dictionary<aContainerBase, double> m_aContainerDictionary = new(); // コンテナと登録時間を保持する辞書
		#endregion

		#region Properties
		/// <summary>登録されている全てのコンテナを表示順（登録時間順）に取得する</summary>
		public static IEnumerable<aContainerBase> Containers => m_aContainerDictionary
			.Where(pair => pair.Key != null)
			.Where(pair => pair.Key.IsVisible)
			.OrderBy(pair => pair.Value)
			.Select(pair => pair.Key);

		/// <summary>管理されているコンテナの数</summary>
		public static int Count => m_aContainerDictionary.Count;
		#endregion

		#region Public Methods
		/// <summary>管理対象にコンテナを追加する</summary>
		/// <param name="container">追加するコンテナ</param>
		public static void Add(aContainerBase container)
			=> m_aContainerDictionary[container] = Time.realtimeSinceStartupAsDouble;

		/// <summary>Null選択防止が有効なコンテナの中で、優先対象かどうか</summary>
		public static bool IsHighestPriorityDisallowNullSelectionContainer(aContainerBase container) {
			if(container == null) return false;
			if(!m_aContainerDictionary.ContainsKey(container)) return false;

			var latest = m_aContainerDictionary
				.Where(pair => pair.Key != null)
				.Where(pair => pair.Key.IsVisible)
				.Where(pair => pair.Key is IDisallowNullSelectionContainer disallow && disallow.DisallowNullSelection)
				.OrderByDescending(pair => pair.Value)
				.Select(pair => pair.Key)
				.FirstOrDefault();

			return latest == container;
		}

		/// <summary>管理対象からコンテナを削除する</summary>
		/// <param name="container">削除するコンテナ</param>
		public static void Remove(aContainerBase container)
			=> m_aContainerDictionary.Remove(container);

		/// <summary>全てのコンテナを管理対象から除外する</summary>
		public static void Clear()
			=> m_aContainerDictionary.Clear();
		#endregion

		#region Event Handlers
		/// <summary>シーンが切り替わった際の処理。管理状態をクリアする</summary>
		private static void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
			Clear();
		}
		#endregion
	}
}
