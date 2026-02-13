using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>Selectableを子要素として管理するコンテナの基底クラス</summary>
	public abstract class aSelectableContainerBase<T> : aContainerBase where T : Selectable {
		#region Enum
		/// <summary>CurrentSelectableIndexに範囲外の値を設定した時の挙動</summary>
		public enum SelectableIndexMode {
			Nullable, // 範囲外のインデックスをNull扱いにする
			Clamp,    // 範囲外のインデックスを最小/最大に丸める
			Loop      // 範囲外のインデックスを最小/最大でループする
		}
		#endregion

		#region Field
		[Header("Selection")]
		[Tooltip("子要素にあるSelectableのキャッシュ")]
		[SerializeField]
		private List<T> m_childSelectableList = new(16); // 子要素にあるSelectableのキャッシュ
		[Tooltip("表示(Show)時に最初に選択されるSelectable")]
		[SerializeField]
		private T m_initialSelectable; // 表示(Show)時に最初に選択されるSelectable
		[Tooltip("現在選択されているSelectable")]
		[SerializeField]
		protected T m_currentSelectable; // 現在選択されているSelectable
		[Tooltip("ChildSelectableにマウスホバーしたらSelectする")]
		[SerializeField]
		protected bool m_selectOnHover = false; // ChildSelectableにマウスホバーしたらSelectする
		[Tooltip("表示時に、前回の選択状態を復元(Resume)することを優先するかどうか")]
		[SerializeField]
		private bool m_defaultResumeSelectionOnShow = true; // 表示時に、前回の選択状態を復元(Resume)することを優先するかどうか
		[Tooltip("CurrentSelectableIndexに範囲外の値を設定した時の挙動")]
		[SerializeField]
		private SelectableIndexMode m_indexMode = SelectableIndexMode.Loop; // CurrentSelectableIndexに範囲外の値を設定した時の挙動

		[Header("Guard")]
		[Tooltip("表示直後の操作をブロックするか？")]
		[SerializeField]
		private bool m_initialGuard = true; // 表示直後の操作をブロックするか？
		[Tooltip("初期ガードする時間(秒)")]
		[SerializeField]
		private float m_initialGuardDuration = 0.05f; // 初期ガードする時間(秒)

		[Tooltip("選択状態が変更された時のイベント")]
		[SerializeField]
		private UnityEvent<T> m_onSelectChanged = new(); // 選択状態が変更された時のイベント

		protected int m_currentSelectableIndex = -1;                        // 現在選択されているSelectableのインデックス
		protected T m_lastSelected;                                         // 非表示時に記録した、最後に選択されていたSelectable
		protected readonly CompositeDisposable m_selectDisposables = new(); // 選択監視用
		#endregion

		#region Property
		/// <summary>CanvasGroupのinteractableへのアクセサ</summary>
		public bool Interactable {
			get => CanvasGroup.interactable;
			set {
				if(CanvasGroup == null) return;
				if(CanvasGroup.interactable == value) return;

				CanvasGroup.interactable = value;
			}
		}

		/// <summary>子要素のSelectableコレクション（内部用）</summary>
		protected virtual IReadOnlyList<T> ChildSelectableList => m_childSelectableList;

		/// <summary> 子要素にあるSelectableのキャッシュ </summary>
		public IEnumerable<T> ChildSelectables => m_childSelectableList;

		/// <summary> 非表示時に記録した、最後に選択されていたSelectable </summary>
		public T LastSelected => m_lastSelected;

		/// <summary>現在選択されているSelectableのインデックス</summary>
		public virtual int CurrentSelectableIndex {
			get => m_currentSelectableIndex;
			set {
				if(!TryGetSelectableIndex(value, out var normalizedIndex)) {
					m_currentSelectableIndex = -1;
					m_currentSelectable = null;
					CaptureCurrentSelection();
					return;
				}

				m_currentSelectableIndex = normalizedIndex;
				m_currentSelectable = m_childSelectableList[normalizedIndex];

				CaptureCurrentSelection();
			}
		}

		/// <summary>現在選択されているSelectable</summary>
		public virtual T CurrentSelectable {
			get => m_currentSelectable;
			set {
				if(m_currentSelectable == value) return;

				if(value == null) {
					m_currentSelectableIndex = -1;
					m_currentSelectable = null;
					m_onSelectChanged?.Invoke(null);
				} else {
					UpdateCurrentSelectableIndex(value);
					m_currentSelectable = value;
					m_onSelectChanged?.Invoke(value);
				}

				CaptureCurrentSelection();
			}
		}

		/// <summary>ChildSelectableにマウスホバーしたらSelectするかどうか</summary>
		public bool SelectOnHover {
			get => m_selectOnHover;
			set => m_selectOnHover = value;
		}

		/// <summary>CurrentSelectableIndexに範囲外の値を設定した時の挙動</summary>
		public SelectableIndexMode IndexMode {
			get => m_indexMode;
			set => m_indexMode = value;
		}

		/// <summary>Show時、デフォルトで選択するSelectable</summary>
		public T InitialSelectable {
			get => m_initialSelectable;
			set => m_initialSelectable = value;
		}

		/// <summary>選択対象が変更された時のイベント</summary>
		public UnityEvent<T> OnSelectChanged => m_onSelectChanged;
		#endregion

	    #region Unity Event
		/// <summary>開始時の処理。初期表示状態の反映を完了させる</summary>
		protected virtual void Start() {
			// 初期状態で表示中の場合、選択状態の復元を試みる
			if(m_isVisible) {
				SetInitialSelection();
			}
		}

		/// <summary>破棄時の処理。選択監視用のDisposableを破棄する</summary>
		protected override void OnDestroy() {
			base.OnDestroy();

			m_selectDisposables.Dispose();
		}
		#endregion

		#region Public Method
		/// <summary>コンテナの初期状態を設定する。子要素のSelectableをキャッシュし、監視を開始する</summary>
		public override void Initialize() {
			if(m_initialized) return;

			if(ChildSelectableList == null || ChildSelectableList.Count == 0) {
				// 子要素の更新とイベントの監視開始
				RefreshChildSelectables();
			} else {
				// 選択イベントの監視開始
				ObserveSelectables();
			}

			base.Initialize();
		}

		/// <summary>子要素のSelectableを検索してキャッシュする</summary>
		public void RefreshChildSelectables() {
			m_childSelectableList ??= new List<T>();
			GetComponentsInChildren(false, m_childSelectableList);

			ObserveSelectables(); // キャッシュ更新時に監視も更新
		}

		/// <summary>子要素のSelectableを手動で設定する</summary>
		public void SetChildSelectableList(List<T> selectables) {
			m_childSelectableList = selectables ?? new List<T>();
			ObserveSelectables();

			if(m_currentSelectable == null) {
				m_currentSelectableIndex = -1;
				return;
			}

			if(m_childSelectableList.Count == 0 || !m_childSelectableList.Contains(m_currentSelectable)) {
				CurrentSelectable = null;
			} else {
				UpdateCurrentSelectableIndex(m_currentSelectable);
			}
		}

		/// <summary>子要素のSelectableを1つ追加する</summary>
		public void AddChildSelectable(T selectable) {
			if(selectable == null) return;

			m_childSelectableList ??= new List<T>();
			m_childSelectableList.Add(selectable);
			ObserveSelectables();

			if(m_currentSelectable == null) {
				m_currentSelectableIndex = -1;
				return;
			}

			if(m_childSelectableList.Count == 0 || !m_childSelectableList.Contains(m_currentSelectable)) {
				CurrentSelectable = null;
			} else {
				UpdateCurrentSelectableIndex(m_currentSelectable);
			}
		}

		/// <summary>子要素のSelectableリストをクリアする</summary>
		public void ClearChildSelectables() {
			m_childSelectableList ??= new List<T>();
			m_childSelectableList.Clear();
			ObserveSelectables();

			if(m_currentSelectable == null) {
				m_currentSelectableIndex = -1;
				return;
			}

			CurrentSelectable = null;
		}

		/// <summary> 引数の要素で子要素のSelectableリストのインデックスを取得する </summary>
		/// <param name="selectable">対称の要素</param>
		/// <returns>要素が含まれるインデックス</returns>
		public int IndexOfChildSelectables(T selectable)
			=> m_childSelectableList.IndexOf(selectable);

		/// <summary>次のSelectableを選択する。末尾の場合は何もしない</summary>
		public void SelectNext() {
			var nextIndex = CurrentSelectableIndex + 1;

			if(nextIndex < m_childSelectableList.Count) {
				CurrentSelectableIndex = nextIndex;
			}
		}

		/// <summary>次のSelectableを選択する。末尾の場合は先頭に戻る</summary>
		public void SelectNextLoop() {
			var nextIndex = CurrentSelectableIndex + 1;

			if(nextIndex < m_childSelectableList.Count) {
				CurrentSelectableIndex = nextIndex;
			} else {
				CurrentSelectableIndex = 0;
			}
		}

		/// <summary>前のSelectableを選択する。先頭の場合は何もしない</summary>
		public void SelectPrevious() {
			var previousIndex = CurrentSelectableIndex - 1;

			if(previousIndex >= 0) {
				CurrentSelectableIndex = previousIndex;
			}
		}

		/// <summary>前のSelectableを選択する。先頭の場合は末尾に戻る</summary>
		public void SelectPreviousLoop() {
			var previousIndex = CurrentSelectableIndex - 1;

			if(previousIndex >= 0) {
				CurrentSelectableIndex = previousIndex;
			} else {
				CurrentSelectableIndex = m_childSelectableList.Count - 1;
			}
		}
		#endregion

		#region Protected Method
		/// <summary>表示処理の実装。InitialGuard機能と初期選択の設定を行う</summary>
		protected override void ShowInternal() {
			base.ShowInternal();

			// InitialGuard機能の実行
			if(m_initialGuard && m_initialGuardDuration > 0) {
				CanvasGroup.blocksRaycasts = false;
				Observable.Timer(TimeSpan.FromSeconds(m_initialGuardDuration))
					.TakeUntilDestroy(this)
					.Subscribe(_ => {
						if(this != null && CanvasGroup != null) {
							CanvasGroup.blocksRaycasts = true;
						}
					});
			}

			// 初期化中（Awake）の場合は Start で実行するためここではスキップ
			if(m_initialized && !m_suppressAnimation) {
				SetInitialSelection();
			}
		}

		/// <summary>非表示処理の実装。現在の選択状態を保存する</summary>
		protected override void HideInternal() {
			base.HideInternal();

			//選択状態を保存
			CaptureCurrentSelection();

			//Disableでもイベントシステムのオブジェクトとして選択され続けるのを防ぐ
			var es = aGuiManager.EventSystem;

			if(ChildSelectableList.Any(selectable => es.currentSelectedGameObject == selectable.gameObject)) {
				es.SetSelectedGameObject(null);
			}
		}

		/// <summary>子要素のSelectableの選択イベントを監視する</summary>
		protected virtual void ObserveSelectables() {
			m_selectDisposables.Clear();
			if(ChildSelectableList == null || ChildSelectableList.Count == 0) return;

			foreach (var selectable in ChildSelectableList) {
				if(selectable == null) continue;

				selectable
					.OnPointerEnterAsObservable()
					.Subscribe(_ => {
						if(!m_selectOnHover) return;
						if(!selectable.IsActive() || !selectable.IsInteractable()) return;

						CurrentSelectable = selectable;
					})
					.AddTo(m_selectDisposables);

				// 選択された時の処理
				selectable.OnSelectAsObservable()
					.Subscribe(_ => CurrentSelectable = selectable)
					.AddTo(m_selectDisposables);
			}
		}

		/// <summary>現在フォーカスされているSelectableを記録する</summary>
		protected virtual void CaptureCurrentSelection() {
			if(m_currentSelectable != null) {
				m_lastSelected = m_currentSelectable;
			}
		}

		/// <summary>現在選択されているSelectableのインデックスを更新する</summary>
		protected virtual void UpdateCurrentSelectableIndex(T selectable) {
			m_currentSelectableIndex = selectable == null ? -1 : m_childSelectableList.IndexOf(selectable);
		}

		/// <summary>IndexModeに基づきSelectableのインデックスを正規化する</summary>
		protected bool TryGetSelectableIndex(int value, out int normalizedIndex) {
			normalizedIndex = -1;
			if(ChildSelectableList == null || ChildSelectableList.Count == 0) return false;

			var count = ChildSelectableList.Count;

			switch(m_indexMode) {
				case SelectableIndexMode.Clamp:
					normalizedIndex = Mathf.Clamp(value, 0, count - 1);
					return true;
				case SelectableIndexMode.Loop:
					var loopIndex = value % count;
					if(loopIndex < 0) loopIndex += count;
					normalizedIndex = loopIndex;
					return true;
				case SelectableIndexMode.Nullable:
				default:
					if(value < 0 || value >= count) return false;
					normalizedIndex = value;
					return true;
			}
		}

		/// <summary>初期設定のSelectable、または最後に選択されていたSelectableにフォーカスを戻す</summary>
		protected virtual void SetInitialSelection() {
			// リジューム設定が有効で、前回選択があった場合はそれを優先
			if(m_defaultResumeSelectionOnShow && m_lastSelected != null && m_lastSelected.IsActive() && m_lastSelected.IsInteractable()) {
				CurrentSelectable = m_lastSelected;
			} else if(m_initialSelectable != null) {
				CurrentSelectable = m_initialSelectable;
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

			if(m_childSelectableList == null || m_childSelectableList.Count == 0) {
				m_childSelectableList ??= new List<T>();
				GetComponentsInChildren(false, m_childSelectableList);
			}
		}

		/// <summary>Inspectorでの値変更時の処理</summary>
		protected override void OnValidate() {
			base.OnValidate();

			if(Application.isPlaying) return;

			if(m_childSelectableList == null || m_childSelectableList.Count == 0) {
				m_childSelectableList ??= new List<T>();
				GetComponentsInChildren(false, m_childSelectableList);
			}
		}
#endif
	}
}
