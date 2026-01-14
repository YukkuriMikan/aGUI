using System;
using UniRx;
using UnityEngine;

namespace ANest.UI {
	/// <summary> aLayoutGroupBase のレイアウト結果に合わせて自身のサイズを調整するフィッター </summary>
	[RequireComponent(typeof(aLayoutGroupBase))]
	[RequireComponent(typeof(RectTransform))]
	public class aContentSizeFitter : MonoBehaviour {
		public enum PivotType {
			UpperLeft,
			UpperCenter,
			UpperRight,
			MiddleLeft,
			MiddleCenter,
			MiddleRight,
			LowerLeft,
			LowerCenter,
			LowerRight
		}

		#region SerializeField
		[SerializeField] private bool fitWidth;                             // 横方向をフィットさせるか
		[SerializeField] private bool fitHeight;                            // 縦方向をフィットさせるか
		[SerializeField] private PivotType pivotType = PivotType.UpperLeft; // 基準点
		[SerializeField] private aLayoutGroupBase layoutGroup;              // 監視対象のレイアウトグループ
		#endregion

		#region Fields
		private IDisposable _subscription;                                                                                      // レイアウト通知購読用
		private RectTransform _rectTransform;                                                                                   // RectTransform キャッシュ
		private RectTransform RectTransform => _rectTransform ? _rectTransform : (_rectTransform = transform as RectTransform); // キャッシュプロパティ
		private Rect _lastLayoutRect;                                                                                           // 直近のレイアウト領域
		#endregion

		#region Unity Methods
		/// <summary> コンポーネントリセット時に参照を補完 </summary>
		private void Reset() {
			AssignLayoutGroup();
		}

		/// <summary> インスペクタ変更時に参照を補完して購読を張り直す </summary>
		private void OnValidate() {
			AssignLayoutGroup();
			Subscribe();
		}

		/// <summary> 有効化時に購読を開始 </summary>
		private void OnEnable() {
			AssignLayoutGroup();
			Subscribe();
		}

		/// <summary> 無効化時に購読を解除 </summary>
		private void OnDisable() {
			Unsubscribe();
		}

		/// <summary> 破棄時に購読を解除 </summary>
		private void OnDestroy() {
			Unsubscribe();
		}
		#endregion

		#region Methods
		/// <summary> layoutGroup が未設定なら自身から取得 </summary>
		private void AssignLayoutGroup() {
			if(layoutGroup == null) {
				layoutGroup = GetComponent<aLayoutGroupBase>();
			}
		}

		/// <summary> レイアウト完了通知の購読を開始 </summary>
		private void Subscribe() {
			Unsubscribe();
			if(layoutGroup == null) return;
			_subscription = layoutGroup.CompleteLayoutAsObservable.Subscribe(ApplyFitting);
		}

		/// <summary> レイアウト完了通知の購読を解除 </summary>
		private void Unsubscribe() {
			if(_subscription != null) {
				_subscription.Dispose();
				_subscription = null;
			}
		}

		/// <summary> 受け取ったレイアウト領域に合わせて自身のサイズを更新 </summary>
		private void ApplyFitting(Rect layoutRect) {
			if(RectTransform == null) return;
			_lastLayoutRect = layoutRect;

			// 指定された pivotType に基づいて RectTransform.pivot を更新
			Vector2 targetPivot = GetPivotVector(pivotType);

			// 現在の左上位置（アンカー基準）を計算しておく
			// anchoredPosition は現在のピボット位置を指しているので、左上に変換する
			Vector2 originalPivot = RectTransform.pivot;
			Vector2 originalAnchoredPosition = RectTransform.anchoredPosition;
			Vector2 currentPivot = RectTransform.pivot;
			Vector2 currentSize = RectTransform.rect.size;
			Vector2 currentTopLeft = RectTransform.anchoredPosition + new Vector2(-currentSize.x * currentPivot.x, currentSize.y * (1f - currentPivot.y));

			HandleSelfFittingAlongAxis(0);
			HandleSelfFittingAlongAxis(1);

			Vector2 newSize = RectTransform.rect.size;

			if(fitWidth || fitHeight) {
				// aLayoutGroupBase の layoutRect は、自身の左上を (0,0) とした座標系での配置範囲
				// (ただし EmitCompleteLayoutRect の実装上、x=左端, y=下端 となっている)

				// 新しい左上位置 = 現在の左上位置 + layoutRect.x(左端のズレ) と (layoutRect.y + layoutRect.height)(上端のズレ)
				float deltaX = layoutRect.x;
				float deltaY = layoutRect.y + layoutRect.height;

				Vector2 newTopLeft = currentTopLeft + new Vector2(deltaX, deltaY);

				// 新しい左上位置から、ターゲットピボットに応じた anchoredPosition を算出
				Vector2 targetAnchoredPos = newTopLeft + new Vector2(newSize.x * targetPivot.x, -newSize.y * (1f - targetPivot.y));

				// 一時的にターゲットピボットを適用して位置を計算・適用
				RectTransform.pivot = targetPivot;

				// 浮動小数点の誤差を考慮して非常に小さな値の変化は無視する
				if(Vector2.Distance(RectTransform.anchoredPosition, targetAnchoredPos) > 0.001f) {
					RectTransform.anchoredPosition = targetAnchoredPos;
				}

				// ピボットを元に戻す
				RectTransform.pivot = originalPivot;

				// ピボットを戻した後の anchoredPosition のズレを補正
				Vector2 finalAnchoredPos = newTopLeft + new Vector2(newSize.x * originalPivot.x, -newSize.y * (1f - originalPivot.y));
				if(Vector2.Distance(RectTransform.anchoredPosition, finalAnchoredPos) > 0.001f) {
					RectTransform.anchoredPosition = finalAnchoredPos;
				}
			} else {
				// サイズだけ変える場合でもピボットだけは更新して戻す（必要性はないが整合性のため）
				RectTransform.pivot = targetPivot;
				RectTransform.pivot = originalPivot;
			}
		}

		public void ApplyFitting()
			=> ApplyFitting(RectTransform.rect);

		/// <summary> PivotType を Vector2 の座標に変換 </summary>
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

		/// <summary> 指定軸方向にサイズをフィットさせる </summary>
		private void HandleSelfFittingAlongAxis(int axis) {
			bool fit = axis == 0 ? fitWidth : fitHeight;
			if(!fit) return;

			float size = axis == 0 ? _lastLayoutRect.width : _lastLayoutRect.height; // 最新レイアウトのサイズを使用
			RectTransform.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, size);
		}
		#endregion
	}
}
