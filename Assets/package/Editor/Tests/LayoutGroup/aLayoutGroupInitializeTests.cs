using System;
using System.Collections;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class aLayoutGroupInitializeTests {
    class Observer : IObserver<Rect> {
        public int Count;
        public void OnNext(Rect value) { Count++; }
        public void OnError(Exception error) { throw error; }
        public void OnCompleted() { }
    }
    static aLayoutGroupHorizontal Create(float size) {
        var root = new GameObject("Initialize audit", typeof(RectTransform));
        root.SetActive(false);
        ((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        ((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
        var group = root.AddComponent<aLayoutGroupHorizontal>();
        typeof(aLayoutGroupBase).GetField("updateMode", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(group, aLayoutGroupBase.UpdateMode.InitializeOnly);
        var child = new GameObject("Child", typeof(RectTransform));
        child.transform.SetParent(root.transform, false);
        ((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
        ((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
        return group;
    }
    [UnityTest]
    public IEnumerator NormalActivationInitializesOnce() {
        var group = Create(300);
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.gameObject.SetActive(true);
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(1));
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
    [UnityTest]
    public IEnumerator InitiallyZeroWaitsForPositiveSize() {
        var group = Create(0);
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.gameObject.SetActive(true);
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(0));
                ((RectTransform)group.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
                ((RectTransform)group.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(1));
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
    [UnityTest]
    public IEnumerator DisablingCancelsInitialLayout() {
        var group = Create(300);
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.gameObject.SetActive(true);
                group.enabled = false;
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(0), "Initialization must not run after disabling the component.");
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
    [UnityTest]
    public IEnumerator QuickReactivationInitializesOnlyOnce() {
        var group = Create(300);
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.gameObject.SetActive(true);
                group.gameObject.SetActive(false);
                group.gameObject.SetActive(true);
                yield return null; yield return null; yield return null;
                Assert.That(observer.Count, Is.EqualTo(1), "Only the current activation's initial request may execute.");
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
    [UnityTest]
    public IEnumerator SizeBecomingZeroDuringWaitIsNotTreatedAsInitialized() {
        var group = Create(300);
        try {
            var observer = new Observer();
            using(var subscription = group.CompleteLayoutAsObservable.Subscribe(observer)) {
                group.gameObject.SetActive(true);
                ((RectTransform)group.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
                ((RectTransform)group.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
                yield return null; yield return null; yield return null;
                var zeroSizeCount = observer.Count;
                ((RectTransform)group.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 600);
                ((RectTransform)group.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 600);
                yield return null; yield return null; yield return null;
                Assert.That(zeroSizeCount, Is.EqualTo(0), "Size became zero while awaiting initialization; zero-size notifications=" + zeroSizeCount + ", total after size restored=" + observer.Count);
                Assert.That(observer.Count, Is.EqualTo(1));
            }
        } finally { Object.DestroyImmediate(group.gameObject); }
    }
}
