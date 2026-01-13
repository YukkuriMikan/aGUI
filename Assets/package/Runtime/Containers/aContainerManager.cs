using System.Collections.Generic;
using System.Linq;
using ANest.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全てのコンテナを管理する静的クラス。
/// 表示順序の管理やシーン切り替え時のクリーンアップを行う。
/// </summary>
public static class aContainerManager {
	#region Fields
	private static Dictionary<aContainerBase, double> m_aContainerDictionary = new(); // コンテナと登録時間を保持する辞書
	#endregion

	#region Properties
	/// <summary> 登録されている全てのコンテナを表示順（登録時間順）に取得する </summary>
	public static IEnumerable<aContainerBase> Containers => m_aContainerDictionary
		.Where(pair => pair.Key != null)
		.Where(pair => pair.Key.IsVisible)
		.OrderBy(pair => pair.Value)
		.Select(pair => pair.Key);

	/// <summary> 管理されているコンテナの数 </summary>
	public static int Count => m_aContainerDictionary.Count;
	#endregion

	#region Public Methods
	/// <summary> 指定されたコンテナが、DisallowNullSelectionが有効なコンテナの中で最新（最後に登録されたもの）かどうかを判定する </summary>
	/// <param name="container">判定するコンテナ</param>
	/// <returns>最新であればtrue</returns>
	public static bool IsLatestContainer(aContainerBase container) {
		if (container == null || !container.DisallowNullSelection || !container.IsVisible) return false;

		var latest = m_aContainerDictionary
			.Where(pair => pair.Key != null && pair.Key.IsVisible && pair.Key.DisallowNullSelection)
			.OrderByDescending(pair => pair.Value)
			.Select(pair => pair.Key)
			.FirstOrDefault();

		return latest == container;
	}

	/// <summary> 管理対象にコンテナを追加する </summary>
	/// <param name="container">追加するコンテナ</param>
	public static void Add(aContainerBase container)
		=> m_aContainerDictionary[container] = Time.realtimeSinceStartupAsDouble;

	/// <summary> 管理対象からコンテナを削除する </summary>
	/// <param name="container">削除するコンテナ</param>
	public static void Remove(aContainerBase container)
		=> m_aContainerDictionary.Remove(container);

	/// <summary> 全てのコンテナを管理対象から除外する </summary>
	public static void Clear()
		=> m_aContainerDictionary.Clear();
	#endregion

	#region Event Handlers
	/// <summary> シーンが切り替わった際の処理。管理状態をクリアする </summary>
	private static void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
		Clear();
	}
	#endregion
}
