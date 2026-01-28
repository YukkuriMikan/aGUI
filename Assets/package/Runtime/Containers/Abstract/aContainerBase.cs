using System;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace ANest.UI {
	/// <summary>UIコンテナの基底クラス</summary>
	[RequireComponent(typeof(CanvasGroup))][RequireComponent(typeof(aGuiInfo))]
	public abstract class aContainerBase : MonoBehaviour {
		#region Field
		[Header("Animation")]
		[Tooltip("個別のアニメーション設定を使用するかどうか")]
		[SerializeField]
		protected bool m_useCustomAnimations = true; // 個別のアニメーション設定を使用するかどうか
		[Tooltip("共通のアニメーションセットを使用するかどうか")]
		[SerializeField]
		protected bool m_useSharedAnimation = false; // 共通のアニメーションセットを使用するかどうか
		[Tooltip("共通のアニメーション設定")]
		[SerializeField]
		protected UiAnimationSet m_sharedAnimation; // 共通のアニメーション設定
		[Tooltip("表示(Show)時に再生するアニメーション")]
		[SerializeReference, SerializeReferenceDropdown]
		private IUiAnimation[] m_showAnimations; // 表示(Show)時に再生するアニメーション
		[Tooltip("非表示(Hide)時に再生するアニメーション")]
		[SerializeReference, SerializeReferenceDropdown]
		private IUiAnimation[] m_hideAnimations; // 非表示(Hide)時に再生するアニメーション

		[Header("State")]
		[Tooltip("現在表示中（表示プロセス中を含む）かどうか")]
		[SerializeField]
		protected bool m_isVisible; // 現在表示中（表示プロセス中を含む）かどうか

		[Header("Reference")]
		[Tooltip("キャンバスグループの参照")]
		[SerializeField]
		private CanvasGroup m_canvasGroup; // キャンバスグループの参照
		[Tooltip("GUI情報の参照")]
		[SerializeField]
		private aGuiInfo m_guiInfo; // GUI情報の参照

		[Header("Event")]
		[Tooltip("表示(Show)完了時のイベント")]
		[SerializeField]
		private UnityEvent m_onShow = new(); // 表示(Show)完了時のイベント
		[FormerlySerializedAs("m_hideEvent")]
		[Tooltip("非表示(Hide)完了時のイベント")]
		[SerializeField]
		private UnityEvent m_onHide = new(); // 非表示(Hide)完了時のイベント

		protected RectTransform m_rectTransform;                   // 自身のRectTransformのキャッシュ
		protected bool m_initialized;                              // 初期化が完了しているかどうか
		private bool m_isQuitting;                                 // アプリケーションが終了処理中かどうか
		protected bool m_suppressAnimation;                        // 内部処理用：一時的にアニメーション再生を抑制するフラグ
		private bool m_suppressActiveWarning;                      // 内部処理用：SetActive実行時の警告を一時的に抑制するフラグ
		private bool m_nowShowing;                                 // 現在表示処理中かどうか
		private bool m_nowHiding;                                  // 現在非表示処理中かどうか
		private readonly Subject<Unit> m_showStartSubject = new(); // 表示開始通知用
		private readonly Subject<Unit> m_showEndSubject = new();   // 表示完了通知用
		private readonly Subject<Unit> m_hideStartSubject = new(); // 非表示開始通知用
		private readonly Subject<Unit> m_hideEndSubject = new();   // 非表示完了通知用
		#endregion

		#region Property
		/// <summary>コンテナに紐付くCanvasGroup</summary>
		public CanvasGroup CanvasGroup => m_canvasGroup;

		/// <summary>表示状態（get: 現在の状態 / set: 状態に応じたShow/Hideの実行）</summary>
		public bool IsVisible {
			get => m_isVisible;
			set {
				if(m_isVisible == value) return;
				if(value) {
					Show();
				} else {
					Hide();
				}
			}
		}

		/// <summary>カスタムアニメーションを使用するかどうか</summary>
		public bool UseCustomAnimations {
			get => m_useCustomAnimations;
			set => m_useCustomAnimations = value;
		}

		/// <summary>表示時に再生するアニメーション配列</summary>
		private IUiAnimation[] ShowAnimations => m_showAnimations;

		/// <summary>非表示時に再生するアニメーション配列</summary>
		private IUiAnimation[] HideAnimations => m_hideAnimations;

		/// <summary>表示完了時のイベント</summary>
		public UnityEvent OnShow => m_onShow;

		/// <summary>非表示完了時のイベント</summary>
		public UnityEvent OnHide => m_onHide;

		/// <summary>表示開始時の通知用Observable</summary>
		public IObservable<Unit> ShowStartObservable => m_showStartSubject;

		/// <summary>表示完了時の通知用Observable</summary>
		public IObservable<Unit> ShowEndObservable => m_showEndSubject;

		/// <summary>非表示開始時の通知用Observable</summary>
		public IObservable<Unit> HideStartObservable => m_hideStartSubject;

		/// <summary>非表示完了時の通知用Observable</summary>
		public IObservable<Unit> HideEndObservable => m_hideEndSubject;

		/// <summary>自身のRectTransform</summary>
		public RectTransform RectTransform => m_rectTransform;
		#endregion

		#region Unity Event
		/// <summary>参照のキャッシュと初期化を行う</summary>
		protected virtual void Awake() {
			Initialize();
		}

		/// <summary>有効化時の処理。意図しないSetActiveへの警告チェックを含む</summary>
		protected virtual void OnEnable() {
			if(!m_suppressActiveWarning) {
				WarnActiveChange();
			}

			m_suppressActiveWarning = false;
		}

		/// <summary>無効化時の処理。意図しないSetActiveへの警告チェックを含む</summary>
		protected virtual void OnDisable() {
			if(!m_suppressActiveWarning) {
				WarnActiveChange();
			}

			m_suppressActiveWarning = false;
		}

		/// <summary>アプリ終了時のフラグを立て、終了時の警告を抑制する</summary>
		protected virtual void OnApplicationQuit() {
			m_isQuitting = true;
		}

		/// <summary>破棄時の処理</summary>
		protected virtual void OnDestroy() {
			aContainerManager.Remove(this);
			m_showStartSubject.OnCompleted();
			m_showEndSubject.OnCompleted();
			m_hideStartSubject.OnCompleted();
			m_hideEndSubject.OnCompleted();
		}
		#endregion

		#region Public Method
		/// <summary>コンテナを表示する</summary>
		public virtual void Show() {
			if(m_nowShowing) return;

			ShowInternal();
		}

		/// <summary>コンテナを非表示にする</summary>
		public virtual void Hide() {
			if(m_nowHiding) return;

			HideInternal();
		}
		#endregion

		#region Protected Method
		/// <summary>コンテナの初期状態を設定する。二重初期化は防止される</summary>
		protected virtual void Initialize() {
			if(m_initialized) return;

			// 必要な参照の取得
			if(m_canvasGroup == null) m_canvasGroup = GetComponent<CanvasGroup>();
			if(m_guiInfo == null) m_guiInfo = GetComponent<aGuiInfo>();
			if(m_rectTransform == null) m_rectTransform = GetComponent<RectTransform>();

			// 管理対象に登録
			aContainerManager.Add(this);

			// 初期化時はアニメーションなしで状態を適用
			m_suppressAnimation = true;
			// 初回の状態適用による警告を抑制
			m_suppressActiveWarning = true;

			if(m_isVisible) {
				ShowInternal();
			} else {
				HideInternal();
			}

			m_suppressAnimation = false;
			m_suppressActiveWarning = false;

			m_initialized = true;
		}

		/// <summary>表示処理の実装</summary>
		protected virtual void ShowInternal() {
			m_nowShowing = true;
			m_canvasGroup.blocksRaycasts = true;

			aContainerManager.Add(this);

#if UNITY_EDITOR
			if(m_debugMode) {
				var stackTrace = new System.Diagnostics.StackTrace();
				var stackFrame = stackTrace.GetFrame(1);
				Debug.Log($"{this.name} Show: Call from {stackFrame?.GetFileName()}.{stackFrame?.GetMethod().Name}", this);
			}
#endif

			// 1. 状態の更新とGameObjectの有効化
			UpdateStateForShow();
			SetActiveInternal(true);
			m_onShow?.Invoke();

			// 2. アニメーションの再生（完了を待たずに選択復帰へ進む）
			TryPlayAnimations(ShowAnimations, () => {
				m_nowShowing = false;
				m_showEndSubject.OnNext(Unit.Default);
			}, () => {
				m_nowShowing = false;
			});

			m_suppressActiveWarning = false;
			m_showStartSubject.OnNext(Unit.Default);
		}

		/// <summary>非表示処理の実装</summary>
		protected virtual void HideInternal() {
			m_nowHiding = true;
			m_canvasGroup.blocksRaycasts = false;

#if UNITY_EDITOR
			if(m_debugMode) {
				var stackTrace = new System.Diagnostics.StackTrace();
				var stackFrame = stackTrace.GetFrame(1);
				Debug.Log($"{this.name} Hide: Call from {stackFrame?.GetFileName()}.{stackFrame?.GetMethod().Name}", this);
			}
#endif

			// 2. 状態の更新
			UpdateStateForHide();
			m_onHide?.Invoke();

			// 3. アニメーションの再生。完了時にGameObjectを非アクティブにする
			TryPlayAnimations(HideAnimations,
				() => {
					// アニメーション完了時のみGameObjectを非表示にする
					m_nowHiding = false;
					SetActiveInternal(false);
				},
				() => {
					// アニメーションが中断（Kill）された場合は、後続のShow処理を優先するため非表示化は行わない
					m_nowHiding = false;
				});

			m_suppressActiveWarning = false;
		}
		#endregion

		#region Private Method
		/// <summary>SetActive時の警告を回避しつつ、GameObjectの活性状態を切り替える</summary>
		private void SetActiveInternal(bool active) {
			if(gameObject.activeSelf == active) return;

			m_suppressActiveWarning = true;
			gameObject.SetActive(active);
			m_suppressActiveWarning = false;
		}

		/// <summary>アニメーションの再生を試行する。抑制フラグや設定がない場合は即座にコールバックを呼ぶ</summary>
		private void TryPlayAnimations(IUiAnimation[] animations, Action completeCallback = null, Action killCallback = null) {
			if(m_suppressAnimation || (!m_useCustomAnimations && !m_useSharedAnimation)) {
				completeCallback?.Invoke();
				return;
			}

			aGuiUtils.PlayAnimation(animations, m_guiInfo.RectTransform, m_guiInfo.TargetGraphic, m_guiInfo.OriginalRectTransformValues,
				() => completeCallback?.Invoke(),
				() => killCallback?.Invoke());
		}

		/// <summary>表示状態の内部フラグを更新する</summary>
		protected virtual void UpdateStateForShow() {
			m_isVisible = true;
		}

		/// <summary>非表示状態の内部フラグを更新する</summary>
		protected virtual void UpdateStateForHide() {
			m_isVisible = false;
		}

		/// <summary>直接GameObject.SetActiveが呼ばれた場合に警告を出力する</summary>
		private void WarnActiveChange() {
			if(m_isQuitting) return;
			Debug.LogWarning($"[{nameof(aContainerBase)}] {name} の gameObject.SetActive が直接変更されました。Show/Hide または表示フラグを使用してください。", this);
		}
		#endregion

		#region Unity Editor
#if UNITY_EDITOR
		[Tooltip("デバッグログの出力フラグ")]
		[SerializeField]
		protected bool m_debugMode = false; // デバッグログの出力フラグ

		/// <summary>コンポーネント追加・リセット時の処理</summary>
		protected virtual void Reset() {
			if(Application.isPlaying) return;
			AutoRenameInEditor();
			CacheReferences();
			ApplySharedAnimationsFromSet();
		}

		/// <summary>Inspectorでの値変更時の処理</summary>
		protected virtual void OnValidate() {
			if(Application.isPlaying) return;
			CacheReferences();
			ApplySharedAnimationsFromSet();
		}

		/// <summary>コンテナ名の接頭辞</summary>
		protected virtual string ContainerNamePrefix => "Container - ";

		/// <summary>既知の接頭辞を削除した名前を取得する</summary>
		protected virtual string TrimKnownPrefixes(string currentName) {
			string prefix = ContainerNamePrefix;
			if(string.IsNullOrEmpty(prefix)) return currentName;
			return currentName.StartsWith(prefix) ? currentName.Substring(prefix.Length) : currentName;
		}

		/// <summary>GameObject名をルールに従って自動リネームする</summary>
		private void AutoRenameInEditor() {
			string prefix = ContainerNamePrefix;
			if(string.IsNullOrEmpty(prefix)) return;
			string trimmedName = TrimKnownPrefixes(gameObject.name);
			string newName = $"{prefix}{trimmedName}";
			if(gameObject.name != newName) {
				gameObject.name = newName;
			}
		}

		/// <summary>必要なコンポーネント参照をキャッシュする</summary>
		private void CacheReferences() {
			if(m_rectTransform == null) {
				m_rectTransform = transform as RectTransform;
			}

			if(m_canvasGroup == null) {
				m_canvasGroup = GetComponent<CanvasGroup>();
			}

			if(m_guiInfo == null) {
				m_guiInfo = GetComponent<aGuiInfo>();
			}
		}

		/// <summary>共有アニメーションセットからアニメーションを複製して適用する</summary>
		private void ApplySharedAnimationsFromSet() {
			if(!m_useSharedAnimation) return;
			if(m_sharedAnimation == null) return;
			m_showAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.showAnimations);
			m_hideAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.hideAnimations);
		}
#endif
		#endregion
	}
}
