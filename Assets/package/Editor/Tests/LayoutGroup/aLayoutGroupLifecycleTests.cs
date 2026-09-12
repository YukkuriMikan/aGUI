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
}
