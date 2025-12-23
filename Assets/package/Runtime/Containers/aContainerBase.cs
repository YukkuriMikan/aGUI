using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary> UIコンテナの基底クラス </summary>
	[RequireComponent(typeof(CanvasGroup))]
	public abstract class aContainerBase : MonoBehaviour {
		/// <summary> コンテナーの表示履歴 </summary>
		public static List<aContainerBase> ContainerHistory { get; } = new(8);

		#region SerializeField
		[Header("Animation")]
		[SerializeField] protected bool m_useCustomAnimations = true;
		[SerializeField] protected bool m_useSharedAnimation = false;
		[SerializeField] protected UiAnimationSet m_sharedAnimation;
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_showAnimations; // Show時に再生するアニメーション
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_hideAnimations; // Hide時に再生するアニメーション

		[Header("Selection")]
		[SerializeField] private Selectable[] m_childSelectables;            // 子Selectableのキャッシュ
		[SerializeField] private Selectable m_initialSelectable;             // Show時に初期選択するSelectable
		[SerializeField] private bool m_defaultResumeSelectionOnShow = true; // Show時にリジュームを優先するか

		[Header("Target")]
		[SerializeField] private Graphic m_targetGraphic;             // アニメーション対象のGraphic
		[SerializeField] private RectTransform m_targetRectTransform; // アニメーション対象のRectTransform
		[SerializeField] private CanvasGroup m_canvasGroup;           // 自身のCanvasGroup

		[Header("State")]
		[SerializeField] protected bool m_isVisible; // 表示中かどうか

		[Header("Layout")]
		[SerializeField] private aLayoutGroupBase m_layoutGroup;

		[Header("Event")]
		[SerializeField] private UnityEvent m_showEvent = new(); // Show完了時イベント
		[SerializeField] private UnityEvent m_hideEvent = new(); // Hide完了時イベント

		[SerializeField]
		private RectTransformValues m_originalRectTransformValues; // アニメーション用RectTransform初期値
		#endregion

		#region Fields
		private Selectable m_lastSelected;     // Hide時に記録した選択中のSelectable
		private RectTransform m_rectTransform; // 自身のRectTransformキャッシュ
		private bool m_initialized;            // 初期化済みか
		private bool m_isQuitting;             // アプリ終了フラグ
		protected bool m_suppressAnimation;    // 内部専用アニメーション抑制フラグ
		private bool m_suppressActiveWarning;  // 内部SetActive実行中の警告抑制フラグ
		private CancellationTokenSource m_animationCts; // アニメーションとコールバックのキャンセル用
		#endregion

		#region Properties
		/// <summary> レイアウトグループ </summary>
		public aLayoutGroupBase LayoutGroup => m_layoutGroup;

		/// <summary> コンテナと紐つくCanvasGroup </summary>
		public CanvasGroup CanvasGroup => m_canvasGroup;

		/// <summary> 表示状態 </summary>
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

		public bool UseCustomAnimations {
			get => m_useCustomAnimations;
			set => m_useCustomAnimations = value;
		}

		private IUiAnimation[] ShowAnimations => m_showAnimations;

		private IUiAnimation[] HideAnimations => m_hideAnimations;

		public UnityEvent ShowEvent => m_showEvent;
		public UnityEvent HideEvent => m_hideEvent;
		
		public RectTransform RectTransform => m_rectTransform;

		public RectTransform TargetRectTransform => m_targetRectTransform;

		public Graphic TargetGraphic => m_targetGraphic;
		
		public RectTransformValues OriginalRectTransformValues => m_originalRectTransformValues;
		
		/// <summary> CanvasGroupのinteractableを中継するフラグ </summary>
		public bool Interactable {
			get => CanvasGroup.interactable;
			set {
				if(CanvasGroup.interactable == value) return;

				if(CanvasGroup != null) {
					CanvasGroup.interactable = value;
				}
			}
		}
		#endregion

		#region Unity Methods
		/// <summary> 参照キャッシュを初期化する </summary>
		protected virtual void Awake() {
			Initialize();
		}

		protected virtual void OnEnable() {
			if(!m_suppressActiveWarning) {
				WarnActiveChange();
			}
			m_suppressActiveWarning = false;
		}

		protected virtual void OnDisable() {
			CancelAnimation();
			if(!m_suppressActiveWarning) {
				WarnActiveChange();
			}
			m_suppressActiveWarning = false;
		}

		/// <summary> 初期化時に初期化やシリアライズ状態の適用、直接SetActiveされた場合の警告制御を行う </summary>
		protected virtual void Initialize() {
			if(m_initialized) return;

			m_initialized = true;

			// 初期化は強制的にアニメーション無し
			m_suppressAnimation = true;

			if(m_isVisible) {
				ShowInternal();
			} else {
				HideInternal();
			}

			m_suppressAnimation = false;

			// 最初のOnEnableの警告を防ぐ
			m_suppressActiveWarning = true;
		}

		/// <summary> アプリ終了時に終了フラグを立て、以降の警告を抑制する </summary>
		protected virtual void OnApplicationQuit() {
			m_isQuitting = true;
			CancelAnimation();
		}

		protected virtual void OnDestroy() {
			CancelAnimation();
		}

		private void CancelAnimation() {
			if (m_animationCts != null) {
				m_animationCts.Cancel();
				m_animationCts.Dispose();
				m_animationCts = null;
			}
		}
		#endregion

		#region Public Methods
		/// <summary> 非同期Show。必要に応じて選択をリジュームする </summary>
		public virtual void Show() {
			if(m_isVisible) return;
			ShowInternal();
		}

		/// <summary> 非同期Hide </summary>
		public virtual void Hide() {
			if(!m_isVisible) return;
			HideInternal();
		}
		#endregion

		#region Internal Methods
		/// <summary> Showの実処理 </summary>
		protected virtual void ShowInternal() {
#if UNITY_EDITOR
			if(m_debugMode) {
				var stackTrace = new System.Diagnostics.StackTrace();
				var stackFrame = stackTrace.GetFrame(1);
				Debug.Log($"{this.name} Show: Call from {stackFrame?.GetFileName()}.{stackFrame?.GetMethod().Name}", this);
			}
#endif

			CancelAnimation();
			m_animationCts = new CancellationTokenSource();
			var token = m_animationCts.Token;

			// 表示状態とインタラクト状態を更新
			UpdateStateForShow();

			SetActiveInternal(true);

			RegisterShowHistory();


			m_showEvent?.Invoke();

			// アニメーションを開始し、待たずに選択を復帰
			TryPlayAnimations(ShowAnimations);
			ResumeSelection();

			m_suppressActiveWarning = false;
		}

		/// <summary> Hideの実処理 </summary>
		protected virtual void HideInternal() {
#if UNITY_EDITOR
			if(m_debugMode) {
				var stackTrace = new System.Diagnostics.StackTrace();
				var stackFrame = stackTrace.GetFrame(1);
				Debug.Log($"{this.name} Hide: Call from {stackFrame?.GetFileName()}.{stackFrame?.GetMethod().Name}", this);
			}
#endif

			CancelAnimation();
			m_animationCts = new CancellationTokenSource();
			var token = m_animationCts.Token;

			// 現在の選択状態を退避
			CaptureCurrentSelection();

			// 表示/操作を無効化
			UpdateStateForHide();

			// 履歴から直前コンテナへフォーカスを戻す
			FocusPreviousContainerFromHistory();

			m_hideEvent?.Invoke();

			// アニメーションを開始のみして即座に非表示へ
			TryPlayAnimations(HideAnimations,
				() => {
					if (m_isVisible) return;
					SetActiveInternal(false);
				});

			m_suppressActiveWarning = false;
		}
		#endregion

		#region Private Methods
		/// <summary> Show実行時に履歴へ登録し最新のコンテナとして記録する </summary>
		private void RegisterShowHistory() {
			if(!Application.isPlaying) return;

			CleanHistory();
			ContainerHistory.Remove(this);
			ContainerHistory.Add(this);
		}

		/// <summary> Hide時に自身を履歴から除外し、直前の可視コンテナへフォーカスを戻す </summary>
		private void FocusPreviousContainerFromHistory() {
			if(!Application.isPlaying) return;

			CleanHistory();
			ContainerHistory.Remove(this);

			// 履歴を後方から走査し、有効かつ表示中のコンテナを探す
			for (int i = ContainerHistory.Count - 1; i >= 0; i--) {
				var container = ContainerHistory[i];
				if(container == null) {
					ContainerHistory.RemoveAt(i);
					continue;
				}
				if(!container.gameObject.activeInHierarchy) {
					ContainerHistory.RemoveAt(i);
					continue;
				}
				if(!container.m_isVisible) continue;

				container.RestoreFocusFromHistory();
				break;
			}
		}

		/// <summary> 履歴から復帰する際に操作可否と選択状態を元に戻す </summary>
		private void RestoreFocusFromHistory() {
			Interactable = true;
			ResumeSelection();
		}

		/// <summary> 無効化時などに履歴から自身を除外する </summary>
		private void UnregisterFromHistory() {
			if(!Application.isPlaying) return;

			CleanHistory();
			ContainerHistory.Remove(this);
		}

		/// <summary> 履歴内のnullエントリを除去して整合性を保つ </summary>
		private static void CleanHistory() {
			for (int i = ContainerHistory.Count - 1; i >= 0; i--) {
				if(ContainerHistory[i] == null) {
					ContainerHistory.RemoveAt(i);
				}
			}
		}

		/// <summary> SetActive実行時の警告抑制を行いながらActive状態を変更する </summary>
		private void SetActiveInternal(bool active) {
			if(gameObject.activeSelf == active) return;

			m_suppressActiveWarning = true;
			gameObject.SetActive(active);
		}

		/// <summary> 非待機版のアニメーション実行。アニメーションが設定されていればその場で再生する </summary>
		private void TryPlayAnimations(IUiAnimation[] animations, Action callback = null) {
			if(m_suppressAnimation || (!m_useCustomAnimations && !m_useSharedAnimation)) {
				callback?.Invoke();
				return;
			}

			aGuiUtils.PlayAnimation(animations, m_targetRectTransform, m_targetGraphic, m_originalRectTransformValues, () => {
				callback?.Invoke();
			});
		}


		/// <summary> Hide前に現在選択されているSelectableを記録する </summary>
		private void CaptureCurrentSelection() {
			var es = EventSystem.current;
			if(es == null) return;
			var selected = es.currentSelectedGameObject;
			if(selected == null) return;
			if(!selected.transform.IsChildOf(transform)) return;
			m_lastSelected = selected.GetComponent<Selectable>();
		}

		/// <summary> 記録済みSelectableまたは初期Selectableへ選択状態を復帰する </summary>
		private void ResumeSelection() {
			var es = EventSystem.current;
			if(es == null) return;

			Selectable target = null;
			// リジュームを優先する設定の場合は最後に選択されていた要素を採用
			if(m_defaultResumeSelectionOnShow && m_lastSelected != null && m_lastSelected.IsActive() && m_lastSelected.IsInteractable()) {
				target = m_lastSelected;
			}

			// フォールバックとして初期Selectableを使用
			if(target == null) {
				target = m_initialSelectable;
			}

			if(target == null) return;
			if(!target.IsActive() || !target.IsInteractable()) return;

			es.SetSelectedGameObject(target.gameObject);
		}


		/// <summary> Show時に表示フラグと操作可否をセットする </summary>
		private void UpdateStateForShow() {
			m_isVisible = true;
			Interactable = true;
		}

		/// <summary> Hide時に表示フラグを下げ、操作を無効化する </summary>
		private void UpdateStateForHide() {
			m_isVisible = false;
			Interactable = false;
		}

		/// <summary> 外部からの直接SetActive操作を検知し、Show/Hide利用を促す警告を出す </summary>
		private void WarnActiveChange() {
			if(m_isQuitting) return;
			Debug.LogWarning($"[{nameof(aContainerBase)}] {name} の gameObject.SetActive が直接変更されました。Show/Hide または表示フラグを使用してください。", this);
		}
		#endregion

		#if UNITY_EDITOR
		[SerializeField]
		private bool m_debugMode = false;

		protected virtual string ContainerNamePrefix => "Container - ";

		/// <summary> コンポーネント追加時に自動リネームとキャッシュ更新を行う </summary>
		private void Reset() {
			if(Application.isPlaying) return;
			AutoRenameInEditor();
			CacheSelectablesInEditor();
			CacheReferences();
			ApplySharedAnimationsFromSet();
		}

		/// <summary> エディタ上で参照キャッシュを更新し、設定漏れを防ぐ </summary>
		protected virtual void OnValidate() {
			if(Application.isPlaying) return;
			CacheSelectablesInEditor();
			CacheReferences();
			ApplySharedAnimationsFromSet();
		}

		/// <summary> コンテナ追加時にGameObject名へ接頭辞を付与する </summary>
		private void AutoRenameInEditor() {
			string prefix = ContainerNamePrefix;
			if(string.IsNullOrEmpty(prefix)) return;

			string trimmedName = TrimKnownPrefixes(gameObject.name);
			string newName = $"{prefix}{trimmedName}";
			if (gameObject.name != newName) {
				gameObject.name = newName;
			}
		}

		/// <summary> 既知のプレフィックスを取り除いて元の名前部分を取得する </summary>
		protected virtual string TrimKnownPrefixes(string currentName) {
			string prefix = ContainerNamePrefix;
			if(string.IsNullOrEmpty(prefix)) return currentName;
			return currentName.StartsWith(prefix) ? currentName.Substring(prefix.Length) : currentName;
		}

		/// <summary> RectTransformやCanvasGroupなど必要な参照を確保し、不足していれば生成する </summary>
		private void CacheReferences() {
			if(m_rectTransform == null) {
				m_rectTransform = transform as RectTransform;
			}

			// CanvasGroupが未設定なら取得または新規追加
			if(m_canvasGroup == null) {
				m_canvasGroup = GetComponent<CanvasGroup>();
			}

			if(m_canvasGroup == null) {
				m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
			}

			// アニメーション対象のRectTransformを補完
			if(m_targetRectTransform == null) {
				m_targetRectTransform = m_rectTransform;
			}

			// アニメーション用に初期RectTransform値を保存
			if(m_targetRectTransform != null) {
				var currentValues = RectTransformValues.CreateValues(m_targetRectTransform);
				
				if (!currentValues.Equals(m_originalRectTransformValues)) {
					m_originalRectTransformValues = currentValues;
				}
			}

			// Graphicが未設定なら同オブジェクトから取得
			if(m_targetGraphic == null) {
				m_targetGraphic = GetComponent<Graphic>();
			}
		}

		/// <summary> エディタ上で子階層のSelectableをキャッシュする </summary>
		private void CacheSelectablesInEditor() {
			var currentSelectables = GetComponentsInChildren<Selectable>(true);
			if (m_childSelectables == null || m_childSelectables.Length != currentSelectables.Length) {
				m_childSelectables = currentSelectables;
				return;
			}

			for (int i = 0; i < m_childSelectables.Length; i++) {
				if (m_childSelectables[i] != currentSelectables[i]) {
					m_childSelectables = currentSelectables;
					return;
				}
			}
		}

		private void ApplySharedAnimationsFromSet() {
			if(!m_useSharedAnimation) return;
			if(m_sharedAnimation == null) return;

			// アセットの内容をクローンして適用
			// 毎回クローンすると参照が変わるため、変更があったかどうかの判定が難しい。
			// ここでは SetDirty(this) の無条件呼び出しを避けることを優先する。
			// 実際の開発では、共有アセット側が変更されたら OnValidate が走るはずなので
			// 何らかの手段で「変更された」ことを検知したい。
			// ひとまず、「現在のアニメーションが null の場合のみ」あるいは「常に上書きするが SetDirty はしない」
			// 形にして、UnityEditor.EditorUtility.SetDirty(this) を削除。
			
			m_showAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.showAnimations);
			m_hideAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.hideAnimations);
		}
		#endif
	}
}
