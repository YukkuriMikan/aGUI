using UnityEngine;
using UniRx;

namespace ANest.UI {
	/// <summary>メインコンテナに従属するサブコンテナ</summary>
	public class aSubContainer : aContainerBase {
		[Header("Sub Container")]
		[Tooltip("紐付けるメインコンテナ")]
		[SerializeField] private aContainerBase m_mainContainer; // メインコンテナ参照
		private aContainerBase m_subscribedMainContainer;
		private System.IDisposable m_pendingEnabledSync;
#if UNITY_EDITOR
		private bool m_refreshConnectionQueued;
#endif

		/// <summary>従属するメインコンテナ</summary>
		public aContainerBase MainContainer {
			get => m_mainContainer;
			set {
				if(m_mainContainer == value) return;

				m_mainContainer = value;
				RefreshMainConnection();
			}
		}

		/// <summary>初期化時にメインコンテナと状態を同期する</summary>
		public override void Initialize() {
			if(m_initialized) return;

			// 初期状態だけを合わせ、参照の準備とイベント発火は基底の初期化で一度だけ行う。
			if(enabled && !IsStandalone) m_isVisible = m_mainContainer.IsVisible;
			base.Initialize();
			RefreshMainConnection();
		}

		protected override void OnEnable() {
			base.OnEnable();
			StopWaitingForEnable();
			RefreshMainConnection();
		}

		protected override void OnDisable() {
			base.OnDisable();
			if(!enabled) WaitForEnable();
		}

		/// <summary>破棄時にメインコンテナの購読を解除する</summary>
		protected override void OnDestroy() {
			StopWaitingForEnable();
			UnsubscribeFromMainContainer();

			base.OnDestroy();
		}

		/// <summary>メインコンテナの状態を考慮して表示する</summary>
		public override void Show() {
			if(m_isVisible) return;
			if(IsMainContainerHiddenWithWarning()) return;

			ShowInternal();
		}

		/// <summary>メインコンテナと同調して非表示にする</summary>
		public override void Hide() {
			if(!m_isVisible) return;
			HideInternal();
		}

		/// <summary>メインコンテナの表示イベントへ購読する</summary>
		private void SubscribeToMainContainer() {
			if(m_subscribedMainContainer == m_mainContainer) return;
			UnsubscribeFromMainContainer();
			m_subscribedMainContainer = m_mainContainer;
			if(m_subscribedMainContainer == null) return;
			m_subscribedMainContainer.OnShow.AddListener(OnMainContainerShow);
			m_subscribedMainContainer.OnHide.AddListener(OnMainContainerHide);
		}

		/// <summary>メインコンテナのイベント購読を解除する</summary>
		private void UnsubscribeFromMainContainer() {
			if(m_subscribedMainContainer != null) {
				m_subscribedMainContainer.OnShow.RemoveListener(OnMainContainerShow);
				m_subscribedMainContainer.OnHide.RemoveListener(OnMainContainerHide);
			}
			m_subscribedMainContainer = null;
		}

		private void RefreshMainConnection() {
			if(!m_initialized) return;
			SubscribeToMainContainer();
			SyncWithMainVisibility();
		}

		/// <summary>メインコンテナの表示状態に合わせて自分を表示/非表示にする</summary>
		private void SyncWithMainVisibility() {
			// Hideで自身のGameObjectを非表示にした後もMainのShowは受け取る。
			// コンポーネントのチェックをOFFにした場合は同期を止める。
			if(IsStandalone) { StopWaitingForEnable(); return; }
			if(!enabled) { WaitForEnable(); return; }
			StopWaitingForEnable();

			if(IsMainContainerHidden()) {
				Hide();
			} else {
				Show();
			}
		}

		/// <summary>メインコンテナのShowイベントに連動して表示する</summary>
		private void OnMainContainerShow() {
			RefreshMainConnection();
		}

		/// <summary>メインコンテナのHideイベントに連動して非表示にする</summary>
		private void OnMainContainerHide() {
			RefreshMainConnection();
		}

		/// <summary>メインコンテナが非表示かどうかを返す</summary>
		private bool IsMainContainerHidden() {
			if(m_mainContainer == null) return false;
			return !m_mainContainer.IsVisible;
		}

		/// <summary>メインコンテナ未指定なら通常のコンテナとして振る舞うか</summary>
		private bool IsStandalone => m_mainContainer == null;

		/// <summary>メインコンテナ非表示時に警告を出して操作を抑止する</summary>
		private bool IsMainContainerHiddenWithWarning() {
			if(IsStandalone) return false;
			if(!IsMainContainerHidden()) return false;
			Debug.LogWarning($"[{nameof(aSubContainer)}] {name} のメインコンテナが非表示のため Show は無視されます。", this);
			return true;
		}

		#if UNITY_EDITOR
		protected override void OnValidate() {
			base.OnValidate();
			if(!Application.isPlaying || !m_initialized || m_refreshConnectionQueued) return;
			// OnValidateではSetActiveせず、Inspectorからの参照変更をメインスレッドで適用する。
			m_refreshConnectionQueued = true;
			UnityEditor.EditorApplication.delayCall += RefreshMainConnectionInEditor;
		}

		private void WaitForEnable() {
			if(!Application.isPlaying || IsStandalone || m_pendingEnabledSync != null) return;
			// 非アクティブなGameObjectではenabledをONにしてもOnEnableが来ない。
			// 同期を保留している間だけ、GameObjectに依存しない更新で復帰を検出する。
			m_pendingEnabledSync = Observable.EveryUpdate().Subscribe(_ => {
				if(this == null || !enabled) return;
				StopWaitingForEnable();
				RefreshMainConnection();
			});
		}

		private void StopWaitingForEnable() {
			m_pendingEnabledSync?.Dispose();
			m_pendingEnabledSync = null;
		}

		private void RefreshMainConnectionInEditor() {
			if(this == null) return;
			m_refreshConnectionQueued = false;
			if(Application.isPlaying) RefreshMainConnection();
		}

		/// <summary>エディタ上で自動付与するコンテナ名の接頭辞</summary>
		protected override string ContainerNamePrefix => "SubContainer - ";

		/// <summary>既知の接頭辞を取り除いた名称を返す</summary>
		/// <param name="currentName">現在のGameObject名</param>
		protected override string TrimKnownPrefixes(string currentName) {
			const string basePrefix = "Container - ";
			const string subPrefix = "SubContainer - ";

			if(currentName.StartsWith(subPrefix)) return currentName.Substring(subPrefix.Length);
			if(currentName.StartsWith(basePrefix)) return currentName.Substring(basePrefix.Length);

			return base.TrimKnownPrefixes(currentName);
		}
		#endif
	}
}
