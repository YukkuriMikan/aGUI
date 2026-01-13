using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// GUI全体の管理を行う静的クラス。
/// EventSystem の一元管理などを行う。
/// </summary>
public static class aGuiManager {
	#region Fields
	private static EventSystem _eventSystem; // キャッシュされた EventSystem
	#endregion

	#region Properties
	/// <summary> 現在アクティブな EventSystem を取得する </summary>
	public static EventSystem EventSystem {
		get {
			if(_eventSystem == null) UpdateEventSystem();
			return _eventSystem;
		}
	}
	#endregion

	#region Lifecycle
	/// <summary> シーンロード後に EventSystem を初期化する </summary>
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Init() {
		UpdateEventSystem();
	}
	#endregion

	#region Public Methods
	/// <summary> シーン内の EventSystem を検索し、キャッシュを更新する </summary>
	public static void UpdateEventSystem() {
		_eventSystem = null;

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
					_eventSystem = es;
					return;
				}
			}
		}

		if(_eventSystem != null) return;

		// 全てのEventSystemを取得
		var allEventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

		foreach (var es in allEventSystems) {
			_eventSystem = es;
		}
	}
	#endregion
}
