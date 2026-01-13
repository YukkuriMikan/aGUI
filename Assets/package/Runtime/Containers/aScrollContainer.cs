using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	public class aScrollContainer : aContainerBase {
		[SerializeField]
		private ScrollRect m_scrollRect;

		[SerializeField]
		private float m_scrollDuration = 0.2f;

		[SerializeField]
		private float m_scrollPadding = 20f;

		private CancellationTokenSource m_scrollCancelSource;
		private Selectable m_previousSelectable;

		protected override void Initialize() {
			base.Initialize();
			OnSelectChanged.AddListener(OnSelectChangedAction);
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			m_scrollCancelSource?.Cancel();
			m_scrollCancelSource?.Dispose();
		}

		private void OnSelectChangedAction(Selectable selectable) {
			if (selectable == null) return;

			var item = selectable.GetComponent<RectTransform>();
			var previousItem = m_previousSelectable != null ? m_previousSelectable.GetComponent<RectTransform>() : null;

			ScrollToItem(m_scrollRect, item, previousItem, m_scrollDuration, m_scrollPadding, ref m_scrollCancelSource);

			m_previousSelectable = selectable;
		}

		/// <summary>指定したアイテムが画面内に表示されるようにスクロール位置を調整します</summary>
		/// <param name="scrollRect">対象のScrollRect</param>
		/// <param name="item">表示対象のアイテムのRectTransform</param>
		/// <param name="previousItem">前回のアイテムのRectTransform（null可）</param>
		/// <param name="scrollDuration">スクロールアニメーションの時間（秒）</param>
		/// <param name="scrollPadding">スクロール時の余白（ピクセル）</param>
		/// <param name="cancellationTokenSource">スクロールアニメーション用キャンセルトークン（参照渡し）</param>
		public static void ScrollToItem(
			UnityEngine.UI.ScrollRect scrollRect,
			RectTransform item,
			RectTransform previousItem,
			float scrollDuration,
			float scrollPadding,
			ref CancellationTokenSource cancellationTokenSource) {

			// レイアウトを強制更新してcontentのサイズを確定させる
			Canvas.ForceUpdateCanvases();

			// ScrollRectが設定されていない場合は何もしない
			if (scrollRect == null) {
				return;
			}

			// viewportまたはcontentが設定されていない場合は何もしない
			if (scrollRect.viewport == null || scrollRect.content == null) {
				return;
			}

			// アイテムのRectTransformを取得
			if (item == null) {
				return;
			}

			// 変更前のアイテムが画面内にあるかチェック（パディングなしで純粋に画面内かどうか）
			bool wasPreviousItemVisible = false;
			if (previousItem != null) {
				var viewport = scrollRect.viewport;
				var previousItemWorldCorners = new Vector3[4];
				previousItem.GetWorldCorners(previousItemWorldCorners);

				var prevItemTopInViewport = viewport.InverseTransformPoint(previousItemWorldCorners[1]).y;
				var prevItemBottomInViewport = viewport.InverseTransformPoint(previousItemWorldCorners[0]).y;
				var vpLocalMin = viewport.rect.yMin;
				var vpLocalMax = viewport.rect.yMax;

				// パディングなしで、アイテムが完全に画面内にあるかチェック（ビューポートのローカル座標範囲を使用）
				wasPreviousItemVisible = prevItemBottomInViewport >= vpLocalMin && prevItemTopInViewport <= vpLocalMax;
			}

			// viewportとcontentのRectTransformを取得
			var viewportRect = scrollRect.viewport;
			var contentRect = scrollRect.content;

			// アイテムの位置をviewport空間に変換
			var itemWorldCorners = new Vector3[4];
			item.GetWorldCorners(itemWorldCorners);

			// アイテムの上端と下端のローカル位置を計算
			var itemTopInViewport = viewportRect.InverseTransformPoint(itemWorldCorners[1]).y;
			var itemBottomInViewport = viewportRect.InverseTransformPoint(itemWorldCorners[0]).y;
			var viewportHeight = viewportRect.rect.height;

			// ビューポートのローカル座標範囲
			var viewportLocalMin = viewportRect.rect.yMin;
			var viewportLocalMax = viewportRect.rect.yMax;

			// アイテムが完全に表示範囲内かどうかをチェック（パディングなしで純粋に画面内かどうか）
			// ビューポートのローカル座標系での範囲を使用
			var isItemFullyVisible = itemBottomInViewport >= viewportLocalMin && itemTopInViewport <= viewportLocalMax;

			// 変更前のアイテムが画面内にあり、かつ変更後のアイテムも画面内にある場合はスクロール不要
			if (wasPreviousItemVisible && isItemFullyVisible) {
				return;
			}

			// 変更後のアイテムが完全に表示されている場合もスクロール不要
			if (isItemFullyVisible) {
				return;
			}

			// 目標スクロール位置を計算（アイテム全体が確実に表示されるようにする）
			float targetScrollPosition;

			// スクロール方向の判定（ビューポートのローカル座標系を使用）
			var shouldScrollUp = itemTopInViewport > viewportLocalMax;
			var shouldScrollDown = itemBottomInViewport < viewportLocalMin;

			if (shouldScrollUp) {
				// アイテムが上にはみ出している場合
				// アイテムの上端がビューポート上端からpadding分下に来るようにする
				var itemTopInContent = contentRect.InverseTransformPoint(itemWorldCorners[1]).y;
				var contentHeight = contentRect.rect.height;
				var scrollableHeight = contentHeight - viewportHeight;

				if (scrollableHeight > 0) {
					// contentの上端からアイテムの上端までの距離を計算
					// アイテムの上端がビューポート上端からpadding分下に来るようにする
					var distanceFromContentTop = -(itemTopInContent - contentRect.rect.yMax) - scrollPadding;
					targetScrollPosition = 1f - Mathf.Clamp01(distanceFromContentTop / scrollableHeight);
				} else {
					targetScrollPosition = 1f;
				}
			} else if (shouldScrollDown) {
				// アイテムが下にはみ出している場合
				// アイテムの下端がビューポート下端からpadding分上に来るようにする
				var itemBottomInContent = contentRect.InverseTransformPoint(itemWorldCorners[0]).y;
				var contentHeight = contentRect.rect.height;
				var scrollableHeight = contentHeight - viewportHeight;

				if (scrollableHeight > 0) {
					// contentの上端からアイテムの下端までの距離を計算
					// アイテムの下端がビューポート下端からpadding分上に来るようにする
					var distanceFromContentTop = -(itemBottomInContent - contentRect.rect.yMax) - viewportHeight + scrollPadding;
					targetScrollPosition = 1f - Mathf.Clamp01(distanceFromContentTop / scrollableHeight);
				} else {
					targetScrollPosition = 1f;
				}
			} else {
				// どちらの方向にもはみ出していない場合（理論的にはここには来ないはず）
				return;
			}

			// 既存のスクロールアニメーションをキャンセル
			cancellationTokenSource?.Cancel();
			cancellationTokenSource?.Dispose();
			cancellationTokenSource = new CancellationTokenSource();

			// 新しいスクロールアニメーションを開始
			SmoothScrollAsync(scrollRect, targetScrollPosition, scrollDuration, cancellationTokenSource.Token).Forget();
		}

		/// <summary>スクロール位置をスムーズにアニメーションさせる非同期メソッド</summary>
		/// <param name="scrollRect">対象のScrollRect</param>
		/// <param name="targetPosition">目標スクロール位置（0～1の正規化された値）</param>
		/// <param name="scrollDuration">スクロールアニメーションの時間（秒）</param>
		/// <param name="cancellationToken">キャンセルトークン</param>
		public static async UniTask SmoothScrollAsync(
			UnityEngine.UI.ScrollRect scrollRect,
			float targetPosition,
			float scrollDuration,
			CancellationToken cancellationToken) {

			var startPosition = scrollRect.verticalNormalizedPosition;
			var elapsed = 0f;

			while (elapsed < scrollDuration) {
				elapsed += Time.deltaTime;
				var t = Mathf.Clamp01(elapsed / scrollDuration);
				// イージング関数（ease-out）を適用
				var easedT = 1f - Mathf.Pow(1f - t, 3f);
				scrollRect.verticalNormalizedPosition = Mathf.Lerp(startPosition, targetPosition, easedT);
				await UniTask.Yield(cancellationToken);
			}

			// 最終位置に確実に設定
			scrollRect.verticalNormalizedPosition = targetPosition;
		}
	}
}
