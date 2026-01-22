using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UniRx;
using DG.Tweening;

namespace ANest.UI.Tests {
	/// <summary>
	/// aContainerBase の基本機能を検証するテストクラス
	/// </summary>
	public class aContainerBaseTests {
		#region Test Helper Classes
		/// <summary>
		/// テスト用に aContainerBase の内部メソッドを公開する継承クラス
		/// </summary>
		private class TestContainer : aContainerBase {
			/// <summary> 初期化メソッドをテスト用に公開 </summary>
			public void TestInitialize() => Initialize();
			/// <summary> 表示処理メソッドをテスト用に公開 </summary>
			public void TestShowInternal() => ShowInternal();
			/// <summary> 非表示処理メソッドをテスト用に公開 </summary>
			public void TestHideInternal() => HideInternal();

			/// <summary>
			/// リフレクションを使用してアニメーションを設定する
			/// </summary>
			public void SetAnimations(IUiAnimation[] show, IUiAnimation[] hide) {
				var type = typeof(aContainerBase);
				type.GetField("m_showAnimations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, show);
				type.GetField("m_hideAnimations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, hide);
			}
		}

		/// <summary>
		/// テスト用に aSelectableContainer の内部メソッドを公開する継承クラス
		/// </summary>
		private class TestSelectableContainer : aSelectableContainer {
			/// <summary> 初期化メソッドをテスト用に公開 </summary>
			public void TestInitialize() => Initialize();

			/// <summary>
			/// リフレクションを使用して m_disallowNullSelection フィールドの値を設定する
			/// </summary>
			/// <param name="value">設定する値</param>
			public void SetDisallowNullSelection(bool value) {
				var field = typeof(aSelectableContainer).GetField("m_disallowNullSelection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				field.SetValue(this, value);
			}

			/// <summary>
			/// リフレクションを使用して m_childSelectables フィールドの値を設定し、監視を開始する
			/// </summary>
			/// <param name="selectables">設定する選択可能要素の配列</param>
			public void SetChildSelectables(Selectable[] selectables) {
				var field = typeof(aSelectableContainer).GetField("m_childSelectables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				field.SetValue(this, selectables);
				ObserveSelectables();
			}

			/// <summary>
			/// リフレクションを使用してアニメーションを設定する
			/// </summary>
			public void SetAnimations(IUiAnimation[] show, IUiAnimation[] hide) {
				var type = typeof(aContainerBase);
				type.GetField("m_showAnimations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, show);
				type.GetField("m_hideAnimations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, hide);
			}
		}

		/// <summary>
		/// テスト用のモックアニメーション。指定された時間だけ再生される。
		/// </summary>
		private class MockUiAnimation : IUiAnimation {
			public float Delay { get; set; } = 0f;
			public float Duration { get; set; } = 1f;
			public bool IsYoYo => false;
			public AnimationCurve Curve => null;
			public Ease Ease => Ease.Linear;
			public bool UseCurve => false;

			public Tween DoAnimate(Graphic graphic, RectTransform callerRect, RectTransformValues original) {
				System.Console.WriteLine($"[DEBUG_LOG] MockUiAnimation DoAnimate start (Duration: {Duration})");
				// 単純に時間を稼ぐための仮想的なTween。ターゲットを設定して Kill されるようにする。
				return DOTween.To(() => 0f, x => { }, 1f, Duration).SetDelay(Delay).SetTarget(callerRect);
			}
		}
		#endregion

		#region Fields
		private GameObject m_testObject;   // テスト対象のGameObject
		private TestContainer m_container; // テスト対象のコンテナコンポーネント
		#endregion

		#region Setup / Teardown
		/// <summary>
		/// 各テスト実行前のセットアップ
		/// </summary>
		[SetUp]
		public void SetUp() {
			m_testObject = new GameObject("TestContainer", typeof(RectTransform));
			// aContainerBase は CanvasGroup と aGuiInfo を必要とする
			m_testObject.AddComponent<CanvasGroup>();
			var guiInfo = m_testObject.AddComponent<aGuiInfo>();

			// aGuiInfo のフィールドをリフレクションで初期化
			var guiInfoType = typeof(aGuiInfo);
			var rectTransform = m_testObject.GetComponent<RectTransform>();
			guiInfoType.GetField("m_rectTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(guiInfo, rectTransform);
			guiInfoType.GetField("m_originalRectTransformValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(guiInfo, RectTransformValues.CreateValues(rectTransform));

			m_container = m_testObject.AddComponent<TestContainer>();
		}

		/// <summary>
		/// 各テスト実行後のクリーンアップ
		/// </summary>
		[TearDown]
		public void TearDown() {
			aContainerManager.Clear();
			Object.DestroyImmediate(m_testObject);
		}
		#endregion

		#region Registration Tests
		/// <summary>
		/// aContainerManager への自動登録および解除が正しく行われるか
		/// </summary>
		[Test]
		public void Registration_IsAutomatic() {
			aContainerManager.Clear();
			Assert.AreEqual(0, aContainerManager.Count, "最初は0であるべき");

			// 新しいコンテナを作成（AddComponent時にAwake -> Initializeが走る）
			var obj = new GameObject("AutoRegContainer", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var container = obj.AddComponent<TestContainer>();
			container.TestInitialize(); // 明示的に呼ぶ（Awakeでの自動呼び出しがテスト環境で不安定な場合があるため）

			Assert.AreEqual(1, aContainerManager.Count, "初期化時に自動登録されるべき");

			Object.DestroyImmediate(obj);
			Assert.AreEqual(0, aContainerManager.Count, "破棄時に自動削除されるべき");
		}
		#endregion

		#region Visibility Tests
		/// <summary>
		/// IsVisible が true で開始されたとき、InitialSelectable が正しく選択されるか
		/// </summary>
		[UnityTest]
		public IEnumerator InitialSelectable_IsSelected_WhenStartedWithIsVisibleTrue() {
			// EventSystemが必要
			var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

			try {
				var go = new GameObject("Container", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
				var btnGo = new GameObject("Button", typeof(RectTransform), typeof(Button));
				btnGo.transform.SetParent(go.transform);
				var btn = btnGo.GetComponent<Button>();

				var container = go.AddComponent<TestSelectableContainer>();
				container.InitialSelectable = btn;
				container.IsVisible = true;

				// テスト環境では Awake/Start が自動で呼ばれない場合があるので明示的に呼ぶ
				container.RefreshChildSelectables();

				yield return null; // 1フレーム待機

				Assert.AreEqual(btn.gameObject, EventSystem.current.currentSelectedGameObject, "開始時に IsVisible が true なら InitialSelectable が選択されているべき");

				Object.DestroyImmediate(go);
			} finally {
				Object.DestroyImmediate(eventSystemGo);
			}
		}

		/// <summary>
		/// IsVisible が true で開始されたとき、DisallowNullSelection が正しく機能するか
		/// </summary>
		[UnityTest]
		public IEnumerator DisallowNullSelection_Works_WhenStartedWithIsVisibleTrue() {
			// EventSystemが必要
			var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

			try {
				var go = new GameObject("TestContainer", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
				var btnGo = new GameObject("TestButton", typeof(RectTransform), typeof(Button));
				btnGo.transform.SetParent(go.transform);
				var btn = btnGo.GetComponent<Button>();

				var container = go.AddComponent<TestSelectableContainer>();
				container.IsVisible = true;
				container.DisallowNullSelection = true;

				// テスト環境では Awake/Start が自動で呼ばれない場合があるので明示的に呼ぶ
				container.RefreshChildSelectables();

				btn.Select();
				yield return null;

				Assert.AreEqual(btn.gameObject, EventSystem.current.currentSelectedGameObject);

				// 選択解除を試みる
				EventSystem.current.SetSelectedGameObject(null);
				yield return null;
				yield return null;

				Assert.AreEqual(btn.gameObject, EventSystem.current.currentSelectedGameObject, "DisallowNullSelection が有効なら null 選択時に引き戻されるべき");

				Object.DestroyImmediate(go);
			} finally {
				Object.DestroyImmediate(eventSystemGo);
			}
		}

		/// <summary>
		/// 初期状態が正しいか
		/// </summary>
		[Test]
		public void InitialState_IsCorrect() {
			// AwakeでInitializeが呼ばれるはず
			Assert.IsFalse(m_container.IsVisible, "デフォルトでは非表示であるべき");
			Assert.IsFalse(m_testObject.activeSelf, "非表示状態なので GameObject は非アクティブであるべき");
		}

		/// <summary>
		/// Show メソッドが可視状態と GameObject の活性状態を正しく変更するか
		/// </summary>
		[Test]
		public void Show_ChangesVisibilityAndActivatesGameObject() {
			bool eventFired = false;
			m_container.OnShow.AddListener(() => eventFired = true);

			m_container.Show();

			Assert.IsTrue(m_container.IsVisible, "Show後はIsVisibleがtrueになるべき");
			Assert.IsTrue(m_testObject.activeSelf, "Show後はGameObjectがアクティブになるべき");
			Assert.IsTrue(eventFired, "OnShowイベントが発火するべき");
		}

		/// <summary>
		/// Hide メソッドが可視状態と GameObject の非活性状態を正しく変更するか
		/// </summary>
		[Test]
		public void Hide_ChangesVisibilityAndDeactivatesGameObject() {
			m_container.Show();
			Assert.IsTrue(m_container.IsVisible);

			bool eventFired = false;
			m_container.OnHide.AddListener(() => eventFired = true);

			m_container.Hide();

			Assert.IsFalse(m_container.IsVisible, "Hide後はIsVisibleがfalseになるべき");
			// HideInternalはアニメーションなしの場合即座に非アクティブにする
			Assert.IsFalse(m_testObject.activeSelf, "Hide後はGameObjectが非アクティブになるべき");
			Assert.IsTrue(eventFired, "OnHideイベントが発火するべき");
		}

		/// <summary>
		/// IsVisible プロパティのセットが Show/Hide を正しくトリガーするか
		/// </summary>
		[Test]
		public void IsVisibleProperty_TriggersShowHide() {
			m_container.IsVisible = true;
			Assert.IsTrue(m_testObject.activeSelf);

			m_container.IsVisible = false;
			Assert.IsFalse(m_testObject.activeSelf);
		}

		/// <summary>
		/// 表示完了時に ShowEndObservable が正しく発火するか
		/// </summary>
		[UnityTest]
		public IEnumerator ShowEndObservable_FiresWhenShowCompletes() {
			bool completed = false;
			m_container.ShowEndObservable.Subscribe(_ => completed = true);

			m_container.Show();

			// アニメーションがない場合は即座に完了するはずだが、一応1フレーム待機
			yield return null;

			Assert.IsTrue(completed, "ShowEndObservableが通知されるべき");
		}
		#endregion

 	#region UI Interaction Tests (aSelectableContainer)
		/// <summary>
		/// InitialGuard が有効な際、指定時間だけ blocksRaycasts が false になるか
		/// </summary>
		[UnityTest]
		public IEnumerator InitialGuard_BlocksRaycastsTemporarily() {
			// aSelectableContainer用のテストオブジェクトを作成
			var testObj = new GameObject("TestSelectableContainer", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var container = testObj.AddComponent<TestSelectableContainer>();

			try {
				// リフレクションを使用してシリアライズフィールドを設定
				var type = typeof(aSelectableContainer);
				type.GetField("m_initialGuard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(container, true);
				type.GetField("m_initialGuardDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(container, 0.5f);

				container.Show();

				var canvasGroup = testObj.GetComponent<CanvasGroup>();
				Assert.IsFalse(canvasGroup.blocksRaycasts, "InitialGuard中はblocksRaycastsがfalseであるべき");

				yield return new WaitForSeconds(0.6f);

				Assert.IsTrue(canvasGroup.blocksRaycasts, "InitialGuard終了後はblocksRaycastsがtrueに戻るべき");
			} finally {
				Object.DestroyImmediate(testObj);
			}
		}

		/// <summary>
		/// DisallowNullSelection が有効な際、選択解除がブロックされるか
		/// </summary>
		[UnityTest]
		public IEnumerator DisallowNullSelection_PreventsNullSelection() {
			// aSelectableContainer用のテストオブジェクトを作成
			var testObj = new GameObject("TestSelectableContainer", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var container = testObj.AddComponent<TestSelectableContainer>();

			// Selectableを作成
			var go1 = new GameObject("Button1", typeof(RectTransform), typeof(UnityEngine.UI.Button));
			var go2 = new GameObject("Button2", typeof(RectTransform), typeof(UnityEngine.UI.Button));
			go1.transform.SetParent(testObj.transform);
			go2.transform.SetParent(testObj.transform);
			var b1 = go1.GetComponent<UnityEngine.UI.Button>();
			var b2 = go2.GetComponent<UnityEngine.UI.Button>();

			// EventSystemが必要
			var eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

			try {
				// リフレクションで設定
				var type = typeof(aSelectableContainer);
				type.GetField("m_childSelectables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(container, new UnityEngine.UI.Selectable[] {
					b1,
					b2
				});
				type.GetField("m_disallowNullSelection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(container, true);

				container.Show();

				// 監視を開始させるために一度呼び出す（本来は内部で自動で呼ばれるべきだが、テストでのセットアップ上）
				var observeMethod = type.GetMethod("ObserveSelectables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				observeMethod.Invoke(container, null);

				// b1を選択
				b1.Select();
				yield return null;
				Assert.AreEqual(b1, type.GetField("m_currentSelectable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(container));

				// 選択を解除（nullを選択）
				UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
				yield return null; // NextFrame 待ち
				yield return null; // さらにもう1フレーム（念のため）

				// m_disallowNullSelection が true なので、b1 が再選択されているか、少なくとも m_currentSelectable が保持されているべき
				Assert.IsNotNull(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, "選択がnullになってはいけない");
				Assert.AreEqual(go1, UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, "b1が再選択されているべき");
			} finally {
				Object.DestroyImmediate(eventSystemGo);
				Object.DestroyImmediate(go1);
				Object.DestroyImmediate(go2);
				Object.DestroyImmediate(testObj);
			}
		}

		/// <summary>
		/// コンテナが無効な際は ObserveSelectables による再選択が行われないか
		/// </summary>
		[UnityTest]
		public IEnumerator ObserveSelectables_IsInactiveWhenDisabled() {
			// aSelectableContainer用のテストオブジェクトを作成
			var testObj = new GameObject("TestSelectableContainer", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var container = testObj.AddComponent<TestSelectableContainer>();

			// Selectableを作成
			var go1 = new GameObject("Button1", typeof(RectTransform), typeof(UnityEngine.UI.Button));
			go1.transform.SetParent(testObj.transform);
			var b1 = go1.GetComponent<UnityEngine.UI.Button>();

			// EventSystemが必要
			var eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

			try {
				var type = typeof(aSelectableContainer);
				type.GetField("m_childSelectables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(container, new UnityEngine.UI.Selectable[] {
					b1
				});
				type.GetField("m_disallowNullSelection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(container, true);

				// 表示して監視を開始させる
				container.Show();
				var observeMethod = type.GetMethod("ObserveSelectables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				observeMethod.Invoke(container, null);

				// b1を選択
				b1.Select();
				yield return null;

				// 非表示にする（Disableにする）
				container.Hide();
				yield return null;
				Assert.IsFalse(testObj.activeInHierarchy, "コンテナは非アクティブであるべき");

				// 選択を解除（nullを選択）
				// 注意: UI.SelectableはGameObjectが非アクティブでもイベントを発火する場合がある。
				// aSelectableContainerのObserveSelectablesが依然として購読中なら、NextFrameで再選択が走るはず。
				UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
				yield return null; // NextFrame 待ち
				yield return null;

				// コンテナがDisableなら再選択は行われないはず
				Assert.IsNull(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, "コンテナがDisableの時は再選択されないべき");
			} finally {
				Object.DestroyImmediate(eventSystemGo);
				Object.DestroyImmediate(go1);
				Object.DestroyImmediate(testObj);
			}
		}

		/// <summary>
		/// 複数の DisallowNullSelection コンテナがある場合、最新のものが優先されるか
		/// </summary>
		[UnityTest]
		public IEnumerator DisallowNullSelection_LatestContainerTakesPrecedence() {
			aContainerManager.Clear();

			// コンテナ1を作成
			var obj1 = new GameObject("Container1", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo), typeof(EventSystem), typeof(StandaloneInputModule));
			var c1 = obj1.AddComponent<TestSelectableContainer>();
			var b1 = new GameObject("Button1").AddComponent<Button>();
			b1.transform.SetParent(obj1.transform);
			c1.SetChildSelectables(new Selectable[] {
				b1
			});
			c1.SetDisallowNullSelection(true);
			c1.IsVisible = true;
			c1.TestInitialize();

			// コンテナ2を作成 (後から登録される)
			var obj2 = new GameObject("Container2", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var c2 = obj2.AddComponent<TestSelectableContainer>();
			var b2 = new GameObject("Button2").AddComponent<Button>();
			b2.transform.SetParent(obj2.transform);
			c2.SetChildSelectables(new Selectable[] {
				b2
			});
			c2.SetDisallowNullSelection(true);
			c2.IsVisible = true;
			c2.TestInitialize();

			// 最初は何も選択されていない
			EventSystem.current.SetSelectedGameObject(null);
			yield return null;

			// コンテナ2（最新）が自分を選択しようとするはず（1フレーム待機が必要なロジックなので少し待つ）
			// 手動でDeselectイベントをシミュレートするのは難しいので、
			// IsLatestDisallowNullContainer の戻り値で直接検証する
			Assert.IsFalse(aContainerManager.IsLatestContainer(c1), "C1は最新ではない");
			Assert.IsTrue(aContainerManager.IsLatestContainer(c2), "C2は最新である");

			// c1 を再度 Add すると c1 が最新になる
			aContainerManager.Add(c1);
			Assert.IsTrue(aContainerManager.IsLatestContainer(c1), "C1が最新になった");
			Assert.IsFalse(aContainerManager.IsLatestContainer(c2), "C2は最新ではなくなった");

			Object.DestroyImmediate(obj1);
			Object.DestroyImmediate(obj2);
		}
		#endregion

		#region Animation Tests
		/// <summary>
		/// Show アニメーション中に Hide を実行した際、正常に中断して非表示に遷移するか
		/// </summary>
		[UnityTest]
		public IEnumerator ShowAnimation_InterruptedByHide() {
			// 1秒のアニメーションを設定
			var showAnim = new MockUiAnimation {
				Duration = 1.0f
			};
			m_container.SetAnimations(new IUiAnimation[] {
				showAnim
			}, null);

			// Show 開始
			m_container.Show();
			Assert.IsTrue(m_container.IsVisible, "Show開始直後にIsVisibleはtrueになるべき");

			// 少し待機（アニメーション中）
			yield return new WaitForSeconds(0.2f);

			// Hide 実行（Showを中断させる）
			m_container.Hide();
			Assert.IsFalse(m_container.IsVisible, "Hide実行直後にIsVisibleはfalseになるべき");

			// Hide のアニメーションがない場合は即座に GameObject が非アクティブになるはず
			// aContainerBase.cs:319 SetActiveInternal(false)
			Assert.IsFalse(m_testObject.activeSelf, "中断後のHideによりGameObjectが非アクティブになるべき");
		}

		/// <summary>
		/// Hide アニメーション中に Show を実行した際、正常に中断して表示に遷移するか
		/// </summary>
		[UnityTest]
		public IEnumerator HideAnimation_InterruptedByShow() {
			var type = typeof(aContainerBase);
			var nowHidingField = type.GetField("m_nowHiding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

			// 表示状態にする
			m_container.Show();
			Assert.IsTrue(m_testObject.activeSelf, "Step 1: Show後にアクティブであるべき");

			// 1秒の非表示アニメーションを設定
			var hideAnim = new MockUiAnimation {
				Duration = 1.0f
			};
			var showAnim = new MockUiAnimation {
				Duration = 0.1f
			};
			m_container.SetAnimations(new IUiAnimation[] {
				showAnim
			}, new IUiAnimation[] {
				hideAnim
			});

			// Hide 開始
			m_container.Hide();
			Assert.IsFalse(m_container.IsVisible, "Step 2: Hide開始直後にIsVisibleはfalseになるべき");
			Assert.IsTrue(m_testObject.activeSelf, "Step 3: Hideアニメーション中はGameObjectはアクティブなままであるべき");
			Assert.IsTrue((bool)nowHidingField.GetValue(m_container), "Step 3.1: m_nowHidingがtrueであるべき");

			// 少し待機（アニメーション中）
			yield return new WaitForSeconds(0.2f);
			Assert.IsTrue(m_testObject.activeSelf, "Step 4: 0.2s待機後もアクティブであるべき");
			Assert.IsTrue((bool)nowHidingField.GetValue(m_container), "Step 4.1: 0.2s待機後もm_nowHidingがtrueであるべき");

			// Show 実行（Hideを中断させる）
			m_container.Show();
			Assert.IsTrue(m_container.IsVisible, "Step 5: Show実行直後にIsVisibleがtrueになるべき");
			Assert.IsTrue(m_testObject.activeSelf, "Step 6: Show実行直後もアクティブであるべき");

			// Killedコールバックが実行されるのを待つ
			yield return null;
			Assert.IsFalse((bool)nowHidingField.GetValue(m_container), "Step 6.1: Show実行によりm_nowHidingがfalseになるべき(Killedコールバック経由)");

			// Show 完了を待つ（0.1sアニメーション + 余裕）
			yield return new WaitForSeconds(0.2f);
			Assert.IsTrue(m_testObject.activeSelf, "Step 7: Show完了後(0.2s)もアクティブであるべき");

			// Hide の中断により SetActiveInternal(false) が呼ばれていないことを確認
			yield return new WaitForSeconds(0.8f); // 元の Hide アニメーション時間を超えるまで待機
			Assert.IsTrue(m_testObject.activeSelf, "Step 8: Hideアニメーション予定時間を超えてもアクティブであるべき");
		}
		#endregion
	}
}
