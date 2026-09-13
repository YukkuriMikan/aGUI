using DG.Tweening;
using System.Collections.Generic;
using TMPro;
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
			Fixed,           // 固定サイズ
			MatchSelectable, // 選択対象のサイズに合わせる
			MatchText,       // 選択対象のテキストサイズに合わせる
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
		private const float TweenTargetEpsilon = 0.0001f;                                      // Tween更新/生成の要否を判定する距離閾値（sqrMagnitude比較用）
		private RectTransform m_currentTargetRect;                                             // 現在のターゲットRectTransform
		private Tweener m_moveTween;                                                           // 移動アニメーション用Tween
		private Tweener m_sizeTween;                                                           // サイズ変更アニメーション用Tween
		private Vector3 m_moveTweenTarget;                                                     // 移動Tweenの終着点キャッシュ
		private Vector2 m_sizeTweenTarget;                                                     // サイズTweenの終着点キャッシュ
		private readonly Dictionary<RectTransform, TextMeshProUGUI> m_targetTextCache = new(); // ターゲット配下テキストのキャッシュ
		protected bool m_wasHidden = true;                                                     // 前フレームで非表示だったかどうか（瞬間移動判定用）
		private bool m_hasTargetNotification;
		private bool m_refreshTargetPending;
		#endregion

	    #region Lifecycle Methods
		protected virtual void OnEnable() {
			m_wasHidden = true;
			m_refreshTargetPending = m_hasTargetNotification;
		}

		protected virtual void OnDisable() {
			KillTweens();
			m_wasHidden = true;
		}

		/// <summary>ターゲットの移動に追従するため、設定に応じて位置とサイズを更新する</summary>
		private void LateUpdate() {
			if(m_refreshTargetPending) {
				OnTargetRectChanged(m_currentTargetRect);
			}
			if(m_updateMode == UpdateMode.EveryFrame || m_wasHidden) {
				UpdateCursor(m_currentTargetRect);
			}
		}

		/// <summary>破棄時に購読解除とTweenの破棄を行う</summary>
		protected virtual void OnDestroy() {
			KillTweens();
			m_targetTextCache.Clear();
		}
		#endregion

		#region Internal Logic
		/// <summary>選択対象が変更された際にカーソル追従の準備を行う</summary>
		/// <param name="targetRect">新しく選択されたRectTransform</param>
		protected virtual void OnTargetRectChanged(RectTransform targetRect) {
			bool wasNull = m_currentTargetRect == null;
			m_currentTargetRect = targetRect;
			m_hasTargetNotification = true;
			// 無効化中は最新の選択だけ記録し、表示やTransformは変更しない。
			m_refreshTargetPending = !isActiveAndEnabled;
			if(m_refreshTargetPending) return;

			if(m_cursorRect == null && m_cursorImage != null) {
				m_cursorRect = m_cursorImage.rectTransform;
			}

			// ターゲットがnullになった場合は非表示にする
			if(m_currentTargetRect == null) {
				KillTweens();
				m_wasHidden = true;
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
				// アンカー・Pivotを変更しても表示中の矩形を維持し、その位置から移動する。
				var center = m_cursorRect.TransformPoint(m_cursorRect.rect.center);
				var size = m_cursorRect.rect.size;
				m_cursorRect.anchorMin = m_currentTargetRect.anchorMin;
				m_cursorRect.anchorMax = m_currentTargetRect.anchorMax;
				m_cursorRect.pivot = m_currentTargetRect.pivot;
				ApplyCursorSize(size);
				m_cursorRect.position = center - m_cursorRect.TransformVector(m_cursorRect.rect.center);

				// アニメーションモードの場合、既存のTweenをリセットして再開させる準備をする
				if(m_moveMode == MoveMode.Animation) {
					KillTweens();
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
				// 自身まで無効化すると、選択解除後の通知で表示を復帰できなくなる。
				if(transform.IsChildOf(m_cursorImage.transform)) m_cursorImage.enabled = visible;
				else m_cursorImage.gameObject.SetActive(visible);
			}
		}

		/// <summary>カーソルの位置とサイズを選択対象に合わせる</summary>
		/// <param name="targetRect">ターゲットのRectTransform</param>
		protected virtual void UpdateCursor(RectTransform targetRect) {
			if(!isActiveAndEnabled || targetRect == null || m_cursorRect == null) return;

			TextMeshProUGUI textComponent = null;
			if(m_sizeMode == SizeMode.MatchText) {
				textComponent = GetCachedTextComponent(targetRect);
				if(textComponent != null && textComponent.havePropertiesChanged) textComponent.ForceMeshUpdate();
			}

			Vector3 targetWorldPos = targetRect.position;
			Vector2 targetSize = m_cursorRect.rect.size;
			if(m_sizeMode != SizeMode.Fixed) {
				var sizeRect = textComponent != null ? textComponent.rectTransform : targetRect;
				var bounds = textComponent != null
					? textComponent.textBounds
					: new Bounds(targetRect.rect.center, targetRect.rect.size);
				targetSize = GetCursorSpaceSize(sizeRect, bounds.size) + m_padding;
				// テキストの中央はPivotではない。カーソル自身のPivotと拡縮を反映する。
				var pivotOffset = Vector2.Scale(m_cursorRect.pivot - new Vector2(0.5f, 0.5f), targetSize);
				targetWorldPos = sizeRect.TransformPoint(bounds.center) + m_cursorRect.TransformVector(pivotOffset);
			}

			// 非表示状態から表示状態に遷移した場合は瞬間移動する
			bool shouldInstantMove = m_wasHidden || m_moveMode == MoveMode.Instant;

			if(shouldInstantMove) {
				m_moveTween?.Kill();
				m_cursorRect.position = targetWorldPos;
				m_wasHidden = false;
			} else {
				if(m_moveTween != null && m_moveTween.IsActive()) {
					// アニメーション中なら、ターゲットの位置が変わった時だけ終着点を更新する（追従）
					if((m_moveTweenTarget - targetWorldPos).sqrMagnitude > TweenTargetEpsilon) {
						m_moveTween.ChangeEndValue(targetWorldPos, true);
						m_moveTweenTarget = targetWorldPos;
					}
				} else if((m_cursorRect.position - targetWorldPos).sqrMagnitude > TweenTargetEpsilon) {
					// 目的地に未到達の場合のみ Tween を生成する（毎フレームの生成を防ぐ）
					m_moveTween = m_cursorRect.DOMove(targetWorldPos, m_moveDuration).SetEase(m_moveEase);
					m_moveTweenTarget = targetWorldPos;
				}
			}

			// サイズ変更
			if(m_sizeMode == SizeMode.MatchSelectable || m_sizeMode == SizeMode.MatchText) {
				if(shouldInstantMove) {
					m_sizeTween?.Kill();
					ApplyCursorSize(targetSize);
				} else {
					if(m_sizeTween != null && m_sizeTween.IsActive()) {
						// アニメーション中なら、ターゲットのサイズが変わった時だけ終着点を更新する（追従）
						if((m_sizeTweenTarget - targetSize).sqrMagnitude > TweenTargetEpsilon) {
							m_sizeTween.ChangeEndValue(targetSize, true);
							m_sizeTweenTarget = targetSize;
						}
					} else if((m_cursorRect.rect.size - targetSize).sqrMagnitude > TweenTargetEpsilon) {
						// 目標サイズに未到達の場合のみ Tween を生成する（毎フレームの生成を防ぐ）
						m_sizeTween = DOTween.To(() => m_cursorRect.rect.size, ApplyCursorSize, targetSize, m_sizeChangeDuration)
							.SetEase(m_sizeChangeEase);
						m_sizeTweenTarget = targetSize;
					}
				}
			}
		}

		private Vector2 GetCursorSpaceSize(RectTransform target, Vector3 size) {
			// カーソルの軸へ投影した幅・高さ。親階層や自身の拡縮・反転も含める。
			var matrix = m_cursorRect.worldToLocalMatrix * target.localToWorldMatrix;
			var x = matrix.MultiplyVector(new Vector3(size.x, 0f, 0f));
			var y = matrix.MultiplyVector(new Vector3(0f, size.y, 0f));
			return new Vector2(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y));
		}

		private void ApplyCursorSize(Vector2 size) {
			m_cursorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
			m_cursorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
		}

		private void KillTweens() {
			m_moveTween?.Kill();
			m_sizeTween?.Kill();
			m_moveTween = null;
			m_sizeTween = null;
		}

		/// <summary>ターゲット配下のテキストコンポーネントをキャッシュ付きで取得する</summary>
		/// <param name="targetRect">検索対象のRectTransform</param>
		/// <returns>キャッシュ済みまたは新規取得した aTextMeshProUgui</returns>
		private TextMeshProUGUI GetCachedTextComponent(RectTransform targetRect) {
			if(targetRect == null) return null;

			if(m_targetTextCache.TryGetValue(targetRect, out TextMeshProUGUI cachedText)
				&& cachedText != null && cachedText.transform.IsChildOf(targetRect)) {
				return cachedText;
			}

			// 未取得・破棄済み・別の親へ移動した参照は再検索する。
			// テキストが存在しないという結果を固定すると、後からの追加を検出できない。
			TextMeshProUGUI textComponent = targetRect.GetComponentInChildren<TextMeshProUGUI>(true);
			if(textComponent != null) m_targetTextCache[targetRect] = textComponent;
			else m_targetTextCache.Remove(targetRect);
			return textComponent;
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
