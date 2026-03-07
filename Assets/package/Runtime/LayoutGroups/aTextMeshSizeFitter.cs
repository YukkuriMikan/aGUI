using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ANest.UI {
	/// <summary>TextMeshProのテキスト幅に合わせて自身のRectTransformサイズを調整するフィッター</summary>
	[RequireComponent(typeof(RectTransform))]
	public class aTextMeshSizeFitter : MonoBehaviour {
		#region Enums
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
		#endregion

		#region SerializeField
		[Tooltip("サイズ計測対象のTextMeshProコンポーネント")]
		[SerializeField] private TMP_Text m_targetText; // 対象のTextMeshProコンポーネント
		[Tooltip("横方向をフィットさせるか")]
		[SerializeField] private bool m_fitWidth = true; // 横方向フィットフラグ
		[Tooltip("縦方向をフィットさせるか")]
		[SerializeField] private bool m_fitHeight; // 縦方向フィットフラグ
		[Tooltip("サイズ変更時の基準ピボット")]
		[SerializeField] private PivotType m_pivotType = PivotType.MiddleCenter; // 基準点
		[Tooltip("trueの場合アニメーションでサイズ変更、falseの場合即時変更")]
		[SerializeField] private bool m_useAnimation; // アニメーション使用フラグ
		[Tooltip("アニメーション時間（秒）")]
		[SerializeField] private float m_animationDuration = 0.3f; // アニメーション時間
		[Tooltip("アニメーションのイージング")]
		[SerializeField] private Ease m_ease = Ease.OutQuad; // イージング種別
		[Tooltip("テキスト幅に加算する左右上下の余白")]
		[SerializeField] private RectOffset m_padding = new(); // パディング
		#endregion

		#region Fields
		private RectTransform m_rectTransform; // RectTransform キャッシュ
		private Tween m_sizeTween;             // サイズ変更用Tween
		private Tween m_posTween;              // 位置補正用Tween
		#endregion

		#region Properties
		/// <summary>自身のRectTransformキャッシュ</summary>
		private RectTransform RectTransform => m_rectTransform ? m_rectTransform : (m_rectTransform = transform as RectTransform);
		#endregion

		#region Unity Methods
		/// <summary>コンポーネントリセット時に参照を補完</summary>
		private void Reset() {
			m_targetText = GetComponentInChildren<TMP_Text>();
		}

		/// <summary>有効化時にテキスト変更イベントを購読</summary>
		private void OnEnable() {
			TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
			ApplyFitting();
		}

		/// <summary>無効化時にテキスト変更イベントを解除</summary>
		private void OnDisable() {
			TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
			KillTweens();
		}

		/// <summary>破棄時にTweenを停止</summary>
		private void OnDestroy() {
			KillTweens();
		}
		#endregion

		#region Methods
		/// <summary>テキスト変更イベントのコールバック</summary>
		/// <param name="obj">変更されたテキストオブジェクト</param>
		private void OnTextChanged(Object obj) {
			if(obj != m_targetText) return;
			ApplyFitting();
		}

		/// <summary>テキスト幅に合わせてサイズを更新する</summary>
		public void ApplyFitting() {
			if(m_targetText == null) return;
			if(RectTransform == null) return;

			var targetPivot = GetPivotVector(m_pivotType);
			var rectTransform = RectTransform;
			var currentSize = rectTransform.rect.size;
			var targetSize = currentSize;

			if(m_fitWidth) {
				targetSize.x = m_targetText.preferredWidth + m_padding.left + m_padding.right;
			}
			if(m_fitHeight) {
				targetSize.y = m_targetText.preferredHeight + m_padding.top + m_padding.bottom;
			}

			var deltaSize = targetSize - currentSize;

			// パディングの非対称分だけテキストの配置を補正
			var textRect = m_targetText.rectTransform;
			textRect.offsetMin = new Vector2(m_padding.left, m_padding.bottom);
			textRect.offsetMax = new Vector2(-m_padding.right, -m_padding.top);

			if(deltaSize == Vector2.zero) return;

			// 現在のピボットと基準ピボットの差分で位置を補正
			var pivot = rectTransform.pivot;
			var posOffset = new Vector2(
				(pivot.x - targetPivot.x) * deltaSize.x,
				(pivot.y - targetPivot.y) * deltaSize.y
				);

			KillTweens();

			if(m_useAnimation && Application.isPlaying) {
				var startPos = rectTransform.anchoredPosition;
				m_sizeTween = DOTween.To(
					() => rectTransform.sizeDelta,
					x => rectTransform.sizeDelta = x,
					rectTransform.sizeDelta + deltaSize,
					m_animationDuration
					).SetEase(m_ease).SetTarget(rectTransform);

				m_posTween = DOTween.To(
					() => startPos,
					x => rectTransform.anchoredPosition = x,
					startPos + posOffset,
					m_animationDuration
					).SetEase(m_ease).SetTarget(rectTransform);
			} else {
				if(m_fitWidth) {
					rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetSize.x);
				}
				if(m_fitHeight) {
					rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize.y);
				}
				rectTransform.anchoredPosition += posOffset;
			}
		}

		/// <summary>実行中のTweenを停止する</summary>
		private void KillTweens() {
			if(m_sizeTween != null && m_sizeTween.IsActive()) {
				m_sizeTween.Kill();
			}
			m_sizeTween = null;

			if(m_posTween != null && m_posTween.IsActive()) {
				m_posTween.Kill();
			}
			m_posTween = null;
		}

		/// <summary>PivotType を Vector2 の座標に変換</summary>
		/// <param name="type">変換するPivotType</param>
		/// <returns>対応するピボット座標</returns>
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
