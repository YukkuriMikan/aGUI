using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using DG.Tweening;
using System.Reflection;

namespace ANest.UI.Tests {
	/// <summary>
	/// aContainerCursor の動作を検証するテストクラス
	/// </summary>
	public class aContainerCursorTests {
		private GameObject m_rootObject;
		private GameObject m_containerObject;
		private aContainerBase m_container;
		private GameObject m_cursorObject;
		private Image m_cursorImage;
		private aContainerCursor m_cursor;
		private GameObject m_selectableObject1;
		private RectTransform m_rect1;

		[SetUp]
		public void SetUp() {
			// Canvas がないと position (ワールド座標) の計算が正しく行われない場合がある
			m_rootObject = new GameObject("TestRoot", typeof(Canvas));
			
			// Container
			m_containerObject = new GameObject("Container", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			m_containerObject.transform.SetParent(m_rootObject.transform);
			m_container = m_containerObject.AddComponent<TestContainer>();
			
			// Selectable
			m_selectableObject1 = new GameObject("Selectable1", typeof(RectTransform), typeof(Image), typeof(Button));
			m_selectableObject1.transform.SetParent(m_containerObject.transform);
			m_rect1 = m_selectableObject1.GetComponent<RectTransform>();
			m_rect1.sizeDelta = new Vector2(100, 50);
			m_rect1.anchoredPosition = new Vector2(0, 0);

			// Cursor
			m_cursorObject = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
			m_cursorObject.transform.SetParent(m_rootObject.transform);
			m_cursorImage = m_cursorObject.GetComponent<Image>();
			m_cursor = m_cursorObject.AddComponent<aContainerCursor>();

			// リフレクションで初期パラメータを設定
			SetPrivateField(m_cursor, "m_container", m_container);
			SetPrivateField(m_cursor, "m_cursorImage", m_cursorImage);
			// 内部キャッシュも強制的にセット（Awakeタイミングのズレ対策）
			SetPrivateField(m_cursor, "m_cursorRect", m_cursorImage.rectTransform);
		}

		[TearDown]
		public void TearDown() {
			Object.DestroyImmediate(m_rootObject);
		}

		private void SetPrivateField(object obj, string fieldName, object value) {
			var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			field.SetValue(obj, value);
		}

		private class TestContainer : aContainerBase {
			public void SetCurrentSelectable(Selectable selectable) {
				var field = typeof(aContainerBase).GetField("m_currentSelectable", BindingFlags.NonPublic | BindingFlags.Instance);
				field.SetValue(this, selectable);
			}
		}

		/// <summary>
		/// UpdateMode.EveryFrame のとき、ターゲットの移動に追従することを検証
		/// </summary>
		[UnityTest]
		public IEnumerator UpdateMode_EveryFrame_FollowsMovingTarget() {
			// Arrange
			SetPrivateField(m_cursor, "m_updateMode", aContainerCursor.UpdateMode.EveryFrame);
			SetPrivateField(m_cursor, "m_moveMode", aContainerCursor.MoveMode.Instant);
			var selectable1 = m_selectableObject1.GetComponent<Selectable>();
			
			yield return null; // Start等完了待ち

			// Act
			var method = typeof(aContainerCursor).GetMethod("OnSelectableChanged", BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(m_cursor, new object[] { selectable1 });
			yield return null; // LateUpdate待ち

			Assert.AreEqual(m_rect1.position, m_cursorImage.rectTransform.position, "最初は一致すべき");

			// ターゲットを移動
			m_rect1.position = new Vector3(500, 500, 0);
			yield return null; // LateUpdate待ち

			// Assert
			Assert.AreEqual(m_rect1.position, m_cursorImage.rectTransform.position, "移動後も追従すべき");
		}

		/// <summary>
		/// UpdateMode.OnSelectChanged のとき、ターゲットの移動に追従しないことを検証
		/// </summary>
		[UnityTest]
		public IEnumerator UpdateMode_OnSelectChanged_DoesNotFollowMovingTarget() {
			// Arrange
			SetPrivateField(m_cursor, "m_updateMode", aContainerCursor.UpdateMode.OnSelectChanged);
			SetPrivateField(m_cursor, "m_moveMode", aContainerCursor.MoveMode.Instant);
			var selectable1 = m_selectableObject1.GetComponent<Selectable>();
			
			yield return null;

			// Act
			var method = typeof(aContainerCursor).GetMethod("OnSelectableChanged", BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(m_cursor, new object[] { selectable1 });
			yield return null;

			Assert.AreEqual(m_rect1.position, m_cursorImage.rectTransform.position, "最初は一致すべき");

			// ターゲットを移動
			Vector3 oldPos = m_rect1.position;
			m_rect1.position = new Vector3(500, 500, 0);
			yield return null;

			// Assert
			Assert.AreEqual(oldPos, m_cursorImage.rectTransform.position, "OnSelectChanged モードでは移動に追従しないはず");
			Assert.AreNotEqual(m_rect1.position, m_cursorImage.rectTransform.position, "ターゲットの位置とは不一致になるはず");
		}

		/// <summary>
		/// 選択切り替え時のサイズ一致を検証
		/// </summary>
		[UnityTest]
		public IEnumerator Cursor_MatchesSize_Instant() {
			// Arrange
			SetPrivateField(m_cursor, "m_moveMode", aContainerCursor.MoveMode.Instant);
			SetPrivateField(m_cursor, "m_sizeMode", aContainerCursor.SizeMode.MatchSelectable);
			Vector2 padding = new Vector2(10, 10);
			SetPrivateField(m_cursor, "m_padding", padding);
			
			var selectable1 = m_selectableObject1.GetComponent<Selectable>();
			
			// Act
			var method = typeof(aContainerCursor).GetMethod("OnSelectableChanged", BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(m_cursor, new object[] { selectable1 });
			yield return null;

			// Assert
			Vector2 expectedSize = m_rect1.rect.size + padding;
			Assert.That(m_cursorImage.rectTransform.sizeDelta.x, Is.EqualTo(expectedSize.x).Within(0.01f));
			Assert.That(m_cursorImage.rectTransform.sizeDelta.y, Is.EqualTo(expectedSize.y).Within(0.01f));
		}

		/// <summary>
		/// MoveMode.Animation のとき、即座にターゲット位置に到達しないことを検証
		/// </summary>
		[UnityTest]
		public IEnumerator Cursor_MoveMode_Animation_TakesTime() {
			// Arrange
			SetPrivateField(m_cursor, "m_updateMode", aContainerCursor.UpdateMode.OnSelectChanged);
			SetPrivateField(m_cursor, "m_moveMode", aContainerCursor.MoveMode.Animation);
			float duration = 1.0f;
			SetPrivateField(m_cursor, "m_moveDuration", duration);
			
			var selectable1 = m_selectableObject1.GetComponent<Selectable>();
			
			// 初期位置を設定
			m_cursorObject.GetComponent<RectTransform>().position = new Vector3(-1000, -1000, 0);
			m_rect1.position = new Vector3(0, 0, 0);

			yield return null;

			// Act
			var method = typeof(aContainerCursor).GetMethod("OnSelectableChanged", BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(m_cursor, new object[] { selectable1 });
			
			// 0.1秒待機（durationより短い時間）
			yield return new WaitForSeconds(0.1f);

			// Assert
			// もしバグがあれば、この時点で既に (0,0,0) になっているはず
			Assert.AreNotEqual(m_rect1.position, m_cursorImage.rectTransform.position, "アニメーション中なので、まだ目的地に到達していないはず");
			
			// 完了を待つ
			yield return new WaitForSeconds(duration);
			Assert.AreEqual(m_rect1.position, m_cursorImage.rectTransform.position, "アニメーション完了後は目的地に到達しているはず");
		}
	}
}
