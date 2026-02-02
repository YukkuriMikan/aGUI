using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aSelectableContainer の CurrentSelectable に追従するカーソルを制御するコンポーネント</summary>
	public class aNormalCursorBase<T, V> : aCursorBase where T : aSelectableContainerBase<V> where V : Selectable {
		#region Serialize Fields
		[Tooltip("追従対象のコンテナ")]
		[SerializeField] private T m_container; // 追従対象のコンテナ
		#endregion

		#region Private Fields
		private CompositeDisposable m_disposables = new(); // 購読管理用
		#endregion

		#region Lifecycle Methods
		/// <summary>開始時にコンテナの選択変更を購読する</summary>
		private void Start() {
			var selectableContainer = m_container;

			if(m_container != null) {
				var currentSelectable = selectableContainer.CurrentSelectable;

				if(currentSelectable != null) {
					OnTargetRectChanged(currentSelectable.transform as RectTransform);
				}

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
				m_container = GetComponentInParent<T>();
			}

			if(m_container == null) {
				m_container = GetComponentInChildren<T>();
			}
		}
#endif
		#endregion
	}
}
