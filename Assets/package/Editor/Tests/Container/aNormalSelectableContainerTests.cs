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
	public class aNormalSelectableContainerTests {
		#region Test Helper Classes
		/// <summary> テスト用に aSelectableContainer の内部メソッドを公開する継承クラス </summary>
		private class TestSelectableContainer : aNormalSelectableContainerBase<Selectable> {
			public void TestObserveSelectables() => SetEvents();

			/// <summary> リフレクションを使用してアニメーションを設定する </summary>
			public void SetAnimations(IUiAnimation[] show, IUiAnimation[] hide) {
				var type = typeof(aContainerBase);
				type.GetField("m_showAnimations", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, show);
				type.GetField("m_hideAnimations", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, hide);
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
			m_container.IsVisible = false;
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
			m_container.Initialize(); // 明示的に初期化
			Assert.IsFalse(m_container.IsVisible);
			Assert.IsFalse(m_testObject.activeSelf);
		}

		[Test]
		public void Show_ActivatesGameObjectAndSetsInteractable() {
			m_container.Initialize(); // 明示的に初期化
			m_container.Show();
			Assert.IsTrue(m_testObject.activeSelf);
			Assert.IsTrue(m_container.Interactable);
		}
		#endregion

		#region Selection Tests
		[UnityTest]
		public IEnumerator InitialSelectable_IsSelectedOnShow() {
			m_container.Initialize(); // 明示的に初期化

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
			m_container.Initialize();
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
			m_container.Initialize();
			m_container.SetChildSelectableList(new List<Selectable>());
			m_container.DisallowNullSelection = true;

			// 空のリストで範囲外のインデックスをセットしてもクラッシュせず、無視されること
			Assert.DoesNotThrow(() => {
				m_container.CurrentSelectableIndex = 0;
			});
			yield return null;
		}

		[UnityTest]
		public IEnumerator CurrentSelectableIndex_ClampMode_ClampsOutOfRange() {
			m_container.Initialize();

			var btnGo0 = new GameObject("Button0", typeof(RectTransform), typeof(Button));
			btnGo0.transform.SetParent(m_testObject.transform);
			var btn0 = btnGo0.GetComponent<Button>();
			var btnGo1 = new GameObject("Button1", typeof(RectTransform), typeof(Button));
			btnGo1.transform.SetParent(m_testObject.transform);
			var btn1 = btnGo1.GetComponent<Button>();

			m_container.SetChildSelectableList(new List<Selectable> {
				btn0,
				btn1
			});
			m_container.IndexMode = aSelectableContainerBase<Selectable>.SelectableIndexMode.Clamp;

			m_container.CurrentSelectableIndex = -1;
			yield return null;
			Assert.AreEqual(0, m_container.CurrentSelectableIndex);
			Assert.AreEqual(btn0, m_container.CurrentSelectable);

			m_container.CurrentSelectableIndex = 5;
			yield return null;
			Assert.AreEqual(1, m_container.CurrentSelectableIndex);
			Assert.AreEqual(btn1, m_container.CurrentSelectable);
		}

		[UnityTest]
		public IEnumerator CurrentSelectableIndex_LoopMode_LoopsOutOfRange() {
			m_container.Initialize();

			var btnGo0 = new GameObject("Button0", typeof(RectTransform), typeof(Button));
			btnGo0.transform.SetParent(m_testObject.transform);
			var btn0 = btnGo0.GetComponent<Button>();
			var btnGo1 = new GameObject("Button1", typeof(RectTransform), typeof(Button));
			btnGo1.transform.SetParent(m_testObject.transform);
			var btn1 = btnGo1.GetComponent<Button>();
			var btnGo2 = new GameObject("Button2", typeof(RectTransform), typeof(Button));
			btnGo2.transform.SetParent(m_testObject.transform);
			var btn2 = btnGo2.GetComponent<Button>();

			m_container.SetChildSelectableList(new List<Selectable> {
				btn0,
				btn1,
				btn2
			});
			m_container.IndexMode = aSelectableContainerBase<Selectable>.SelectableIndexMode.Loop;

			m_container.CurrentSelectableIndex = -1;
			yield return null;
			Assert.AreEqual(2, m_container.CurrentSelectableIndex);
			Assert.AreEqual(btn2, m_container.CurrentSelectable);

			m_container.CurrentSelectableIndex = 3;
			yield return null;
			Assert.AreEqual(0, m_container.CurrentSelectableIndex);
			Assert.AreEqual(btn0, m_container.CurrentSelectable);

			m_container.CurrentSelectableIndex = 4;
			yield return null;
			Assert.AreEqual(1, m_container.CurrentSelectableIndex);
			Assert.AreEqual(btn1, m_container.CurrentSelectable);
		}

		[UnityTest]
		public IEnumerator CurrentSelectableIndex_NullableMode_SetsNullOnOutOfRange() {
			m_container.Initialize();

			var btnGo = new GameObject("Button", typeof(RectTransform), typeof(Button));
			btnGo.transform.SetParent(m_testObject.transform);
			var btn = btnGo.GetComponent<Button>();

			m_container.SetChildSelectableList(new List<Selectable> {
				btn
			});
			m_container.DisallowNullSelection = false;
			m_container.IndexMode = aSelectableContainerBase<Selectable>.SelectableIndexMode.Nullable;

			m_container.CurrentSelectableIndex = 3;
			yield return null;

			Assert.AreEqual(-1, m_container.CurrentSelectableIndex);
			Assert.IsNull(m_container.CurrentSelectable);
		}

		[UnityTest]
		public IEnumerator CurrentSelectable_DisallowNull_DoesNotClearSelection() {
			m_container.Initialize();

			var btnGo = new GameObject("Button", typeof(RectTransform), typeof(Button));
			btnGo.transform.SetParent(m_testObject.transform);
			var btn = btnGo.GetComponent<Button>();

			m_container.SetChildSelectableList(new List<Selectable> {
				btn
			});
			m_container.DisallowNullSelection = true;
			m_container.InitialSelectable = btn;

			m_container.Show();
			yield return null;

			Assert.AreEqual(btnGo, EventSystem.current.currentSelectedGameObject);

			m_container.CurrentSelectable = null;
			yield return null;

			Assert.AreEqual(btn, m_container.CurrentSelectable, "DisallowNullSelectionが有効ならCurrentSelectableは保持されるべき");
			Assert.AreEqual(btnGo, EventSystem.current.currentSelectedGameObject, "DisallowNullSelectionが有効なら選択状態は維持されるべき");
		}

		[UnityTest]
		public IEnumerator DisallowNullSelection_PreventsDeselection() {
			m_container.Initialize(); // 明示的に初期化

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

		#endregion

		#region Guard Tests
		/// <summary> InitialGuard が有効な際、指定時間だけ blocksRaycasts が false になるか </summary>
		[UnityTest]
		public IEnumerator InitialGuard_BlocksRaycastsTemporarily() {
			m_container.Initialize();
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
			m_container.Initialize();
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
