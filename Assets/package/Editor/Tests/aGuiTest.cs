using System.Collections;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

/// <summary>aGuiManagerのEventSystem切替挙動を検証するテスト</summary>
public class aGuiTest {
	#region Tests
	/// <summary>UpdateEventSystemがシーン内のEventSystemを参照することを確認する</summary>
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

	/// <summary>DontDestroyOnLoad内に別のEventSystemがあってもUnityの入力先に従う</summary>
	[UnityTest]
	public IEnumerator EventSystemPriorityTest() {
		var goNormal = new GameObject("NormalSceneEventSystem");
		var esNormal = goNormal.AddComponent<EventSystem>();

		var goDontDestroy = new GameObject("DontDestroyEventSystem");
		var esDontDestroy = goDontDestroy.AddComponent<EventSystem>();
		Object.DontDestroyOnLoad(goDontDestroy);

		yield return null;

		try {
			EventSystem.current = esNormal;
			aGuiManager.UpdateEventSystem();
			Assert.AreEqual(esNormal, aGuiManager.EventSystem);
			EventSystem.current = esDontDestroy;
			Assert.AreEqual(esDontDestroy, aGuiManager.EventSystem);
			esDontDestroy.enabled = false;
			aGuiManager.UpdateEventSystem();
			Assert.AreEqual(esNormal, aGuiManager.EventSystem);
		} finally {
			Object.DestroyImmediate(goNormal);
			Object.DestroyImmediate(goDontDestroy);
			aGuiManager.UpdateEventSystem();
		}
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(2)]
	public void EventSystemSwitchDoesNotKeepOldCache(int switchMode) {
		var oldObject = new GameObject("Old EventSystem", typeof(EventSystem));
		var newObject = new GameObject("New EventSystem", typeof(EventSystem));
		try {
			var oldSystem = oldObject.GetComponent<EventSystem>();
			var newSystem = newObject.GetComponent<EventSystem>();
			EventSystem.current = oldSystem;
			Assert.That(aGuiManager.EventSystem, Is.SameAs(oldSystem));
			if(switchMode == 0) oldSystem.enabled = false;
			else if(switchMode == 1) oldObject.SetActive(false);
			else EventSystem.current = newSystem;
			Assert.That(EventSystem.current, Is.SameAs(newSystem));
			Assert.That(aGuiManager.EventSystem, Is.SameAs(newSystem));
		} finally {
			Object.DestroyImmediate(oldObject);
			Object.DestroyImmediate(newObject);
			aGuiManager.UpdateEventSystem();
		}
	}

	[Test]
	public void DisabledEventSystemIsNotReturnedByCacheOrSearch() {
		var go = new GameObject("Disabled EventSystem", typeof(EventSystem));
		try {
			var es = go.GetComponent<EventSystem>();
			Assert.That(aGuiManager.EventSystem, Is.SameAs(es));
			es.enabled = false;
			Assert.That(aGuiManager.EventSystem, Is.Null);
			aGuiManager.UpdateEventSystem();
			Assert.That(aGuiManager.EventSystem, Is.Null);
			es.enabled = true;
			Assert.That(aGuiManager.EventSystem, Is.SameAs(es));
		} finally {
			Object.DestroyImmediate(go);
			aGuiManager.UpdateEventSystem();
		}
	}
	#endregion
}
