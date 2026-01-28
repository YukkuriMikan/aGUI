using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aSelectableContainerBase の CurrentSelectable に追従するカーソルの基底クラス</summary>
	public abstract class aCustomCursorBase<T> : aCursorBase where T : Selectable {
		#region Serialize Fields
		[Tooltip("追従対象のコンテナ")]
		[SerializeField]
		private aSelectableContainerBase<T> m_container;
		#endregion

		#region Private Fields
		private CompositeDisposable m_disposables = new(); // 購読管理用
		#endregion

		#region Lifecycle Methods
		/// <summary>開始時にコンテナの選択変更を購読する</summary>
		private void Start() {
			if(m_container != null) {
				var currentSelectable = m_container.CurrentSelectable;

				if(currentSelectable != null) {
					OnTargetRectChanged(currentSelectable.transform as RectTransform);
				}

				m_container.OnSelectChanged.AsObservable()
					.Subscribe(selectable => OnTargetRectChanged(selectable.transform as RectTransform))
					.AddTo(m_disposables);

				// コンテナがShowされた時に瞬間移動フラグを立てる
				m_container.ShowStartObservable
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
				m_container = GetComponentInParent<aSelectableContainerBase<T>>();
			}

			if(m_container == null) {
				m_container = GetComponentInChildren<aSelectableContainerBase<T>>();
			}
		}
#endif
		#endregion
	}
}
