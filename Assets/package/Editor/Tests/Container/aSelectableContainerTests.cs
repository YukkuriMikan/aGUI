using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Reflection;

namespace ANest.UI.Tests {
	/// <summary> aSelectableContainer の機能を検証するテストクラス </summary>
	public class aSelectableContainerTests {
		#region Test Helper Classes
		/// <summary> テスト用に aSelectableContainer の内部メソッドを公開する継承クラス </summary>
		private class TestSelectableContainer : aSelectableContainer {
			public void TestInitialize() => Initialize();
			public void TestObserveSelectables() => ObserveSelectables();

			/// <summary> リフレクションを使用してアニメーションを設定する </summary>
			public void SetAnimations(IUiAnimation[] show, IUiAnimation[] hide) {
				var type = typeof(aContainerBase);
				type.GetField("m_showAnimations", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, show);
				type.GetField("m_hideAnimations", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, hide);
			}

			public void SetChildSelectableList(List<Selectable> selectables) {
				m_childSelectableList = selectables;
			}

			public void SetInitialGuard(bool enable, float duration) {
				var type = typeof(aSelectableContainerBase<Selectable>);
				type.GetField("m_initialGuard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, enable);
				type.GetField("m_initialGuardDuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, duration);
			}
		}

		private class MockUiAnimation : IUiAnimation {
			public float Delay { get; set; } = 0f;
			public float Duration { get; set; } = 1f;
			public bool IsYoYo => false;
			public AnimationCurve Curve => null;
			public Ease Ease => Ease.Linear;
			public bool UseCurve => false;

			public Tween DoAnimate(Graphic graphic, RectTransform callerRect, RectTransformValues original) {
				return DOTween.To(() => 0f, x => { }, 1f, Duration).SetDelay(Delay).SetTarget(callerRect);
			}
		}
		#endregion

		#region Fields
		private GameObject m_testObject;
		private TestSelectableContainer m_container;
		private GameObject m_eventSystemObject;
		#endregion

		#region Setup / Teardown
		[SetUp]
		public void SetUp() {
			m_eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

			m_testObject = new GameObject("aSelectableContainer", typeof(RectTransform));
			m_testObject.AddComponent<CanvasGroup>();
			var guiInfo = m_testObject.AddComponent<aGuiInfo>();

			var guiInfoType = typeof(aGuiInfo);
			var rectTransform = m_testObject.GetComponent<RectTransform>();
			guiInfoType.GetField("m_rectTransform", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(guiInfo, rectTransform);
			guiInfoType.GetField("m_originalRectTransformValues", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(guiInfo, RectTransformValues.CreateValues(rectTransform));

			m_container = m_testObject.AddComponent<TestSelectableContainer>();
			// Initialize は呼ばない。Show() の中で base.Initialize() が走るか、
			// あるいはテストで明示的に呼ぶ必要がある場合は各テストで行う。
			// aContainerBase.cs の Show() -> ShowInternal() -> UpdateStateForShow() -> base.UpdateStateForShow() -> m_isVisible = true
		}

		[TearDown]
		public void TearDown() {
			aContainerManager.Clear();
			Object.DestroyImmediate(m_testObject);
			Object.DestroyImmediate(m_eventSystemObject);
		}
		#endregion

		#region Basic Tests
		[Test]
		public void InitialState_IsCorrect() {
			m_container.TestInitialize(); // 明示的に初期化
			Assert.IsFalse(m_container.IsVisible);
			Assert.IsFalse(m_testObject.activeSelf);
		}

		[Test]
		public void Show_ActivatesGameObjectAndSetsInteractable() {
			m_container.TestInitialize(); // 明示的に初期化
			m_container.Show();
			Assert.IsTrue(m_testObject.activeSelf);
			Assert.IsTrue(m_container.Interactable);
		}
		#endregion

		#region Selection Tests
		[UnityTest]
		public IEnumerator InitialSelectable_IsSelectedOnShow() {
			m_container.TestInitialize(); // 明示的に初期化

			var btnGo = new GameObject("Button", typeof(RectTransform), typeof(Button));
			btnGo.transform.SetParent(m_testObject.transform);
			var btn = btnGo.GetComponent<Button>();

			m_container.SetChildSelectableList(new List<Selectable> {
				btn
			});
			m_container.InitialSelectable = btn;

			m_container.Show();
			yield return null;

			Assert.AreEqual(btnGo, EventSystem.current.currentSelectedGameObject);
		}

		[UnityTest]
		public IEnumerator CurrentSelectable_DisallowNull_WithEmptyList_DoesNotCrash() {
			m_container.TestInitialize();
			m_container.SetChildSelectableList(new List<Selectable>());
			m_container.DisallowNullSelection = true;

			// 空のリストで null をセットしてもクラッシュしないこと
			Assert.DoesNotThrow(() => {
				m_container.CurrentSelectable = null;
			});
			yield return null;
		}

		[UnityTest]
		public IEnumerator CurrentSelectableIndex_DisallowNull_WithEmptyList_DoesNotCrash() {
			m_container.TestInitialize();
			m_container.SetChildSelectableList(new List<Selectable>());
			m_container.DisallowNullSelection = true;

			// 空のリストで範囲外のインデックスをセットしてもクラッシュせず、無視されること
			Assert.DoesNotThrow(() => {
				m_container.CurrentSelectableIndex = 0;
			});
			yield return null;
		}

		[UnityTest]
		public IEnumerator DisallowNullSelection_PreventsDeselection() {
			m_container.TestInitialize(); // 明示的に初期化

			var btnGo = new GameObject("Button", typeof(RectTransform), typeof(Button));
			btnGo.transform.SetParent(m_testObject.transform);
			var btn = btnGo.GetComponent<Button>();

			m_container.SetChildSelectableList(new List<Selectable> {
				btn
			});
			m_container.DisallowNullSelection = true;

			m_container.Show();
			m_container.TestObserveSelectables();

			btn.Select();
			yield return null;
			Assert.AreEqual(btnGo, EventSystem.current.currentSelectedGameObject);

			// null を選択しようとする
			EventSystem.current.SetSelectedGameObject(null);
			yield return null; // NextFrame 待ち
			yield return null;

			Assert.AreEqual(btnGo, EventSystem.current.currentSelectedGameObject, "DisallowNullSelectionが有効なら再選択されるべき");
		}

		[UnityTest]
		public IEnumerator DisallowNullSelection_LatestContainerTakesPrecedence() {
			// コンテナ1
			var obj1 = new GameObject("Container1", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var c1 = obj1.AddComponent<TestSelectableContainer>();
			var b1 = new GameObject("Button1", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
			b1.transform.SetParent(obj1.transform);
			c1.SetChildSelectableList(new List<Selectable> {
				b1
			});
			c1.DisallowNullSelection = true;
			c1.IsVisible = true;
			c1.TestInitialize();

			// コンテナ2 (後から登録される)
			var obj2 = new GameObject("Container2", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var c2 = obj2.AddComponent<TestSelectableContainer>();
			var b2 = new GameObject("Button2", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
			b2.transform.SetParent(obj2.transform);
			c2.SetChildSelectableList(new List<Selectable> {
				b2
			});
			c2.DisallowNullSelection = true;
			c2.IsVisible = true;
			c2.TestInitialize();

			Assert.IsFalse(aContainerManager.IsLatestSelectableContainer(c1), "C1は最新ではない");
			Assert.IsTrue(aContainerManager.IsLatestSelectableContainer(c2), "C2は最新である");

			// c1 を再度登録（最新にする）
			aContainerManager.Add(c1);
			Assert.IsTrue(aContainerManager.IsLatestSelectableContainer(c1), "C1が最新になった");

			Object.DestroyImmediate(obj1);
			Object.DestroyImmediate(obj2);
			yield return null;
		}
		#endregion

		#region Guard Tests
		/// <summary> InitialGuard が有効な際、指定時間だけ blocksRaycasts が false になるか </summary>
		[UnityTest]
		public IEnumerator InitialGuard_BlocksRaycastsTemporarily() {
			m_container.TestInitialize();
			m_container.SetInitialGuard(true, 0.2f);

			m_container.Show();
			var canvasGroup = m_testObject.GetComponent<CanvasGroup>();

			// ShowInternal 内で base.ShowInternal (blocksRaycasts = true) の後に false に設定される
			Assert.IsFalse(canvasGroup.blocksRaycasts, "InitialGuard中はblocksRaycastsがfalseであるべき");

			yield return new WaitForSeconds(0.3f);

			Assert.IsTrue(canvasGroup.blocksRaycasts, "InitialGuard終了後はblocksRaycastsがtrueに戻るべき");
		}
		#endregion

		#region Animation Tests
		/// <summary> Show アニメーション中に Hide を実行した際、正常に中断して非表示に遷移するか </summary>
		[UnityTest]
		public IEnumerator ShowAnimation_InterruptedByHide() {
			m_container.TestInitialize();
			var showAnim = new MockUiAnimation {
				Duration = 0.5f
			};
			m_container.SetAnimations(new IUiAnimation[] {
				showAnim
			}, null);

			m_container.Show();
			Assert.IsTrue(m_container.IsVisible);

			yield return new WaitForSeconds(0.1f);

			m_container.Hide();
			Assert.IsFalse(m_container.IsVisible);
			Assert.IsFalse(m_testObject.activeSelf, "中断後のHideによりGameObjectが非アクティブになるべき");
		}
		#endregion
	}
}
