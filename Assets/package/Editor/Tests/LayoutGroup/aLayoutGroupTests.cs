using System.Collections;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary> aLayoutGroup 系の基本動作を確認するためのテストクラス </summary>
public class aLayoutGroupTests {
	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void FixedGridFollowsStartAxisForEveryCorner(bool fixedRows, bool vertical) {
		var root = new GameObject("Grid axis comparison", typeof(RectTransform));
		try {
			var actualRoot = (RectTransform)new GameObject("aGUI", typeof(RectTransform)).transform;
			actualRoot.SetParent(root.transform, false);
			var expectedRoot = (RectTransform)new GameObject("Unity", typeof(RectTransform)).transform;
			expectedRoot.SetParent(root.transform, false);
			foreach(var rect in new[] { actualRoot, expectedRoot }) {
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500);
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);
				for(int i = 0; i < 6; i++) new GameObject(i.ToString(), typeof(RectTransform)).transform.SetParent(rect, false);
			}
			var actual = actualRoot.gameObject.AddComponent<aLayoutGroupGrid>();
			var expected = expectedRoot.gameObject.AddComponent<GridLayoutGroup>();
			SetField(actual, "constraint", fixedRows ? aLayoutGroupGrid.Constraint.FixedRowCount : aLayoutGroupGrid.Constraint.FixedColumnCount);
			SetField(actual, "constraintCount", 2);
			SetField(actual, "startAxis", vertical ? aLayoutGroupGrid.Axis.Vertical : aLayoutGroupGrid.Axis.Horizontal);
			SetField(actual, "childControlWidth", true);
			SetField(actual, "childControlHeight", true);
			SetField(actual, "childForceExpandWidth", false);
			SetField(actual, "childForceExpandHeight", false);
			SetField(actual, "childAlignment", TextAnchor.MiddleCenter);
			expected.constraint = fixedRows ? GridLayoutGroup.Constraint.FixedRowCount : GridLayoutGroup.Constraint.FixedColumnCount;
			expected.constraintCount = 2;
			expected.startAxis = vertical ? GridLayoutGroup.Axis.Vertical : GridLayoutGroup.Axis.Horizontal;
			expected.childAlignment = TextAnchor.MiddleCenter;
			for(int corner = 0; corner < 4; corner++) {
				SetField(actual, "startCorner", (aLayoutGroupGrid.Corner)corner);
				expected.startCorner = (GridLayoutGroup.Corner)corner;
				actual.AlignWithCollectionNonAnimate();
				LayoutRebuilder.ForceRebuildLayoutImmediate(expectedRoot);
				for(int i = 0; i < 6; i++) Assert.That(((RectTransform)actualRoot.GetChild(i)).anchoredPosition,
					Is.EqualTo(((RectTransform)expectedRoot.GetChild(i)).anchoredPosition), "Corner " + corner + ", child " + i);
			}
		} finally { Object.DestroyImmediate(root); }
	}
	#region Methods
	[TestCase(false)]
	[TestCase(true)]
	public void RepeatedCircularAnimationStaysOnCircumference(bool interruptFirstTween) {
		var root = new GameObject("Repeated circular animation", typeof(RectTransform));
		try {
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
			var layout = root.AddComponent<aLayoutGroupCircular>();
			SetField(layout, "useAnimation", true);
			SetField(layout, "useCircularMove", true);
			SetField(layout, "animationEase", DG.Tweening.Ease.Linear);
			SetField(layout, "setNavigation", false);
			var child = (RectTransform)new GameObject("Child", typeof(RectTransform)).transform;
			child.SetParent(root.transform, false);
			child.anchorMin = child.anchorMax = new Vector2(0, 1);
			var center = new Vector2(150, -150);
			child.anchoredPosition = center + new Vector2(100, 0);
			layout.AlignWithCollection();
			var tweens = (System.Collections.IDictionary)typeof(aLayoutGroupBase)
				.GetField("m_positionTweens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layout);
			var first = (DG.Tweening.Tween)tweens[child];
			if(interruptFirstTween) DG.Tweening.TweenExtensions.Goto(first, layout.AnimationDuration * 0.5f, false);
			else DG.Tweening.TweenExtensions.Complete(first);
			var before = child.anchoredPosition;
			layout.StartAngle = 90;
			layout.Align();
			var next = (DG.Tweening.Tween)tweens[child];
			DG.Tweening.TweenExtensions.Goto(next, 0, false);
			Assert.That(Vector2.Distance(child.anchoredPosition, before), Is.LessThan(0.001f));
			foreach(var progress in new[] { 0.25f, 0.5f, 0.75f }) {
				DG.Tweening.TweenExtensions.Goto(next, layout.AnimationDuration * progress, false);
				Assert.That(Vector2.Distance(child.anchoredPosition, center), Is.EqualTo(100f).Within(0.001f),
					"Subsequent animations must follow the circle, including retargeting during playback.");
			}
			DG.Tweening.TweenExtensions.Complete(next);
			Assert.That(Vector2.Distance(child.anchoredPosition, center + new Vector2(100, 0)), Is.LessThan(0.001f));
		} finally { Object.DestroyImmediate(root); }
	}

	[TestCase(typeof(aLayoutGroupHorizontal))]
	[TestCase(typeof(aLayoutGroupVertical))]
	[TestCase(typeof(aLayoutGroupGrid))]
	[TestCase(typeof(aLayoutGroupCircular))]
	public void ContentBoundsExpandTopUpwardAndBottomDownward(System.Type layoutType) {
		var root = new GameObject("Asymmetric padding bounds", typeof(RectTransform));
		try {
			var layout = (aLayoutGroupBase)root.AddComponent(layoutType);
			var child = (RectTransform)new GameObject("Child", typeof(RectTransform)).transform;
			child.SetParent(root.transform, false);
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
			child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
			child.anchoredPosition = new Vector2(40, -30);
			layout.AddRectChild(child);
			SetField(layout, "padding", new RectOffset(7, 13, 17, 23));
			var bounds = layout.CalculateContentRect();
			Assert.That(bounds.xMin, Is.EqualTo(-17f).Within(0.001f));
			Assert.That(bounds.xMax, Is.EqualTo(103f).Within(0.001f));
			Assert.That(bounds.yMin, Is.EqualTo(-93f).Within(0.001f));
			Assert.That(bounds.yMax, Is.EqualTo(27f).Within(0.001f));
			layout.AlignWithCollectionNonAnimate();
			SetField(layout, "padding", new RectOffset());
			var content = layout.CalculateContentRect();
			SetField(layout, "padding", new RectOffset(7, 13, 17, 23));
			bounds = layout.CalculateContentRect();
			Assert.That(bounds.yMin, Is.EqualTo(content.yMin - 23f).Within(0.001f));
			Assert.That(bounds.yMax, Is.EqualTo(content.yMax + 17f).Within(0.001f));
		} finally { Object.DestroyImmediate(root); }
	}

	[TestCase(typeof(aLayoutGroupHorizontal), false)]
	[TestCase(typeof(aLayoutGroupHorizontal), true)]
	[TestCase(typeof(aLayoutGroupVertical), false)]
	[TestCase(typeof(aLayoutGroupVertical), true)]
	public void DestroyedChildrenAreRemovedWithoutRecollecting(System.Type layoutType, bool reverse) {
		var root = new GameObject("Destroyed children", typeof(RectTransform));
		try {
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500);
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);
			var layout = (aLayoutGroupBase)root.AddComponent(layoutType);
			SetField(layout, "childForceExpandWidth", false);
			SetField(layout, "childForceExpandHeight", false);
			SetField(layout, "spacing", 10f);
			SetField(layout, "reverseArrangement", reverse);
			var children = new RectTransform[4];
			for(var i = 0; i < children.Length; i++) {
				children[i] = (RectTransform)new GameObject("Child", typeof(RectTransform), typeof(Button)).transform;
				children[i].SetParent(root.transform, false);
				children[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
				children[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
				layout.AddRectChild(children[i]);
			}
			layout.Align();
			Object.DestroyImmediate(children[1].gameObject);
			Object.DestroyImmediate(children[2].gameObject);
			var excluded = (RectTransform)new GameObject("Not collected", typeof(RectTransform)).transform;
			excluded.SetParent(root.transform, false);
			excluded.anchoredPosition = new Vector2(900, 900);
			var before = excluded.anchoredPosition;
			Assert.DoesNotThrow(() => layout.Align());
			var axis = layoutType == typeof(aLayoutGroupHorizontal) ? 0 : 1;
			var separation = children[3].anchoredPosition[axis] - children[0].anchoredPosition[axis];
			var expected = axis == 0 ? 110f : -90f;
			Assert.That(separation, Is.EqualTo(reverse ? -expected : expected).Within(0.001f));
			Assert.That(excluded.anchoredPosition, Is.EqualTo(before));
			var nav = children[0].GetComponent<Button>().navigation;
			var neighbor = axis == 0 ? (reverse ? nav.selectOnLeft : nav.selectOnRight) : (reverse ? nav.selectOnUp : nav.selectOnDown);
			Assert.That(neighbor, Is.SameAs(children[3].GetComponent<Button>()));
			Object.DestroyImmediate(children[0].gameObject);
			Object.DestroyImmediate(children[3].gameObject);
			Assert.DoesNotThrow(() => layout.Align());
			Assert.That(layout.CalculateContentRect(), Is.EqualTo(new Rect()));
		} finally { Object.DestroyImmediate(root); }
	}

	/// <summary> シンプルな同期待ちなしテストの雛形 </summary>
	[Test]
	public void aLayoutGroupTestsSimplePasses() {
		// 条件を追加して検証する際のテンプレート
	}

	/// <summary> コルーチンを用いた非同期テストの雛形 </summary>
	[UnityTest]
	public IEnumerator aLayoutGroupTestsWithEnumeratorPasses() {
		// フレームをまたぐ検証を行う際のテンプレート
		yield return null;
	}

	[Test]
	public void GridSetNavigation_AssignsExplicitNavigationToArrangedSelectables() {
		var root = new GameObject("Grid", typeof(RectTransform));
		try {
			var rootRect = root.GetComponent<RectTransform>();
			rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200f);
			rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 200f);

			var buttons = new Button[4];
			for (int i = 0; i < buttons.Length; i++) {
				var child = new GameObject($"Button {i}", typeof(RectTransform), typeof(Button));
				child.transform.SetParent(root.transform, false);
				buttons[i] = child.GetComponent<Button>();
			}

			var grid = root.AddComponent<aLayoutGroupGrid>();
			SetField(grid, "constraint", aLayoutGroupGrid.Constraint.FixedColumnCount);
			SetField(grid, "constraintCount", 2);
			SetField(grid, "setNavigation", true);

			grid.AlignWithCollectionNonAnimate();

			Assert.That(buttons[0].navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
			Assert.That(buttons[0].navigation.selectOnRight, Is.SameAs(buttons[1]));
			Assert.That(buttons[0].navigation.selectOnDown, Is.SameAs(buttons[2]));
			Assert.That(buttons[1].navigation.selectOnLeft, Is.SameAs(buttons[0]));
			Assert.That(buttons[1].navigation.selectOnDown, Is.SameAs(buttons[3]));
			Assert.That(buttons[2].navigation.selectOnUp, Is.SameAs(buttons[0]));
			Assert.That(buttons[2].navigation.selectOnRight, Is.SameAs(buttons[3]));
			Assert.That(buttons[3].navigation.selectOnUp, Is.SameAs(buttons[1]));
			Assert.That(buttons[3].navigation.selectOnLeft, Is.SameAs(buttons[2]));
		} finally {
			Object.DestroyImmediate(root);
		}
	}

	[TestCase(typeof(aLayoutGroupHorizontal), false)]
	[TestCase(typeof(aLayoutGroupHorizontal), true)]
	[TestCase(typeof(aLayoutGroupVertical), false)]
	[TestCase(typeof(aLayoutGroupVertical), true)]
	[TestCase(typeof(aLayoutGroupGrid), false)]
	[TestCase(typeof(aLayoutGroupGrid), true)]
	[TestCase(typeof(aLayoutGroupCircular), false)]
	[TestCase(typeof(aLayoutGroupCircular), true)]
	public void NegativeChildScalesMatchAbsoluteScales(System.Type layoutType, bool forceExpand) {
		var roots = new GameObject[2];
		var children = new RectTransform[2, 3];
		var bounds = new Rect[2];
		try {
			for (int variant = 0; variant < roots.Length; variant++) {
				var root = roots[variant] = new GameObject("Scale comparison", typeof(RectTransform));
				((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 900f);
				((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 800f);
				for (int i = 0; i < 3; i++) {
					var child = new GameObject("Child", typeof(RectTransform)).GetComponent<RectTransform>();
					children[variant, i] = child;
					child.SetParent(root.transform, false);
					child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 40f + 10f * i);
					child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30f + 5f * i);
					child.pivot = new Vector2(0.2f * i, 1f - 0.3f * i);
					// Xのみ、Yのみ、両軸の反転を非中央ピボットで比較する。
					child.localScale = new Vector3(variant == 1 && i != 1 ? -2f : 2f,
						variant == 1 && i != 0 ? -3f : 3f, 1f);
				}

				var layout = (aLayoutGroupBase)root.AddComponent(layoutType);
				SetField(layout, "childScaleWidth", true);
				SetField(layout, "childScaleHeight", true);
				SetField(layout, "childForceExpandWidth", forceExpand);
				SetField(layout, "childForceExpandHeight", forceExpand);
				SetField(layout, "childAlignment", TextAnchor.LowerRight);
				SetField(layout, "padding", new RectOffset(7, 13, 17, 23));
				layout.AlignWithCollectionNonAnimate();
				bounds[variant] = layout.CalculateContentRect();

				var fitter = root.AddComponent<aContentSizeFitter>();
				SetField(fitter, "m_fitWidth", true);
				SetField(fitter, "m_fitHeight", true);
				fitter.ApplyFitting();
			}

			Assert.That(Vector2.Distance(bounds[0].min, bounds[1].min), Is.LessThan(0.001f));
			Assert.That(Vector2.Distance(bounds[0].max, bounds[1].max), Is.LessThan(0.001f));
			Assert.That(Vector2.Distance(((RectTransform)roots[0].transform).rect.size,
				((RectTransform)roots[1].transform).rect.size), Is.LessThan(0.001f), "Fitted sizes must match.");
			for (int i = 0; i < 3; i++) {
				Assert.That(Vector3.Distance(children[0, i].position, children[1, i].position),
					Is.LessThan(0.001f), "Placement must ignore scale signs.");
				Assert.That(Vector2.Distance(children[0, i].rect.size, children[1, i].rect.size), Is.LessThan(0.001f));
				Assert.That(children[1, i].localScale,
					Is.EqualTo(new Vector3(i != 1 ? -2f : 2f, i != 0 ? -3f : 3f, 1f)),
					"Fitting must preserve the actual reflection.");
			}
		} finally {
			foreach (var root in roots) {
				if(root != null) Object.DestroyImmediate(root);
			}
		}
	}

	[TestCase(typeof(aLayoutGroupHorizontal))]
	[TestCase(typeof(aLayoutGroupVertical))]
	[TestCase(typeof(aLayoutGroupGrid))]
	[TestCase(typeof(aLayoutGroupCircular))]
	public void ReusedBuffersMatchFreshLayoutAfterChildCountAndOrderChanges(System.Type layoutType) {
		GameObject CreateRoot() {
			var root = new GameObject("Buffer check", typeof(RectTransform));
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 900);
			((RectTransform)root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 800);
			root.AddComponent(layoutType);
			for(var i = 0; i < 17; i++) {
				var child = new GameObject("Child " + i, typeof(RectTransform));
				child.transform.SetParent(root.transform, false);
				((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 40 + i);
				((RectTransform)child.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30 + i);
				if(i % 3 != 0) child.AddComponent<UnityEngine.UI.Button>();
			}
			return root;
		}
		var reused = CreateRoot();
		try {
			foreach(var count in new[] { 1, 9, 3, 17, 0, 5 }) {
				var fresh = CreateRoot();
				try {
					foreach(var root in new[] { reused, fresh }) {
						var layout = root.GetComponent<aLayoutGroupBase>();
						SetField(layout, "reverseArrangement", count % 2 == 1);
						SetField(layout, "childAlignment", TextAnchor.LowerRight);
						layout.ClearRectChildren();
						for(var i = count - 1; i >= 0; i--) layout.AddRectChild((RectTransform)root.transform.GetChild(i));
						layout.AlignNonAnimate(false);
					}
					for(var i = 0; i < count; i++) {
						var actual = (RectTransform)reused.transform.GetChild(i);
						var expected = (RectTransform)fresh.transform.GetChild(i);
						Assert.That(Vector2.Distance(actual.anchoredPosition, expected.anchoredPosition), Is.LessThan(0.001f));
						Assert.That(Vector2.Distance(actual.rect.size, expected.rect.size), Is.LessThan(0.001f));
						if(actual.TryGetComponent<UnityEngine.UI.Button>(out var button)) {
							var a = button.navigation;
							var b = expected.GetComponent<UnityEngine.UI.Button>().navigation;
							Assert.That(a.selectOnLeft?.name, Is.EqualTo(b.selectOnLeft?.name));
							Assert.That(a.selectOnRight?.name, Is.EqualTo(b.selectOnRight?.name));
							Assert.That(a.selectOnUp?.name, Is.EqualTo(b.selectOnUp?.name));
							Assert.That(a.selectOnDown?.name, Is.EqualTo(b.selectOnDown?.name));
						}
					}
				} finally { Object.DestroyImmediate(fresh); }
			}
		} finally { Object.DestroyImmediate(reused); }
	}

	private static void SetField(object target, string fieldName, object value) {
		for (var type = target.GetType(); type != null; type = type.BaseType) {
			var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
			if(field == null) continue;
			field.SetValue(target, value);
			return;
		}

		Assert.Fail($"Field '{fieldName}' was not found.");
	}
	#endregion
}
