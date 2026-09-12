using System;
using System.Collections;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>フィット後の配置、購読の更新、有効化時の同期を検証する。</summary>
public class aContentSizeFitterTests {
	[TestCase(true, true)]
	[TestCase(true, false)]
	[TestCase(false, true)]
	[TestCase(false, false)]
	public void PaddingFitsOnlyEnabledAxesAndPreservesStretchedChildren(bool fitWidth, bool fitHeight) {
		var layout = CreateLayout("Padding", out var child);
		try {
			SetField(layout, "padding", new RectOffset(10, 20, 30, 40));
			child.anchorMin = Vector2.zero;
			child.anchorMax = Vector2.one;
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80f);
			var childPosition = child.position;
			var fitter = AddFitter(layout);
			SetField(fitter, "m_fitWidth", fitWidth);
			SetField(fitter, "m_fitHeight", fitHeight);
			SetField(fitter, "m_padding", new RectOffset(7, 13, 17, 23));
			var rect = (RectTransform)layout.transform;
			var referencePoint = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax));

			fitter.ApplyFitting();
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(fitWidth ? 150f : 300f, fitHeight ? 190f : 300f)));
			var fittedPosition = rect.position;
			for(var i = 0; i < 4; i++) fitter.ApplyFitting();
			Assert.That(Vector3.Distance(rect.position, fittedPosition), Is.LessThan(0.001f));
			Assert.That(Vector3.Distance(rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax)), referencePoint), Is.LessThan(0.001f));
			Assert.That(Vector3.Distance(child.position, childPosition), Is.LessThan(0.001f));
			Assert.That(child.rect.size, Is.EqualTo(new Vector2(100f, 80f)));
			Assert.That(child.anchorMin, Is.EqualTo(Vector2.zero));
			Assert.That(child.anchorMax, Is.EqualTo(Vector2.one));

			SetField(fitter, "m_padding", new RectOffset());
			fitter.ApplyFitting();
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(fitWidth ? 130f : 300f, fitHeight ? 150f : 300f)),
				"Removing fitter padding must retain the layout group's padding.");
		} finally { Object.DestroyImmediate(layout.gameObject); }
	}

	[TestCase(true)]
	[TestCase(false)]
	public void LayoutNotificationsIncludeFitterPaddingWithoutAccumulatingIt(bool preservePositions) {
		var layout = CreateLayout("Padding notification", out _);
		try {
			SetField(layout, "padding", new RectOffset(10, 20, 30, 40));
			var fitter = AddFitter(layout);
			SetField(fitter, "m_preserveChildPositions", preservePositions);
			SetField(fitter, "m_padding", new RectOffset(7, 13, 17, 23));
			var observer = new LayoutObserver();
			using var subscription = layout.CompleteLayoutAsObservable.Subscribe(observer);
			for(var i = 0; i < 3; i++) {
				layout.AlignWithCollectionNonAnimate();
				Assert.That(((RectTransform)layout.transform).rect.size, Is.EqualTo(new Vector2(150f, 190f)));
				Assert.That(observer.Count, Is.EqualTo(i + 1));
			}
		} finally { Object.DestroyImmediate(layout.gameObject); }
	}

	[TestCase(TextAnchor.UpperLeft)]
	[TestCase(TextAnchor.MiddleCenter)]
	[TestCase(TextAnchor.LowerRight)]
	public void FittingRealignsChildrenWithoutRepeatingLayoutNotifications(TextAnchor alignment) {
		var layout = CreateLayout("Layout", out var child);
		try {
			SetField(layout, "childAlignment", alignment);
			SetField(layout, "padding", new RectOffset(10, 20, 30, 40));
			var fitter = AddFitter(layout);
			SetField(fitter, "m_preserveChildPositions", false);
			// テスト設定後の状態で通知を購読する。
			Invoke(fitter, "OnEnable");
			var observer = new LayoutObserver();
			using var subscription = layout.CompleteLayoutAsObservable.Subscribe(observer);

			layout.AlignWithCollectionNonAnimate();

			var rect = (RectTransform)layout.transform;
			Assert.That(rect.rect.width, Is.EqualTo(130f).Within(0.001f));
			Assert.That(rect.rect.height, Is.EqualTo(150f).Within(0.001f));
			Assert.That(child.anchoredPosition.x - child.rect.width * child.pivot.x,
				Is.EqualTo(10f).Within(0.001f), "Left padding must survive fitting.");
			Assert.That(-child.anchoredPosition.y - child.rect.height * (1f - child.pivot.y),
				Is.EqualTo(30f).Within(0.001f), "Top padding must survive fitting.");
			Assert.That(observer.Count, Is.EqualTo(1), "Fitting must not recursively send completion notifications.");

			var fittedPosition = rect.anchoredPosition;
			fitter.ApplyFitting();
			Assert.That(rect.anchoredPosition, Is.EqualTo(fittedPosition), "Repeated fitting must not move the parent.");
		} finally {
			Object.DestroyImmediate(layout.gameObject);
		}
	}

	[Test]
	public void FittingPreservesExplicitChildCollection() {
		var layout = CreateLayout("Layout", out var included);
		try {
			var excluded = CreateChild(layout.transform, "Uncollected", new Vector2(500f, 500f));
			excluded.anchoredPosition = new Vector2(700f, 800f);
			var excludedPosition = excluded.position;
			layout.ClearRectChildren();
			layout.AddRectChild(included);
			var fitter = AddFitter(layout);
			Invoke(fitter, "OnEnable");

			layout.Align();

			Assert.That(((RectTransform)layout.transform).rect.size, Is.EqualTo(new Vector2(100f, 80f)));
			Assert.That(Vector3.Distance(excluded.position, excludedPosition), Is.LessThan(0.001f));
		} finally {
			Object.DestroyImmediate(layout.gameObject);
		}
	}

	[Test]
	public void EnablingWaitsForInitialLayoutAndResynchronizesAfterDisabledUpdates() {
		var layout = CreateLayout("Layout", out var child);
		try {
			var fitter = AddFitter(layout);
			fitter.enabled = false;
			fitter.enabled = true;
			var rect = (RectTransform)layout.transform;
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(300f, 300f)),
				"An uninitialized layout must not shrink the parent to zero.");

			layout.AlignWithCollectionNonAnimate();
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(100f, 80f)));
			fitter.enabled = false;
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 160f);
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 120f);
			layout.AlignWithCollectionNonAnimate();
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(100f, 80f)));

			fitter.enabled = true;
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(160f, 120f)),
				"Re-enabling must synchronize without another layout notification.");
		} finally {
			Object.DestroyImmediate(layout.gameObject);
		}
	}

	[UnityTest]
	[UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
	public IEnumerator InspectorTargetChangeRebindsOnlyWhileEnabled() {
		var original = CreateLayout("Original", out var originalChild);
		var replacement = CreateLayout("Replacement", out var replacementChild);
		try {
			var fitter = AddFitter(original);
			original.AlignWithCollectionNonAnimate();
			replacementChild.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 160f);
			replacementChild.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 120f);
			replacement.AlignWithCollectionNonAnimate();
			var rect = (RectTransform)original.transform;

			SetField(fitter, "m_layoutGroup", replacement);
			Invoke(fitter, "OnValidate");
			for(var frame = 0; frame < 10 && rect.rect.width != 160f; frame++) yield return null;
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(160f, 120f)));

			originalChild.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 240f);
			originalChild.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 200f);
			original.AlignWithCollectionNonAnimate();
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(160f, 120f)), "Old target must be unsubscribed.");
			replacementChild.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 180f);
			replacementChild.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 140f);
			replacement.AlignWithCollectionNonAnimate();
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(180f, 140f)));

			// 検証コールバック待ちの間に無効化しても、購読を復活させない。
			SetField(fitter, "m_layoutGroup", original);
			Invoke(fitter, "OnValidate");
			fitter.enabled = false;
			yield return null;
			yield return null;
			original.AlignWithCollectionNonAnimate();
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(180f, 140f)));
			fitter.enabled = true;
			Assert.That(rect.rect.size, Is.EqualTo(new Vector2(240f, 200f)));
		} finally {
			Object.DestroyImmediate(original.gameObject);
			Object.DestroyImmediate(replacement.gameObject);
		}
	}

	[Test]
	public void PreservingChildrenIgnoresAnchorsAndRepeatedFittingIsStable() {
		var layout = CreateLayout("Preserve", out var child);
		try {
			var rect = (RectTransform)layout.transform;
			var fitter = AddFitter(layout);
			child.anchorMin = Vector2.zero;
			child.anchorMax = Vector2.one;
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80f);
			child.anchoredPosition = new Vector2(40f, -30f);
			var ignored = CreateChild(rect, "Inactive", new Vector2(70f, 50f));
			ignored.anchorMin = ignored.anchorMax = Vector2.one;
			ignored.gameObject.SetActive(false);
			var before = child.position;
			var ignoredBefore = ignored.position;
			var size = child.rect.size;

			Assert.That(fitter.PreserveChildPositions, Is.True, "Position preservation is explicitly enabled for this test.");
			fitter.ApplyFitting();
			Assert.That(rect.rect.size, Is.EqualTo(size));
			Assert.That(Vector3.Distance(child.position, before), Is.LessThan(0.001f));
			Assert.That(Vector3.Distance(ignored.position, ignoredBefore), Is.LessThan(0.001f));
			Assert.That(child.rect.size, Is.EqualTo(size));
			Assert.That(child.anchorMin, Is.EqualTo(Vector2.zero));
			Assert.That(child.anchorMax, Is.EqualTo(Vector2.one));

			var fittedPosition = rect.position;
			for(var i = 0; i < 4; i++) fitter.ApplyFitting();
			Assert.That(rect.rect.size, Is.EqualTo(size));
			Assert.That(Vector3.Distance(rect.position, fittedPosition), Is.LessThan(0.001f));
			Assert.That(Vector3.Distance(child.position, before), Is.LessThan(0.001f));
		} finally {
			Object.DestroyImmediate(layout.gameObject);
		}
	}

	[TestCase(true, 2f)]
	[TestCase(true, -2f)]
	[TestCase(false, 2f)]
	public void ScaleOptionControlsReferencePointCompensation(bool considerScale, float scaleX) {
		var layout = CreateLayout("Scale", out var child);
		try {
			var rect = (RectTransform)layout.transform;
			rect.localScale = new Vector3(scaleX, 3f, 1f);
			rect.localRotation = Quaternion.Euler(0f, 0f, 30f);
			var fitter = AddFitter(layout);
			var scaleField = typeof(aContentSizeFitter).GetField("m_considerScale", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(scaleField.GetValue(fitter), Is.EqualTo(true), "Scale consideration must default to ON.");
			SetField(fitter, "m_considerScale", considerScale);
			var before = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax));
			var childBefore = child.position;
			fitter.ApplyFitting();
			var after = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax));
			if(considerScale) Assert.That(Vector3.Distance(before, after), Is.LessThan(0.001f));
			else Assert.That(Vector3.Distance(before, after), Is.GreaterThan(1f));
			Assert.That(Vector3.Distance(childBefore, child.position), Is.LessThan(0.001f));
		} finally {
			Object.DestroyImmediate(layout.gameObject);
		}
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void CollectionOptionControlsManualFitting(bool collectChildren, bool preservePositions) {
		var layout = CreateLayout("Collection", out var included);
		try {
			var uncollected = CreateChild(layout.transform, "Uncollected", new Vector2(500f, 500f));
			uncollected.anchoredPosition = new Vector2(700f, 800f);
			var fitter = AddFitter(layout);
			Assert.That(fitter.CollectChildrenEveryTime, Is.False, "Collection must default to OFF.");
			SetField(fitter, "m_collectChildrenEveryTime", collectChildren);
			SetField(fitter, "m_preserveChildPositions", preservePositions);
			layout.AlignNonAnimate(false);
			fitter.ApplyFitting();
			var children = (System.Collections.IList)typeof(aLayoutGroupBase)
				.GetField("rectChildren", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layout);
			Assert.That(children.Count, Is.EqualTo(collectChildren ? 2 : 1));
			if(!collectChildren) {
				Assert.That(children[0], Is.SameAs(included));
				Assert.That(((RectTransform)layout.transform).rect.size, Is.EqualTo(new Vector2(100f, 80f)));
			} else {
				Assert.That(((RectTransform)layout.transform).rect.width, Is.GreaterThanOrEqualTo(500f));
			}
			// 次回のフィットでも設定が尊重される。
			CreateChild(layout.transform, "Added later", new Vector2(60f, 40f));
			fitter.ApplyFitting();
			Assert.That(children.Count, Is.EqualTo(collectChildren ? 3 : 1));
		} finally { Object.DestroyImmediate(layout.gameObject); }
	}

	[TestCase(90f, 1f, 1f)]
	[TestCase(35f, 2f, 3f)]
	[TestCase(35f, -2f, -3f)]
	public void FittingMeasuresRotatedCornersWithAbsoluteScale(float angle, float scaleX, float scaleY) {
		var layout = CreateLayout("Rotation", out var child);
		try {
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
			child.pivot = new Vector2(0.2f, 0.8f);
			child.localRotation = Quaternion.Euler(0, 0, angle);
			child.localScale = new Vector3(scaleX, scaleY, 1);
			SetField(layout, "padding", new RectOffset(7, 13, 17, 23));
			// 正のスケールの実際の四隅を独立した期待値として取得する。
			child.localScale = new Vector3(Mathf.Abs(scaleX), Mathf.Abs(scaleY), 1);
			var corners = new Vector3[4];
			child.GetWorldCorners(corners);
			var min = corners[0]; var max = corners[0];
			foreach(var corner in corners) { min = Vector3.Min(min, corner); max = Vector3.Max(max, corner); }
			child.localScale = new Vector3(scaleX, scaleY, 1);
			var expected = (Vector2)(max - min) + new Vector2(20, 40);
			var fitter = AddFitter(layout);
			fitter.ApplyFitting();
			Assert.That(Vector2.Distance(((RectTransform)layout.transform).rect.size, expected), Is.LessThan(0.001f));
			// 通知に使う配置先の範囲計算にも同じ回転を反映する。
			layout.AlignNonAnimate(false);
			Assert.That(Vector2.Distance(layout.CalculateContentRect().size, expected), Is.LessThan(0.001f));
			Assert.That(child.localScale, Is.EqualTo(new Vector3(scaleX, scaleY, 1)));
		} finally { Object.DestroyImmediate(layout.gameObject); }
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ParentFittingPreservesAnimationPathAndCompletion(bool circularMove) {
		var roots = new GameObject[2];
		var children = new RectTransform[2];
		var tweens = new DG.Tweening.Tween[2];
		try {
			for(var i = 0; i < 2; i++) {
				roots[i] = new GameObject("Animated fitting", typeof(RectTransform));
				((RectTransform)roots[i].transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
				((RectTransform)roots[i].transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
				var layout = roots[i].AddComponent<aLayoutGroupCircular>();
				SetField(layout, "useAnimation", true);
				SetField(layout, "useCircularMove", circularMove);
				SetField(layout, "setNavigation", false);
				var child = children[i] = CreateChild(roots[i].transform, "Child", new Vector2(100, 100));
				child.anchorMin = child.anchorMax = new Vector2(0, 1);
				child.anchoredPosition = new Vector2(150 + Mathf.Sqrt(5000), -150 + Mathf.Sqrt(5000));
				var start = child.position;
				layout.AlignWithCollection();
				var dictionary = (System.Collections.IDictionary)typeof(aLayoutGroupBase)
					.GetField("m_positionTweens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layout);
				tweens[i] = (DG.Tweening.Tween)dictionary[child];
				DG.Tweening.TweenExtensions.Goto(tweens[i], 0, false);
				Assert.That(Vector3.Distance(start, child.position), Is.LessThan(0.001f), "Animation must start at the current position.");
				DG.Tweening.TweenExtensions.Goto(tweens[i], layout.AnimationDuration * 0.3f, false);
				if(i == 1) {
					var before = child.position;
					var fitter = AddFitter(layout);
					SetField(fitter, "m_pivotType", aContentSizeFitter.PivotType.MiddleCenter);
					fitter.ApplyFitting();
					Assert.That(Vector3.Distance(before, child.position), Is.LessThan(0.001f));
				}
			}
			foreach(var progress in new[] { 0.3f, 0.6f, 1f }) {
				for(var i = 0; i < 2; i++) DG.Tweening.TweenExtensions.Goto(tweens[i], 0.25f * progress, false);
				Assert.That(Vector3.Distance(children[0].position, children[1].position), Is.LessThan(0.001f),
					"World path and completion must survive fitting at progress " + progress);
			}
		} finally {
			foreach(var root in roots) if(root != null) Object.DestroyImmediate(root);
		}
	}

	[Test]
	public void CircularTargetReuseDoesNotChangeExcludedChildTween() {
		var root = new GameObject("Independent circular paths", typeof(RectTransform));
		try {
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
			var layout = root.AddComponent<aLayoutGroupCircular>();
			SetField(layout, "useAnimation", true);
			SetField(layout, "setNavigation", false);
			var child = CreateChild(root.transform, "Excluded later", new Vector2(100, 100));
			child.anchorMin = child.anchorMax = new Vector2(0, 1);
			child.anchoredPosition = new Vector2(250, -150);
			layout.AlignWithCollection();
			var tweens = (System.Collections.IDictionary)typeof(aLayoutGroupBase)
				.GetField("m_positionTweens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layout);
			var tween = (DG.Tweening.Tween)tweens[child];
			DG.Tweening.TweenExtensions.Goto(tween, 0.1f, false);
			SetField(layout, "excludedChildren", new System.Collections.Generic.List<RectTransform> { child });
			CreateChild(root.transform, "New child", new Vector2(60, 60));
			layout.Radius = 40;
			layout.StartAngle = 90;
			layout.AlignWithCollection();
			DG.Tweening.TweenExtensions.Goto(tween, 0.25f, false);
			Assert.That(Vector2.Distance(child.anchoredPosition, new Vector2(150, -50)), Is.LessThan(0.001f),
				"The excluded child's existing Tween must retain its own center, radius and completion target.");
		} finally { Object.DestroyImmediate(root); }
	}

	private static aLayoutGroupVertical CreateLayout(string name, out RectTransform child) {
		var root = new GameObject(name, typeof(RectTransform));
		((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
		((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300f);
		var layout = root.AddComponent<aLayoutGroupVertical>();
		SetField(layout, "childForceExpandWidth", false);
		SetField(layout, "childForceExpandHeight", false);
		SetField(layout, "setNavigation", false);
		child = CreateChild(root.transform, "Child", new Vector2(100f, 80f));
		layout.AddRectChild(child);
		return layout;
	}

	private static RectTransform CreateChild(Transform parent, string name, Vector2 size) {
		var child = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
		child.SetParent(parent, false);
		child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
		child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
		return child;
	}

	private static aContentSizeFitter AddFitter(aLayoutGroupBase layout) {
		var fitter = layout.gameObject.AddComponent<aContentSizeFitter>();
		Assert.That(fitter.PreserveChildPositions, Is.False, "Position preservation must default to OFF.");
		// 位置維持を検証する既存ケースは明示的にONにする。
		SetField(fitter, "m_preserveChildPositions", true);
		SetField(fitter, "m_fitWidth", true);
		SetField(fitter, "m_fitHeight", true);
		return fitter;
	}

	private static void SetField(object target, string name, object value) {
		for(var type = target.GetType(); type != null; type = type.BaseType) {
			var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
			if(field == null) continue;
			field.SetValue(target, value);
			return;
		}
		Assert.Fail($"Field '{name}' was not found.");
	}

	private static void Invoke(aContentSizeFitter fitter, string name) {
		var method = typeof(aContentSizeFitter).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		method.Invoke(fitter, null);
	}

	private class LayoutObserver : IObserver<Rect> {
		public int Count { get; private set; }
		public void OnNext(Rect value) => Count++;
		public void OnError(Exception error) => Assert.Fail(error.ToString());
		public void OnCompleted() { }
	}
}
