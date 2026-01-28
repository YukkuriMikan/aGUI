using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aContainerBase の CurrentSelectable に追従するカーソルを制御するコンポーネント</summary>
	public class aCursorBase : MonoBehaviour {
		#region Enums
		/// <summary>移動モード</summary>
		public enum MoveMode {
			Instant,  // 即座に移動
			Animation // アニメーションを伴って移動
		}

		/// <summary>サイズ変更モード</summary>
		public enum SizeMode {
			Fixed,          // 固定サイズ
			MatchSelectable // 選択対象のサイズに合わせる
		}

		/// <summary>更新モード</summary>
		public enum UpdateMode {
			OnSelectChanged, // 選択対象が変更された時のみ更新
			EveryFrame       // 毎フレーム（LateUpdate）更新
		}
		#endregion

		#region Serialize Fields
		[Tooltip("カーソルとして扱うRectTransform")]
		[SerializeField] protected RectTransform m_cursorRect; // カーソルとして扱うRectTransform

		[Tooltip("カーソルとして表示するイメージ")]
		[SerializeField] protected Image m_cursorImage; // カーソルイメージ

		[Tooltip("更新タイミングの設定")]
		[SerializeField] private UpdateMode m_updateMode = UpdateMode.EveryFrame; // 更新モード

		[Header("Move Settings")]
		[Tooltip("移動の演出モード")]
		[SerializeField] private MoveMode m_moveMode = MoveMode.Animation; // 移動モード
		[Tooltip("移動にかかる時間（秒）")]
		[SerializeField] protected float m_moveDuration = 0.2f; // 移動時間
		[Tooltip("移動のイージング設定")]
		[SerializeField] private Ease m_moveEase = Ease.OutQuad; // 移動イージング

		[Header("Size Settings")]
		[Tooltip("サイズの演出モード")]
		[SerializeField] private SizeMode m_sizeMode = SizeMode.MatchSelectable; // サイズ変更モード
		[Tooltip("ターゲットサイズに対するパディング")]
		[SerializeField] private Vector2 m_padding = Vector2.zero; // サイズパディング
		[Tooltip("サイズ変更にかかる時間（秒）")]
		[SerializeField] private float m_sizeChangeDuration = 0.2f; // サイズ変更時間
		[Tooltip("サイズ変更のイージング設定")]
		[SerializeField] private Ease m_sizeChangeEase = Ease.OutQuad; // サイズ変更イージング
		#endregion

	    #region Private Fields
		private RectTransform m_currentTargetRect; // 現在のターゲットRectTransform
		private Tweener m_moveTween;               // 移動アニメーション用Tween
		private Tweener m_sizeTween;               // サイズ変更アニメーション用Tween
		protected bool m_wasHidden = true;         // 前フレームで非表示だったかどうか（瞬間移動判定用）
		#endregion

	    #region Lifecycle Methods
		/// <summary>ターゲットの移動に追従するため、設定に応じて位置とサイズを更新する</summary>
		private void LateUpdate() {
			if(m_updateMode == UpdateMode.EveryFrame) {
				UpdateCursor(m_currentTargetRect);
			}
		}

		/// <summary>破棄時に購読解除とTweenの破棄を行う</summary>
		protected virtual void OnDestroy() {
			m_moveTween?.Kill();
			m_sizeTween?.Kill();
		}
		#endregion

		#region Internal Logic
		/// <summary>選択対象が変更された際にカーソル追従の準備を行う</summary>
		/// <param name="targetRect">新しく選択されたRectTransform</param>
		protected virtual void OnTargetRectChanged(RectTransform targetRect) {
			bool wasNull = m_currentTargetRect == null;
			m_currentTargetRect = targetRect;

			if(m_cursorRect == null && m_cursorImage != null) {
				m_cursorRect = m_cursorImage.rectTransform;
			}

			// ターゲットがnullになった場合は非表示にする
			if(m_currentTargetRect == null) {
				SetCursorVisible(false);
				return;
			}

			// nullから有効なターゲットに変わった場合は瞬間移動フラグを立てる
			if(wasNull) {
				m_wasHidden = true;
			}

			// ターゲットが有効な場合は表示する
			SetCursorVisible(true);

			if(m_cursorRect != null) {
				// ターゲットが切り替わった瞬間に、アンカーとピボットを合わせる
				m_cursorRect.anchorMin = m_currentTargetRect.anchorMin;
				m_cursorRect.anchorMax = m_currentTargetRect.anchorMax;
				m_cursorRect.pivot = m_currentTargetRect.pivot;

				// アニメーションモードの場合、既存のTweenをリセットして再開させる準備をする
				if(m_moveMode == MoveMode.Animation) {
					m_moveTween?.Kill();
					m_sizeTween?.Kill();
				}

				// 選択変更時のみ更新のモードなら、ここで一度更新を実行する
				if(m_updateMode == UpdateMode.OnSelectChanged) {
					UpdateCursor(m_currentTargetRect);
				}
			}
		}

		/// <summary>カーソルの表示/非表示を切り替える</summary>
		/// <param name="visible">表示するかどうか</param>
		protected void SetCursorVisible(bool visible) {
			if(m_cursorImage != null) {
				m_cursorImage.gameObject.SetActive(visible);
			}
		}

		/// <summary>カーソルの位置とサイズを選択対象に合わせる</summary>
		/// <param name="targetRect">ターゲットのRectTransform</param>
		protected virtual void UpdateCursor(RectTransform targetRect) {
			if(targetRect == null || m_cursorRect == null) return;

			// カーソルの位置は CurrentSelectable の位置に移動する
			// Canvas内での絶対座標を合わせるために、ワールド座標を使用する
			Vector3 targetWorldPos = targetRect.position;

			// 非表示状態から表示状態に遷移した場合は瞬間移動する
			bool shouldInstantMove = m_wasHidden || m_moveMode == MoveMode.Instant;

			if(shouldInstantMove) {
				m_moveTween?.Kill();
				m_cursorRect.position = targetWorldPos;
				m_wasHidden = false;
			} else {
				if(m_moveTween != null && m_moveTween.IsActive()) {
					// アニメーション中なら、ターゲットの最新位置を終着点として更新し続ける（追従）
					m_moveTween.ChangeEndValue(targetWorldPos, true);
				} else {
					// OnSelectableChanged で Kill された後、最初の更新で Tween を生成する
					if(m_moveTween == null || !m_moveTween.IsActive()) {
						m_moveTween = m_cursorRect.DOMove(targetWorldPos, m_moveDuration).SetEase(m_moveEase);
					}
				}
			}

			// サイズ変更
			if(m_sizeMode == SizeMode.MatchSelectable) {
				Vector2 targetSize = targetRect.rect.size + m_padding;
				if(shouldInstantMove) {
					m_sizeTween?.Kill();
					m_cursorRect.sizeDelta = targetSize;
				} else {
					if(m_sizeTween != null && m_sizeTween.IsActive()) {
						// アニメーション中なら、ターゲットの最新サイズを終着点として更新し続ける（追従）
						m_sizeTween.ChangeEndValue(targetSize, true);
					} else {
						if(m_sizeTween == null || !m_sizeTween.IsActive()) {
							m_sizeTween = DOTween.To(() => m_cursorRect.sizeDelta, x => m_cursorRect.sizeDelta = x, targetSize, m_sizeChangeDuration)
								.SetEase(m_sizeChangeEase);
						}
					}
				}
			}
		}
		#endregion

		#region Editor Support
#if UNITY_EDITOR
		/// <summary>インスペクターでの値変更時に参照を更新する</summary>
		protected virtual void OnValidate() {
			// RectもイメージもないならCursorって名前が付いたオブジェクトを探す
			if(m_cursorRect == null && m_cursorImage == null) {
				m_cursorRect = transform.Find("Cursor")?.GetComponent<RectTransform>();
			}

			// Rectがあってイメージが無いなら、Rectの配下からImageを取る
			if(m_cursorRect != null && m_cursorImage == null) {
				m_cursorImage = m_cursorRect.GetComponentInChildren<Image>();
			}

			// イメージがあってRectがないならイメージからRectを取る
			if(m_cursorImage != null && m_cursorRect == null) {
				m_cursorRect = m_cursorImage.rectTransform;
			}
		}
#endif
		#endregion
	}
}
