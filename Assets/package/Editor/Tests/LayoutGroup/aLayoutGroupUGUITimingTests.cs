using System;
using System.Collections;
using System.Reflection;
using ANest.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class aLayoutGroupUGUITimingTests {
	private sealed class Observer : IObserver<Rect> {
		public int Count;
		public int Frame;
		public float Width;
		public RectTransform Target;
		public void OnNext(Rect value) { Count++; Frame = Time.frameCount; Width = Target.rect.width; }
		public void OnError(Exception error) { throw error; }
		public void OnCompleted() { }
	}

	[UnityTest]
	public IEnumerator InitializeOnlyReadsParentUGUILayoutAfterLateUpdateChanges() {
		yield return VerifyUGUIOrder(aLayoutGroupBase.UpdateMode.InitializeOnly, aLayoutGroupBase.UpdateTiming.Immediate);
	}

	[UnityTest]
	public IEnumerator ChildrenChangesWaitForUGUIRegardlessOfLegacyTiming() {
		foreach(var timing in new[] { aLayoutGroupBase.UpdateTiming.Immediate, aLayoutGroupBase.UpdateTiming.Update, aLayoutGroupBase.UpdateTiming.LateUpdate }) {
			yield return VerifyUGUIOrder(aLayoutGroupBase.UpdateMode.OnTransformChildrenChanged, timing);
		}
	}

	private IEnumerator VerifyUGUIOrder(aLayoutGroupBase.UpdateMode mode, aLayoutGroupBase.UpdateTiming timing) {
		var canvasRoot = new GameObject("uGUI timing test", typeof(RectTransform), typeof(Canvas));
		canvasRoot.SetActive(false);
		try {
			canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			var container = new GameObject("uGUI parent", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
			container.transform.SetParent(canvasRoot.transform, false);
			((RectTransform)container.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 800);
			((RectTransform)container.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 200);
			var standard = container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
			standard.childControlWidth = standard.childControlHeight = true;
			standard.childForceExpandWidth = standard.childForceExpandHeight = false;
			var row = new GameObject("aGUI row", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
			row.transform.SetParent(container.transform, false);
			var rowRect = (RectTransform)row.transform;
			rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200);
			rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
			var element = row.GetComponent<UnityEngine.UI.LayoutElement>();
			element.preferredWidth = 200;
			element.preferredHeight = 100;
			var group = row.AddComponent<aLayoutGroupHorizontal>();
			Set(group, "updateMode", mode);
			Set(group, "updateTiming", timing);
			Set(group, "childForceExpandWidth", false);
			Set(group, "childForceExpandHeight", false);
			Set(group, "setNavigation", false);
			var child = new GameObject("Child", typeof(RectTransform));
			child.transform.SetParent(mode == aLayoutGroupBase.UpdateMode.InitializeOnly ? row.transform : canvasRoot.transform, false);
			((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
			((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40);
			var probe = row.AddComponent<LayoutInputLateUpdateProbe>();
			probe.Element = element;
			probe.ChangeFrame = Time.frameCount + 1;
			var observer = new Observer { Target = rowRect };
			using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
				var requestedFrame = Time.frameCount;
				canvasRoot.SetActive(true);
				Canvas.ForceUpdateCanvases(); // 初期状態200を確定。次フレームの更新は通常のuGUIに任せる。
				Assert.That(rowRect.rect.width, Is.EqualTo(200f).Within(0.001f));
				if(mode == aLayoutGroupBase.UpdateMode.OnTransformChildrenChanged) child.transform.SetParent(row.transform, false);
				for(var i = 0; i < 5; i++) yield return null;
				Assert.That(probe.Changed, Is.True);
				Assert.That(observer.Count, Is.EqualTo(1));
				Assert.That(observer.Frame, Is.EqualTo(requestedFrame + 1), "Wait exactly one frame, then execute after uGUI.");
				Assert.That(observer.Width, Is.EqualTo(600f).Within(0.001f), "aGUI must see the size calculated by uGUI after LateUpdate.");
				Assert.That(((RectTransform)child.transform).anchoredPosition.x, Is.EqualTo(300f).Within(0.001f));
			}
		} finally { Object.DestroyImmediate(canvasRoot); }
	}

	[UnityTest]
	public IEnumerator AwaitableAndQueuedRequestsShareOneCompletedLayout() {
		var root = new GameObject("Awaitable test", typeof(RectTransform));
		try {
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
			var group = root.AddComponent<aLayoutGroupHorizontal>();
			var child = new GameObject("Child", typeof(RectTransform));
			child.transform.SetParent(root.transform, false);
			var observer = new Observer { Target = (RectTransform)root.transform };
			using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
				var frame = Time.frameCount;
				group.AlignWithFrameWaitAndCollectionAsync().Forget();
				var first = group.AlignWithFrameWaitAsync();
				var second = group.AlignWithFrameWaitAndCollectionAsync();
				yield return UniTask.WhenAll(first, second).ToCoroutine();
				Assert.That(observer.Count, Is.EqualTo(1), "Awaiters must complete after the shared layout, without a second delay.");
				Assert.That(observer.Frame, Is.EqualTo(frame + 1));
			}
		} finally { Object.DestroyImmediate(root); }
	}

	[UnityTest]
	public IEnumerator DestroyingPendingInitializationProducesNoCallbackOrException() {
		var root = new GameObject("Destroyed initialization", typeof(RectTransform));
		root.SetActive(false);
		((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
		((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
		var group = root.AddComponent<aLayoutGroupHorizontal>();
		Set(group, "updateMode", aLayoutGroupBase.UpdateMode.InitializeOnly);
		var observer = new Observer { Target = (RectTransform)root.transform };
		using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
			root.SetActive(true);
			Object.DestroyImmediate(root);
			yield return null; yield return null; yield return null;
			Assert.That(observer.Count, Is.Zero);
		}
	}

	private static void Set(object target, string name, object value) {
		for(var type = target.GetType(); type != null; type = type.BaseType) {
			var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
			if(field == null) continue;
			field.SetValue(target, value);
			return;
		}
		throw new MissingFieldException(name);
	}
}

public class LayoutInputLateUpdateProbe : MonoBehaviour {
	public UnityEngine.UI.LayoutElement Element;
	public int ChangeFrame;
	public bool Changed;
	private void LateUpdate() {
		if(Changed || Time.frameCount < ChangeFrame) return;
		Element.preferredWidth = 600;
		Changed = true;
	}
}
