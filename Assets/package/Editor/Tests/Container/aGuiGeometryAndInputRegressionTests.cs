using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using ANest.UI;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class aGuiGeometryAndInputRegressionTests {
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private RectTransform root;

    [SetUp] public void SetUp() {
        root = Rect("Geometry and input regression", null, 600, 400);
        root.gameObject.AddComponent<Canvas>();
    }

    [TearDown] public void TearDown() => Object.DestroyImmediate(root.gameObject);

    private static RectTransform Rect(string name, Transform parent, float width = 100, float height = 100) {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        return rect;
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static aGuiInfo Info(RectTransform rect) {
        var info = rect.GetComponent<aGuiInfo>() ?? rect.gameObject.AddComponent<aGuiInfo>();
        Set(info, "m_rectTransform", rect);
        info.Refresh();
        return info;
    }

    private ScrollRect Scroll(float scale, out RectTransform item) {
        var viewport = Rect("Viewport", root);
        var content = Rect("Content", viewport, 100, 1000);
        content.localScale = new Vector3(1, scale, 1);
        item = Rect("Item", content, 80, 20);
        item.anchoredPosition = new Vector2(0, -500);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.inertia = false;
        scroll.Rebuild(CanvasUpdate.PostLayout);
        return scroll;
    }

    private static Bounds BoundsIn(RectTransform rect, Transform space) {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var bounds = new Bounds(space.InverseTransformPoint(corners[0]), Vector3.zero);
        for(var i = 1; i < 4; i++) bounds.Encapsulate(space.InverseTransformPoint(corners[i]));
        return bounds;
    }

    [TestCase(1f, 1f, 0f)]
    [TestCase(2f, 1f, 10f)]
    [TestCase(2f, 0f, 10f)]
    [TestCase(0.5f, 1f, 10f)]
    [TestCase(0.5f, 0f, 10f)]
    [TestCase(-2f, 1f, 10f)]
    [TestCase(-2f, 0f, 10f)]
    public void ScrollShowsScaledItemWithViewportPadding(float scale, float start, float padding) {
        var scroll = Scroll(scale, out var item);
        scroll.verticalNormalizedPosition = start;
        var before = BoundsIn(item, scroll.viewport);
        Assert.That(before.min.y < scroll.viewport.rect.yMin || before.max.y > scroll.viewport.rect.yMax, Is.True);
        CancellationTokenSource cts = null;
        try {
            aNormalScrollContainer.ScrollToItem(scroll, item, null, 0f, padding, ref cts);
            var after = BoundsIn(item, scroll.viewport);
            Assert.That(after.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin + padding - 0.01f));
            Assert.That(after.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax - padding + 0.01f));
        } finally { cts?.Cancel(); cts?.Dispose(); }
    }

    [UnityTest] public IEnumerator AutomaticScrollPausesAndResumesWithTimeScale() {
        var originalScale = Time.timeScale;
        var scroll = Scroll(2f, out var item);
        CancellationTokenSource cts = null;
        try {
            Time.timeScale = 0f;
            yield return null;
            scroll.verticalNormalizedPosition = 1f;
            aNormalScrollContainer.ScrollToItem(scroll, item, null, 0.1f, 0f, ref cts);
            for(var i = 0; i < 3; i++) yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.2f);
            Assert.That(BoundsIn(item, scroll.viewport).min.y, Is.EqualTo(scroll.viewport.rect.yMin).Within(0.01f));
        } finally { Time.timeScale = originalScale; cts?.Cancel(); cts?.Dispose(); }
    }

    [TestCase(0f, 1f, 1f, false)]
    [TestCase(1f, 2f, 0.5f, false)]
    [TestCase(0.5f, -2f, 2f, false)]
    [TestCase(0f, 2f, 0.5f, true)]
    public void TextCursorMatchesCenterAndBounds(float pivot, float targetScale, float cursorScale, bool animate) {
        var target = Rect("Target", root, 200, 60);
        target.pivot = new Vector2(pivot, pivot);
        var text = target.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.text = "ABC";
        text.fontSize = 30;
        var cursorParent = Rect("Cursor parent", root);
        cursorParent.localScale = new Vector3(cursorScale, cursorScale, 1);
        var cursorRect = Rect("Cursor", cursorParent);
        cursorRect.localScale = new Vector3(0.75f, 1.25f, 1);
        var cursor = cursorRect.gameObject.AddComponent<aCursorBase>();
        Set(cursor, "m_cursorRect", cursorRect);
        Set(cursor, "m_moveMode", animate ? aCursorBase.MoveMode.Animation : aCursorBase.MoveMode.Instant);
        Set(cursor, "m_updateMode", aCursorBase.UpdateMode.OnSelectChanged);
        Set(cursor, "m_sizeMode", aCursorBase.SizeMode.MatchText);
        var update = (Action<RectTransform>)Delegate.CreateDelegate(typeof(Action<RectTransform>), cursor,
            typeof(aCursorBase).GetMethod("OnTargetRectChanged", Private));
        text.ForceMeshUpdate();
        update(target);
        target.localScale = new Vector3(targetScale, targetScale, 1);
        target.anchoredPosition += new Vector2(50, 30);
        text.ForceMeshUpdate();
        update(target);
        if(animate) {
            ((Tween)typeof(aCursorBase).GetField("m_sizeTween", Private).GetValue(cursor))?.Complete();
            ((Tween)typeof(aCursorBase).GetField("m_moveTween", Private).GetValue(cursor))?.Complete();
        }
        var actual = BoundsIn(cursorRect, root);
        var expectedCenter = root.InverseTransformPoint(target.TransformPoint(text.textBounds.center));
        var expectedSize = root.InverseTransformVector(target.TransformVector(text.textBounds.size));
        Assert.That(Vector3.Distance(actual.center, expectedCenter), Is.LessThan(0.01f));
        Assert.That(actual.size.x, Is.EqualTo(Mathf.Abs(expectedSize.x)).Within(0.01f));
        Assert.That(actual.size.y, Is.EqualTo(Mathf.Abs(expectedSize.y)).Within(0.01f));
    }

    private sealed class CountingAnimation : IUiAnimation {
        public int Calls;
        public float Delay => 0;
        public float Duration => 0;
        public bool IsYoYo => false;
        public AnimationCurve Curve => null;
        public Ease Ease => Ease.Linear;
        public bool UseCurve => false;
        public Tween DoAnimate(Graphic graphic, RectTransform rect, RectTransformValues original) { Calls++; return null; }
    }

    [Test] public void TogglePlaysClickAnimationOnceForEveryAcceptedInput() {
        var rect = Rect("Toggle", root);
        var toggle = rect.gameObject.AddComponent<aToggle>();
        var animation = new CountingAnimation();
        Set(toggle, "m_guiInfo", Info(rect));
        Set(toggle, "m_useCustomAnimation", true);
        Set(toggle, "m_clickAnimations", new IUiAnimation[] { animation });
        Set(toggle, "useMultipleInputGuard", false);
        toggle.SetIsOnWithoutNotify(false);
        toggle.OnSubmit(new BaseEventData(null));
        Assert.That(toggle.isOn, Is.True);
        Assert.That(animation.Calls, Is.EqualTo(1));
        toggle.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(toggle.isOn, Is.False);
        Assert.That(animation.Calls, Is.EqualTo(2));
        Assert.That(toggle.InvokeClickWithGuard(), Is.True);
        Assert.That(animation.Calls, Is.EqualTo(3));
        toggle.InitialGuardActive = true;
        toggle.OnSubmit(new BaseEventData(null));
        Assert.That(animation.Calls, Is.EqualTo(3));
        toggle.InitialGuardActive = false;
        toggle.interactable = false;
        toggle.OnSubmit(new BaseEventData(null));
        Assert.That(animation.Calls, Is.EqualTo(3));
    }

    [Test] public void RenamesRespectRegistrationOrderAndRemoval() {
        var first = Rect("First", root).gameObject.AddComponent<aStaticContainer>();
        var second = Rect("Second", root).gameObject.AddComponent<aStaticContainer>();
        var prefix = Guid.NewGuid().ToString("N");
        first.name = prefix;
        second.name = prefix + "Other";
        aContainerManager.Add(first);
        aContainerManager.Add(second);
        try {
            second.name = prefix;
            Assert.That(aContainerManager.GetContainer(prefix), Is.SameAs(second));
            Assert.That(aContainerManager.GetContainer(prefix + "Other"), Is.Null);
            aContainerManager.Add(first);
            Assert.That(aContainerManager.GetContainer(prefix), Is.SameAs(first));
            first.name = prefix + "Renamed";
            Assert.That(aContainerManager.GetContainer(prefix), Is.SameAs(second));
            Assert.That(aContainerManager.GetContainer(first.name), Is.SameAs(first));
            aContainerManager.Remove(first);
            Assert.That(aContainerManager.GetContainer(prefix + "Renamed"), Is.Null);
            aContainerManager.Remove(second);
            Assert.That(aContainerManager.GetContainer(prefix), Is.Null);
        } finally { aContainerManager.Remove(first); aContainerManager.Remove(second); }
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 2)]
    [TestCase(true, 2)]
    public void MissingTargetDoesNotBlockFollowingAnimationOrCompletion(bool rotate, int missingKind) {
        IUiAnimation animation = rotate ? new RotateTarget() : new MoveTarget();
        if(missingKind != 0) {
            var target = Rect("Animation target", root);
            var info = Info(target);
            Set(animation, "m_target", info);
            if(missingKind == 1) Object.DestroyImmediate(target.gameObject);
            else Set(info, "m_rectTransform", null);
        }
        var completed = 0;
        aGuiUtils.PlayAnimation(new[] { animation }, root, null, RectTransformValues.CreateValues(root), () => completed++);
        Assert.That(completed, Is.EqualTo(1), "All skipped animations must still complete.");
        var move = new Move();
        Set(move, "m_duration", 1f);
        try {
            aGuiUtils.PlayAnimation(new[] { animation, move }, root, null, RectTransformValues.CreateValues(root), () => completed++);
            Assert.That(DOTween.IsTweening(root), Is.True);
            Assert.That(completed, Is.EqualTo(1), "Completion must wait for the valid animation.");
            DOTween.Complete(root, true);
            Assert.That(completed, Is.EqualTo(2));
        } finally { root.DOKill(); }
    }
}
