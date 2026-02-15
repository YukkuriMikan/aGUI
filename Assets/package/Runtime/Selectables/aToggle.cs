using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ANest.UI {
	/// <summary>各種ガードとテキスト遷移・カスタムアニメーションを備えた拡張Toggle</summary>
	[RequireComponent(typeof(aGuiInfo))]
	public class aToggle : Toggle {
		#region SerializeField
		[Header("Shared Parameters")]
		[Tooltip("共通パラメータを使用するかどうか")]
		[SerializeField] private bool useSharedParameters; // 共通パラメータを使用するか
		[Tooltip("共通パラメータの参照")]
		[SerializeField] private aSelectablesSharedParameters sharedParameters; // 共通パラメータの参照

		[Header("Text Transition")]
		[Tooltip("状態遷移に合わせてテキストを変更する対象")]
		[SerializeField] private TMP_Text targetText; // 遷移対象のテキスト
		[Tooltip("テキスト遷移の種別")]
		[SerializeField] private TextTransitionType textTransition = TextTransitionType.TextColor; // テキスト遷移種別
		[Tooltip("テキストカラー設定")]
		[SerializeField] private ColorBlock textColors = ColorBlock.defaultColorBlock; // テキストカラー設定
		[Tooltip("テキスト差し替え設定")]
		[SerializeField] private TextSwapState textSwapState; // テキスト差し替え設定
		[Tooltip("テキストアニメーション用トリガー")]
		[SerializeField] private AnimationTriggers textAnimationTriggers = new(); // テキストアニメーション用トリガー
		[Tooltip("テキスト用アニメーター")]
		[SerializeField] private Animator textAnimator; // テキスト用アニメーター

		[Header("Navigation")]
		[Tooltip("入力ガード（連打防止）を使用するかどうか")]
		[SerializeField] private bool useMultipleInputGuard = true; // 入力ガードを使うか
		[Tooltip("入力ガードの待機秒数")]
		[SerializeField] private float multipleInputGuardInterval = 0.5f; // 入力ガードの待機秒数
		[Tooltip("非Interactableをスキップして次のSelectableに移動するかどうか")]
		[SerializeField] private bool skipNonInteractableNavigation = true; // 非Interactableをスキップするか
		[Tooltip("ショートカット入力の設定")]
		[SerializeReference, SerializeReferenceDropdown] private IShortCut shortCut; // ショートカット入力

		[Header("Animation")]
		[Tooltip("共通のアニメーションセットを使用するかどうか")]
		[SerializeField] private bool m_useSharedAnimation = false; // 共有アニメーションを使用するか
		[Tooltip("個別のアニメーション設定を使用するかどうか")]
		[SerializeField] private bool m_useCustomAnimation; // カスタムアニメーションを使用するか
		[Tooltip("共通のアニメーション設定")]
		[SerializeField] private UiAnimationSet m_sharedAnimation; // 共有アニメーション設定
		[Tooltip("クリック時に再生するアニメーション")]
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_clickAnimations; // クリック時のカスタムアニメーション
		[Tooltip("ON時に再生するアニメーション")]
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_onAnimations; // ON時のカスタムアニメーション
		[Tooltip("OFF時に再生するアニメーション")]
		[SerializeReference, SerializeReferenceDropdown] private IUiAnimation[] m_offAnimations; // OFF時のカスタムアニメーション

		[Header("Reference")]
		[Tooltip("GUI情報の参照")]
		[SerializeField] private aGuiInfo m_guiInfo; // GUI情報の参照
		[Tooltip("トグルグラフィック（チェックマーク等）用のGUI情報の参照")]
		[SerializeField] private aGuiInfo m_graphicGuiInfo; // トグルグラフィック用のGUI情報の参照
		#endregion

		#region Fields
		private float _lastAcceptedClickTime = -999f;            // 最後に受理した入力時刻
		private CancellationTokenSource _textColorTransitionCts; // テキストカラー遷移のCTS
		private bool _shortCutPressed;                           // ショートカット押下状態
		private bool _shortCutPressAccepted;                     // ショートカット入力がガードを通過したか
		#endregion

		#region Unity Methods
		/// <summary>有効化時に初期化とRectTransformの初期値取得を行う</summary>
		protected override void OnEnable() {
			ApplySharedParametersIfNeeded();
			base.OnEnable();

			ResetShortCutState();
			RegisterToggleListener(true);
		}

		/// <summary>無効化時にリスナー解除やアニメーションのキャンセルを行う</summary>
		protected override void OnDisable() {
			base.OnDisable();

			RegisterToggleListener(false);
			aGuiUtils.StopTextColorTransition(ref _textColorTransitionCts);
			ResetShortCutState();
		}

		/// <summary>ショートカット入力を監視する</summary>
		private void Update() {
#if UNITY_EDITOR
			if(!Application.isPlaying) return;
#endif

			UpdateShortCutState();
		}

		/// <summary>クリック入力時のガード判定とアニメーション再生を処理する</summary>
		public override void OnPointerClick(PointerEventData eventData) {
			if(eventData.button != PointerEventData.InputButton.Left) return;
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			base.OnPointerClick(eventData);
			PlayClickAnimations();
		}

		/// <summary>Submit入力時にガードを適用する</summary>
		public override void OnSubmit(BaseEventData eventData) {
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			base.OnSubmit(eventData);
		}

		/// <summary>方向入力によるナビゲーション移動（非Interactableを無視）</summary>
		public override void OnMove(AxisEventData eventData) {
			if(!IsActive()) return;
			if(eventData == null) return;
			if(!skipNonInteractableNavigation) {
				base.OnMove(eventData);
				return;
			}

			var target = FindInteractableSelectable(eventData.moveDir);
			if(target == null) return;

			eventData.selectedObject = target.gameObject;
		}

		/// <summary>ステート遷移に応じてテキスト遷移を適用する</summary>
		protected override void DoStateTransition(SelectionState state, bool instant) {
			base.DoStateTransition(state, instant);
			if(targetText == null) return;

			switch(textTransition) {
				case TextTransitionType.TextColor:
					aGuiUtils.ApplyTextColorTransition(this, targetText, textColors, (int)state, instant, ref _textColorTransitionCts, () => _textColorTransitionCts = null);
					break;
				case TextTransitionType.TextSwap:
					aGuiUtils.ApplyTextSwapTransition(targetText, textSwapState, (int)state);
					break;
				case TextTransitionType.TextAnimation:
					aGuiUtils.ApplyTextAnimationTransition(targetText, textAnimator, textAnimationTriggers, (int)state);
					break;
			}
		}
		#endregion

		/// <summary>指定方向にあるInteractableなSelectableを探索する</summary>
		private Selectable FindInteractableSelectable(MoveDirection direction) {
			if(direction == MoveDirection.None) return null;

			var visited = new HashSet<Selectable> { this };
			var current = (Selectable)this;

			while(true) {
				var next = FindSelectableInDirection(current, direction);

				if(next == null) return null;
				if(!visited.Add(next)) return null;

				if(next.IsActive() && next.IsInteractable()) {
					return next;
				}

				current = next;
			}
		}

		/// <summary>方向に応じて次のSelectableを取得する（非Interactableも対象）</summary>
		private static Selectable FindSelectableInDirection(Selectable current, MoveDirection direction) {
			if(current == null) return null;
			var navigation = current.navigation;

			if(navigation.mode == Navigation.Mode.Explicit) {
				return direction switch {
					MoveDirection.Left => navigation.selectOnLeft,
					MoveDirection.Right => navigation.selectOnRight,
					MoveDirection.Up => navigation.selectOnUp,
					MoveDirection.Down => navigation.selectOnDown,
					_ => null
				};
			}

			return direction switch {
				MoveDirection.Left when (navigation.mode & Navigation.Mode.Horizontal) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.left),
				MoveDirection.Right when (navigation.mode & Navigation.Mode.Horizontal) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.right),
				MoveDirection.Up when (navigation.mode & Navigation.Mode.Vertical) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.up),
				MoveDirection.Down when (navigation.mode & Navigation.Mode.Vertical) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.down),
				_ => null
			};
		}

		/// <summary>Interactable判定を除外したSelectable探索</summary>
		private static Selectable FindSelectableWithoutInteractableFilter(Selectable current, Vector3 dir) {
			dir = dir.normalized;
			Vector3 localDir = Quaternion.Inverse(current.transform.rotation) * dir;
			Vector3 pos = current.transform.TransformPoint(GetPointOnRectEdge(current.transform as RectTransform, localDir));
			float maxScore = Mathf.NegativeInfinity;
			float maxFurthestScore = Mathf.NegativeInfinity;
			float score = 0f;
			var navigation = current.navigation;
			bool wantsWrapAround = navigation.wrapAround && (navigation.mode == Navigation.Mode.Vertical || navigation.mode == Navigation.Mode.Horizontal);

			Selectable bestPick = null;
			Selectable bestFurthestPick = null;

			var selectables = Selectable.allSelectablesArray;
			for(int i = 0; i < selectables.Length; ++i) {
				Selectable sel = selectables[i];
				if(sel == null || sel == current) continue;
				if(sel.navigation.mode == Navigation.Mode.None) continue;

				var selRect = sel.transform as RectTransform;
				Vector3 selCenter = selRect != null ? (Vector3)selRect.rect.center : Vector3.zero;
				Vector3 myVector = sel.transform.TransformPoint(selCenter) - pos;
				float dot = Vector3.Dot(dir, myVector);

				if(wantsWrapAround && dot < 0) {
					score = -dot * myVector.sqrMagnitude;
					if(score > maxFurthestScore) {
						maxFurthestScore = score;
						bestFurthestPick = sel;
					}
					continue;
				}

				if(dot <= 0) continue;
				score = dot / myVector.sqrMagnitude;
				if(score > maxScore) {
					maxScore = score;
					bestPick = sel;
				}
			}

			if(wantsWrapAround && bestPick == null) return bestFurthestPick;
			return bestPick;
		}

		/// <summary>RectTransformのエッジ上の点を取得する</summary>
		private static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir) {
			if(rect == null) return Vector3.zero;
			if(dir != Vector2.zero) {
				dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
			}
			dir = rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
			return dir;
		}

		#region ShortCut Support
		/// <summary>ショートカット入力状態の更新と押下・解放処理</summary>
		private void UpdateShortCutState() {
			if(shortCut == null) return;

			bool isPressed = shortCut.IsPressed;

			if(isPressed && !_shortCutPressed) {
				HandleShortCutPress();
			} else if(!isPressed && _shortCutPressed) {
				HandleShortCutRelease();
			}

			_shortCutPressed = isPressed;
		}

		/// <summary>ショートカット押下開始時の処理</summary>
		private void HandleShortCutPress() {
			if(!IsActive() || !IsInteractable()) return;

			float now = Time.unscaledTime;
			if(IsGuardActive(now)) return;

			StartGuard(now);
			_shortCutPressAccepted = true;
		}

		/// <summary>ショートカット押下終了時の処理</summary>
		private void HandleShortCutRelease() {
			if(!_shortCutPressAccepted) {
				_shortCutPressed = false;
				return;
			}

			var eventSystem = aGuiManager.EventSystem;
			base.OnSubmit(eventSystem != null ? new BaseEventData(eventSystem) : null);
			PlayClickAnimations();

			_shortCutPressAccepted = false;
		}

		/// <summary>ショートカット状態をリセットする</summary>
		private void ResetShortCutState() {
			_shortCutPressed = false;
			_shortCutPressAccepted = false;
		}
		#endregion

		#region Toggle Events
		/// <summary>トグルのON/OFFリスナーを登録または解除する</summary>
		private void RegisterToggleListener(bool register) {
			if(register) {
				onValueChanged.AddListener(OnToggleValueChanged);
			} else {
				onValueChanged.RemoveListener(OnToggleValueChanged);
			}
		}

		/// <summary>トグル状態変化時にカスタムアニメーションを再生する</summary>
		private void OnToggleValueChanged(bool isOn) {
#if UNITY_EDITOR
			if(!Application.isPlaying) return;
#endif
			
			if(!m_useCustomAnimation) return;

			PlayToggleAnimations(isOn);
		}
		#endregion

		#region Toggle Animation Async
		/// <summary>ON/OFF切替時のカスタムアニメーションを非同期待機で再生する</summary>
		private void PlayToggleAnimations(bool isOn) {
			if(!m_useCustomAnimation) return;

			//ToggleのGraphic指定がないなら無視
			if(graphic == null) return;
			if(m_graphicGuiInfo == null) return;

			if(isOn) {
				aGuiUtils.PlayAnimation(m_onAnimations, m_graphicGuiInfo.RectTransform, graphic, m_graphicGuiInfo.OriginalRectTransformValues);
			} else {
				//デフォルト処理によるトグルグラフィックの透明化を抑制
				graphic.canvasRenderer.SetAlpha(1f);

				aGuiUtils.PlayAnimation(m_offAnimations, m_graphicGuiInfo.RectTransform, graphic, m_graphicGuiInfo.OriginalRectTransformValues,
					() => {
						// OFF時、最後は非表示になるようにトグル標準の非表示処理を適用
						if(graphic != null) {
							graphic.canvasRenderer.SetAlpha(0f);
						}
					});
			}
		}

		/// <summary>クリック時のカスタムアニメーションを再生する</summary>
		private void PlayClickAnimations() {
			if(!m_useCustomAnimation && !m_useSharedAnimation) return;
			if(m_clickAnimations == null || m_clickAnimations.Length == 0) return;
			if(m_guiInfo == null) return;

			aGuiUtils.PlayAnimation(m_clickAnimations, m_guiInfo.RectTransform, targetGraphic, m_guiInfo.OriginalRectTransformValues);
		}
		#endregion

		#region Guards
		/// <summary>初期ガード・連打ガードの状態を判定する</summary>
		private bool IsGuardActive(float now) {
			if(!useMultipleInputGuard) return false;

			bool active = (now - _lastAcceptedClickTime) < multipleInputGuardInterval;
			#if UNITY_EDITOR
			if(active) {
				Debug.Log($"[{nameof(aToggle)}] Multiple Input Guard active. Input blocked.", this);
			}
			#endif
			return active;
		}

		/// <summary>入力ガード開始時刻を記録する</summary>
		private void StartGuard(float now) {
			_lastAcceptedClickTime = now;
		}
		#endregion

		#region Shared Apply
		/// <summary>共通パラメータを使用している場合に値を反映する</summary>
		private void ApplySharedParametersIfNeeded() {
			if(!useSharedParameters) return;
			if(sharedParameters == null) return;

			transition = sharedParameters.transition;
			colors = sharedParameters.transitionColors;
			spriteState = sharedParameters.spriteState;
			animationTriggers = sharedParameters.selectableAnimationTriggers;

			useMultipleInputGuard = sharedParameters.useMultipleInputGuard;
			multipleInputGuardInterval = sharedParameters.multipleInputGuardInterval;

			textTransition = sharedParameters.textTransition;
			textColors = sharedParameters.textColors;
			textSwapState = sharedParameters.textSwapState;
			textAnimationTriggers = sharedParameters.textAnimationTriggers;
		}
		#endregion

#if UNITY_EDITOR
		/// <summary>インスペクターでの値変更時の処理</summary>
		protected override void OnValidate() {
			base.OnValidate();
			if(Application.isPlaying) return;

			ApplySharedParametersIfNeeded();
			ApplySharedAnimationsFromSet();

			if(m_guiInfo == null) {
				m_guiInfo = GetComponent<aGuiInfo>();
			}

			if(m_graphicGuiInfo == null && graphic != null) {
				m_graphicGuiInfo = graphic.GetComponent<aGuiInfo>();
			}

			if(targetGraphic == null) {
				targetGraphic = GetComponentInChildren<Graphic>();
			}
		}

		/// <summary>共有アニメーションセットからアニメーションを複製して適用する</summary>
		private void ApplySharedAnimationsFromSet() {
			if(!m_useSharedAnimation) return;
			if(m_sharedAnimation == null) return;

			m_clickAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.clickAnimations);
			m_onAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.onAnimations);
			m_offAnimations = aGuiUtils.CloneAnimations(m_sharedAnimation.offAnimations);

			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
	}
}
