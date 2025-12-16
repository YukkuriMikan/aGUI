using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ANest.UI {
	/// <summary> メインコンテナに従属するサブコンテナ </summary>
	public class aSubContainer : aContainerBase {
		[Header("Sub Container")]
		[SerializeField] private aContainerBase m_mainContainer;

		public aContainerBase MainContainer {
			get => m_mainContainer;
			set {
				if(m_mainContainer == value) return;
				UnsubscribeFromMainContainer();
				m_mainContainer = value;
				SubscribeToMainContainer();
				SyncWithMainVisibility();
			}
		}

		protected override void OnEnable() {
			base.OnEnable();
			SubscribeToMainContainer();
			SyncWithMainVisibility();
		}

		private void OnDestroy() {
			UnsubscribeFromMainContainer();
		}

		public override async UniTask ShowAsync() {
			if(IsMainContainerHiddenWithWarning()) return;

			await base.ShowAsync();
		}

		public override void Show() {
			if(IsMainContainerHiddenWithWarning()) return;
			
			base.Show();
		}

		private void SubscribeToMainContainer() {
			if(m_mainContainer == null) return;
			m_mainContainer.ShowEvent.RemoveListener(OnMainContainerShow);
			m_mainContainer.HideEvent.RemoveListener(OnMainContainerHide);
			m_mainContainer.ShowEvent.AddListener(OnMainContainerShow);
			m_mainContainer.HideEvent.AddListener(OnMainContainerHide);
		}

		private void UnsubscribeFromMainContainer() {
			if(m_mainContainer == null) return;
			m_mainContainer.ShowEvent.RemoveListener(OnMainContainerShow);
			m_mainContainer.HideEvent.RemoveListener(OnMainContainerHide);
		}

		private void SyncWithMainVisibility() {
			if(m_mainContainer == null) return;
			if(IsMainContainerHidden()) {
				Hide();
			} else {
				Show();
			}
		}

		private void OnMainContainerShow() {
			Show();
		}

		private void OnMainContainerHide() {
			Hide();
		}

		private bool IsMainContainerHidden() {
			if(m_mainContainer == null) return false;
			return !m_mainContainer.IsVisible;
		}

		private bool IsMainContainerHiddenWithWarning() {
			if(!IsMainContainerHidden()) return false;
			Debug.LogWarning($"[{nameof(aSubContainer)}] {name} のメインコンテナが非表示のため Show は無視されます。", this);
			return true;
		}

		#if UNITY_EDITOR
		protected override string ContainerNamePrefix => "SubContainer - ";

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
