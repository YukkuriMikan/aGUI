using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UniRx;

namespace ANest.UI {
	/// <summary>LayoutGroupを継承せず子RectTransformを直接制御し、uGUI LayoutGroup相当の配置・アニメーション・Navigation設定を提供する基底クラス。</summary>
	[DisallowMultipleComponent]
	public abstract class aLayoutGroupBase : MonoBehaviour {
		/// <summary> レイアウトを更新するタイミングの種類 </summary>
		public enum UpdateMode {
			Manual,                    // 手動でのみ更新
			InitializeOnly,            // 初期化時のみ更新
			OnTransformChildrenChanged // 子Transform変更時に更新
		}

		#region SerializeField
		[Tooltip("対象となる子RectTransform一覧")]
		[SerializeField] protected List<RectTransform> rectChildren = new(); // 対象となる子RectTransform一覧
		[Tooltip("配置時に考慮するパディング")]
		[SerializeField] protected RectOffset padding = new RectOffset(); // パディング
		[Tooltip("子要素の配置基準")]
		[SerializeField] protected TextAnchor childAlignment = TextAnchor.MiddleCenter; // 子の配置基準
		[Tooltip("並び順を反転するか")]
		[SerializeField] protected bool reverseArrangement; // 並び順を反転するか
		[Tooltip("レイアウトを更新するタイミング")]
		[SerializeField] protected UpdateMode updateMode = UpdateMode.Manual; // レイアウト更新モード
		[Tooltip("子の幅を制御するか")]
		[SerializeField] protected bool childControlWidth = false; // 子幅を制御するか
		[Tooltip("子の高さを制御するか")]
		[SerializeField] protected bool childControlHeight = false; // 子高さを制御するか
		[Tooltip("子の幅にスケールを反映するか")]
		[SerializeField] protected bool childScaleWidth; // 子幅にスケールを反映するか
		[Tooltip("子の高さにスケールを反映するか")]
		[SerializeField] protected bool childScaleHeight; // 子高さにスケールを反映するか
		[Tooltip("子の幅を強制的に拡張するか")]
		[SerializeField] protected bool childForceExpandWidth = true; // 子幅を強制拡張するか
		[Tooltip("子の高さを強制的に拡張するか")]
		[SerializeField] protected bool childForceExpandHeight = true; // 子高さを強制拡張するか
		[Tooltip("レイアウト計算から除外する子Transformリスト")]
		[SerializeField] protected List<RectTransform> excludedChildren = new(); // 除外する子Transform
		[Tooltip("Navigationを設定するか")]
		[SerializeField] protected bool setNavigation = true; // ナビゲーションを設定するか
		[Tooltip("Navigationをループさせるか")]
		[SerializeField] protected bool navigationLoop = true; // ナビゲーションをループさせるか
		[Tooltip("レイアウト移動をアニメーションさせるか")]
		[SerializeField] protected bool useAnimation; // レイアウト移動をアニメーションするか
		[Tooltip("アニメーションの再生時間（秒）")]
		[SerializeField] protected float animationDuration = 0.25f; // アニメーション時間
		[Tooltip("アニメーションを適用する距離の閾値")]
		[SerializeField] protected float animationDistanceThreshold = 1000f; // アニメ適用距離閾値
		[Tooltip("アニメーションでカーブを使用するか")]
		[SerializeField] protected bool useAnimationCurve; // カーブを使うか
		[Tooltip("アニメーションに使用するカーブ")]
		[SerializeField] protected AnimationCurve animationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f); // アニメーションカーブ
		[Tooltip("アニメーションのイージング設定")]
		[SerializeField] protected Ease animationEase = Ease.OutQuad; // アニメーションイージング
		#endregion

		#region Fields
		[NonSerialized] protected readonly Dictionary<RectTransform, Tween> m_positionTweens = new();        // 位置Tween管理
		[NonSerialized] protected readonly Dictionary<RectTransform, Vector2> m_lastTargetPositions = new(); // 最終ターゲット位置
		[NonSerialized] protected bool m_suppressAnimation;                                                  // アニメーション抑制フラグ
		private RectTransform m_rectTransform;                                                               // 自身のRectTransformキャッシュ
		private bool m_initialized;                                                                          // 初期化済みか
		private bool m_dirty;                                                                                // 再計算が必要か

