using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
		[SerializeField] private bool m_disableInteractableDuringAnimation = true;                // アニメーション中は操作不可にするか
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
		[SerializeField] private bool m_isVisible;           // 表示中かどうか
		[SerializeField] private bool m_interactable = true; // CanvasGroupのinteractableを中継するフラグ

		[Header("Layout")]
		[SerializeField] private aLayoutGroupBase m_layoutGroup;

		[Header("Event")]
		[SerializeField] private bool m_invokeEventsAfterAnimation = false; // イベントをアニメーション完了後に発火するか
		[SerializeField] private UnityEvent m_showEvent = new();            // Show完了時イベント
		[SerializeField] private UnityEvent m_hideEvent = new();            // Hide完了時イベント
		#endregion

		#region Fields
		private RectTransformValues? m_originalRectTransformValues; // アニメーション用RectTransform初期値
		private CancellationTokenSource _animationCts;              // Show/Hide待機用CTS
		private CancellationTokenSource _visibilityCts;             // Show/Hide全体制御用CTS
		private Selectable _lastSelected;                           // Hide時に記録した選択中のSelectable
		private RectTransform m_selfRectTransform;                  // 自身のRectTransformキャッシュ
		private bool _initialized;                                  // 初期化済みか
		private bool _isQuitting;                                   // アプリ終了フラグ
		private bool _suppressActiveWarning;                        // 内部SetActive実行中の警告抑制フラグ
		#endregion

		#region Properties
		/// <summary> レイアウトグループ </summary>
		public aLayoutGroupBase LayoutGroup => m_layoutGroup;

		/// <summary> 表示状態 </summary>
		public bool IsVisible {
			get => m_isVisible;
			set {
				if(m_isVisible == value) return;
				m_isVisible = value;
				ApplySerializedState();
			}
		}

		public UnityEvent ShowEvent => m_showEvent;
		public UnityEvent HideEvent => m_hideEvent;
		public bool InvokeEventsAfterAnimation {
			get => m_invokeEventsAfterAnimation;
			set => m_invokeEventsAfterAnimation = value;
		}

		/// <summary> CanvasGroupのinteractableを中継するフラグ </summary>
		public bool Interactable {
			get => m_interactable;
			set {
				if(m_interactable == value) return;
				m_interactable = value;
				ApplyInteractableState(m_interactable && m_isVisible);
			}
		}
		#endregion

		#region Unity Methods
		/// <summary> 有効化時に初期化やシリアライズ状態の適用、直接SetActiveされた場合の警告制御を行う </summary>
		protected virtual void OnEnable() {
			if(!Application.isPlaying) return;

			bool suppressed = _suppressActiveWarning;
			_suppressActiveWarning = false;

			if(!_initialized) {
				_initialized = true;
				ApplySerializedState();
				return;
			}

			if(suppressed) return;

			WarnActiveChange();
			ApplySerializedState();
		}

		/// <summary> 無効化時に履歴からの除外や警告表示を行う </summary>
		protected virtual void OnDisable() {
			if(!Application.isPlaying || _isQuitting) return;

			UnregisterFromHistory();

			if(_suppressActiveWarning) {
				_suppressActiveWarning = false;
				return;
			}

			if(!_initialized) return;

			WarnActiveChange();
		}

		/// <summary> アプリ終了時に終了フラグを立て、以降の警告を抑制する </summary>
		protected virtual void OnApplicationQuit() {
			_isQuitting = true;
		}
		#endregion

		#region Public Methods
		/// <summary> 非同期Show。必要に応じて選択をリジュームする </summary>
		public virtual async UniTask ShowAsync() {
			var visibilityCts = BeginVisibilityOperation();
			var token = visibilityCts.Token;

			// アクティブ化して履歴へ記録
			SetActiveInternal(true);

			RegisterShowHistory();

			// 表示状態とインタラクト状態を更新
			UpdateStateForShow();

			bool invokeAfterAnimation = m_invokeEventsAfterAnimation;
			if(!invokeAfterAnimation) {
				m_showEvent?.Invoke();
			}

			try {
				// アニメーション再生を待機し、完了後に選択復帰を行う
				await WaitAnimationsAsync(m_showAnimations, true, token);
				token.ThrowIfCancellationRequested();
				if(invokeAfterAnimation) {
					m_showEvent?.Invoke();
				}
			} catch (OperationCanceledException) {
				// 新しいShow/Hideによりキャンセルされた場合は何もしない
			} finally {
				CompleteVisibilityOperation(visibilityCts);
			}
		}

		/// <summary> 非同期Hide </summary>
		public virtual async UniTask HideAsync() {
			var visibilityCts = BeginVisibilityOperation();
			var token = visibilityCts.Token;

			// 現在の選択状態を退避
			CaptureCurrentSelection();

			// 表示/操作を無効化
			UpdateStateForHide();

			// 履歴から直前コンテナへフォーカスを戻す
			FocusPreviousContainerFromHistory();

			bool invokeAfterAnimation = m_invokeEventsAfterAnimation;
			if(!invokeAfterAnimation) {
				m_hideEvent?.Invoke();
			}

			try {
				// アニメーション完了を待ってから非アクティブ化
				await WaitAnimationsAsync(m_hideAnimations, false, token);
				token.ThrowIfCancellationRequested();
				SetActiveInternal(false);
				if(invokeAfterAnimation) {
					m_hideEvent?.Invoke();
				}
			} catch (OperationCanceledException) {
				// 新しいShow/Hideによりキャンセルされた場合は何もしない
			} finally {
				CompleteVisibilityOperation(visibilityCts);
			}
		}

		/// <summary> 同期呼び出し用のShow </summary>
		public virtual void Show() {
#if UNITY_EDITOR
			if(m_debugMode) {
				var stackTrace = new System.Diagnostics.StackTrace();
				var stackFrame = stackTrace.GetFrame(1);
				Debug.Log($"{this.name} Show: Call from {stackFrame?.GetFileName()}.{stackFrame?.GetMethod().Name}", this);
			}
#endif

			ShowAsync().Forget();
		}

		/// <summary> 同期呼び出し用のHide </summary>
		public virtual void Hide() {
#if UNITY_EDITOR
			if(m_debugMode) {
				var stackTrace = new System.Diagnostics.StackTrace();
				var stackFrame = stackTrace.GetFrame(1);
				Debug.Log($"{this.name} Hide: Call from {stackFrame?.GetFileName()}.{stackFrame?.GetMethod().Name}", this);
			}
#endif

			HideAsync().Forget();
		}
		#endregion

		#region Private Methods
		/// <summary> 実行時に子階層のSelectableをまとめてキャッシュする </summary>
		private void CacheSelectablesRuntime() {
			m_childSelectables = GetComponentsInChildren<Selectable>(true);
		}

		/// <summary> 指定アニメーションの再生を待ち、必要に応じて選択状態や操作可否を調整する </summary>
		private async UniTask WaitAnimationsAsync(IUiAnimation[] animations, bool isShow, CancellationToken cancellationToken = default) {
			CancelAnimationDelay();
			_animationCts = cancellationToken.CanBeCanceled
				? CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, cancellationToken)
				: CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			var token = _animationCts.Token;
			bool suppressedInteraction = false;

			try {
				// アニメーションが無い場合は即座に選択復帰のみ行う
				if(animations == null || animations.Length == 0 || m_targetRectTransform == null || m_originalRectTransformValues == null) {
					if(isShow) {
						ResumeSelection();
					}
					return;
				}

				var targetGraphic = m_targetGraphic != null ? m_targetGraphic : GetComponent<Graphic>();

				// アニメーション中は操作不可にして入力を抑制
				if(m_disableInteractableDuringAnimation) {
					suppressedInteraction = true;
					ApplyInteractableState(false);
				}

				await aGuiUtils.PlayAnimationsAsync(animations, targetGraphic, m_targetRectTransform, m_originalRectTransformValues.Value, token);

				if(this == null || this.Equals(null)) return;

				// Show完了後に選択を復帰
				if(isShow) {
					ResumeSelection();
				}
			} catch (OperationCanceledException) {
				if(cancellationToken.IsCancellationRequested) throw;
			} finally {
				// 入力抑制していた場合は元の状態に戻す
				if(suppressedInteraction && !token.IsCancellationRequested) {
					ApplyInteractableState(m_interactable && m_isVisible);
				}
				_animationCts?.Dispose();
				_animationCts = null;
			}
		}

		/// <summary> 進行中のShow/Hide処理をキャンセルし、新しい処理用のCTSを開始する </summary>
		private CancellationTokenSource BeginVisibilityOperation() {
			CancelVisibilityOperation();
			_visibilityCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			return _visibilityCts;
		}

		/// <summary> 進行中のShow/Hide用CTSをキャンセルして破棄する </summary>
		private void CancelVisibilityOperation() {
			if(_visibilityCts == null) return;
			if(!_visibilityCts.IsCancellationRequested) {
				_visibilityCts.Cancel();
			}
			_visibilityCts?.Dispose();
			_visibilityCts = null;
		}

		/// <summary> 完了したShow/Hide処理に紐づくCTSを破棄する </summary>
		private void CompleteVisibilityOperation(CancellationTokenSource cts) {
			if(_visibilityCts != cts) return;
			_visibilityCts?.Dispose();
			_visibilityCts = null;
		}

		/// <summary> 進行中のアニメーション待機をキャンセルし、リソースを開放する </summary>
		private void CancelAnimationDelay() {
			if(_animationCts == null) return;
			_animationCts.Cancel();
			_animationCts?.Dispose();
			_animationCts = null;
		}

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
			m_interactable = true;
			ApplyInteractableState(true);
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
			_suppressActiveWarning = true;
			gameObject.SetActive(active);
		}

		/// <summary> 非待機版のアニメーション実行。アニメーションが設定されていればその場で再生する </summary>
		private void TryPlayAnimations(IUiAnimation[] animations) {
			if(animations == null || animations.Length == 0) return;
			if(m_targetRectTransform == null || m_originalRectTransformValues == null) return;

			var targetGraphic = m_targetGraphic != null ? m_targetGraphic : GetComponent<Graphic>();
			// それぞれのアニメーションを個別に起動
			for (int i = 0; i < animations.Length; i++) {
				var anim = animations[i];
				anim?.DoAnimate(targetGraphic, m_targetRectTransform, m_originalRectTransformValues.Value, destroyCancellationToken).Forget();
			}
		}

		/// <summary> Hide前に現在選択されているSelectableを記録する </summary>
		private void CaptureCurrentSelection() {
			var es = EventSystem.current;
			if(es == null) return;
			var selected = es.currentSelectedGameObject;
			if(selected == null) return;
			if(!selected.transform.IsChildOf(transform)) return;
			_lastSelected = selected.GetComponent<Selectable>();
		}

		/// <summary> 記録済みSelectableまたは初期Selectableへ選択状態を復帰する </summary>
		private void ResumeSelection() {
			var es = EventSystem.current;
			if(es == null) return;

			Selectable target = null;
			// リジュームを優先する設定の場合は最後に選択されていた要素を採用
			if(m_defaultResumeSelectionOnShow && _lastSelected != null && _lastSelected.IsActive() && _lastSelected.IsInteractable()) {
				target = _lastSelected;
			}

			// フォールバックとして初期Selectableを使用
			if(target == null) {
				target = m_initialSelectable;
			}

			if(target == null) return;
			if(!target.IsActive() || !target.IsInteractable()) return;

			es.SetSelectedGameObject(target.gameObject);
		}

		/// <summary> シリアライズされた表示・インタラクト状態を現在のコンポーネントに適用する </summary>
		private void ApplySerializedState() {
			if(m_isVisible) {
				Show();
			} else {
				Hide();
			}

			ApplyInteractableState(m_interactable && m_isVisible);
		}

		/// <summary> Show時に表示フラグと操作可否をセットする </summary>
		private void UpdateStateForShow() {
			m_isVisible = true;
			ApplyInteractableState(m_interactable);
		}

		/// <summary> Hide時に表示フラグを下げ、操作を無効化する </summary>
		private void UpdateStateForHide() {
			m_isVisible = false;
			ApplyInteractableState(false);
		}

		/// <summary> CanvasGroupと子Selectableへインタラクト状態を反映する </summary>
		private void ApplyInteractableState(bool interactable) {
			if(m_canvasGroup != null) {
				m_canvasGroup.interactable = interactable;
			}

			if(m_childSelectables == null || m_childSelectables.Length == 0) return;
			// 子Selectable群へ一括で適用
			for (int i = 0; i < m_childSelectables.Length; i++) {
				var selectable = m_childSelectables[i];
				if(selectable == null) continue;
				selectable.interactable = interactable;
			}
		}

		/// <summary> 外部からの直接SetActive操作を検知し、Show/Hide利用を促す警告を出す </summary>
		private void WarnActiveChange() {
			Debug.LogWarning($"[{nameof(aContainerBase)}] {name} の gameObject.SetActive が直接変更されました。Show/Hide または表示フラグを使用してください。", this);
		}
		#endregion

		#if UNITY_EDITOR
		protected virtual string ContainerNamePrefix => "Container - ";

		/// <summary> コンポーネント追加時に自動リネームとキャッシュ更新を行う </summary>
		private void Reset() {
			if(Application.isPlaying) return;
			AutoRenameInEditor();
			CacheSelectablesInEditor();
			CacheReferences();
		}

		/// <summary> エディタ上で参照キャッシュを更新し、設定漏れを防ぐ </summary>
		protected virtual void OnValidate() {
			if(Application.isPlaying) return;
			AutoRenameInEditor();
			CacheSelectablesInEditor();
			CacheReferences();
		}

		/// <summary> コンテナ追加時にGameObject名へ接頭辞を付与する </summary>
		private void AutoRenameInEditor() {
			string prefix = ContainerNamePrefix;
			if(string.IsNullOrEmpty(prefix)) return;

			string trimmedName = TrimKnownPrefixes(gameObject.name);
			gameObject.name = $"{prefix}{trimmedName}";
		}

		/// <summary> 既知のプレフィックスを取り除いて元の名前部分を取得する </summary>
		protected virtual string TrimKnownPrefixes(string currentName) {
			string prefix = ContainerNamePrefix;
			if(string.IsNullOrEmpty(prefix)) return currentName;
			return currentName.StartsWith(prefix) ? currentName.Substring(prefix.Length) : currentName;
		}

		/// <summary> RectTransformやCanvasGroupなど必要な参照を確保し、不足していれば生成する </summary>
		private void CacheReferences() {
			m_selfRectTransform ??= transform as RectTransform;

			// CanvasGroupが未設定なら取得または新規追加
			if(m_canvasGroup == null) {
				m_canvasGroup = GetComponent<CanvasGroup>();
			}

			if(m_canvasGroup == null) {
				m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
			}

			// アニメーション対象のRectTransformを補完
			if(m_targetRectTransform == null) {
				m_targetRectTransform = m_selfRectTransform;
			}

			// アニメーション用に初期RectTransform値を保存
			if(m_targetRectTransform != null && m_originalRectTransformValues == null) {
				m_originalRectTransformValues = RectTransformValues.CreateValues(m_targetRectTransform);
			}

			// Graphicが未設定なら同オブジェクトから取得
			if(m_targetGraphic == null) {
				m_targetGraphic = GetComponent<Graphic>();
			}

			// 実行時に子Selectableキャッシュが空なら取得
			if(Application.isPlaying && (m_childSelectables == null || m_childSelectables.Length == 0)) {
				CacheSelectablesRuntime();
			}
		}

		/// <summary> エディタ上で子階層のSelectableをキャッシュする </summary>
		private void CacheSelectablesInEditor() {
			m_childSelectables = GetComponentsInChildren<Selectable>(true);
		}
		#endif

		#if UNITY_EDITOR
		[SerializeField]
		private bool m_debugMode = true;
		#endif
	}
}
