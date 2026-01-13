using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public class aGuiTest {
	[Test]
	public void EventSystemTest() {
		var go = new GameObject("EventSystem");
		var es = go.AddComponent<EventSystem>();
		try {
			aGuiManager.UpdateEventSystem();
			Assert.AreEqual(es, aGuiManager.EventSystem);
		} finally {
			Object.DestroyImmediate(go);
			aGuiManager.UpdateEventSystem();
		}
	}

	[UnityTest]
	public IEnumerator EventSystemPriorityTest() {
		var goNormal = new GameObject("NormalSceneEventSystem");
		var esNormal = goNormal.AddComponent<EventSystem>();

		var goDontDestroy = new GameObject("DontDestroyEventSystem");
		var esDontDestroy = goDontDestroy.AddComponent<EventSystem>();
		Object.DontDestroyOnLoad(goDontDestroy);

		yield return null;

		try {
			Assert.AreEqual(esDontDestroy, aGuiManager.EventSystem, "DontDestroyOnLoad scene should be prioritized.");
		} finally {
			Object.DestroyImmediate(goNormal);
			Object.DestroyImmediate(goDontDestroy);
			aGuiManager.UpdateEventSystem();
		}
	}
}