		private Subject<Rect> m_completeLayoutSubject = new(); // レイアウト完了通知Subject
		#endregion

		#region Properties
		/// <summary> 自身のRectTransformキャッシュを取得 </summary>
		protected RectTransform RectTransform => m_rectTransform ? m_rectTransform : (m_rectTransform = transform as RectTransform);

		/// <summary> レイアウト完了を通知するObservable </summary>
		public IObservable<Rect> CompleteLayoutAsObservable => m_completeLayoutSubject;

		/// <summary> アニメーションの再生時間（秒） </summary>
		public float AnimationDuration => animationDuration;

		/// <summary> アニメーションを適用する距離の閾値 </summary>
		public float AnimationDistanceThreshold => animationDistanceThreshold;

		/// <summary> アニメーションのイージング設定 </summary>
		public Ease AnimationEase => animationEase;
		#endregion

		#region Unity Methods
		/// <summary> 有効化時の初期化 </summary>
		protected virtual void OnEnable() {
			m_initialized = false;
			m_dirty = false;
			if(updateMode == UpdateMode.InitializeOnly) {
				TryInit();
			}
		}

		/// <summary> 無効化時に状態をリセット </summary>
		protected virtual void OnDisable() {
			m_initialized = false;
			m_dirty = false;
			KillAllTweens();
		}

		/// <summary> 破棄時にTweenを停止 </summary>
		protected virtual void OnDestroy() {
			KillAllTweens();
		}

		/// <summary> 子Transform変更時の処理 </summary>
		protected virtual void OnTransformChildrenChanged() {
			if(updateMode == UpdateMode.OnTransformChildrenChanged) {
				AlignWithFrameWaitAndCollectionAsync().Forget();
			}
		}

		/// <summary> RectTransform寸法変更時の処理 </summary>
		protected virtual void OnRectTransformDimensionsChange() {
			if(!gameObject.activeSelf) return;
			if(updateMode != UpdateMode.InitializeOnly) return;
			m_dirty = true;
		}

		/// <summary> InitializeOnly モード時にサイズ確定を待ってから初期化をトリガー </summary>
		private void LateUpdate() {
			if(updateMode != UpdateMode.InitializeOnly) return; // 他モードでは不要
			if(!m_dirty) return;                                // 変化がなければ何もしない
			m_dirty = false;
			TryInit(); // サイズが確定したタイミングで初期化
		}
		#endregion

		#region Methods
		/// <summary> アニメーションを強制無効化してレイアウトを適用 </summary>
		[ContextMenu("Rebuild Layout")]
		public void AlignEditor() {
			if(!gameObject.activeSelf) return; // 無効時は処理しない

			bool previousSuppress = useAnimation; // 元の抑制状態を保存
			useAnimation = false;
			KillAllTweens();
			AlignWithCollection();
			useAnimation = previousSuppress;
		}

		/// <summary> 子要素を収集して整列 </summary>
		public void AlignWithCollection() {
			if(!gameObject.activeSelf) return; // 無効時は処理しない

			m_lastTargetPositions.Clear();
			CollectRectChildren();
			CalculateLayout();
			m_completeLayoutSubject.OnNext(CalculateContentRect());
		}

		/// <summary> 子要素を整列 </summary>
		public void Align() {
			if(!gameObject.activeSelf) return; // 無効時は処理しない

			m_lastTargetPositions.Clear();
			if(rectChildren.Count == 0) {
				CollectRectChildren();
			}
			CalculateLayout();
			m_completeLayoutSubject.OnNext(CalculateContentRect());
		}

		/// <summary> rectChildrenへ子要素を追加 </summary>
		public void AddRectChild(RectTransform child) {
			if(!IsRectChildValid(child)) return;
			if(rectChildren.Contains(child)) return;
			rectChildren.Add(child);
		}

		/// <summary> rectChildrenへ複数の子要素を追加 </summary>
		public void AddRectChildren(IEnumerable<RectTransform> children) {
			if(children == null) return;
			foreach (var child in children) {
				AddRectChild(child);
			}
		}

		/// <summary> rectChildrenから子要素を削除 </summary>
		public bool RemoveRectChild(RectTransform child) {
			if(child == null) return false;
			KillTween(child);
			m_lastTargetPositions.Remove(child);
			return rectChildren.Remove(child);
		}

