using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ANest.UI;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class aGuiLifecycleRegressionTests {
    private static void Set(object target, string name, object value) {
        for(var type = target.GetType(); type != null; type = type.BaseType) {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if(field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new Exception(name);
    }

    private static RectTransform Rect(string name, Transform parent = null) {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if(parent != null) rect.SetParent(parent, false);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 200);
        return rect;
    }

    [Test]
    public void CompletionWaitsForValidAnimationWhenLongestReturnsNull() {
        var root = Rect("Missing graphic audit");
        try {
            var move = new Move();
            Set(move, "m_duration", 1f);
            var fade = new Fade();
            Set(fade, "m_duration", 2f);
            bool completed = false;
            LogAssert.Expect(LogType.Error, "[Fade] Graphic is null. Cannot animate on Missing graphic audit");
            aGuiUtils.PlayAnimation(new IUiAnimation[] { move, fade }, root, null,
                RectTransformValues.CreateValues(root), () => completed = true);
            Assert.That(DOTween.IsTweening(root), Is.True);
            Assert.That(completed, Is.False, "Completion must wait for the running 1-second Move.");
        } finally { root.DOKill(); Object.DestroyImmediate(root.gameObject); }
    }

    [UnityTest]
    public IEnumerator DisabledTopContainerDoesNotBlockUnderlyingRecovery() => RecoverSelection(false);
    [UnityTest]
    public IEnumerator InactiveTopContainerDoesNotBlockUnderlyingRecovery() => RecoverSelection(true);

    private static IEnumerator RecoverSelection(bool deactivate) {
        var esObject = new GameObject("Audit EventSystem", typeof(EventSystem));
        var root = Rect("Priority audit");
        try {
            aGuiManager.ClearSelectionHistory();
            aGuiManager.UpdateEventSystem();
            var lowerRect = Rect("Lower", root);
            var lowerButton = Rect("Lower button", lowerRect).gameObject.AddComponent<Button>();
            var lower = lowerRect.gameObject.AddComponent<aNormalSelectableContainer>();
            Set(lower, "m_initialGuard", false);
            lower.SetChildSelectableList(new List<Selectable> { lowerButton });
            var topRect = Rect("Top", root);
            var topButton = Rect("Top button", topRect).gameObject.AddComponent<Button>();
            var top = topRect.gameObject.AddComponent<aNormalSelectableContainer>();
            Set(top, "m_initialGuard", false);
            top.SetChildSelectableList(new List<Selectable> { topButton });
            yield return null;
            yield return null;
            lowerButton.Select();
            topButton.Select();
            if(deactivate) topRect.gameObject.SetActive(false);
            else top.enabled = false;
            var eventSystem = esObject.GetComponent<EventSystem>();
            eventSystem.SetSelectedGameObject(null);
            yield return null;
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(lowerButton.gameObject));
            if(deactivate) topRect.gameObject.SetActive(true);
            else top.enabled = true;
            eventSystem.SetSelectedGameObject(null);
            yield return null;
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(topButton.gameObject));
        } finally {
            Object.DestroyImmediate(root.gameObject);
            Object.DestroyImmediate(esObject);
            aContainerManager.Clear();
            aGuiManager.ClearSelectionHistory();
        }
    }

    [Test]
    public void CursorFindsTextAddedAfterFirstSelection() {
        var root = Rect("Cursor audit");
        root.gameObject.AddComponent<Canvas>();
        try {
            var target = Rect("Target", root);
            var cursorRect = Rect("Cursor", root);
            var cursor = root.gameObject.AddComponent<aCursorBase>();
            Set(cursor, "m_cursorRect", cursorRect);
            Set(cursor, "m_updateMode", aCursorBase.UpdateMode.OnSelectChanged);
            Set(cursor, "m_moveMode", aCursorBase.MoveMode.Instant);
            Set(cursor, "m_sizeMode", aCursorBase.SizeMode.MatchText);
            var update = typeof(aCursorBase).GetMethod("OnTargetRectChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            update.Invoke(cursor, new object[] { target });
            var text = Rect("Late text", target).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.text = "HELLO";
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate();
            Assert.That(text.textBounds.size.x, Is.GreaterThan(0));
            update.Invoke(cursor, new object[] { target });
            Assert.That(cursorRect.rect.width, Is.EqualTo(text.textBounds.size.x).Within(0.01f));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void RemovingSameNamedContainerPreservesOtherContainerLookup(bool removeLatest, bool destroy) {
        var root = Rect("Name audit");
        try {
            var first = Rect("Duplicate", root).gameObject.AddComponent<aStaticContainer>();
            var second = Rect("Duplicate", root).gameObject.AddComponent<aStaticContainer>();
            Assert.That(aContainerManager.GetContainer("Duplicate"), Is.SameAs(second));
            var removed = removeLatest ? second : first;
            var remaining = removeLatest ? first : second;
            if(destroy) Object.DestroyImmediate(removed.gameObject);
            else removed.Hide();
            Assert.That(aContainerManager.GetContainer("Duplicate"), Is.SameAs(remaining));
            remaining.Hide();
            Assert.That(aContainerManager.GetContainer("Duplicate"), Is.Null);
        } finally { Object.DestroyImmediate(root.gameObject); aContainerManager.Clear(); }
    }

    private sealed class ProbeAnimation : IUiAnimation {
        public float Delay => 0f;
        public float Duration => 0.1f;
        public bool IsYoYo => false;
        public AnimationCurve Curve => null;
        public Ease Ease => Ease.Linear;
        public bool UseCurve => false;
        public float TweenDuration = 0.1f;
        public float TweenDelay;
        public Tween Tween;
        public int CompletionCount;
        public Tween DoAnimate(Graphic graphic, RectTransform rect, RectTransformValues original) {
            Tween = DOTween.To(() => 0f, _ => { }, 1f, TweenDuration)
                .SetDelay(TweenDelay).SetTarget(rect).OnComplete(() => CompletionCount++);
            return Tween;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompletionUsesActualTweenDurationAndDelay(bool delayed) {
        var root = Rect("Actual tween duration");
        try {
            var shortAnimation = new ProbeAnimation();
            var longAnimation = new ProbeAnimation {
                TweenDuration = delayed ? 0.1f : 1f,
                TweenDelay = delayed ? 1f : 0f
            };
            int completed = 0, killed = 0;
            aGuiUtils.PlayAnimation(new IUiAnimation[] { longAnimation, shortAnimation }, root, null,
                RectTransformValues.CreateValues(root), () => completed++, () => killed++);
            shortAnimation.Tween.Complete();
            Assert.That(completed, Is.Zero);
            longAnimation.Tween.Complete();
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(killed, Is.Zero, "AutoKill after completion is not cancellation.");
            Assert.That(longAnimation.CompletionCount, Is.EqualTo(1), "Preserve animation-owned callbacks.");
        } finally { root.DOKill(); Object.DestroyImmediate(root.gameObject); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InterruptingAnimationNotifiesKillOnce(bool killOnly) {
        var root = Rect("Interrupt animation");
        try {
            int completed = 0, killed = 0;
            Action completion = killOnly ? null : () => completed++;
            aGuiUtils.PlayAnimation(new IUiAnimation[] { new ProbeAnimation() }, root, null,
                RectTransformValues.CreateValues(root), completion, () => killed++);
            aGuiUtils.PlayAnimation(null, root, null, RectTransformValues.CreateValues(root));
            Assert.That(killed, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
        } finally { root.DOKill(); Object.DestroyImmediate(root.gameObject); }
    }

    [Test]
    public void EmptyAnimationsCompleteImmediately() {
        var root = Rect("Empty animation");
        try {
            int completed = 0;
            aGuiUtils.PlayAnimation(new IUiAnimation[] { null }, root, null,
                RectTransformValues.CreateValues(root), () => completed++);
            Assert.That(completed, Is.EqualTo(1));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CursorReplacesDestroyedOrReparentedText(bool reparent) {
        var root = Rect("Replace cursor text");
        root.gameObject.AddComponent<Canvas>();
        try {
            var target = Rect("Target", root);
            var cursorRect = Rect("Cursor", root);
            var cursor = root.gameObject.AddComponent<aCursorBase>();
            Set(cursor, "m_cursorRect", cursorRect);
            Set(cursor, "m_moveMode", aCursorBase.MoveMode.Instant);
            Set(cursor, "m_sizeMode", aCursorBase.SizeMode.MatchText);
            var update = typeof(aCursorBase).GetMethod("UpdateCursor", BindingFlags.Instance | BindingFlags.NonPublic);
            var oldText = Rect("Old text", target).gameObject.AddComponent<TextMeshProUGUI>();
            oldText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            oldText.text = "OLD";
            Canvas.ForceUpdateCanvases();
            update.Invoke(cursor, new object[] { target });
            if(reparent) oldText.transform.SetParent(root, false);
            else Object.DestroyImmediate(oldText.gameObject);
            // With no text remaining, use the selected rectangle safely.
            update.Invoke(cursor, new object[] { target });
            Assert.That(cursorRect.rect.width, Is.EqualTo(target.rect.width));
            var text = Rect("Replacement", target).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.text = "NEW TEXT";
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate();
            update.Invoke(cursor, new object[] { target });
            Assert.That(text.textBounds.size.x, Is.GreaterThan(0));
            Assert.That(cursorRect.rect.width, Is.EqualTo(text.textBounds.size.x).Within(0.01f));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [TestCase(PointerEventData.InputButton.Left)]
    [TestCase(PointerEventData.InputButton.Right)]
    public void InitialGuardDoesNotStartMultipleInputGuard(PointerEventData.InputButton inputButton) {
        var esObject = new GameObject("Guard EventSystem", typeof(EventSystem));
        var root = Rect("Guard button");
        try {
            var button = root.gameObject.AddComponent<aButton>();
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            button.OnRightClick.AddListener(() => clicks++);
            var pointer = new PointerEventData(esObject.GetComponent<EventSystem>()) { button = inputButton };
            button.InitialGuardActive = true;
            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.Zero);
            button.InitialGuardActive = false;
            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1));
            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1), "Accepted clicks must still activate the multiple-input guard.");
        } finally { Object.DestroyImmediate(root.gameObject); Object.DestroyImmediate(esObject); aGuiManager.ClearSelectionHistory(); }
    }
}
