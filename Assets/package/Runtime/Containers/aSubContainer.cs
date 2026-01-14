using UnityEngine;

namespace ANest.UI {
	/// <summary>メインコンテナに従属するサブコンテナ</summary>
	public class aSubContainer : aContainerBase {
		[Header("Sub Container")]
		[Tooltip("紐付けるメインコンテナ")]
		[SerializeField] private aContainerBase m_mainContainer; // メインコンテナ参照

		/// <summary>従属するメインコンテナ</summary>
		public aContainerBase MainContainer {
			get => m_mainContainer;
			set {
				if(m_mainContainer == value) return;

				UnsubscribeFromMainContainer();

				m_mainContainer = value;

				if(!IsStandalone) {
					SubscribeToMainContainer();
					SyncWithMainVisibility();
				}
			}
		}

		/// <summary>初期化時にメインコンテナと状態を同期する</summary>
		protected override void Initialize() {
			base.Initialize();
			
			m_suppressAnimation = true;

			if(!IsStandalone) {
				SubscribeToMainContainer();
				SyncWithMainVisibility();
			}

			m_suppressAnimation = false;
		}

		/// <summary>破棄時にメインコンテナの購読を解除する</summary>
		private void OnDestroy() {
			UnsubscribeFromMainContainer();
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
			if(m_mainContainer == null) return;
			m_mainContainer.OnShow.RemoveListener(OnMainContainerShow);
			m_mainContainer.OnHide.RemoveListener(OnMainContainerHide);
			m_mainContainer.OnShow.AddListener(OnMainContainerShow);
			m_mainContainer.OnHide.AddListener(OnMainContainerHide);
		}

		/// <summary>メインコンテナのイベント購読を解除する</summary>
		private void UnsubscribeFromMainContainer() {
			if(m_mainContainer == null) return;
			m_mainContainer.OnShow.RemoveListener(OnMainContainerShow);
			m_mainContainer.OnHide.RemoveListener(OnMainContainerHide);
		}

		/// <summary>メインコンテナの表示状態に合わせて自分を表示/非表示にする</summary>
		private void SyncWithMainVisibility() {
			if(IsStandalone) return;

			if(IsMainContainerHidden()) {
				Hide();
			} else {
				Show();
			}
		}

		/// <summary>メインコンテナのShowイベントに連動して表示する</summary>
		private void OnMainContainerShow() {
			Show();
		}

		/// <summary>メインコンテナのHideイベントに連動して非表示にする</summary>
		private void OnMainContainerHide() {
			Hide();
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