		/// <summary> rectChildrenをクリア </summary>
		public void ClearRectChildren() {
			for (int i = 0; i < rectChildren.Count; i++) {
				KillTween(rectChildren[i]);
			}
			rectChildren.Clear();
			m_lastTargetPositions.Clear();
		}

		/// <summary> 1フレーム待ってから整列する </summary>
		public async UniTask AlignWithFrameWaitAndCollectionAsync() {
			await UniTask.DelayFrame(1);

			AlignWithCollection();
		}

		/// <summary> 1フレーム待ってから整列する </summary>
		public async UniTask AlignWithFrameWaitAsync() {
			await UniTask.DelayFrame(1);

			Align();
		}

		/// <summary> 初期化条件を満たした際にレイアウトを構築 </summary>
		private void TryInit() {
			if(m_initialized) return;
			if(updateMode != UpdateMode.InitializeOnly) return;
			if(RectTransform == null) return;
			var size = RectTransform.rect.size;
			if(size.x <= 1f || size.y <= 1f) return;
			m_initialized = true;
			AlignEditor();
		}

		/// <summary> レイアウト対象となる子RectTransformを収集 </summary>
		protected void CollectRectChildren() {
			rectChildren.Clear();
			if(RectTransform == null) return;
			for (int i = 0; i < transform.childCount; i++) {
				var child = transform.GetChild(i) as RectTransform;
				if(!IsRectChildValid(child)) continue;
				rectChildren.Add(child);
			}
		}

		/// <summary> rectChildrenへ追加可能な子要素かを判定 </summary>
		private bool IsRectChildValid(RectTransform child) {
			if(child == null || !child.gameObject.activeSelf) return false;
			if(excludedChildren != null && excludedChildren.Contains(child)) return false;
			return true;
		}

		/// <summary> 指定軸での配置揃え値を取得（0:左/上、0.5:中央、1:右/下） </summary>
		protected float GetAlignmentOnAxis(int axis) {
			int column = (int)childAlignment % 3;
			int row = (int)childAlignment / 3;
			if(axis == 0) return column * 0.5f; // left=0, middle=0.5, right=1
			return row * 0.5f;                  // upper=0, middle=0.5, lower=1
		}

		/// <summary> パディングとアライメントを考慮した開始位置を算出 </summary>
		protected float GetStartOffset(int axis, float requiredSpaceWithoutPadding) {
			float paddingStart = axis == 0 ? padding.left : padding.top;
			float paddingEnd = axis == 0 ? padding.right : padding.bottom;
			float availableSpace = RectTransform.rect.size[axis] - paddingStart - paddingEnd;
			float surplusSpace = availableSpace - requiredSpaceWithoutPadding;
			float alignmentOnAxis = GetAlignmentOnAxis(axis);
			return paddingStart + surplusSpace * alignmentOnAxis;
		}

		/// <summary> 子RectTransformを両軸方向に配置しサイズを設定 </summary>
		protected void SetChildAlongBothAxes(RectTransform rect, float posX, float posY, float sizeX, float sizeY, float scaleX = 1f, float scaleY = 1f) {
			if(rect == null) return;

			Vector2 anchorMin = rect.anchorMin;
			Vector2 anchorMax = rect.anchorMax;
			anchorMin.x = anchorMax.x = 0f;
			anchorMin.y = anchorMax.y = 1f;
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;

			Vector2 sizeDelta = rect.sizeDelta;
			sizeDelta.x = sizeX;
			sizeDelta.y = sizeY;
			rect.sizeDelta = sizeDelta;

			Vector2 targetPos = new Vector2(
				posX + sizeX * rect.pivot.x * scaleX,
				-(posY + sizeY * (1f - rect.pivot.y) * scaleY)
				);

			ApplyPosition(rect, targetPos);
		}

