using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using UniRx;
using DG.Tweening;

namespace ANest.UI.Tests {
	/// <summary> aStaticContainer の基本機能を検証するテストクラス </summary>
	public class aStaticContainerTests {
		#region Test Helper Classes
		/// <summary> テスト用に aStaticContainer の内部メソッドを公開する継承クラス </summary>
		private class TestStaticContainer : aStaticContainer {
			/// <summary> 初期化メソッドをテスト用に公開 </summary>
			public void TestInitialize() => Initialize();

			/// <summary> リフレクションを使用してアニメーションを設定する </summary>
			public void SetAnimations(IUiAnimation[] show, IUiAnimation[] hide) {
				var type = typeof(aContainerBase);
				type.GetField("m_showAnimations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, show);
				type.GetField("m_hideAnimations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, hide);
			}
		}

		/// <summary> テスト用のモックアニメーション。指定された時間だけ再生される。 </summary>
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
		private GameObject m_testObject;         // テスト対象のGameObject
		private TestStaticContainer m_container; // テスト対象のコンテナコンポーネント
		#endregion

		#region Setup / Teardown
		/// <summary> 各テスト実行前のセットアップ </summary>
		[SetUp]
		public void SetUp() {
			m_testObject = new GameObject("aStaticContainer", typeof(RectTransform));
			// aContainerBase は CanvasGroup と aGuiInfo を必要とする
			m_testObject.AddComponent<CanvasGroup>();
			var guiInfo = m_testObject.AddComponent<aGuiInfo>();

			// aGuiInfo のフィールドをリフレクションで初期化
			var guiInfoType = typeof(aGuiInfo);
			var rectTransform = m_testObject.GetComponent<RectTransform>();
			guiInfoType.GetField("m_rectTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(guiInfo, rectTransform);
			guiInfoType.GetField("m_originalRectTransformValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(guiInfo, RectTransformValues.CreateValues(rectTransform));

			m_container = m_testObject.AddComponent<TestStaticContainer>();
		}

		/// <summary> 各テスト実行後のクリーンアップ </summary>
		[TearDown]
		public void TearDown() {
			aContainerManager.Clear();
			Object.DestroyImmediate(m_testObject);
		}
		#endregion

		#region Registration Tests
		/// <summary> aContainerManager への自動登録および解除が正しく行われるか </summary>
		[Test]
		public void Registration_IsAutomatic() {
			aContainerManager.Clear();
			Assert.AreEqual(0, aContainerManager.Count, "最初は0であるべき");

			// 新しいコンテナを作成（AddComponent時にAwake -> Initializeが走る）
			var obj = new GameObject("AutoRegContainer", typeof(RectTransform), typeof(CanvasGroup), typeof(aGuiInfo));
			var container = obj.AddComponent<TestStaticContainer>();
			container.TestInitialize(); // 明示的に呼ぶ（Awakeでの自動呼び出しがテスト環境で不安定な場合があるため）

			Assert.AreEqual(1, aContainerManager.Count, "初期化時に自動登録されるべき");

			Object.DestroyImmediate(obj);
			Assert.AreEqual(0, aContainerManager.Count, "破棄時に自動削除されるべき");
		}
		#endregion

		#region Visibility Tests
		/// <summary> 初期状態が正しいか </summary>
		[Test]
		public void InitialState_IsCorrect() {
			// AwakeでInitializeが呼ばれるはず
			Assert.IsFalse(m_container.IsVisible, "デフォルトでは非表示であるべき");
			Assert.IsFalse(m_testObject.activeSelf, "非表示状態なので GameObject は非アクティブであるべき");
		}

		/// <summary> Show メソッドが可視状態と GameObject の活性状態を正しく変更するか </summary>
		[Test]
		public void Show_ChangesVisibilityAndActivatesGameObject() {
			bool eventFired = false;
			m_container.OnShow.AddListener(() => eventFired = true);

			m_container.Show();

			Assert.IsTrue(m_container.IsVisible, "Show後はIsVisibleがtrueになるべき");
			Assert.IsTrue(m_testObject.activeSelf, "Show後はGameObjectがアクティブになるべき");
			Assert.IsTrue(eventFired, "OnShowイベントが発火するべき");
		}

		/// <summary> Hide メソッドが可視状態と GameObject の非活性状態を正しく変更するか </summary>
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

		/// <summary> IsVisible プロパティのセットが Show/Hide を正しくトリガーするか </summary>
		[Test]
		public void IsVisibleProperty_TriggersShowHide() {
			m_container.IsVisible = true;
			Assert.IsTrue(m_testObject.activeSelf);

			m_container.IsVisible = false;
			Assert.IsFalse(m_testObject.activeSelf);
		}

		/// <summary> 表示完了時に ShowEndObservable が正しく発火するか </summary>
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

		#region Animation Tests
		/// <summary> Show アニメーション中に Hide を実行した際、正常に中断して非表示に遷移するか </summary>
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
			Assert.IsFalse(m_testObject.activeSelf, "中断後のHideによりGameObjectが非アクティブになるべき");
		}

		/// <summary> Hide アニメーション中に Show を実行した際、正常に中断して表示に遷移するか </summary>
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
