using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aSelectableContainer の CurrentSelectable に追従するカーソルを制御するコンポーネント</summary>
	public class aNormalCursorBase<ContainerType, SelectableType> : aCursorBase where ContainerType : aSelectableContainerBase<SelectableType> where SelectableType : Selectable {
		#region Serialize Fields
		[Tooltip("追従対象のコンテナ")]
		[SerializeField] private ContainerType m_container; // 追従対象のコンテナ
		#endregion

		#region Private Fields
		private CompositeDisposable m_disposables = new(); // 購読管理用
		#endregion

		#region Lifecycle Methods
		/// <summary>開始時にコンテナの選択変更を購読する</summary>
		private void Start() {
			var selectableContainer = m_container;

			// 参照先が設定されている場合のみ購読を開始する
			if(m_container != null) {
				// 現在選択中のSelectableがあれば即座に追従させる
				var currentSelectable = selectableContainer.CurrentSelectable;

				if(currentSelectable != null) {
					OnTargetRectChanged(currentSelectable.transform as RectTransform);
				}

				// 選択変更を監視して追従対象を切り替える
				selectableContainer.OnSelectChanged.AsObservable()
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
				m_container = GetComponentInParent<ContainerType>();
			}

			if(m_container == null) {
				m_container = GetComponentInChildren<ContainerType>();
			}
		}
#endif
		#endregion
	}
}
