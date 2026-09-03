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

				// 範囲内の場合は選択を実行（EventSystem側の選択と同期させる）
				var selectable = ChildSelectableList[normalizedIndex];
				if(!aGuiSelectableUtils.CanReceiveFocus(selectable)) return;

				var currentSelectedObject = selectable.gameObject;

				if(es != null && es.currentSelectedGameObject != currentSelectedObject) {
					selectable.Select();
				}

				base.CurrentSelectableIndex = normalizedIndex;
			}
		}

		/// <summary>現在選択されているSelectable</summary>
		public override T CurrentSelectable {
			get => m_currentSelectable;
			set {
				// Null許可時のみEventSystem側の選択をクリアする
				void TrySetNull() {
					if(m_disallowNullSelection) return;

					base.CurrentSelectable = null;

					var es = aGuiManager.EventSystem;
					if(es != null)
						es.SetSelectedGameObject(null);
				}

				if(value == null) {
					TrySetNull();

					return;
				}

				if(!aGuiSelectableUtils.CanReceiveFocus(value)) return;

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
		protected override void SetEvents() {
			base.SetEvents();

			Observable.EveryUpdate()
				.Subscribe(_ => {
					// 破棄済みチェックを最初に行ってから他の判定に進む
					if(this == null || !gameObject.activeInHierarchy) return;
					if(!m_disallowNullSelection) return;
					if(!aContainerManager.IsHighestPriorityDisallowNullSelectionContainer(this)) return;

					var es = aGuiManager.EventSystem;

					// EventSystemの選択が空の時だけ復帰処理を行う
					if(es == null) return;
					if(es.currentSelectedGameObject != null) return;

					if(LastSelected != null && LastSelected.IsActive() && LastSelected.IsInteractable() && aGuiSelectableUtils.CanReceiveFocus(LastSelected)) {
						// 直近の選択を優先して復帰する
						LastSelected.Select();

					} else if(InitialSelectable != null && InitialSelectable.IsActive() && InitialSelectable.IsInteractable() && aGuiSelectableUtils.CanReceiveFocus(InitialSelectable)) {
						// 初期選択に設定された要素があれば採用する
						InitialSelectable.Select();

					} else if(ChildSelectableList != null && ChildSelectableList.Count > 0) {
						// それ以外は最初に選択可能な要素を選択する
						var first = ChildSelectableList.FirstOrDefault(s => s.IsActive() && s.IsInteractable() && aGuiSelectableUtils.CanReceiveFocus(s));

						if(first != null && first.IsActive() && first.IsInteractable()) {
							first.Select();
						}
					}
				}).AddTo(m_eventDisposables);
		}

		/// <summary>表示直後に最低1つは選択状態にする</summary>
		protected override void SetInitialSelection() {
			base.SetInitialSelection();

			if(CurrentSelectable == null && m_disallowNullSelection && ChildSelectableList.Count > 0) {
				CurrentSelectable = ChildSelectableList.FirstOrDefault(aGuiSelectableUtils.CanReceiveFocus);
			}
		}

		/// <summary>表示状態の内部フラグを更新する</summary>
		protected override void UpdateStateForShow() {
			base.UpdateStateForShow();
			Interactable = true;
		}
		#endregion
	}
}
