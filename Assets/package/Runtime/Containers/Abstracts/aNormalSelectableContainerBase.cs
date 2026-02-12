using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>EventSystemに依存したSelectableなコンポーネントを抱えるコンテナ</summary>
	public abstract class aNormalSelectableContainerBase<T> : aSelectableContainerBase<T>, IDisallowNullSelectionContainer where T : Selectable {
		#region Field
		[Tooltip("CurrentSelectableがNullになる事を許可しない")]
		[SerializeField]
		protected bool m_disallowNullSelection = true; // CurrentSelectableがNullになる事を許可しない
		#endregion

		#region Property
		/// <summary>CurrentSelectableがNullになる事を許可しないかどうか</summary>
		public bool DisallowNullSelection {
			get => m_disallowNullSelection;
			set => m_disallowNullSelection = value;
		}

		/// <summary>現在選択されているSelectableのインデックス</summary>
		public override int CurrentSelectableIndex {
			get => m_currentSelectableIndex;
			set {
				var es = aGuiManager.EventSystem;
				if(!TryGetSelectableIndex(value, out var normalizedIndex)) {
					if(m_disallowNullSelection) return;

					if(es != null) {
						es.SetSelectedGameObject(null);
					}
					base.CurrentSelectableIndex = value;

					return;
				}

				// 範囲内の場合は選択を実行（base.CurrentSelectableIndex の set 内で m_currentSelectableIndex が更新される）
				var currentSelectedObject = ChildSelectableList[normalizedIndex].gameObject;

				if(es != null && es.currentSelectedGameObject != currentSelectedObject) {
					ChildSelectableList[normalizedIndex].Select();
				}

				base.CurrentSelectableIndex = normalizedIndex;
			}
		}

		/// <summary>現在選択されているSelectable</summary>
		public override T CurrentSelectable {
			get => m_currentSelectable;
			set {
				void TrySetNull() {
					if(m_disallowNullSelection) return;

					base.CurrentSelectable = null;

					var es = aGuiManager.EventSystem;
					if(es != null)
						es.SetSelectedGameObject(null);
				}

				if(m_currentSelectable == value) return;

				if(value == null) {
					TrySetNull();

					return;
				}

				if(ChildSelectableList == null || ChildSelectableList.Count == 0) {
					TrySetNull();

					return;
				}

				var index = IndexOfChildSelectables(value);

				if(index == -1) {
					TrySetNull();

					return;
				}

				base.CurrentSelectable = value;

				var es = aGuiManager.EventSystem;

				if(es != null && es.currentSelectedGameObject != value.gameObject) {
					value.Select();
				}
			}
		}
		#endregion

		#region Protected Method
		/// <summary>子要素のSelectableの選択イベントを監視する</summary>
		protected override void ObserveSelectables() {
			base.ObserveSelectables();

			Observable.EveryUpdate()
				.Subscribe(_ => {
					if(!m_disallowNullSelection) return;
					if(!aContainerManager.IsHighestPriorityDisallowNullSelectionContainer(this)) return;
					if(this == null || !gameObject.activeInHierarchy) return;

					var es = aGuiManager.EventSystem;

					// EventSystemのCurrentObjectがNullでなければ処理しない
					if(es == null) return;
					if(es.currentSelectedGameObject != null) return;

					if(LastSelected != null && LastSelected.IsActive() && LastSelected.IsInteractable()) {
						// 最後に選択していたアイテムを選択
						LastSelected.Select();

					} else if(InitialSelectable != null && InitialSelectable.IsActive() && InitialSelectable.IsInteractable()) {
						// 初期選択に設定されているオブジェクトを選択
						InitialSelectable.Select();

					} else if(ChildSelectableList != null && ChildSelectableList.Count > 0) {
						// 選択中のアイテムが選択不能だった場合、最初のアイテムを選択する
						var first = ChildSelectableList.FirstOrDefault(s => s.IsActive() && s.IsInteractable());

						if(first != null && first.IsActive() && first.IsInteractable()) {
							first.Select();
						}
					}
				}).AddTo(m_selectDisposables);
		}

		protected override void SetInitialSelection() {
			base.SetInitialSelection();

			if(CurrentSelectable == null && m_disallowNullSelection && ChildSelectableList.Count > 0) {
				CurrentSelectable = ChildSelectableList[0];
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
