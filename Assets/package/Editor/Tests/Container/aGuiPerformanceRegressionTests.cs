using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ANest.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class aGuiPerformanceRegressionTests {
    private sealed class ReentrantButton : Button, IPreventFocusSelectable {
        public Action OnFocusCheck;
        public bool PreventFocus {
            get { OnFocusCheck?.Invoke(); return true; }
            set { }
        }
    }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private RectTransform root;

    [SetUp] public void SetUp() {
        root = Rect("Performance regression", null);
        root.gameObject.AddComponent<Canvas>();
    }

    [TearDown] public void TearDown() {
        Object.DestroyImmediate(root.gameObject);
        aGuiManager.ClearSelectionHistory();
        aGuiManager.MaxHistorySize = 10;
    }

    private static RectTransform Rect(string name, Transform parent) {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
        return rect;
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    private static void AssertNoAlloc(Action action) {
        for(var i = 0; i < 32; i++) action();
        var recorder = Recorder.Get("GC.Alloc");
        recorder.enabled = false;
        recorder.FilterToCurrentThread();
        recorder.enabled = true;
        try { for(var i = 0; i < 100; i++) action(); }
        finally { recorder.enabled = false; recorder.CollectFromAllThreads(); }
        Assert.That(recorder.sampleBlockCount, Is.Zero, "Steady-state calls must not allocate after buffers warm up.");
    }

    [TestCase(false, CornerType.Default)]
    [TestCase(false, CornerType.Round)]
    [TestCase(false, CornerType.Bevel)]
    [TestCase(true, CornerType.Default)]
    public void RepeatedLineRebuildsDoNotAllocate(bool interpolate, CornerType corner) {
        var line = Rect("Line", root).gameObject.AddComponent<aUiLineRenderer>();
        line.EnableCornerInterpolation = interpolate;
        line.CornerMeshType = corner;
        for(var i = 0; i < 17; i++) line.AddPoint(new Vector2(i * 10, (i % 2) * 20));
        var populate = (Action<VertexHelper>)Delegate.CreateDelegate(typeof(Action<VertexHelper>), line,
            typeof(aUiLineRenderer).GetMethod("OnPopulateMesh", Private, null, new[] { typeof(VertexHelper) }, null));
        using(var helper = new VertexHelper()) {
            AssertNoAlloc(() => populate(helper));
            line.ClearPoints();
            line.AddPoint(Vector2.zero);
            line.AddPoint(new Vector2(30, 0));
            AssertNoAlloc(() => populate(helper));
            Assert.That(helper.currentVertCount, Is.GreaterThan(0), "Shrinking must not reuse stale points.");
        }
    }

    [Test]
    public void NavigationReusesBuffersAndStillSkipsDisabledAndCycles() {
        var first = Rect("First", root).gameObject.AddComponent<Button>();
        var middle = Rect("Disabled", root).gameObject.AddComponent<Button>();
        var last = Rect("Last", root).gameObject.AddComponent<Button>();
        middle.interactable = false;
        var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = middle };
        first.navigation = nav;
        nav.selectOnRight = last;
        middle.navigation = nav;
        AssertNoAlloc(() => aGuiSelectableUtils.FindInteractableSelectable(first, MoveDirection.Right));
        Assert.That(aGuiSelectableUtils.FindInteractableSelectable(first, MoveDirection.Right), Is.SameAs(last));
        nav.selectOnRight = first;
        middle.navigation = nav;
        Assert.That(aGuiSelectableUtils.FindInteractableSelectable(first, MoveDirection.Right), Is.Null);
        first.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        ((RectTransform)middle.transform).anchoredPosition = new Vector2(150, 0);
        ((RectTransform)last.transform).anchoredPosition = new Vector2(300, 0);
        middle.navigation = first.navigation;
        last.navigation = first.navigation;
        AssertNoAlloc(() => aGuiSelectableUtils.FindInteractableSelectable(first, MoveDirection.Right));
        Assert.That(aGuiSelectableUtils.FindInteractableSelectable(first, MoveDirection.Right), Is.SameAs(last));
    }

    [Test]
    public void NestedNavigationDoesNotOverwriteOuterVisitedSet() {
        var first = Rect("Origin", root).gameObject.AddComponent<Button>();
        var middle = Rect("Reentrant", root).gameObject.AddComponent<ReentrantButton>();
        var last = Rect("Last", root).gameObject.AddComponent<Button>();
        first.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = middle };
        middle.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = first };
        last.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = first };
        var checks = 0;
        middle.OnFocusCheck = () => {
            checks++;
            Assert.That(aGuiSelectableUtils.FindInteractableSelectable(last, MoveDirection.Right), Is.SameAs(first));
        };
        Assert.That(aGuiSelectableUtils.FindInteractableSelectable(first, MoveDirection.Right), Is.Null);
        Assert.That(checks, Is.EqualTo(1));
    }

    [Test]
    public void NavigationSeesSelectableEnabledDuringFocusCheck() {
        var first = Rect("Origin", root).gameObject.AddComponent<Button>();
        var middle = Rect("Focus callback", root).gameObject.AddComponent<ReentrantButton>();
        var last = Rect("Enabled during search", root).gameObject.AddComponent<Button>();
        ((RectTransform)middle.transform).anchoredPosition = new Vector2(150, 0);
        ((RectTransform)last.transform).anchoredPosition = new Vector2(300, 0);
        last.gameObject.SetActive(false);
        middle.OnFocusCheck = () => last.gameObject.SetActive(true);
        Assert.That(aGuiSelectableUtils.FindInteractableSelectable(first, MoveDirection.Right), Is.SameAs(last));
    }

    [Test]
    public void NoOpAnimationAndFittingDoNotAllocate() {
        var text = Rect("Text", root).gameObject.AddComponent<TextMeshProUGUI>();
        var fitter = text.gameObject.AddComponent<aTextMeshSizeFitter>();
        Set(fitter, "m_targetText", text);
        Set(fitter, "m_fitWidth", false);
        Set(fitter, "m_fitHeight", false);
        var original = RectTransformValues.CreateValues(root);
        AssertNoAlloc(() => aGuiUtils.PlayAnimation(null, root, null, original));
        AssertNoAlloc(fitter.ApplyFitting);
    }

    [Test]
    public void CursorOnlyRebuildsChangedText() {
        var text = Rect("Text", root).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = "A";
        text.ForceMeshUpdate();
        var cursor = Rect("Cursor", root).gameObject.AddComponent<aCursorBase>();
        Set(cursor, "m_cursorRect", (RectTransform)cursor.transform);
        Set(cursor, "m_moveMode", aCursorBase.MoveMode.Instant);
        Set(cursor, "m_sizeMode", aCursorBase.SizeMode.MatchText);
        var update = (Action<RectTransform>)Delegate.CreateDelegate(typeof(Action<RectTransform>), cursor,
            typeof(aCursorBase).GetMethod("UpdateCursor", Private));
        var events = 0;
        Action<Object> onText = obj => { if(obj == text) events++; };
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(onText);
        try {
            AssertNoAlloc(() => update(text.rectTransform));
            Assert.That(events, Is.Zero);
            var oldWidth = ((RectTransform)cursor.transform).rect.width;
            text.text = "ABCDEFGH";
            update(text.rectTransform);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(((RectTransform)cursor.transform).rect.width, Is.GreaterThan(oldWidth));
        } finally { TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(onText); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RubyRebuildIsSingleAndReusesStringsUntilTextChanges(bool customTag) {
        var text = Rect("Ruby", root).gameObject.AddComponent<aTextMeshProUgui>();
        text.text = customTag ? "<ruby=\"abc\">ABC</ruby>" : "<link=\"ruby:abc\">ABC</link>";
        text.ForceMeshUpdate();
        var events = 0;
        Action<Object> onText = obj => { if(obj == text) events++; };
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(onText);
        try {
            AssertNoAlloc(() => text.ForceMeshUpdate());
            events = 0;
            text.ForceMeshUpdate();
            Assert.That(events, Is.EqualTo(1));
            text.SetText(customTag ? "<ruby=\"xyz\">ABC</ruby>" : "<link=\"ruby:xyz\">ABC</link>");
            text.ForceMeshUpdate();
            Assert.That(text.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text, Is.EqualTo("xyz"));
        } finally { TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(onText); }
    }

    [Test]
    public void ScrollGeometryReusesBuffersAndTracksNestedChanges() {
        var stop = root.gameObject.AddComponent<aScrollStop>();
        var content = Rect("Content", root);
        Set(stop, "m_targetRect", content);
        var branch = new GameObject("Non-rect branch").transform;
        branch.SetParent(content, false);
        var child = Rect("Child", branch);
        AssertNoAlloc(() => stop.TryGetChildRegionPolygon(out _));
        AssertRegion(stop, content);
        child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 280);
        AssertRegion(stop, content);
        child.localScale = new Vector3(-2, 3, 1);
        branch.localRotation = Quaternion.Euler(0, 0, 35);
        AssertRegion(stop, content);
        var extra = Rect("New descendant", branch);
        extra.anchoredPosition = new Vector2(300, -150);
        AssertRegion(stop, content);
        extra.gameObject.SetActive(false);
        AssertRegion(stop, content);
        extra.gameObject.SetActive(true);
        extra.SetParent(content, false);
        AssertRegion(stop, content);
        Object.DestroyImmediate(child.gameObject);
        AssertRegion(stop, content);
        var padding = new Vector2(5, 7);
        Set(stop, "m_padding", padding);
        AssertRegion(stop, content, padding);
        AssertNoAlloc(() => {
            extra.anchoredPosition += Vector2.right;
            stop.TryGetChildRegionPolygon(out _);
        });
        content.anchoredPosition += new Vector2(30, 15);
        AssertRegion(stop, content, padding);
    }

    private static void AssertRegion(aScrollStop stop, RectTransform content, Vector2 padding = default) {
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var corners = new Vector3[4];
        foreach(var rect in content.GetComponentsInChildren<RectTransform>()) {
            if(rect == content) continue;
            rect.GetWorldCorners(corners);
            foreach(var corner in corners) {
                var point = (Vector2)content.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
        }
        Assert.That(stop.TryGetChildRegionRect(out var actual), Is.True);
        Assert.That(Vector2.Distance(actual.min, min - padding), Is.LessThan(0.001f));
        Assert.That(Vector2.Distance(actual.max, max + padding), Is.LessThan(0.001f));
        Assert.That(stop.TryGetChildRegionPolygon(out var polygon), Is.True);
        foreach(var point in polygon) {
            Assert.That(point.x, Is.InRange(actual.xMin - 0.001f, actual.xMax + 0.001f));
            Assert.That(point.y, Is.InRange(actual.yMin - 0.001f, actual.yMax + 0.001f));
        }
    }

    [UnityTest]
    public IEnumerator CompletedAndCancelledTextFadesDisposeTheirSources() {
        var text = Rect("Fade", root).gameObject.AddComponent<TextMeshProUGUI>();
        var colors = ColorBlock.defaultColorBlock;
        colors.fadeDuration = 0.02f;
        CancellationTokenSource running = null;
        var complete = false;
        aGuiUtils.ApplyTextColorTransition(text, text, colors, 1, false, ref running, () => complete = true);
        var finished = running;
        for(var frame = 0; frame < 120 && !complete; frame++) yield return null;
        Assert.That(complete, Is.True, $"Fade did not complete. UniTask loop: {Cysharp.Threading.Tasks.PlayerLoopHelper.IsInjectedUniTaskPlayerLoop()}, cancelled: {finished.IsCancellationRequested}, unscaledDeltaTime: {Time.unscaledDeltaTime}");
        Assert.Throws<ObjectDisposedException>(() => { var token = finished.Token; });
        Assert.DoesNotThrow(() => aGuiUtils.StopTextColorTransition(ref running));
        complete = false;
        aGuiUtils.ApplyTextColorTransition(text, text, colors, 2, false, ref running, () => complete = true);
        var cancelled = running;
        aGuiUtils.StopTextColorTransition(ref running);
        yield return null;
        Assert.That(complete, Is.False);
        Assert.Throws<ObjectDisposedException>(() => { var token = cancelled.Token; });
        colors.fadeDuration = 5f;
        aGuiUtils.ApplyTextColorTransition(text, text, colors, 1, false, ref running, () => complete = true);
        var destroyed = running;
        Object.DestroyImmediate(text.gameObject);
        yield return null;
        yield return null;
        Assert.That(complete, Is.False);
        Assert.Throws<ObjectDisposedException>(() => { var token = destroyed.Token; });
    }

    [Test]
    public void HistoryReusesNodesAcrossOverflowAndGoingBack() {
        var first = Rect("First", root).gameObject.AddComponent<Button>();
        var second = Rect("Second", root).gameObject.AddComponent<Button>();
        aGuiManager.ClearSelectionHistory();
        aGuiManager.MaxHistorySize = 3;
        AssertNoAlloc(() => { aGuiManager.SetSelectedSelectable(first); aGuiManager.SetSelectedSelectable(second); });
        Assert.That(aGuiManager.SelectionHistoryCount, Is.EqualTo(3));
        Assert.That(aGuiManager.GoBack(), Is.SameAs(first));
        Assert.That(aGuiManager.GoBack(), Is.SameAs(second));
        Assert.That(aGuiManager.GoBack(), Is.SameAs(first));
        Assert.That(aGuiManager.GoBack(), Is.Null);
        AssertNoAlloc(() => { aGuiManager.SetSelectedSelectable(second); aGuiManager.GoBack(); });
    }

    [Test]
    public void MissingEventSystemDoesNotSearchOnEachAccessAndFindsNewSystem() {
        Assert.That(EventSystem.current, Is.Null, "This test requires the isolated test scene.");
        aGuiManager.UpdateEventSystem();
        Assert.That(aGuiManager.EventSystem, Is.Null);
        AssertNoAlloc(() => { var system = aGuiManager.EventSystem; });
        var go = new GameObject("New EventSystem", typeof(EventSystem));
        try { Assert.That(aGuiManager.EventSystem, Is.SameAs(go.GetComponent<EventSystem>())); }
        finally { Object.DestroyImmediate(go); aGuiManager.UpdateEventSystem(); }
    }
}
