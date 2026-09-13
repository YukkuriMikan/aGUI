using System;
using System.Collections;
using System.Reflection;
using ANest.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class aLayoutGroupLifecycleTests {
    class Observer : IObserver<Rect> {
        public int Count;
        public void OnNext(Rect value) { Count++; }
        public void OnError(Exception error) { throw error; }
        public void OnCompleted() { }
    }
    static void Set(object target, string name, object value) {
        for(var type = target.GetType(); type != null; type = type.BaseType) {
            var f = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if(f != null) { f.SetValue(target, value); return; }
        }
        throw new Exception(name);
    }
    static aLayoutGroupHorizontal Create() {
        var root = new GameObject("Lifecycle audit", typeof(RectTransform));
        ((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
        ((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
        var group = root.AddComponent<aLayoutGroupHorizontal>();
        var child = new GameObject("Child", typeof(RectTransform));
        child.transform.SetParent(root.transform, false);
        ((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
        ((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
        return group;
    }
    [UnityTest]
    public IEnumerator DisabledComponentCancelsPendingLayout() {
        var group = Create();
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.AlignWithFrameWaitAndCollectionAsync().Forget();
                group.enabled = false;
                yield return null;
                yield return null;
                yield return null;
                Assert.That(observer.Count, Is.EqualTo(0), "A disabled component must cancel its queued layout.");
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
    [UnityTest]
    public IEnumerator ReenabledComponentDoesNotExecuteOldAndNewRequests() {
        var group = Create();
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.AlignWithFrameWaitAndCollectionAsync().Forget();
                group.enabled = false;
                group.enabled = true;
                group.AlignWithFrameWaitAndCollectionAsync().Forget();
                yield return null;
                yield return null;
                yield return null;
                Assert.That(observer.Count, Is.EqualTo(1), "Only the new request should execute after re-enabling.");
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
    [UnityTest]
    public IEnumerator ImmediateRebuildConsumesPendingLayoutRequest() {
        var group = Create();
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.AlignWithFrameWaitAndCollectionAsync().Forget();
                group.AlignWithCollectionNonAnimate();
                yield return null;
                yield return null;
                yield return null;
                Assert.That(observer.Count, Is.EqualTo(1), "An immediate rebuild should not be followed by a stale queued rebuild.");
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
    [UnityTest]
    public IEnumerator DisabledComponentIgnoresHierarchyChanges() {
        var group = Create();
        try {
            Set(group, "updateMode", aLayoutGroupBase.UpdateMode.OnTransformChildrenChanged);
            group.enabled = false;
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                var child = new GameObject("Added while disabled", typeof(RectTransform));
                child.transform.SetParent(group.transform, false);
                yield return null;
                yield return null;
                yield return null;
                Assert.That(observer.Count, Is.EqualTo(0), "Disabled automatic layout must ignore hierarchy changes.");
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }

    [UnityTest]
    public IEnumerator ChildrenCreatedUnderInactiveParentAlignAndFitAfterShowing() {
        var root = new GameObject("Hidden menu", typeof(RectTransform));
        root.SetActive(false);
        try {
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            var rect = (RectTransform)content.transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 342.4f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 640f);
            var group = content.AddComponent<aLayoutGroupVertical>();
            Set(group, "updateMode", aLayoutGroupBase.UpdateMode.OnTransformChildrenChanged);
            Set(group, "childAlignment", TextAnchor.UpperLeft);
            Set(group, "childControlWidth", true);
            Set(group, "childForceExpandHeight", false);
            Set(group, "spacing", 10f);
            var fitter = content.AddComponent<aContentSizeFitter>();
            Set(fitter, "m_fitHeight", true);
            for(var i = 0; i < 5; i++) {
                var child = new GameObject("Item", typeof(RectTransform));
                child.transform.SetParent(content.transform, false);
                ((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 465f);
                ((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 95f);
            }
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                // K2と同じく、非表示中に構築と整列依頼を済ませてから親メニューを表示する。
                group.AlignWithFrameWaitAndCollectionAsync().Forget();
                yield return null; yield return null;
                Assert.That(observer.Count, Is.Zero, "Hidden layouts must not execute.");
                root.SetActive(true);
                Assert.That(observer.Count, Is.Zero, "Showing must still wait for uGUI.");
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(1));
                Assert.That(rect.rect.height, Is.EqualTo(515f).Within(0.001f));
                for(var i = 0; i < content.transform.childCount; i++) {
                    var child = (RectTransform)content.transform.GetChild(i);
                    Assert.That(child.anchoredPosition.y, Is.EqualTo(-47.5f - 105f * i).Within(0.001f));
                    Assert.That(child.rect.width, Is.EqualTo(342.4f).Within(0.001f));
                }
            }
        } finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator ReenabledAutomaticLayoutCollectsChildrenAddedWhileDisabled() {
        var group = Create();
        try {
            Set(group, "updateMode", aLayoutGroupBase.UpdateMode.OnTransformChildrenChanged);
            group.enabled = false;
            var child = new GameObject("Added while disabled", typeof(RectTransform));
            child.transform.SetParent(group.transform, false);
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                yield return null; yield return null;
                Assert.That(observer.Count, Is.Zero);
                group.enabled = true;
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(1));
                Assert.That(((RectTransform)child.transform).anchoredPosition.x,
                    Is.GreaterThan(((RectTransform)group.transform.GetChild(0)).anchoredPosition.x));
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }

    [UnityTest]
    public IEnumerator AutomaticReactivationAndExplicitRequestsProduceOneLayout() {
        var group = Create();
        try {
            Set(group, "updateMode", aLayoutGroupBase.UpdateMode.OnTransformChildrenChanged);
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.gameObject.SetActive(false);
                group.gameObject.SetActive(true);
                group.AlignWithFrameWaitAndCollectionAsync().Forget();
                group.gameObject.SetActive(false);
                group.gameObject.SetActive(true);
                group.AlignWithCollectionNonAnimate();
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(1), "Immediate rebuild must consume the activation request too.");
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }

    [UnityTest]
    public IEnumerator ManualLayoutDoesNotAlignOnReactivation() {
        var group = Create();
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.gameObject.SetActive(false);
                group.gameObject.SetActive(true);
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.Zero);
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
}
