using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using System.Reflection;
using DG.Tweening;

namespace ANest.UI.Tests {
	/// <summary>
	/// aSelectableCursor の動作を検証するテストクラス
	/// </summary>
	public class aSelectableCursorTests {
		#region Test Helper Classes
		/// <summary>
		/// テスト用に aSelectableCursor の内部メンバを公開する継承クラス
		/// </summary>
		private class TestSelectableCursor : aSelectableCursor {
			public void SetContainer(aSelectableContainer container) {
				var field = typeof(aNormalCursorBase<aSelectableContainerBase<Selectable>, Selectable>)
					.GetField("m_container", BindingFlags.NonPublic | BindingFlags.Instance);
				field.SetValue(this, container);
			}

			public void SetUpdateMode(UpdateMode mode) {
				var field = typeof(aCursorBase).GetField("m_updateMode", BindingFlags.NonPublic | BindingFlags.Instance);
				field.SetValue(this, mode);
			}

			public void SetMoveMode(MoveMode mode) {
				var field = typeof(aCursorBase).GetField("m_moveMode", BindingFlags.NonPublic | BindingFlags.Instance);
				field.SetValue(this, mode);
			}

			public void SetSizeMode(SizeMode mode) {
				var field = typeof(aCursorBase).GetField("m_sizeMode", BindingFlags.NonPublic | BindingFlags.Instance);
				field.SetValue(this, mode);
			}

			public void SetPadding(Vector2 padding) {
				var field = typeof(aCursorBase).GetField("m_padding", BindingFlags.NonPublic | BindingFlags.Instance);
				field.SetValue(this, padding);
			}

			public void SetMoveDuration(float duration) {
				var field = typeof(aCursorBase).GetField("m_moveDuration", BindingFlags.NonPublic | BindingFlags.Instance);
				field.SetValue(this, duration);
			}

			public void InvokeOnTargetRectChanged(RectTransform target) {
				var method = typeof(aCursorBase).GetMethod("OnTargetRectChanged", BindingFlags.NonPublic | BindingFlags.Instance);
				method.Invoke(this, new object[] { target });
			}
		}

		private class TestSelectableContainer : aSelectableContainer {
			public void SetCurrentSelectable(Selectable selectable) {
				CurrentSelectable = selectable;
				OnSelectChanged.Invoke(selectable);
			}
		}
		#endregion

		#region Fields
		private GameObject m_rootObject;
		private GameObject m_containerObject;
		private TestSelectableContainer m_container;
		private GameObject m_cursorObject;
		private Image m_cursorImage;
		private TestSelectableCursor m_cursor;
		private GameObject m_selectableObject1;
		private RectTransform m_rect1;
		#endregion

