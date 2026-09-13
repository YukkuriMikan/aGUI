using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ANest.UI;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class aGuiRegressionTests {
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    public void TextFitterPadsChildrenWithoutChangingTheirAnchors(int anchorMode, bool animate) {
        var root = Rect("Text padding", null, new Vector2(300, 100));
        try {
            var child = Rect("Text", root, new Vector2(200, 60));
            if(anchorMode == 1) { child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one; }
            if(anchorMode == 2) { child.anchorMin = new Vector2(.1f, .3f); child.anchorMax = new Vector2(.7f, .8f); }
            child.pivot = new Vector2(.2f, .8f);
            var min = child.anchorMin;
            var max = child.anchorMax;
            var text = child.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = "ABC";
            var fitter = root.gameObject.AddComponent<aTextMeshSizeFitter>();
            Set(fitter, "m_targetText", text);
            Set(fitter, "m_padding", new RectOffset(10, 20, 5, 15));
            Set(fitter, "m_useAnimation", animate);
            fitter.ApplyFitting();
            if(animate) {
                var tween = TweenField(fitter, "m_sizeTween");
                tween.Goto(.15f);
                Assert.That(child.rect.width, Is.EqualTo(root.rect.width - 30).Within(.001f));
                tween.Complete();
            }
            Assert.That(child.anchorMin, Is.EqualTo(min));
            Assert.That(child.anchorMax, Is.EqualTo(max));
            Assert.That(child.rect.size.x, Is.EqualTo(text.preferredWidth).Within(.001f));
            Assert.That(child.rect.size.y, Is.EqualTo(80).Within(.001f));
            Assert.That(child.localPosition.x + child.rect.xMin, Is.EqualTo(root.rect.xMin + 10).Within(.001f));
            Assert.That(child.localPosition.y + child.rect.yMax, Is.EqualTo(root.rect.yMax - 5).Within(.001f));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [Test] public void TextPaddingLargerThanParentDoesNotInvertChild() {
        var root = Rect("Small parent", null, new Vector2(20, 10));
        try {
            var child = Rect("Text", root, new Vector2(100, 60));
            var text = child.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            var fitter = root.gameObject.AddComponent<aTextMeshSizeFitter>();
            Set(fitter, "m_targetText", text);
            Set(fitter, "m_fitWidth", false);
            Set(fitter, "m_padding", new RectOffset(10, 20, 5, 15));
            fitter.ApplyFitting();
            Assert.That(child.rect.size, Is.EqualTo(Vector2.zero));
        } finally { Object.DestroyImmediate(root.gameObject); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TextColorIsAppliedOnceToRenderedVertices(bool ruby) {
        var root = Rect("Color", null, new Vector2(300, 200));
        root.gameObject.AddComponent<Canvas>();
        try {
            var go = Rect("Text", root, new Vector2(250, 150)).gameObject;
            TMPro.TextMeshProUGUI text = ruby ? go.AddComponent<aTextMeshProUgui>() : go.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = ruby ? "<ruby=abc>ABC</ruby>" : "ABC";
            var target = new Color(.4f, .6f, .8f, .5f);
            text.canvasRenderer.SetColor(new Color(.2f, .2f, .2f, .2f));
            aGuiUtils.SetTextColorImmediate(text, target);
            text.ForceMeshUpdate();
            Assert.That(text.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
            for(int i = 0; i < text.textInfo.meshInfo[0].vertexCount; i++) {
                Color rendered = (Color)text.textInfo.meshInfo[0].colors32[i] * text.canvasRenderer.GetColor();
                Assert.That(rendered.r, Is.EqualTo(target.r).Within(1f / 255));
                Assert.That(rendered.a, Is.EqualTo(target.a).Within(1f / 255));
            }
        } finally { Object.DestroyImmediate(root.gameObject); }
    }
    static void SetSize(RectTransform rect, Vector2 size) {
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }
    static Tween TweenField(object target, string name) => (Tween)target.GetType().GetField(name, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);

    static void Set(object target, string name, object value) {
        for(var type=target.GetType(); type!=null; type=type.BaseType) {
            var field=type.GetField(name, BindingFlags.Instance|BindingFlags.NonPublic);
            if(field!=null) { field.SetValue(target,value); return; }
        }
        throw new Exception(name);
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size) {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        if(parent!=null) rect.SetParent(parent,false);
        SetSize(rect, size);
        return rect;
    }
    [TestCase(false)]
    [TestCase(true)]
    public void CursorMatchesStretchedSelectableActualSize(bool animate) {
        var root=Rect("Cursor audit",null,new Vector2(500,400));
        try {
            var target=Rect("Target",root,Vector2.zero);
            target.anchorMin=Vector2.zero; target.anchorMax=Vector2.one;
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,100);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,80);
            var cursorRect=Rect("Cursor",root,new Vector2(20,20));
            var cursor=root.gameObject.AddComponent<aCursorBase>();
            Set(cursor,"m_cursorRect",cursorRect);
            Set(cursor,"m_updateMode",aCursorBase.UpdateMode.OnSelectChanged);
            typeof(aCursorBase).GetMethod("OnTargetRectChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cursor,new object[]{target});
            if(animate) {
                SetSize(target, new Vector2(160,120));
                typeof(aCursorBase).GetMethod("OnTargetRectChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cursor,new object[]{target});
                var tween = TweenField(cursor,"m_sizeTween");
                Assert.That(tween, Is.Not.Null);
                tween.Complete();
            }
            Assert.That(cursorRect.rect.size,Is.EqualTo(target.rect.size),"Cursor must match the selected 100x80 rectangle.");
        } finally {Object.DestroyImmediate(root.gameObject);}
    }
    [TestCase(false)]
    [TestCase(true)]
    public void RectSyncMatchesActualSizeAcrossDifferentParents(bool syncAnchors) {
        var root=Rect("Sync",null,new Vector2(500,400));
        try {
            var parent=Rect("Small parent",root,new Vector2(200,150));
            var target=Rect("Target",root,Vector2.zero);
            target.anchorMin=Vector2.zero;target.anchorMax=Vector2.one;
            SetSize(target,new Vector2(100,80));
            var actual=Rect("Actual",parent,new Vector2(20,20));
            var sync=actual.gameObject.AddComponent<RectSync>();
            Set(sync,"target",target);Set(sync,"syncAnchors",syncAnchors);
            sync.Sync();
            Assert.That(actual.rect.size,Is.EqualTo(new Vector2(100,80)));
            SetSize(parent,new Vector2(700,600));
            sync.Sync();
            Assert.That(actual.rect.size,Is.EqualTo(new Vector2(100,80)));
        } finally {Object.DestroyImmediate(root.gameObject);}
    }
    [Test]
    public void AnimatedTextFitterKeepsDisabledAxisWithStretchAnchors() {
        var root=Rect("Parent",null,new Vector2(500,400));
        try {
            var child=Rect("Text",root,Vector2.zero);
            child.anchorMin=Vector2.zero;child.anchorMax=Vector2.one;
            SetSize(child,new Vector2(100,80));
            var text=child.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            var fitter=child.gameObject.AddComponent<aTextMeshSizeFitter>();
            Set(fitter,"m_targetText",text);Set(fitter,"m_useAnimation",true);
            Set(fitter,"m_padding",new RectOffset(10,20,30,40));
            fitter.ApplyFitting();
            var tween=TweenField(fitter,"m_sizeTween");
            Assert.That(tween,Is.Not.Null);
            tween.Complete();
            Assert.That(child.rect.width,Is.EqualTo(text.preferredWidth+30).Within(0.001f));
            Assert.That(child.rect.height,Is.EqualTo(80f).Within(0.001f));
        } finally {Object.DestroyImmediate(root.gameObject);}
    }
    [UnityTest]
    public IEnumerator RecoveryUsesPreviousSelectionInsteadOfFirstChild() => RecoverHistory(0);
    [UnityTest]
    public IEnumerator RecoverySkipsInactiveHistory() => RecoverHistory(1);
    [UnityTest]
    public IEnumerator RecoverySkipsNonInteractableHistory() => RecoverHistory(2);
    [UnityTest]
    public IEnumerator RecoverySkipsDestroyedHistory() => RecoverHistory(3);
    [UnityTest]
    public IEnumerator RecoverySkipsPreventFocusHistory() => RecoverHistory(4);

    static IEnumerator RecoverHistory(int invalidMode) {
        var es=new GameObject("History EventSystem",typeof(EventSystem));
        var root=Rect("History",null,new Vector2(300,300));
        try {
            aGuiManager.ClearSelectionHistory();aGuiManager.UpdateEventSystem();
            var first=Rect("First fallback",root,new Vector2(100,80)).gameObject.AddComponent<Button>();
            var older=Rect("Older",root,new Vector2(100,80)).gameObject.AddComponent<Button>();
            var previous=Rect("Previous",root,new Vector2(100,80)).gameObject.AddComponent<aButton>();
            var dead=Rect("Selected",root,new Vector2(100,80)).gameObject.AddComponent<Button>();
            var outside=Rect("Outside container",root,new Vector2(100,80)).gameObject.AddComponent<aButton>();
            var container=root.gameObject.AddComponent<aNormalSelectableContainer>();
            Set(container,"m_initialGuard",false);
            container.SetChildSelectableList(new List<Selectable>{first,older,previous,dead});
            yield return null;yield return null;
            older.Select();previous.Select();outside.Select();dead.Select();
            if(invalidMode==1)previous.gameObject.SetActive(false);
            if(invalidMode==2)previous.interactable=false;
            if(invalidMode==3)Object.DestroyImmediate(previous.gameObject);
            if(invalidMode==4)previous.PreventFocus=true;
            Object.DestroyImmediate(dead.gameObject);
            es.GetComponent<EventSystem>().SetSelectedGameObject(null);
            yield return null;yield return null;
            var expected=invalidMode==0 ? previous.gameObject : older.gameObject;
            Assert.That(es.GetComponent<EventSystem>().currentSelectedGameObject,Is.SameAs(expected));
            Assert.That(aGuiManager.CurrentSelectable.gameObject,Is.SameAs(expected));
        } finally {Object.DestroyImmediate(root.gameObject);Object.DestroyImmediate(es);aGuiManager.ClearSelectionHistory();}
    }
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void TextFitterDoesNotChangeDisabledAxesOnSameObject(bool fitWidth, bool fitHeight) {
        var root=Rect("Text fitter audit",null,new Vector2(100,80));
        try {
            var text=root.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            var fitter=root.gameObject.AddComponent<aTextMeshSizeFitter>();
            Set(fitter,"m_targetText",text); Set(fitter,"m_fitWidth",fitWidth); Set(fitter,"m_fitHeight",fitHeight);
            SetSize(root, new Vector2(100,80));
            fitter.ApplyFitting();
            Assert.That(root.rect.width,Is.EqualTo(fitWidth ? text.preferredWidth : 100f).Within(0.001f));
            Assert.That(root.rect.height,Is.EqualTo(fitHeight ? text.preferredHeight : 80f).Within(0.001f));
        } finally {Object.DestroyImmediate(root.gameObject);}
    }
    static ScrollRect Scroll(out RectTransform top, out RectTransform bottom) {
        var root=Rect("Scroll audit",null,new Vector2(100,200));
        var viewport=Rect("Viewport",root,new Vector2(100,200));
        var content=Rect("Content",viewport,new Vector2(100,1000));
        content.anchorMin=content.anchorMax=new Vector2(0.5f,1); content.pivot=new Vector2(0.5f,1);
        top=Rect("Top",content,new Vector2(80,40));
        bottom=Rect("Bottom",content,new Vector2(80,40));
        top.anchorMin=top.anchorMax=bottom.anchorMin=bottom.anchorMax=new Vector2(0.5f,1);
        top.anchoredPosition=new Vector2(0,-30); bottom.anchoredPosition=new Vector2(0,-950);
        var scroll=root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport=viewport; scroll.content=content; scroll.horizontal=false; scroll.inertia=false;
        scroll.Rebuild(CanvasUpdate.PostLayout); scroll.verticalNormalizedPosition=1;
        return scroll;
    }
    [UnityTest]
    public IEnumerator VisibleNewSelectionCancelsOldScroll() {
        var scroll=Scroll(out var top,out var bottom);
        CancellationTokenSource cts=null;
        try {
            aNormalScrollContainer.ScrollToItem(scroll,bottom,null,0.3f,0,ref cts);
            // 最初の非同期処理も開始フレームのdeltaTime分だけ移動する。
            // 実行中の処理を残しつつ、次の選択先が表示済みという前提を確実に作る。
            scroll.verticalNormalizedPosition = 1f;
            var corners=new Vector3[4]; top.GetWorldCorners(corners);
            Assert.That(scroll.viewport.InverseTransformPoint(corners[1]).y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+0.001f));
            aNormalScrollContainer.ScrollToItem(scroll,top,bottom,0.3f,0,ref cts);
            Assert.That(cts,Is.Null,"Visible selection must cancel without creating another scroll task.");
            yield return new WaitForSecondsRealtime(0.4f);
            top.GetWorldCorners(corners);
            Assert.That(scroll.viewport.InverseTransformPoint(corners[1]).y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+0.001f),"Old scroll must not move the newly selected top item out of view.");
        } finally {cts?.Cancel();cts?.Dispose();Object.DestroyImmediate(scroll.gameObject);}
    }
    [UnityTest]
    public IEnumerator ClearingSelectionCancelsOldScroll() {
        var scroll=Scroll(out _,out var bottom);
        CancellationTokenSource cts=null;
        try {
            aNormalScrollContainer.ScrollToItem(scroll,bottom,null,0.3f,0,ref cts);
            aNormalScrollContainer.ScrollToItem(scroll,null,bottom,0.3f,0,ref cts);
            var position=scroll.verticalNormalizedPosition;
            Assert.That(cts,Is.Null);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(position).Within(0.001f));
        } finally {cts?.Cancel();cts?.Dispose();Object.DestroyImmediate(scroll.gameObject);}
    }
    [UnityTest]
    public IEnumerator DestroyedSelectableDoesNotBreakSelectionRecovery() {
        var es=new GameObject("Audit EventSystem",typeof(EventSystem));
        var root=Rect("Selection audit",null,new Vector2(300,300));
        try {
            aGuiManager.ClearSelectionHistory();
            aGuiManager.UpdateEventSystem();
            var dead=Rect("Destroyed",root,new Vector2(100,80)).gameObject.AddComponent<Button>();
            var live=Rect("Survivor",root,new Vector2(100,80)).gameObject.AddComponent<Button>();
            var container=root.gameObject.AddComponent<aNormalSelectableContainer>();
            Set(container,"m_initialGuard",false);
            container.SetChildSelectableList(new List<Selectable>{dead,live});
            yield return null;
            yield return null;
            Object.DestroyImmediate(dead.gameObject);
            es.GetComponent<EventSystem>().SetSelectedGameObject(null);
            yield return null;
            yield return null;
            Assert.That(es.GetComponent<EventSystem>().currentSelectedGameObject,Is.SameAs(live.gameObject));
        } finally {Object.DestroyImmediate(root.gameObject);Object.DestroyImmediate(es);aGuiManager.ClearSelectionHistory();}
    }
    [TestCase(aLayoutGroupCircular.CircularMoveType.Clockwise,1)]
    [TestCase(aLayoutGroupCircular.CircularMoveType.CounterClockwise,-1)]
    public void CircularDirectionMatchesSetting(aLayoutGroupCircular.CircularMoveType direction,int expectedXSign) {
        var root=Rect("Circular audit",null,new Vector2(300,300));
        try {
            var child=Rect("Child",root,new Vector2(20,20));
            child.anchorMin=child.anchorMax=new Vector2(0,1); child.anchoredPosition=new Vector2(150,-50);
            var layout=root.gameObject.AddComponent<aLayoutGroupCircular>();
            layout.StartAngle=180;
            Set(layout,"useAnimation",true);Set(layout,"setNavigation",false);Set(layout,"animationEase",Ease.Linear);
            Set(layout,"circularMoveType",direction);
            layout.AlignWithCollection();
            var tweens=(System.Collections.IDictionary)typeof(aLayoutGroupBase).GetField("m_positionTweens",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(layout);
            var tween=(Tween)tweens[child];
            TweenExtensions.Goto(tween,layout.AnimationDuration*0.5f,false);
            Assert.That((child.anchoredPosition.x-150)*expectedXSign,Is.GreaterThan(90),"Clockwise from the top must go through the right side.");
        } finally {Object.DestroyImmediate(root.gameObject);}
    }
    [TestCase(false, aLayoutGroupCircular.NavigationType.Default)]
    [TestCase(true, aLayoutGroupCircular.NavigationType.Default)]
    [TestCase(false, aLayoutGroupCircular.NavigationType.TwoAxis)]
    [TestCase(true, aLayoutGroupCircular.NavigationType.TwoAxis)]
    [TestCase(false, aLayoutGroupCircular.NavigationType.TwoAxisReverse)]
    [TestCase(true, aLayoutGroupCircular.NavigationType.TwoAxisReverse)]
    public void CircularNavigationMatchesPhysicalOrder(bool reverse, aLayoutGroupCircular.NavigationType mode) {
        var root=Rect("Circular navigation audit",null,new Vector2(300,300));
        try {
            var layout=root.gameObject.AddComponent<aLayoutGroupCircular>();
            Set(layout,"reverseArrangement",reverse);Set(layout,"navigationType",mode);
            var buttons=new Button[4];
            for(int i=0;i<4;i++)buttons[i]=Rect("Button "+i,root,new Vector2(20,20)).gameObject.AddComponent<Button>();
            layout.AlignWithCollectionNonAnimate();
            var top=buttons[reverse ? 3 : 0];
            var left=buttons[reverse ? 0 : 3];
            var right=buttons[reverse ? 2 : 1];
            var invert=mode==aLayoutGroupCircular.NavigationType.TwoAxisReverse;
            Assert.That(top.navigation.selectOnLeft,Is.SameAs(invert ? right : left));
            Assert.That(top.navigation.selectOnRight,Is.SameAs(invert ? left : right));
        } finally {Object.DestroyImmediate(root.gameObject);}
    }
}
