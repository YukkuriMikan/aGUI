using System;
using UniRx;
using UnityEngine;

namespace ANest.UI {
	/// <summary> aLayoutGroupBase のレイアウト結果に合わせて自身のサイズを調整するフィッター </summary>
	[RequireComponent(typeof(aLayoutGroupBase))]
	[RequireComponent(typeof(RectTransform))]
	public class aContentSizeFitter : MonoBehaviour {
		#region SerializeField
		[SerializeField] private bool fitWidth;                 // 横方向をフィットさせるか
		[SerializeField] private bool fitHeight;                // 縦方向をフィットさせるか
		[SerializeField] private aLayoutGroupBase layoutGroup;  // 監視対象のレイアウトグループ
		#endregion

		#region Fields
		private IDisposable _subscription;         // レイアウト通知購読用
		private RectTransform _rectTransform;      // RectTransform キャッシュ
		private RectTransform RectTransform => _rectTransform ? _rectTransform : (_rectTransform = transform as RectTransform); // キャッシュプロパティ
		private Rect _lastLayoutRect;              // 直近のレイアウト領域
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
			HandleSelfFittingAlongAxis(0);
			HandleSelfFittingAlongAxis(1);
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
