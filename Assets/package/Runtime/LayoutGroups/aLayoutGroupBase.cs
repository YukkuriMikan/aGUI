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
			Manual,                     // 手動でのみ更新
			InitializeOnly,             // 初期化時のみ更新
			OnTransformChildrenChanged, // 子Transform変更時に更新
		}

		/// <summary> アニメーションの再生方式 </summary>
		public enum AnimationMode {
			Duration, // 再生時間を固定し、速度は距離によって変動する
			Speed,    // 速度を固定し、再生時間は距離によって変動する
		}

		/// <summary> レイアウトを更新するタイミング </summary>
		public enum UpdateTiming {
			Immediate,  // 即時実行
			Update,     // 次のUpdateで実行
			LateUpdate, // 次のLateUpdateで実行
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
		// 旧シーン・派生クラスとの互換用。自動整列は常にuGUI更新後に行う。
		[SerializeField, HideInInspector] protected UpdateTiming updateTiming = UpdateTiming.Immediate;
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
		[Tooltip("アニメーションの再生方式")]
		[SerializeField] protected AnimationMode animationMode = AnimationMode.Duration; // アニメーション再生方式
		[Tooltip("アニメーションの再生時間（秒）")]
		[SerializeField] protected float animationDuration = 0.25f; // アニメーション時間
		[Tooltip("アニメーションの移動速度（単位/秒）")]
		[SerializeField] protected float animationSpeed = 1000f; // アニメーション速度
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
		private bool m_isScheduled;
		private bool m_pendingWithCollection;
		private bool m_pendingInitialization;
		private uint m_layoutRequestVersion; // 古い待機処理を、実行せずに失効させる。

		private Subject<Rect> m_completeLayoutSubject = new(); // レイアウト完了通知Subject
		#endregion

		#region Properties
		/// <summary> 自身のRectTransformキャッシュを取得 </summary>
		protected RectTransform RectTransform => m_rectTransform ? m_rectTransform : (m_rectTransform = transform as RectTransform);

		/// <summary> レイアウト完了を通知するObservable </summary>
		public IObservable<Rect> CompleteLayoutAsObservable => m_completeLayoutSubject;

		/// <summary>少なくとも一度レイアウトが完了しているか（Fitterの再同期用）</summary>
		internal bool HasCompletedLayout { get; private set; }

		/// <summary> アニメーションの再生方式 </summary>
		public AnimationMode Mode => animationMode;

		/// <summary> アニメーションの再生時間（秒） </summary>
		public float AnimationDuration => animationDuration;

		/// <summary> アニメーションの移動速度（単位/秒） </summary>
		public float AnimationSpeed => animationSpeed;

		/// <summary> アニメーションを適用する距離の閾値 </summary>
		public float AnimationDistanceThreshold => animationDistanceThreshold;

		/// <summary> アニメーションのイージング設定 </summary>
		public Ease AnimationEase => animationEase;
		#endregion

		#region Unity Methods
		/// <summary> 有効化時の初期化 </summary>
		protected virtual void OnEnable() {
			CancelPendingLayout();
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
			CancelPendingLayout();
			KillAllTweens();
		}

		/// <summary> 破棄時にTweenを停止 </summary>
		protected virtual void OnDestroy() {
			CancelPendingLayout();
			KillAllTweens();
		}

		/// <summary> 子Transform変更時の処理 </summary>
		protected virtual void OnTransformChildrenChanged() {
			if(!isActiveAndEnabled) return;
			if(updateMode == UpdateMode.OnTransformChildrenChanged) {
				ScheduleLayout(true, false);
			}
		}

		/// <summary> RectTransform寸法変更時の処理 </summary>
		protected virtual void OnRectTransformDimensionsChange() {
			if(!isActiveAndEnabled) return;
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
		public void AlignWithCollectionNonAnimate() {
			AlignNonAnimate(true);
		}

		/// <summary>収集の有無を指定して、遅延・アニメーションなしで整列する。</summary>
		public void AlignNonAnimate(bool collectChildren) {
			if(!CanAlignNow()) return;
			CancelPendingLayout();

			bool previousSuppress = useAnimation; // 元の抑制状態を保存
			useAnimation = false;
			KillAllTweens();
			try {
				AlignWithCollectionCore(collectChildren);
			} finally {
				useAnimation = previousSuppress;
			}
		}

		/// <summary>子要素を収集して即時整列する。uGUI待ちにはAlignWithFrameWaitAndCollectionAsyncを使用する。</summary>
		public void AlignWithCollection() {
			if(!CanAlignNow()) return;

			CancelPendingLayout();
			AlignWithCollectionCore();
		}

		/// <summary> 子要素を収集して整列（即時実行本体） </summary>
		private void AlignWithCollectionCore(bool collectChildren = true) {
			m_lastTargetPositions.Clear();
			if(collectChildren) CollectRectChildren();
			CalculateLayout();
			CompleteLayout();
		}

		/// <summary>収集済みの子要素を即時整列する。uGUI待ちにはAlignWithFrameWaitAsyncを使用する。</summary>
		public void Align() {
			if(!CanAlignNow()) return;

			CancelPendingLayout();
			AlignCore();
		}

		/// <summary> 子要素を整列（即時実行本体） </summary>
		private void AlignCore() {
			m_lastTargetPositions.Clear();
			if(rectChildren.Count == 0) {
				CollectRectChildren();
			}
			CalculateLayout();
			CompleteLayout();
		}

		private bool CanAlignNow() {
			// 編集時の手動Rebuildは、無効なコンポーネントでも使用できる。
			return Application.isPlaying ? isActiveAndEnabled : gameObject.activeInHierarchy;
		}

		private bool HasUsableSize() {
			if(RectTransform == null) return false;
			var size = RectTransform.rect.size;
			return size.x > 0f && size.y > 0f;
		}

		private void CompleteLayout() {
			HasCompletedLayout = true;
			if(updateMode == UpdateMode.InitializeOnly && isActiveAndEnabled && HasUsableSize()) m_initialized = true;
			m_completeLayoutSubject.OnNext(CalculateContentRect());
		}

		/// <summary>フィット後の寸法で収集済みの子を再配置する。完了通知は再送せず、再帰的なフィットを防ぐ。</summary>
		internal void RecalculateLayoutAfterFitting() {
			var previousSuppress = m_suppressAnimation;
			m_suppressAnimation |= !Application.isPlaying;
			try {
				m_lastTargetPositions.Clear();
				CalculateLayout();
			} finally {
				m_suppressAnimation = previousSuppress;
			}
		}

		/// <summary>親の寸法が変わる前の座標系でTweenの開始値を確定する。</summary>
		internal void InitializeChildLayoutTween(RectTransform child) {
			if(m_positionTweens.TryGetValue(child, out var tween) && tween.IsActive()) tween.ForceInit();
		}

		/// <summary>アンカー補正に合わせて、保存済みの目標と進行中のTweenの座標系もずらす。</summary>
		internal virtual void OffsetChildLayoutTarget(RectTransform child, Vector2 offset) {
			if(m_lastTargetPositions.TryGetValue(child, out var target)) m_lastTargetPositions[child] = target + offset;
			if(m_positionTweens.TryGetValue(child, out var tween) &&
				tween is DG.Tweening.Core.TweenerCore<Vector2, Vector2, DG.Tweening.Plugins.Options.VectorOptions> positionTween && tween.IsActive()) {
				positionTween.startValue += offset;
				positionTween.endValue += offset;
			}
		}

		private void CancelPendingLayout() {
			unchecked { m_layoutRequestVersion++; }
			m_isScheduled = false;
			m_pendingWithCollection = false;
			m_pendingInitialization = false;
		}

		/// <summary>要求をまとめ、初期化・フレーム待ちはuGUI更新後に実行する。</summary>
		private void ScheduleLayout(bool withCollection, bool initialization) {
			if(!isActiveAndEnabled) return;
			m_pendingWithCollection |= withCollection;
			m_pendingInitialization |= initialization;
			if(m_isScheduled) return;
			unchecked { m_layoutRequestVersion++; }
			m_isScheduled = true;
			RunScheduledLayoutAsync(m_layoutRequestVersion).Forget();
		}

		private async UniTaskVoid RunScheduledLayoutAsync(uint version) {
			await UniTask.DelayFrame(1, PlayerLoopTiming.LastPostLateUpdate);
			if(!this || version != m_layoutRequestVersion || !isActiveAndEnabled) return;
			ExecuteScheduledLayout(version);
		}

		private void ExecuteScheduledLayout(uint version) {
			if(version != m_layoutRequestVersion || !m_isScheduled) return;
			var withCollection = m_pendingWithCollection;
			var initialization = m_pendingInitialization;
			m_isScheduled = false;
			m_pendingWithCollection = false;
			m_pendingInitialization = false;
			if(initialization) {
				// 予約時に正のサイズでも、uGUIの更新後に0へ戻っている場合がある。
				if(updateMode != UpdateMode.InitializeOnly || m_initialized || !HasUsableSize()) return;
				AlignNonAnimate(withCollection);
			} else if(withCollection) {
				AlignWithCollectionCore();
			} else {
				AlignCore();
			}
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
		public UniTask AlignWithFrameWaitAndCollectionAsync() => AlignAfterUGUIAsync(true);

		/// <summary> 1フレーム待ってから整列する </summary>
		public UniTask AlignWithFrameWaitAsync() => AlignAfterUGUIAsync(false);

		private async UniTask AlignAfterUGUIAsync(bool withCollection) {
			if(!isActiveAndEnabled) return;
			ScheduleLayout(withCollection, false);
			var version = m_layoutRequestVersion;
			await UniTask.DelayFrame(1, PlayerLoopTiming.LastPostLateUpdate);
			if(!this || version != m_layoutRequestVersion || !isActiveAndEnabled) return;
			ExecuteScheduledLayout(version);
		}

		/// <summary> 初期化条件を満たした際にレイアウトを構築 </summary>
		private void TryInit() {
			if(m_initialized || !isActiveAndEnabled || updateMode != UpdateMode.InitializeOnly || !HasUsableSize()) return;
			ScheduleLayout(true, true);
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

			// 範囲計算と同様、反転は無視してスケールの大きさだけで配置する。
			scaleX = Mathf.Abs(scaleX);
			scaleY = Mathf.Abs(scaleY);

			var anchorMin = rect.anchorMin;
			var anchorMax = rect.anchorMax;

			anchorMin.x = anchorMax.x = 0f;
			anchorMin.y = anchorMax.y = 1f;
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;

			rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sizeX);
			rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sizeY);

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

			var delta = rect.anchoredPosition - targetPos;
			var distance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
			var duration = CalculateAnimationDuration(distance);
			var shouldAnimate = useAnimation && !m_suppressAnimation && distance <= animationDistanceThreshold && duration > 0f;

			KillTween(rect);

			if(shouldAnimate) {
				StartPositionTween(rect, targetPos, duration);
			} else {
				rect.anchoredPosition = targetPos;
			}
		}

		// ラムダのキャプチャは、実際にアニメーションを開始するときだけ生成する。
		private void StartPositionTween(RectTransform rect, Vector2 targetPos, float duration) {
			var tween = DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, targetPos, duration);
			if(useAnimationCurve && animationCurve != null) tween.SetEase(animationCurve);
			else tween.SetEase(animationEase);
			tween.SetLink(rect.gameObject);
			m_positionTweens[rect] = tween;
		}

		/// <summary> アニメーション方式に応じた再生時間を算出 </summary>
		protected virtual float CalculateAnimationDuration(float distance) {
			switch (animationMode) {
				case AnimationMode.Speed:
					return animationSpeed > 0f ? distance / animationSpeed : 0f;
				case AnimationMode.Duration:
				default:
					return animationDuration;
			}
		}

		/// <summary> 指定RectTransformに紐づくTweenを停止 </summary>
		protected void KillTween(RectTransform rect) {
			// 破棄済みUnityオブジェクトも辞書のキーとして残るため、実際のnullだけを除外する。
			if(ReferenceEquals(rect, null)) return;

			if(m_positionTweens.TryGetValue(rect, out Tween tween)) {
				if(tween.IsActive()) tween.Kill();
				m_positionTweens.Remove(rect);
			}
		}

		/// <summary> 管理中の全Tweenを停止 </summary>
		private void KillAllTweens() {
			foreach (var kvp in m_positionTweens) {
				var tween = kvp.Value;
				if(tween != null && tween.IsActive()) tween.Kill();
			}
			m_positionTweens.Clear();
		}

		/// <summary> 子要素の範囲をRectとして取得 </summary>
		/// <returns>子要素が含まれるRect</returns>
		public Rect CalculateContentRect() {
			return CalculateContentRect(false);
		}

		/// <summary>収集の有無を指定し、現在位置または配置先からフィット範囲を計算する。</summary>
		internal Rect CalculateContentRectForFitting(bool useCurrentTransforms, bool collectChildren) {
			if(collectChildren) CollectRectChildren();
			return CalculateContentRect(useCurrentTransforms);
		}

		private Rect CalculateContentRect(bool useCurrentTransforms) {
			if(rectChildren.Count == 0) {
				return new Rect();
			}

			var minX = float.PositiveInfinity;
			var minY = float.PositiveInfinity;
			var maxX = float.NegativeInfinity;
			var maxY = float.NegativeInfinity;

			for (int i = 0; i < rectChildren.Count; i++) {
				var child = rectChildren[i];
				if(child == null) continue;

				Vector2 pos;
				if(useCurrentTransforms) {
					pos = child.localPosition;
				} else if(!m_lastTargetPositions.TryGetValue(child, out pos)) {
					pos = child.anchoredPosition;
				}

				var size = child.rect.size;
				var scale = child.localScale;
				var pivot = child.pivot;
				var rotation = child.localRotation;
				var width = size.x * Mathf.Abs(scale.x);
				var height = size.y * Mathf.Abs(scale.y);
				// スケールの符号は無視し、ピボット周りで回転した四隅の範囲を求める。
				for(var corner = 0; corner < 4; corner++) {
					var offset = new Vector3(
						((corner & 1) - pivot.x) * width,
						((corner >> 1) - pivot.y) * height, 0f);
					var rotated = rotation * offset;
					var x = pos.x + rotated.x;
					var y = pos.y + rotated.y;
					minX = Mathf.Min(minX, x);
					minY = Mathf.Min(minY, y);
					maxX = Mathf.Max(maxX, x);
					maxY = Mathf.Max(maxY, y);
				}
			}

			if(float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY)) {
				return new Rect();
			}

			// パディングを含めた親内側の境界も考慮したRectを作成
			var paddingLeft = padding != null ? padding.left : 0f;
			var paddingRight = padding != null ? padding.right : 0f;
			var paddingTop = padding != null ? padding.top : 0f;
			var paddingBottom = padding != null ? padding.bottom : 0f;

			var paddedMinX = minX - paddingLeft;
			var paddedMaxX = maxX + paddingRight;
			var paddedMinY = minY - paddingBottom;
			var paddedMaxY = maxY + paddingTop;

			var rect = Rect.MinMaxRect(paddedMinX, paddedMinY, paddedMaxX, paddedMaxY);
			return rect;
		}

		/// <summary>コンポーネント未装着時もEditorで割り当てを発生させずに取得する。</summary>
		protected static Selectable GetSelectable(RectTransform rect) {
			if(rect == null) return null;
			rect.TryGetComponent<Selectable>(out var selectable);
			return selectable;
		}

		/// <summary> レイアウト計算で使用する子サイズ情報 </summary>
		protected struct ChildSizes {
			public float min;
			public float preferred;
			public float flexible;
		}

		/// <summary> 子要素の最小/推奨/柔軟サイズを取得（制御フラグを考慮） </summary>
		protected void GetChildSizes(RectTransform child, int axis, bool controlSize, bool forceExpand, out ChildSizes sizes) {
			if(!controlSize) {
				float current = child.rect.size[axis];
				sizes = new ChildSizes { min = current, preferred = current, flexible = 0f };
				return;
			}
			float min = LayoutUtility.GetMinSize(child, axis);
			float preferred = LayoutUtility.GetPreferredSize(child, axis);
			float flexible = LayoutUtility.GetFlexibleSize(child, axis);

			if(forceExpand) {
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
		/// <summary>エディタ上での値変更時に子要素一覧を再収集する</summary>
		protected virtual void OnValidate() {
			if(Application.isPlaying) return;
			CollectRectChildren();
		}
#endif
	}
}
