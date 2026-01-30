using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>EventSystemに依存したSelectableなコンポーネントを抱えるコンテナ</summary>
	public class aSelectableContainer : aSelectableContainerBase<Selectable> {
		#region Field
		[SerializeField]
		[Tooltip("CurrentSelectableがNullになる事を許可しない")]
		protected bool m_disallowNullSelection = true; // CurrentSelectableがNullになる事を許可しない
		#endregion

		#region Property
		public bool DisallowNullSelection {
			get => m_disallowNullSelection;
			set => m_disallowNullSelection = value;
		}

		/// <summary>現在選択されているSelectableのインデックス</summary>
		public override int CurrentSelectableIndex {
			get => m_currentSelectableIndex;
			set {
				// 範囲外チェック
				if(m_childSelectableList == null || m_childSelectableList.Count == 0 || value < 0 || value >= m_childSelectableList.Count) {
					if(m_disallowNullSelection) return;

					var es = aGuiManager.EventSystem;
					if(es != null) {
						es.SetSelectedGameObject(null);
					}
					base.CurrentSelectableIndex = value;

					return;
				}

				// 範囲内の場合は選択を実行（base.CurrentSelectableIndex の set 内で m_currentSelectableIndex が更新される）
				m_childSelectableList[value].Select();
				base.CurrentSelectableIndex = value;
			}
		}

		/// <summary>現在選択されているSelectable</summary>
		public override Selectable CurrentSelectable {
			get => m_currentSelectable;
			set {
				if(value == null) {
					if(m_disallowNullSelection) return;
					base.CurrentSelectable = null;

					var es = aGuiManager.EventSystem;
					if(es != null) {
						es.SetSelectedGameObject(null);
					}

					return;
				}

				if(m_childSelectableList == null || m_childSelectableList.Count == 0) {
					if(m_disallowNullSelection) return;
					base.CurrentSelectable = null;

					var es = aGuiManager.EventSystem;
					if(es != null) {
						es.SetSelectedGameObject(null);
					}

					return;
				}

				var index = m_childSelectableList.IndexOf(value);

				if(index == -1) {
					if(m_disallowNullSelection) return;
					base.CurrentSelectable = null;

					var es = aGuiManager.EventSystem;
					if(es != null) {
						es.SetSelectedGameObject(null);
					}

					return;
				}

				base.CurrentSelectable = value;
			}
		}
		#endregion

		#region Protected Method
		/// <summary>子要素のSelectableの選択イベントを監視する</summary>
		protected override void ObserveSelectables() {
			base.ObserveSelectables();

			foreach (var selectable in ChildSelectableList) {
				if(selectable == null) continue;

				// 選択解除された時の処理
				selectable.OnDeselectAsObservable()
					.Subscribe(_ => {
						if(!m_disallowNullSelection) return;

						// 1フレーム待ってから再選択を試みる（Deselectの直後にSelectを呼ぶ必要があるため）
						Observable.NextFrame().Subscribe(__ => {
							// 自身が非アクティブになっていたら何もしない
							if(this == null || !gameObject.activeInHierarchy) return;

							// 最新のDisallowNullSelection有効コンテナである場合のみ再選択を行う
							if(!aContainerManager.IsLatestSelectableContainer(this)) return;

							var es = aGuiManager.EventSystem;

							if(es != null && es.currentSelectedGameObject == null) {
								if(m_currentSelectable != null && m_currentSelectable.IsActive() && m_currentSelectable.IsInteractable()) {
									m_currentSelectable.Select();
								}
							}
						}).AddTo(m_selectDisposables);
					})
					.AddTo(m_selectDisposables);
			}
		}

		protected override void SetInitialSelection() {
			base.SetInitialSelection();

			if(CurrentSelectable == null && m_disallowNullSelection && ChildSelectableList.Count > 0) {
				CurrentSelectable = ChildSelectableList[0];
			}

			if(CurrentSelectable != null) {
				CurrentSelectable.Select();
			}
		}

		/// <summary>表示状態の内部フラグを更新する</summary>
		protected override void UpdateStateForShow() {
			base.UpdateStateForShow();
			Interactable = true;
		}
		#endregion

#if UNITY_EDITOR
		/// <summary>コンポーネント追加・リセット時の処理</summary>
		protected override void Reset() {
			base.Reset();

			if(Application.isPlaying) return;

			RefreshChildSelectables();
		}

		/// <summary>Inspectorでの値変更時の処理</summary>
		protected override void OnValidate() {
			base.OnValidate();

			if(Application.isPlaying) return;

			RefreshChildSelectables();
		}
#endif
	}
}
