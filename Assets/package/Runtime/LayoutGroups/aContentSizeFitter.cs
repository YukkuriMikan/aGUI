using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ANest.UI {
	/// <summary>aLayoutGroupBase のレイアウト結果に合わせて自身のサイズを調整するフィッター</summary>
	[RequireComponent(typeof(aLayoutGroupBase))]
	[RequireComponent(typeof(RectTransform))]
	public class aContentSizeFitter : MonoBehaviour {
		public enum PivotType {
			UpperLeft,    // 左上
			UpperCenter,  // 上中央
			UpperRight,   // 右上
			MiddleLeft,   // 左中央
			MiddleCenter, // 中央
			MiddleRight,  // 右中央
			LowerLeft,    // 左下
			LowerCenter,  // 下中央
			LowerRight    // 右下
		}

		#region SerializeField
		[Tooltip("横方向をフィットさせるか")]
		[SerializeField] private bool m_fitWidth; // 横方向をフィットさせるか
		[Tooltip("縦方向をフィットさせるか")]
		[SerializeField] private bool m_fitHeight; // 縦方向をフィットさせるか
		[Tooltip("レイアウトグループの余白に追加するサイズ。幅には左右、高さには上下の合計を加算する。子の配置と拡張方向は既存の位置維持・基準ピボット設定に従う")]
		[SerializeField] private RectOffset m_padding = new RectOffset();
		[Tooltip("子のアンカー設定にかかわらずワールド位置とサイズを維持し、親だけをフィットする。OFFの場合はフィット後に再配置する")]
		[SerializeField] private bool m_preserveChildPositions = false;
		[Tooltip("フィットのたびに直下の子要素を再収集する。OFFの場合はレイアウトグループの現在の子リストを使用する")]
		[SerializeField] private bool m_collectChildrenEveryTime = false;
		[Tooltip("基準ピボットを固定する位置補正に、自身のスケール（反転を含む）を反映する")]
		[SerializeField] private bool m_considerScale = true;
		[Tooltip("フィット時に使用する基準ピボット")]
		[SerializeField] private PivotType m_pivotType = PivotType.UpperLeft; // 基準点
		[Tooltip("監視対象のレイアウトグループ")]
		[SerializeField] private aLayoutGroupBase m_layoutGroup; // 監視対象のレイアウトグループ
		#endregion

		#region Fields
		private IDisposable m_subscription;    // レイアウト通知購読用
		private RectTransform m_rectTransform; // RectTransform キャッシュ
		private bool m_isApplying;             // 寸法変更中の再入を防ぐ
		private readonly List<ChildState> m_childStates = new(); // 親の寸法変更前の子の状態
		#if UNITY_EDITOR
		private volatile bool m_refreshAfterValidation; // OnValidateからメインスレッドへ更新要求を渡す
		#endif
		/// <summary>自身のRectTransformキャッシュ</summary>
		private RectTransform RectTransform => m_rectTransform ? m_rectTransform : (m_rectTransform = transform as RectTransform); // キャッシュプロパティ
		#endregion

		/// <summary>子の配置を変えずに親のみをフィットするか。</summary>
		public bool PreserveChildPositions => m_preserveChildPositions;
		public bool CollectChildrenEveryTime => m_collectChildrenEveryTime;

		private struct ChildState {
			public Transform transform;
			public Vector3 position;
			public Vector2 size;
			public Vector2 anchoredPosition;
		}

		#region Unity Methods
		/// <summary>有効化時に購読を開始</summary>
		private void OnEnable() {
			AssignLayoutGroup();
			Subscribe();
		}

		/// <summary>無効化時に購読を解除</summary>
		private void OnDisable() {
			Unsubscribe();
		}

		/// <summary>破棄時に購読を解除</summary>
		private void OnDestroy() {
			Unsubscribe();
		}
		#endregion

		#region Methods
		/// <summary>layoutGroup が未設定なら自身から取得</summary>
		private void AssignLayoutGroup() {
			if(m_layoutGroup == null) {
				m_layoutGroup = GetComponent<aLayoutGroupBase>();
			}
		}

		/// <summary>レイアウト完了通知の購読を開始</summary>
		private void Subscribe() {
			Unsubscribe();
			if(m_layoutGroup == null) return;
			m_subscription = m_layoutGroup.CompleteLayoutAsObservable.Subscribe(OnLayoutCompleted);
			// 初回レイアウト前は空の子一覧を使ってサイズを0にしない。
			if(m_layoutGroup.HasCompletedLayout) {
				OnLayoutCompleted(m_layoutGroup.CalculateContentRect());
			}
		}

		private void OnLayoutCompleted(Rect layoutRect) {
			ApplyFitting(m_collectChildrenEveryTime
				? m_layoutGroup.CalculateContentRectForFitting(false, true)
				: layoutRect);
		}

		/// <summary>レイアウト完了通知の購読を解除</summary>
		private void Unsubscribe() {
			if(m_subscription != null) {
				m_subscription.Dispose();
				m_subscription = null;
			}
		}

		/// <summary>受け取ったレイアウト領域に合わせて自身のサイズを更新</summary>
		private void ApplyFitting(Rect layoutRect) {
			if(m_isApplying || RectTransform == null) return;

			var targetPivot = GetPivotVector(m_pivotType);
			var rectTransform = RectTransform;
			var currentSize = rectTransform.rect.size;
			var targetSize = currentSize;

			if(m_fitWidth) {
				targetSize.x = layoutRect.width + (m_padding?.horizontal ?? 0);
			}

			if(m_fitHeight) {
				targetSize.y = layoutRect.height + (m_padding?.vertical ?? 0);
			}

			var deltaSize = targetSize - currentSize;
			if(deltaSize == Vector2.zero) return;
			m_isApplying = true;
			try {
				if(m_preserveChildPositions) CaptureChildren();
				if(m_fitWidth) {
					rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetSize.x);
				}

				if(m_fitHeight) {
					rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize.y);
				}

				var pivot = rectTransform.pivot;
				var offset = new Vector3(
					(pivot.x - targetPivot.x) * deltaSize.x,
					(pivot.y - targetPivot.y) * deltaSize.y,
					0f
					);
				if(m_considerScale) offset = Vector3.Scale(offset, rectTransform.localScale);
				rectTransform.localPosition += rectTransform.localRotation * offset;
				if(m_preserveChildPositions) {
					RestoreChildren();
				} else if(m_layoutGroup != null) {
					m_layoutGroup.RecalculateLayoutAfterFitting();
				}
			} finally {
				m_childStates.Clear();
				m_isApplying = false;
			}
		}

		/// <summary>現在のRectを使用してフィット処理を実行する</summary>
		public void ApplyFitting() {
			AssignLayoutGroup();
			if(m_layoutGroup == null) return;
			ApplyFitting(m_layoutGroup.CalculateContentRectForFitting(m_preserveChildPositions, m_collectChildrenEveryTime));
		}

		/// <summary>アンカーによる移動・ストレッチを打ち消すため、直下の子の状態を保存する。</summary>
		private void CaptureChildren() {
			m_childStates.Clear();
			var parent = transform;
			for(var i = 0; i < parent.childCount; i++) {
				var child = parent.GetChild(i);
				var rect = child as RectTransform;
				if(rect != null && m_layoutGroup != null) m_layoutGroup.InitializeChildLayoutTween(rect);
				m_childStates.Add(new ChildState {
					transform = child,
					position = child.position,
					size = rect != null ? rect.rect.size : Vector2.zero,
					anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero
				});
			}
		}

		/// <summary>子のアンカーは変えず、変更前のワールド位置とサイズを復元する。</summary>
		private void RestoreChildren() {
			foreach(var state in m_childStates) {
				if(state.transform == null) continue;
				if(state.transform is RectTransform rect) {
					rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, state.size.x);
					rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, state.size.y);
					rect.position = state.position;
					if(m_layoutGroup != null) {
						m_layoutGroup.OffsetChildLayoutTarget(rect, rect.anchoredPosition - state.anchoredPosition);
					}
				} else {
					state.transform.position = state.position;
				}
			}
		}

		/// <summary>PivotType を Vector2 の座標に変換</summary>
		private Vector2 GetPivotVector(PivotType type) {
			switch(type) {
				case PivotType.UpperLeft: return new Vector2(0f, 1f);
				case PivotType.UpperCenter: return new Vector2(0.5f, 1f);
				case PivotType.UpperRight: return new Vector2(1f, 1f);
				case PivotType.MiddleLeft: return new Vector2(0f, 0.5f);
				case PivotType.MiddleCenter: return new Vector2(0.5f, 0.5f);
				case PivotType.MiddleRight: return new Vector2(1f, 0.5f);
				case PivotType.LowerLeft: return new Vector2(0f, 0f);
				case PivotType.LowerCenter: return new Vector2(0.5f, 0f);
				case PivotType.LowerRight: return new Vector2(1f, 0f);
				default: return new Vector2(0.5f, 0.5f);
			}
		}
		#endregion

		#if UNITY_EDITOR
		/// <summary>コンポーネントリセット時に参照を補完</summary>
		private void Reset() {
			AssignLayoutGroup();
		}

		/// <summary>検証中のRectTransform変更を避け、次フレームで購読を更新する</summary>
		private void OnValidate() {
			m_refreshAfterValidation = true;
		}

		/// <summary>有効なコンポーネントのみ購読し直して現在のレイアウトに同期する</summary>
		private void Update() {
			if(!m_refreshAfterValidation || !Application.isPlaying || !isActiveAndEnabled) return;
			m_refreshAfterValidation = false;
			AssignLayoutGroup();
			Subscribe();
		}
		#endif
	}
}
