using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aCustomCursorBase の CurrentSelectable に追従するカーソルの基底クラス</summary>
	public abstract class aCustomCursorBase<T> : aCursorBase where T : Selectable {
		#region Serialize Fields
		[Tooltip("追従対象のコンテナ")]
		[SerializeField]
		private aContainerBase m_container; // ジェネリックなフィールドはインスペクタに出せないため、基底クラスで参照を取得

		private aSelectableContainerBase<T> m_selectableContainer;
		#endregion

		#region Private Fields
		private CompositeDisposable m_disposables = new(); // 購読管理用
		#endregion

		#region Lifecycle Methods
		/// <summary>開始時にコンテナの選択変更を購読する</summary>
		private void Start() {
			m_selectableContainer = m_container as aSelectableContainerBase<T>;

			if(m_selectableContainer == null) {
#if UNITY_EDITOR
				var containerName = m_container != null ? m_container.name : "未設定";
				Debug.LogError($"リンク先のコンテナ（{containerName}）がSelectableContainerではありません", this.gameObject);
#endif

				return;
			}

			if(m_selectableContainer != null) {
				var currentSelectable = m_selectableContainer.CurrentSelectable;

				if(currentSelectable != null) {
					var rect = currentSelectable.transform as RectTransform;

					if(rect != null) {
						OnTargetRectChanged(rect);
					}
				}

				// 選択変更を監視して追従対象を切り替える（選択解除時はnullが通知される）
				m_selectableContainer.OnSelectChanged.AsObservable()
					.Subscribe(selectable => OnTargetRectChanged(selectable != null ? selectable.transform as RectTransform : null))
					.AddTo(m_disposables);

				// コンテナがShowされた時に瞬間移動フラグを立てる
				m_selectableContainer.ShowStartObservable
					.Subscribe(_ => m_wasHidden = true)
					.AddTo(m_disposables);
			}
		}

		/// <summary>破棄時に購読解除とTweenの破棄を行う</summary>
		protected override void OnDestroy() {
			base.OnDestroy();

			m_disposables.Dispose();
		}
		#endregion

		#region Editor Support
#if UNITY_EDITOR
		/// <summary>インスペクターでの値変更時に参照を更新する</summary>
		protected override void OnValidate() {
			base.OnValidate();

			if(m_container == null) {
				m_container = GetComponentInParent<aContainerBase>();
			}

			if(m_container == null) {
				m_container = GetComponentInChildren<aContainerBase>();
			}
		}
#endif
		#endregion
	}
}