		#region Setup / Teardown
		[SetUp]
		public void SetUp() {
			m_rootObject = new GameObject("TestRoot", typeof(Canvas));

			m_containerObject = new GameObject("Container", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			m_containerObject.transform.SetParent(m_rootObject.transform);
			m_container = m_containerObject.AddComponent<TestSelectableContainer>();

			m_selectableObject1 = new GameObject("Selectable1", typeof(RectTransform), typeof(Image), typeof(Button));
			m_selectableObject1.transform.SetParent(m_containerObject.transform);
			m_rect1 = m_selectableObject1.GetComponent<RectTransform>();
			m_rect1.sizeDelta = new Vector2(100, 50);
			m_rect1.anchoredPosition = new Vector2(0, 0);

			m_cursorObject = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
			m_cursorObject.transform.SetParent(m_rootObject.transform);
			m_cursorImage = m_cursorObject.GetComponent<Image>();
			m_cursor = m_cursorObject.AddComponent<TestSelectableCursor>();

			m_cursor.SetContainer(m_container);
			var cursorRectField = typeof(aCursorBase).GetField("m_cursorRect", BindingFlags.NonPublic | BindingFlags.Instance);
			cursorRectField.SetValue(m_cursor, m_cursorImage.rectTransform);
			var cursorImageField = typeof(aCursorBase).GetField("m_cursorImage", BindingFlags.NonPublic | BindingFlags.Instance);
			cursorImageField.SetValue(m_cursor, m_cursorImage);
		}

		[TearDown]
		public void TearDown() {
			Object.DestroyImmediate(m_rootObject);
		}
		#endregion

		#region Tests
		[UnityTest]
		public IEnumerator UpdateMode_EveryFrame_FollowsMovingTarget() {
			m_cursor.SetUpdateMode(aCursorBase.UpdateMode.EveryFrame);
			m_cursor.SetMoveMode(aCursorBase.MoveMode.Instant);
			var selectable1 = m_selectableObject1.GetComponent<Selectable>();

			yield return null;

			m_cursor.InvokeOnTargetRectChanged(m_rect1);
			yield return null;

			Assert.AreEqual(m_rect1.position, m_cursorImage.rectTransform.position, "最初は一致すべき");

			m_rect1.position = new Vector3(500, 500, 0);
			yield return null;

			Assert.AreEqual(m_rect1.position, m_cursorImage.rectTransform.position, "移動後も追従すべき");
		}

		[UnityTest]
		public IEnumerator UpdateMode_OnSelectChanged_DoesNotFollowMovingTarget() {
			m_cursor.SetUpdateMode(aCursorBase.UpdateMode.OnSelectChanged);
			m_cursor.SetMoveMode(aCursorBase.MoveMode.Instant);
			var selectable1 = m_selectableObject1.GetComponent<Selectable>();

			yield return null;

			m_cursor.InvokeOnTargetRectChanged(m_rect1);
			yield return null;

			Assert.AreEqual(m_rect1.position, m_cursorImage.rectTransform.position, "最初は一致すべき");

			Vector3 oldPos = m_rect1.position;
			m_rect1.position = new Vector3(500, 500, 0);
			yield return null;

			Assert.AreEqual(oldPos, m_cursorImage.rectTransform.position, "OnSelectChanged モードでは移動に追従しないはず");
		}

		[UnityTest]
		public IEnumerator Cursor_MatchesSize_Instant() {
			m_cursor.SetMoveMode(aCursorBase.MoveMode.Instant);
			m_cursor.SetSizeMode(aCursorBase.SizeMode.MatchSelectable);
			Vector2 padding = new Vector2(10, 10);
			m_cursor.SetPadding(padding);

			yield return null;

			m_cursor.InvokeOnTargetRectChanged(m_rect1);
			yield return null;

			Vector2 expectedSize = m_rect1.rect.size + padding;
			Assert.That(m_cursorImage.rectTransform.sizeDelta.x, Is.EqualTo(expectedSize.x).Within(0.01f));
			Assert.That(m_cursorImage.rectTransform.sizeDelta.y, Is.EqualTo(expectedSize.y).Within(0.01f));
		}

		[UnityTest]
		public IEnumerator Cursor_MoveMode_Animation_TakesTime() {
			m_cursor.SetUpdateMode(aCursorBase.UpdateMode.EveryFrame);
			m_cursor.SetMoveMode(aCursorBase.MoveMode.Animation);
			float duration = 0.5f;
			m_cursor.SetMoveDuration(duration);

			DOTween.Init();

			// アニメーションの対象となる RectTransform の位置を初期化
			m_cursorImage.rectTransform.anchoredPosition = new Vector2(-1000, -1000);
			m_rect1.anchoredPosition = new Vector2(0, 0);

			yield return null;

			// 初回移動を開始させる
			m_cursor.InvokeOnTargetRectChanged(m_rect1);

			yield return null;
			
			// 初回移動は強制的に瞬間移動となるので、再度アニメーションの対象となる RectTransform の位置を初期化
			m_cursorImage.rectTransform.anchoredPosition = new Vector2(900, 900);
			m_rect1.anchoredPosition = new Vector2(0, 0);

			yield return null;
			
			// フレームを進める
			yield return null;
			yield return null;
			yield return null;
			
			// 距離を測定
			float currentDistance = Vector2.Distance(m_rect1.anchoredPosition, m_cursorImage.rectTransform.anchoredPosition);
			
			// アニメーション中であることを検証（即座に移動していないこと）
			Assert.Greater(currentDistance, 100f, "アニメーション中なので、まだ目的地から十分に離れているはず");

			// 完了まで待機
			yield return new WaitForSeconds(duration + 2f);
			
			currentDistance = Vector2.Distance(m_rect1.anchoredPosition, m_cursorImage.rectTransform.anchoredPosition);
			Assert.Less(currentDistance, 1f, "アニメーション完了後は目的地に到達しているはず");
		}
		#endregion
	}
}