		/// <summary> 必要に応じてアニメーションしつつ位置を適用 </summary>
		protected virtual void ApplyPosition(RectTransform rect, Vector2 targetPos) {
			if(rect == null) return;
			m_lastTargetPositions[rect] = targetPos;

			Vector2 delta = rect.anchoredPosition - targetPos;
			float distance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
			bool shouldAnimate = useAnimation && !m_suppressAnimation && distance <= animationDistanceThreshold && animationDuration > 0f;

			KillTween(rect);

			if(shouldAnimate) {
				Tween tween = DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, targetPos, animationDuration);
				if(useAnimationCurve && animationCurve != null) {
					tween.SetEase(animationCurve);
				} else {
					tween.SetEase(animationEase);
				}
				tween.SetLink(rect.gameObject);
				m_positionTweens[rect] = tween;
			} else {
				rect.anchoredPosition = targetPos;
			}
		}

		/// <summary> 指定RectTransformに紐づくTweenを停止 </summary>
		protected void KillTween(RectTransform rect) {
			if(rect == null) return;
			if(m_positionTweens.TryGetValue(rect, out Tween tween)) {
				if(tween.IsActive()) tween.Kill();
				m_positionTweens.Remove(rect);
			}
		}

		/// <summary> 管理中の全Tweenを停止 </summary>
		private void KillAllTweens() {
			foreach (var kvp in m_positionTweens) {
				Tween tween = kvp.Value;
				if(tween != null && tween.IsActive()) tween.Kill();
			}
			m_positionTweens.Clear();
		}

		/// <summary> 子要素の範囲をRectとして取得 </summary>
		/// <returns>子要素が含まれるRect</returns>
		public Rect CalculateContentRect() {
			if(rectChildren.Count == 0) {
				return new Rect();
			}

			float minX = float.PositiveInfinity;
			float minY = float.PositiveInfinity;
			float maxX = float.NegativeInfinity;
			float maxY = float.NegativeInfinity;

			for (int i = 0; i < rectChildren.Count; i++) {
				var child = rectChildren[i];
				if(child == null) continue;

				Vector2 pos;
				if(!m_lastTargetPositions.TryGetValue(child, out pos)) {
					pos = child.anchoredPosition;
				}

				float width = child.sizeDelta.x * Mathf.Abs(child.localScale.x);
				float height = child.sizeDelta.y * Mathf.Abs(child.localScale.y);
				float left = pos.x - width * child.pivot.x;
				float right = left + width;
				float bottom = pos.y - height * child.pivot.y;
				float top = bottom + height;

				if(left < minX) minX = left;
				if(bottom < minY) minY = bottom;
				if(right > maxX) maxX = right;
				if(top > maxY) maxY = top;
			}

			if(float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY)) {
				return new Rect();
			}

			// パディングを含めた親内側の境界も考慮したRectを作成
			float paddingLeft = padding != null ? padding.left : 0f;
			float paddingRight = padding != null ? padding.right : 0f;
			float paddingTop = padding != null ? padding.top : 0f;
			float paddingBottom = padding != null ? padding.bottom : 0f;

			float paddedMinX = minX - paddingLeft;
			float paddedMaxX = maxX + paddingRight;
			float paddedMinY = minY - paddingTop;
			float paddedMaxY = maxY + paddingBottom;

			var rect = Rect.MinMaxRect(paddedMinX, paddedMinY, paddedMaxX, paddedMaxY);
			return rect;
		}

		/// <summary> レイアウト計算で使用する子サイズ情報 </summary>
		protected struct ChildSizes {
			public float min;
			public float preferred;
			public float flexible;
		}

		/// <summary> 子要素の最小/推奨/柔軟サイズを取得（制御フラグを考慮） </summary>
		protected void GetChildSizes(RectTransform child, int axis, bool controlSize, bool forceExpand, out ChildSizes sizes) {
			float min = LayoutUtility.GetMinSize(child, axis);
			float preferred = LayoutUtility.GetPreferredSize(child, axis);
			float flexible = LayoutUtility.GetFlexibleSize(child, axis);

			if(!controlSize) {
				float current = child.rect.size[axis];
				min = preferred = current;
				flexible = 0f;
			} else if(forceExpand) {
				flexible = Mathf.Max(flexible, 1f);
			}

			sizes = new ChildSizes {
				min = min,
				preferred = preferred,
				flexible = flexible
			};
		}

		/// <summary> 収集済みのrectChildrenを用いて位置・サイズを決定し、必要に応じてNavigationやアニメーションを適用する抽象メソッド。 </summary>
		protected abstract void CalculateLayout();
		#endregion

#if UNITY_EDITOR
		protected virtual void OnValidate() {
			if(Application.isPlaying) return;
			CollectRectChildren();
		}
#endif
	}
}
