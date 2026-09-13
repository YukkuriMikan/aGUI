using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>選択変更に応じてScrollRectを自動スクロールするコンテナ</summary>
	public abstract class aScrollContainerBase<T> : aNormalSelectableContainerBase<T> where T : Selectable {
		[Tooltip("スクロール対象のScrollRect")]
		[SerializeField] private ScrollRect m_scrollRect;       // スクロール対象のScrollRect

		[Tooltip("スクロールにかける時間（秒）")]
		[SerializeField] private float m_scrollDuration = 0.2f; // スクロール時間（秒）

		[Tooltip("アイテム表示時の上下余白（ピクセル）")]
		[SerializeField] private float m_scrollPadding = 20f;   // スクロール時の余白

		private static readonly Vector3[] s_worldCornersBuffer = new Vector3[4]; // GetWorldCorners用の共有バッファ（GC Alloc回避）

		private CancellationTokenSource m_scrollCancelSource; // スクロールキャンセル用CTS

		/// <summary>初期化時に選択変更リスナーを登録する</summary>
		public override void Initialize() {
			if(m_initialized) return;
			
			OnSelectChanged.AddListener(OnSelectChangedAction);

			base.Initialize();
		}

		/// <summary>無効化時に自動スクロールを中断する</summary>
		protected override void OnDisable() {
			StopAutoScroll();
			base.OnDisable();
		}

		/// <summary>非表示アニメーションの開始時点で自動スクロールを中断する</summary>
		protected override void UpdateStateForHide() {
			base.UpdateStateForHide();
			StopAutoScroll();
		}

		/// <summary>破棄時にスクロールアニメーションをキャンセルする</summary>
		protected override void OnDestroy() {
			StopAutoScroll();
			base.OnDestroy();
		}

		private void StopAutoScroll() {
			var source = m_scrollCancelSource;
			m_scrollCancelSource = null;
			source?.Cancel();
			source?.Dispose();
		}

		/// <summary>選択対象が変わった際にスクロールを開始する</summary>
		/// <param name="selectable">現在選択されたSelectable</param>
		private void OnSelectChangedAction(T selectable) {
			if(!isActiveAndEnabled || !IsVisible) return;

			var item = selectable != null ? selectable.GetComponent<RectTransform>() : null;
			ScrollToItem(m_scrollRect, item, null, m_scrollDuration, m_scrollPadding, ref m_scrollCancelSource);
		}

		/// <summary>指定したアイテムが画面内に表示されるようにスクロール位置を調整します</summary>
		/// <param name="scrollRect">対象のScrollRect</param>
		/// <param name="item">表示対象のアイテムのRectTransform</param>
		/// <param name="previousItem">既存の呼び出しとの互換性のために残している引数。現在の計算では使用しない（null可）。</param>
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

			// 表示済みの項目への選択変更でも、前の選択に向かう移動を停止する。
			cancellationTokenSource?.Cancel();
			cancellationTokenSource?.Dispose();
			cancellationTokenSource = null;

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

			// レイアウトを強制更新してcontentのサイズを確定させる
			Canvas.ForceUpdateCanvases();

			var viewportRect = scrollRect.viewport;
			var contentRect = scrollRect.content;
			GetVerticalBounds(item, viewportRect, out var itemBottom, out var itemTop);
			var viewportBottom = viewportRect.rect.yMin;
			var viewportTop = viewportRect.rect.yMax;

			// 表示済みなら移動しない。Paddingもviewportのローカル単位で扱う。
			float offset;
			if(itemTop > viewportTop) offset = viewportTop - scrollPadding - itemTop;
			else if(itemBottom < viewportBottom) offset = viewportBottom + scrollPadding - itemBottom;
			else return;

			// ScrollRectと同じviewport空間の範囲から移動量を正規化する。
			// Contentの拡縮・反転があっても、異なるローカル単位を混ぜない。
			GetVerticalBounds(contentRect, viewportRect, out var contentBottom, out var contentTop);
			var scrollableHeight = contentTop - contentBottom - viewportRect.rect.height;
			var targetScrollPosition = scrollableHeight > 0f
				? Mathf.Clamp01(scrollRect.verticalNormalizedPosition - offset / scrollableHeight)
				: 1f;

			// 移動が必要な場合だけ新しいキャンセルトークンを作成する。
			cancellationTokenSource = new CancellationTokenSource();

			// 新しいスクロールアニメーションを開始
			SmoothScrollAsync(scrollRect, targetScrollPosition, scrollDuration, cancellationTokenSource.Token).Forget();
		}

		private static void GetVerticalBounds(RectTransform rect, RectTransform viewport, out float bottom, out float top) {
			rect.GetWorldCorners(s_worldCornersBuffer);
			bottom = float.PositiveInfinity;
			top = float.NegativeInfinity;
			for(var i = 0; i < 4; i++) {
				var y = viewport.InverseTransformPoint(s_worldCornersBuffer[i]).y;
				bottom = Mathf.Min(bottom, y);
				top = Mathf.Max(top, y);
			}
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
				// timeScale = 0 では自動スクロールも停止する仕様。
				elapsed += Time.deltaTime;
				var t = Mathf.Clamp01(elapsed / scrollDuration);
				// イージング関数（ease-out）を適用
				var easedT = 1f - Mathf.Pow(1f - t, 3f);
				scrollRect.verticalNormalizedPosition = Mathf.Lerp(startPosition, targetPosition, easedT);
				await UniTask.Yield(cancellationToken);

				// スクロール中にScrollRectが破棄された場合は中断
				if (scrollRect == null) {
					return;
				}
			}

			// 最終位置に確実に設定
			scrollRect.verticalNormalizedPosition = targetPosition;
		}
	}
}
