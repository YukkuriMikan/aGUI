using System;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;

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
		[Tooltip("フィット時に使用する基準ピボット")]
		[SerializeField] private PivotType m_pivotType = PivotType.UpperLeft; // 基準点
		[Tooltip("監視対象のレイアウトグループ")]
		[SerializeField] private aLayoutGroupBase m_layoutGroup; // 監視対象のレイアウトグループ
		#endregion

		#region Fields
		private IDisposable m_subscription;    // レイアウト通知購読用
		private RectTransform m_rectTransform; // RectTransform キャッシュ
		/// <summary>自身のRectTransformキャッシュ</summary>
		private RectTransform RectTransform => m_rectTransform ? m_rectTransform : (m_rectTransform = transform as RectTransform); // キャッシュプロパティ
		private Rect m_lastLayoutRect;                                                                                             // 直近のレイアウト領域
		#endregion

		#region Unity Methods
		/// <summary>コンポーネントリセット時に参照を補完</summary>
		private void Reset() {
			AssignLayoutGroup();
		}

		/// <summary>インスペクタ変更時に参照を補完して購読を張り直す</summary>
		private void OnValidate() {
			AssignLayoutGroup();
			Subscribe();
		}

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
			m_subscription = m_layoutGroup.CompleteLayoutAsObservable.Subscribe(ApplyFitting);
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
			if(RectTransform == null) return;
			m_lastLayoutRect = layoutRect;

			var targetPivot = GetPivotVector(m_pivotType);
			var rectTransform = RectTransform;
			var currentSize = rectTransform.rect.size;
			var targetSize = currentSize;

			if(m_fitWidth) {
				targetSize.x = m_lastLayoutRect.width;
			}

			if(m_fitHeight) {
				targetSize.y = m_lastLayoutRect.height;
			}

			var deltaSize = targetSize - currentSize;
			if(deltaSize != Vector2.zero) {
				if(m_fitWidth) {
					rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetSize.x);
				}

				if(m_fitHeight) {
					rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize.y);
				}

				var pivot = rectTransform.pivot;
				rectTransform.anchoredPosition += new Vector2(
					(pivot.x - targetPivot.x) * deltaSize.x,
					(pivot.y - targetPivot.y) * deltaSize.y
				);
			}
		}

		/// <summary>現在のRectを使用してフィット処理を実行する</summary>
		public void ApplyFitting()
			=> ApplyFitting(m_layoutGroup.CalculateContentRect());

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
	}
}
