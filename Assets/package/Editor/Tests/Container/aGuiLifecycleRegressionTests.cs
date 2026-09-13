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
using UniRx;
using Object = UnityEngine.Object;

public class aGuiLifecycleRegressionTests {
    [UnityTest]
    public IEnumerator InitialGuardResetsDeadlineAndStopsWithScaledTime() {
        var root = Rect("Guard deadline");
        var originalScale = Time.timeScale;
        try {
            Time.timeScale = 1;
            var container = InactiveContainer<aNormalSelectableContainer>(root, true);
            container.DisallowNullSelection = false;
            Set(container, "m_initialGuardDuration", .2f);
            container.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(.1f);
            container.Hide(); container.Show();
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(container.CanvasGroup.blocksRaycasts, Is.False, "Guard uses scaled time.");
            Time.timeScale = 1;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(container.CanvasGroup.blocksRaycasts, Is.False, "A previous deadline must not release the new guard.");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(container.CanvasGroup.blocksRaycasts, Is.True);
            container.Hide(); container.Show(); container.Hide();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(container.CanvasGroup.blocksRaycasts, Is.False, "Hidden containers must not be released by a stale timer.");
        } finally { Object.DestroyImmediate(root.gameObject); Time.timeScale = originalScale; }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RecycledMoveUsesNewTargetAndKeepsYoyo(bool yoyo) {
        var root = Rect("Move recycling");
        try {
            var first = Rect("First", root);
            var second = Rect("Second", root);
            var move = new Move();
            Set(move, "m_startValue", Vector2.zero); Set(move, "m_endValue", new Vector2(100, 0));
            Set(move, "m_duration", 1f); Set(move, "m_isYoYo", yoyo); Set(move, "m_ease", Ease.Linear);
            var firstTween = move.DoAnimate(null, first, RectTransformValues.CreateValues(first));
            firstTween.Kill();
            var tween = move.DoAnimate(null, second, RectTransformValues.CreateValues(second));
            tween.Goto(yoyo ? .25f : .5f);
            Assert.That(first.anchoredPosition.x, Is.Zero);
            Assert.That(second.anchoredPosition.x, Is.EqualTo(50).Within(.001f));
            tween.Complete();
            Assert.That(second.anchoredPosition.x, Is.EqualTo(yoyo ? 0 : 100).Within(.001f));
        } finally { foreach(var rect in root.GetComponentsInChildren<RectTransform>()) rect.DOKill(); Object.DestroyImmediate(root.gameObject); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AnimationCallbackCanStartAnotherAnimation(bool interrupt) {
        var root = Rect("Reentrant callbacks");
        var next = Rect("Next callback", root);
        try {
            var animations = new IUiAnimation[] { new Move() };
            var values = RectTransformValues.CreateValues(root);
            int completed = 0, killed = 0, nextCompleted = 0;
            Action startNext = () => aGuiUtils.PlayAnimation(new IUiAnimation[] { new Move() }, next, null,
                RectTransformValues.CreateValues(next), () => nextCompleted++);
            aGuiUtils.PlayAnimation(animations, root, null, values,
                () => { completed++; startNext(); }, () => { killed++; startNext(); });
            if(interrupt) root.DOKill(); else root.DOComplete();
            Assert.That(completed, Is.EqualTo(interrupt ? 0 : 1));
            Assert.That(killed, Is.EqualTo(interrupt ? 1 : 0));
            // 別の再生で通知状態が再利用されても、まだ進行中の通知に混ざらない。
            aGuiUtils.PlayAnimation(animations, root, null, values, () => completed++);
            next.DOComplete(); root.DOComplete();
            Assert.That(nextCompleted, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(interrupt ? 1 : 2));
            Assert.That(killed, Is.EqualTo(interrupt ? 1 : 0));
        } finally { root.DOKill(); next.DOKill(); Object.DestroyImmediate(root.gameObject); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContainerActiveWarningStillDetectsDirectChangesDuringPlay(bool useShowHide) {
        var root = Rect("Container warning regression");
        var warnings = 0;
        Application.LogCallback onLog = (message, stack, type) => {
            if(message.Contains("[aContainerBase]")) warnings++;
        };
        Application.logMessageReceived += onLog;
        try {
            var container = InactiveContainer<aStaticContainer>(root, true);
            container.gameObject.SetActive(true);
            if(useShowHide) { container.Hide(); container.Show(); }
            else container.gameObject.SetActive(false);
            Assert.That(warnings, Is.EqualTo(useShowHide ? 0 : 1));
        } finally {
            Application.logMessageReceived -= onLog;
            Object.DestroyImmediate(root.gameObject);
        }
    }

    [UnityTest]
    public IEnumerator DisablingScrollContainerStopsAutoScroll() => VerifyScrollInterruption(0);

    [UnityTest]
    public IEnumerator DeactivatingScrollContainerStopsAutoScroll() => VerifyScrollInterruption(1);

    [UnityTest]
    public IEnumerator HidingScrollContainerStopsAutoScroll() => VerifyScrollInterruption(2);

    [UnityTest]
    public IEnumerator HideAnimationStopsAutoScrollBeforeDeactivation() => VerifyScrollInterruption(3);

    private static IEnumerator VerifyScrollInterruption(int mode) {
        var root = Rect("Scroll interruption");
        var originalTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        try {
            var container = InactiveContainer<aNormalScrollContainer>(root, true);
            var host = (RectTransform)container.transform;
            Set(container, "m_initialGuard", false);
            container.DisallowNullSelection = false;
            host.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            var scroll = host.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = host; scroll.horizontal = false; scroll.inertia = false;
            var content = Rect("Content", host);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1000);
            scroll.content = content;
            var item = Rect("Last item", content);
            item.anchorMin = item.anchorMax = item.pivot = new Vector2(.5f, 1f);
            item.anchoredPosition = new Vector2(0f, -900f);
            item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            var selectable = item.gameObject.AddComponent<Button>();
            Set(container, "m_scrollRect", scroll);
            Set(container, "m_scrollDuration", 1f);
            Set(container, "m_scrollPadding", 0f);
            if(mode == 3) Set(container, "m_hideAnimations", new IUiAnimation[] { new ProbeAnimation { TweenDuration = 10f } });
            host.gameObject.SetActive(true);
            yield return null;
            scroll.verticalNormalizedPosition = 1f;
            container.OnSelectChanged.Invoke(selectable);
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.999f), "Selection must start scrolling.");
            if(mode == 0) container.enabled = false;
            else if(mode == 1) host.gameObject.SetActive(false);
            else container.Hide();
            if(mode == 3) Assert.That(host.gameObject.activeSelf, Is.True, "Hide animation is still running.");
            var stoppedPosition = scroll.verticalNormalizedPosition;
            Assert.That(stoppedPosition, Is.GreaterThan(.1f), "Interrupt before reaching the destination.");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(stoppedPosition).Within(.0001f));
            container.OnSelectChanged.Invoke(selectable);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(stoppedPosition).Within(.0001f), "Ignore selection notifications while stopped.");
            if(mode == 0) container.enabled = true;
            else if(mode == 1) host.gameObject.SetActive(true);
            else container.Show();
            container.OnSelectChanged.Invoke(selectable);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(stoppedPosition - .01f), "New selection notifications must work after reactivation.");
        } finally {
            foreach(var rect in root.GetComponentsInChildren<RectTransform>(true)) rect.DOKill();
            Object.DestroyImmediate(root.gameObject);
            Time.timeScale = originalTimeScale;
        }
    }

    [TestCase(false, 0)] [TestCase(false, 1)] [TestCase(false, 2)] [TestCase(false, 3)] [TestCase(false, 4)]
    [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)] [TestCase(true, 3)] [TestCase(true, 4)]
    public void TextColorMultiplierMatchesGraphic(bool toggle, int state) {
        var root = Rect("Text multiplier");
        try {
            var selectable = CreateTextColorControl(root, toggle, out var text, out var graphic);
            var colors = selectable.colors;
            colors.normalColor = new Color(.1f, .2f, .3f, .4f);
            colors.highlightedColor = new Color(.2f, .1f, .4f, .3f);
            colors.pressedColor = new Color(.3f, .4f, .1f, .2f);
            colors.selectedColor = new Color(.4f, .3f, .2f, .1f);
            colors.disabledColor = new Color(.15f, .25f, .35f, .45f);
            selectable.colors = colors; Set(selectable, "textColors", colors);
            TransitionTextColor(selectable, state, true);
            Assert.That(text.color, Is.EqualTo(aGuiUtils.GetStateColor(colors, state) * 2f));
            Assert.That(graphic.canvasRenderer.GetColor(), Is.EqualTo(text.color));
            Assert.That(text.canvasRenderer.GetColor(), Is.EqualTo(Color.white), "Do not multiply the vertex color twice.");
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [UnityTest]
    public IEnumerator ButtonTextColorFadeAppliesMultiplier() => VerifyTextColorFade(false);

    [UnityTest]
    public IEnumerator ToggleTextColorFadeAppliesMultiplier() => VerifyTextColorFade(true);

    private static IEnumerator VerifyTextColorFade(bool toggle) {
        var root = Rect("Text multiplier fade");
        try {
            var selectable = CreateTextColorControl(root, toggle, out var text, out _);
            text.color = Color.black;
            TransitionTextColor(selectable, 0, false);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(text.color, Is.EqualTo(selectable.colors.normalColor * 2f));
            Assert.That(text.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static Selectable CreateTextColorControl(RectTransform root, bool toggle, out TMP_Text text, out Image graphic) {
        root.gameObject.SetActive(false);
        graphic = root.gameObject.AddComponent<Image>();
        var selectable = toggle ? (Selectable)root.gameObject.AddComponent<aToggle>() : root.gameObject.AddComponent<aButton>();
        text = Rect("Text", root).gameObject.AddComponent<TextMeshProUGUI>();
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(.2f, .3f, .4f, .25f);
        colors.colorMultiplier = 2f; colors.fadeDuration = .05f;
        Set(selectable, "targetText", text); Set(selectable, "textColors", colors);
        selectable.targetGraphic = graphic; selectable.colors = colors;
        root.gameObject.SetActive(true);
        return selectable;
    }

    private static void TransitionTextColor(Selectable selectable, int state, bool instant) {
        var method = selectable.GetType().GetMethod("DoStateTransition", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(selectable, new object[] { Enum.ToObject(method.GetParameters()[0].ParameterType, state), instant });
    }

    private sealed class HeldShortcut : IShortCut {
        public bool IsPressed { get; set; }
    }

    private static void UpdateShortcut(aButton button) => typeof(aButton)
        .GetMethod("UpdateShortCutState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, null);

    [UnityTest]
    public IEnumerator ReenablingSubOnInactiveObjectResumesPendingSync() {
        var root = Rect("Hidden sub recovery");
        try {
            var main = InactiveContainer<aStaticContainer>(root, true);
            main.gameObject.SetActive(true);
            var sub = InactiveContainer<aSubContainer>(root, true, main);
            sub.gameObject.SetActive(true);
            main.Hide();
            sub.enabled = false;
            main.Show();
            yield return null;
            Assert.That(sub.gameObject.activeSelf, Is.False);
            sub.enabled = true; // 非アクティブなGameObjectなのでOnEnableは発火しない。
            yield return null;
            yield return null;
            Assert.That(sub.gameObject.activeSelf, Is.True);
            Assert.That(sub.IsVisible, Is.True);
            Assert.That(typeof(aSubContainer).GetField("m_pendingEnabledSync", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sub), Is.Null);
            main.Hide(); main.Show();
            Assert.That(sub.gameObject.activeSelf, Is.True);
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [UnityTest]
    public IEnumerator DestroyingSubDisposesPendingSync() {
        var root = Rect("Pending sync cleanup");
        var main = InactiveContainer<aStaticContainer>(root, true);
        main.gameObject.SetActive(true);
        var sub = InactiveContainer<aSubContainer>(root, true, main);
        sub.gameObject.SetActive(true);
        sub.enabled = false;
        var field = typeof(aSubContainer).GetField("m_pendingEnabledSync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field.GetValue(sub), Is.Not.Null);
        Object.DestroyImmediate(root.gameObject);
        Assert.That(field.GetValue(sub), Is.Null);
        yield return null;
    }

    [TestCase(270f, false, false)]
    [TestCase(360f, false, false)]
    [TestCase(-360f, false, false)]
    [TestCase(720f, false, true)]
    [TestCase(270f, true, false)]
    [TestCase(360f, true, false)]
    [TestCase(-360f, true, false)]
    [TestCase(720f, true, true)]
    public void RotationPreservesAngleTravelAndBaseRotation(float angle, bool targetMode, bool yoyo) {
        var rect = Rect("Rotation travel");
        Tween tween = null;
        try {
            var baseRotation = Quaternion.Euler(20f, 30f, 40f);
            rect.localRotation = baseRotation;
            var info = rect.gameObject.AddComponent<aGuiInfo>();
            Set(info, "m_rectTransform", rect); info.Refresh();
            IUiAnimation animation = targetMode ? new RotateTarget() : new Rotate();
            if(targetMode) Set(animation, "m_target", info);
            var start = new Vector3(5f, 10f, -20f);
            var end = new Vector3(5f, 10f, angle - 20f);
            Set(animation, "m_startValue", start); Set(animation, "m_endValue", end);
            Set(animation, "m_duration", 1f); Set(animation, "m_ease", Ease.Linear); Set(animation, "m_isYoYo", yoyo);
            tween = animation.DoAnimate(null, rect, info.OriginalRectTransformValues);
            for(int i = 0; i <= 8; i++) {
                float time = i / 8f;
                tween.Goto(time);
                float progress = yoyo ? (time <= .5f ? time * 2 : (1 - time) * 2) : time;
                var expected = baseRotation * Quaternion.Euler(Vector3.Lerp(start, end, progress));
                Assert.That(Quaternion.Angle(expected, rect.localRotation), Is.LessThan(.1f), "Incorrect angle at " + time);
            }
            tween = animation.DoAnimate(null, rect, info.OriginalRectTransformValues);
            Assert.That(Quaternion.Angle(baseRotation * Quaternion.Euler(start), rect.localRotation), Is.LessThan(.1f), "Retrigger must not accumulate rotation.");
        } finally { tween?.Kill(); Object.DestroyImmediate(rect.gameObject); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RightClickDoesNotConsumeHeldLeftClick(bool useGuard) {
        var rect = Rect("Mouse overlap");
        try {
            var button = rect.gameObject.AddComponent<aButton>();
            Set(button, "useMultipleInputGuard", useGuard);
            int leftClicks = 0, rightClicks = 0;
            button.onClick.AddListener(() => leftClicks++);
            ((UnityEngine.Events.UnityEvent)typeof(aButton).GetField("onRightClick", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button)).AddListener(() => rightClicks++);
            var left = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            var right = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            button.OnPointerDown(left);
            button.OnPointerDown(right); button.OnPointerUp(right); button.OnPointerClick(right);
            Assert.That(leftClicks, Is.Zero);
            button.OnPointerUp(left); button.OnPointerClick(left);
            Assert.That(leftClicks, Is.EqualTo(1));
            Assert.That(rightClicks, Is.EqualTo(useGuard ? 0 : 1));
        } finally { Object.DestroyImmediate(rect.gameObject); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReleasingShortcutCannotClickForHeldMouse(bool useGuard) {
        var rect = Rect("Shortcut overlap");
        try {
            var button = rect.gameObject.AddComponent<aButton>();
            Set(button, "useMultipleInputGuard", useGuard);
            var shortcut = new HeldShortcut(); Set(button, "shortCut", shortcut);
            int clicks = 0; button.onClick.AddListener(() => clicks++);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            button.OnPointerDown(pointer);
            shortcut.IsPressed = true; UpdateShortcut(button);
            shortcut.IsPressed = false; UpdateShortcut(button);
            Assert.That(clicks, Is.Zero);
            button.OnPointerUp(pointer); button.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1));
        } finally { Object.DestroyImmediate(rect.gameObject); }
    }

    [Test]
    public void MouseEventsCannotReleaseHeldShortcut() {
        var rect = Rect("Held shortcut");
        try {
            var button = rect.gameObject.AddComponent<aButton>(); Set(button, "useMultipleInputGuard", false);
            var shortcut = new HeldShortcut(); Set(button, "shortCut", shortcut);
            int clicks = 0; button.onClick.AddListener(() => clicks++);
            shortcut.IsPressed = true; UpdateShortcut(button);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            button.OnPointerDown(pointer); button.OnPointerUp(pointer); button.OnPointerClick(pointer);
            Assert.That(clicks, Is.Zero);
            shortcut.IsPressed = false; UpdateShortcut(button);
            Assert.That(clicks, Is.EqualTo(1));
        } finally { Object.DestroyImmediate(rect.gameObject); }
    }

    [Test]
    public void SecondPointerCannotConsumeFirstPointerClick() {
        var rect = Rect("Two pointers");
        try {
            var button = rect.gameObject.AddComponent<aButton>(); Set(button, "useMultipleInputGuard", false);
            int clicks = 0; button.onClick.AddListener(() => clicks++);
            var first = new PointerEventData(EventSystem.current) { pointerId = 1, button = PointerEventData.InputButton.Left };
            var second = new PointerEventData(EventSystem.current) { pointerId = 2, button = PointerEventData.InputButton.Left };
            button.OnPointerDown(first);
            button.OnPointerDown(second); button.OnPointerUp(second); button.OnPointerClick(second);
            Assert.That(clicks, Is.Zero);
            button.OnPointerUp(first); button.OnPointerClick(first);
            Assert.That(clicks, Is.EqualTo(1));
        } finally { Object.DestroyImmediate(rect.gameObject); }
    }

    private static T InactiveContainer<T>(Transform parent, bool visible, aContainerBase main = null) where T : aContainerBase {
        var rect = Rect(typeof(T).Name, parent);
        rect.gameObject.SetActive(false);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        var info = rect.gameObject.AddComponent<aGuiInfo>();
        Set(info, "m_rectTransform", rect);
        info.Refresh();
        var container = rect.gameObject.AddComponent<T>();
        Set(container, "m_canvasGroup", group);
        Set(container, "m_guiInfo", info);
        Set(container, "m_isVisible", visible);
        if(container is aSubContainer sub) sub.MainContainer = main;
        return container;
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public void ReentrantVisibilityChangeKeepsLatestRequestAndSubInSync(bool requestShow, bool useObservable) {
        var root = Rect("Reentrant visibility");
        IDisposable subscription = null;
        try {
            var main = InactiveContainer<aStaticContainer>(root, !requestShow);
            main.gameObject.SetActive(true);
            var sub = InactiveContainer<aSubContainer>(root, !requestShow, main);
            sub.gameObject.SetActive(true);
            var show = new FadeCanvasGroup();
            var hide = new FadeCanvasGroup();
            Set(show, "m_startValue", 0f); Set(show, "m_endValue", 1f);
            Set(hide, "m_startValue", 1f); Set(hide, "m_endValue", 0f);
            Set(main, "m_showAnimations", new IUiAnimation[] { show });
            Set(main, "m_hideAnimations", new IUiAnimation[] { hide });
            Action reverse = () => { if(requestShow) main.Hide(); else main.Show(); };
            if(useObservable) subscription = (requestShow ? main.ShowStartObservable : main.HideStartObservable).Subscribe(_ => reverse());
            else if(requestShow) main.OnShow.AddListener(() => reverse());
            else main.OnHide.AddListener(() => reverse());
            if(requestShow) main.Show(); else main.Hide();
            main.RectTransform.DOComplete();
            Assert.That(main.IsVisible, Is.EqualTo(!requestShow));
            Assert.That(main.gameObject.activeSelf, Is.EqualTo(!requestShow));
            Assert.That(main.CanvasGroup.alpha, Is.EqualTo(requestShow ? 0f : 1f).Within(.001f));
            Assert.That(main.CanvasGroup.blocksRaycasts, Is.EqualTo(!requestShow));
            Assert.That(sub.IsVisible, Is.EqualTo(main.IsVisible));
            Assert.That(sub.gameObject.activeSelf, Is.EqualTo(main.IsVisible));
        } finally {
            subscription?.Dispose();
            foreach(var rect in root.GetComponentsInChildren<RectTransform>(true)) rect.DOKill();
            Object.DestroyImmediate(root.gameObject);
        }
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void SubInitialSyncFiresVisibilityEventOnce(bool mainVisible, bool subVisible) {
        var root = Rect("Initial sub sync");
        try {
            var main = InactiveContainer<aStaticContainer>(root, mainVisible);
            main.gameObject.SetActive(true);
            var sub = InactiveContainer<aSubContainer>(root, subVisible, main);
            int shows = 0, hides = 0;
            sub.OnShow.AddListener(() => shows++);
            sub.OnHide.AddListener(() => hides++);
            sub.gameObject.SetActive(true);
            Assert.That(shows, Is.EqualTo(mainVisible ? 1 : 0));
            Assert.That(hides, Is.EqualTo(mainVisible ? 0 : 1));
            Assert.That(sub.IsVisible, Is.EqualTo(mainVisible));
            Assert.That(sub.gameObject.activeSelf, Is.EqualTo(mainVisible));
            main.IsVisible = !mainVisible;
            Assert.That(sub.IsVisible, Is.EqualTo(!mainVisible));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [UnityTest]
    public IEnumerator InspectorMainChangeDisconnectsOldMain() {
        var root = Rect("Inspector sub sync");
        try {
            var a = InactiveContainer<aStaticContainer>(root, true);
            var b = InactiveContainer<aStaticContainer>(root, true);
            a.gameObject.SetActive(true); b.gameObject.SetActive(true);
            var sub = InactiveContainer<aSubContainer>(root, true, a);
            sub.gameObject.SetActive(true);
#if UNITY_EDITOR
            var serialized = new UnityEditor.SerializedObject(sub);
            serialized.FindProperty("m_mainContainer").objectReferenceValue = b;
            serialized.ApplyModifiedPropertiesWithoutUndo();
#endif
            yield return null;
            yield return null;
            a.Hide();
            Assert.That(sub.IsVisible, Is.True, "The old main must no longer control the sub.");
            b.Hide();
            Assert.That(sub.IsVisible, Is.False);
            b.Show();
            Assert.That(sub.gameObject.activeSelf, Is.True);
#if UNITY_EDITOR
            serialized.Update();
            serialized.FindProperty("m_mainContainer").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
#endif
            yield return null;
            yield return null;
            b.Hide();
            Assert.That(sub.IsVisible, Is.True, "Clearing the reference must disconnect the previous main.");
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [Test]
    public void DisabledSubStopsSyncAndResyncsOnEnable() {
        var root = Rect("Disabled sub sync");
        try {
            var main = InactiveContainer<aStaticContainer>(root, true);
            main.gameObject.SetActive(true);
            var sub = InactiveContainer<aSubContainer>(root, true, main);
            sub.gameObject.SetActive(true);
            int shows = 0, hides = 0;
            sub.OnShow.AddListener(() => shows++); sub.OnHide.AddListener(() => hides++);
            sub.enabled = false;
            main.Hide(); main.Show(); main.Hide();
            Assert.That(sub.gameObject.activeSelf, Is.True);
            Assert.That(shows + hides, Is.Zero);
            sub.enabled = true;
            Assert.That(sub.IsVisible, Is.False);
            Assert.That(hides, Is.EqualTo(1));
            main.Show();
            Assert.That(sub.gameObject.activeSelf, Is.True, "Normal Hide must not break the connection needed for Show.");
            Assert.That(shows, Is.EqualTo(1));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [Test]
    public void MainPropertyChangeWhileDisabledUsesNewMainOnEnable() {
        var root = Rect("Disabled main reassignment");
        try {
            var a = InactiveContainer<aStaticContainer>(root, true);
            var b = InactiveContainer<aStaticContainer>(root, false);
            a.gameObject.SetActive(true); b.gameObject.SetActive(true);
            var sub = InactiveContainer<aSubContainer>(root, true, a);
            sub.gameObject.SetActive(true);
            sub.enabled = false;
            sub.MainContainer = b;
            Assert.That(sub.IsVisible, Is.True);
            sub.enabled = true;
            Assert.That(sub.IsVisible, Is.False);
            a.Hide(); a.Show();
            Assert.That(sub.IsVisible, Is.False);
            b.Show();
            Assert.That(sub.IsVisible, Is.True);
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

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
