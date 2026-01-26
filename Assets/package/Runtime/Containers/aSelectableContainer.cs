using System;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace ANest.UI {
	/// <summary>状態遷移の特別な挙動を持たないシンプルなコンテナ</summary>
	public class aSelectableContainer : aContainerBase {
		#region Field
		[Header("Selection")]
		[Tooltip("子要素にあるSelectableのキャッシュ")]
		[SerializeField]
		private List<Selectable> m_childSelectableList = new(16); // 子要素にあるSelectableのキャッシュ
		[Tooltip("表示(Show)時に最初に選択されるSelectable")]
		[SerializeField]
		private Selectable m_initialSelectable; // 表示(Show)時に最初に選択されるSelectable
		[Tooltip("現在選択されているSelectable")]
		[SerializeField]
		private Selectable m_currentSelectable; // 現在選択されているSelectable
		[Tooltip("表示時に、前回の選択状態を復元(Resume)することを優先するかどうか")]
		[SerializeField]
		private bool m_defaultResumeSelectionOnShow = true; // 表示時に、前回の選択状態を復元(Resume)することを優先するかどうか
		[Tooltip("CurrentSelectableがNullになる事を許可しない")]
		[SerializeField]
		private bool m_disallowNullSelection = true; // CurrentSelectableがNullになる事を許可しない

		[Header("Guard")]
		[Tooltip("表示直後の操作をブロックするか？")]
		[SerializeField]
		private bool m_initialGuard = true; // 表示直後の操作をブロックするか？
		[Tooltip("初期ガードする時間(秒)")]
		[SerializeField]
		private float m_initialGuardDuration = 0.05f; // 初期ガードする時間(秒)

		[Header("Event")]
		[Tooltip("選択状態が変更された時のイベント")]
		[SerializeField]
		private UnityEvent<Selectable> m_onSelectChanged = new(); // 選択状態が変更された時のイベント

		/// <summary>CanvasGroupのinteractableへのアクセサ</summary>
		public bool Interactable {
			get => CanvasGroup.interactable;
			set {
				if(CanvasGroup == null) return;
				if(CanvasGroup.interactable == value) return;
				CanvasGroup.interactable = value;
			}
		}

		private Selectable m_lastSelected;                                // 非表示時に記録した、最後に選択されていたSelectable
		private readonly CompositeDisposable m_selectDisposables = new(); // 選択監視用
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

		#region Property
		/// <summary>子要素のSelectableコレクション（内部用）</summary>
		protected virtual ICollection<Selectable> ChildSelectableCollection => m_childSelectableList;
		
		/// <summary> 子要素にあるSelectableのキャッシュ </summary>
		public IEnumerable<Selectable> ChildSelectables => m_childSelectableList;

		/// <summary>CurrentSelectableがNullになる事を許可しないかどうか</summary>
		public bool DisallowNullSelection {
			get => m_disallowNullSelection;
			set => m_disallowNullSelection = value;
		}

		/// <summary>現在選択されているSelectable</summary>
		public virtual Selectable CurrentSelectable => m_currentSelectable;

		/// <summary>Show時、デフォルトで選択するSelectable</summary>
		public Selectable InitialSelectable {
			get => m_initialSelectable;
			set => m_initialSelectable = value;
		}

		/// <summary>選択対象が変更された時のイベント</summary>
		public UnityEvent<Selectable> OnSelectChanged => m_onSelectChanged;
		#endregion

		#region Public Method
		/// <summary>子要素のSelectableを検索してキャッシュする</summary>
		public virtual void RefreshChildSelectables() {
			m_childSelectableList ??= new List<Selectable>();
			GetComponentsInChildren(false, m_childSelectableList);
			ObserveSelectables(); // キャッシュ更新時に監視も更新
		}
		#endregion

 	#region Protected Method
		/// <summary>コンテナの初期状態を設定する。子要素のSelectableをキャッシュし、監視を開始する</summary>
		protected override void Initialize() {
			if(m_initialized) return;

			if(ChildSelectableCollection == null || ChildSelectableCollection.Count == 0) {
				// 子要素の更新とイベントの監視開始
				RefreshChildSelectables();
			} else {
				// 選択イベントの監視開始
				ObserveSelectables();
			}

			base.Initialize();
		}

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

			// 1. 現在の選択状態を保存
			CaptureCurrentSelection();
		}

		/// <summary>子要素のSelectableの選択イベントを監視する</summary>
		protected void ObserveSelectables() {
			m_selectDisposables.Clear();
			if(ChildSelectableCollection == null || ChildSelectableCollection.Count == 0) return;

			foreach (var selectable in ChildSelectableCollection) {
				if(selectable == null) continue;

				// 選択された時の処理
				selectable.OnSelectAsObservable()
					.Subscribe(_ => {
						m_currentSelectable = selectable;
						m_onSelectChanged?.Invoke(selectable);
					})
					.AddTo(m_selectDisposables);

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

							if(aGuiManager.EventSystem != null && aGuiManager.EventSystem.currentSelectedGameObject == null) {
								if(m_currentSelectable != null) {
									m_currentSelectable.Select();
								}
							}
						}).AddTo(m_selectDisposables);
					})
					.AddTo(m_selectDisposables);
			}
		}

		/// <summary>現在フォーカスされているSelectableがこのコンテナ内であれば記録する</summary>
		private void CaptureCurrentSelection() {
			var es = aGuiManager.EventSystem;
			if(es == null) return;
			var selected = es.currentSelectedGameObject;
			if(selected == null) {
				m_currentSelectable = null;
				return;
			}

			// 自身の子要素である場合のみ記録
			if(!selected.transform.IsChildOf(transform)) {
				m_currentSelectable = null;
				return;
			}

			m_lastSelected = selected.GetComponent<Selectable>();
			m_currentSelectable = m_lastSelected;
		}

		/// <summary>初期設定のSelectable、または最後に選択されていたSelectableにフォーカスを戻す</summary>
		protected virtual void SetInitialSelection() {
			var es = aGuiManager.EventSystem;
			if(es == null) return;

			Selectable target = null;
			// リジューム設定が有効で、前回選択があった場合はそれを優先
			if(m_defaultResumeSelectionOnShow && m_lastSelected != null && m_lastSelected.IsActive() && m_lastSelected.IsInteractable()) {
				target = m_lastSelected;
			}

			// 候補がない場合は初期選択ターゲットを使用
			if(target == null) {
				target = m_initialSelectable;
			}

			if(target == null) return;
			if(!target.IsActive() || !target.IsInteractable()) return;

			es.SetSelectedGameObject(target.gameObject);
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
