using System.Collections;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary> aLayoutGroup 系の基本動作を確認するためのテストクラス </summary>
public class aLayoutGroupTests {
	#region Methods
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
			rootRect.sizeDelta = new Vector2(200f, 200f);

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
