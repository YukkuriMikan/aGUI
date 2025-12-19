using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ANest.UI {
	/// <summary> 子要素を円形に配置するレイアウトグループ </summary>
	public class aLayoutGroupCircular : aLayoutGroupBase {
		/// <summary> 円周移動時の進行方向 </summary>
		public enum CircularMoveType {
			ShortestDistance,
			Clockwise,
			CounterClockwise
		}

		/// <summary> Navigation の入力解釈タイプ </summary>
		public enum NavigationType {
			Default,       // 既存のバンド判定ロジック
			TwoAxis,       // 左右:前後, 上下:反対側
			TwoAxisReverse // 左右を反転しつつ上下は反対側
		}

		#region SerializeField
		[SerializeField] private float radius = 100f;                                                   // 円の半径（0以下なら利用可能サイズに合わせて決定）
		[SerializeField] private float startAngle = 0f;                                                 // 配置開始角度（度数法、時計回り正）
		[SerializeField] private float endAngle = 360f;                                                 // 配置終了角度（度数法、時計回り正）
		[SerializeField] private float spacing;                                                         // 要素間の角度間隔（度数法）
		[SerializeField] private bool childForceExpand = true;                                          // 子要素を均等角度で配置するか
		[SerializeField] private Vector2 centerOffset = Vector2.zero;                                   // ピボット基準からの中心オフセット
		[SerializeField] private bool useCircularMove = true;                                           // アニメーション時に円周上を移動させるか
		[SerializeField] private CircularMoveType circularMoveType = CircularMoveType.ShortestDistance; // 円周移動方向
		[SerializeField] private float navigationAxisRange = 10f;                                       // 方向判定に使う軸周囲の角度幅（片側度数）
		[SerializeField] private NavigationType navigationType = NavigationType.Default;                // Navigation の入力解釈
		[SerializeField] private RectTransform headStartTarget;                                         // 頭出し対象
		#endregion

	    #region Fields
		private readonly System.Collections.Generic.Dictionary<RectTransform, CircularTarget> _circularTargets = new();
		#endregion

		#region Properties
		/// <summary>開始角度と終了角度を同時に設定するオフセット</summary>
		public float AngleOffset {
			get => startAngle;
			set {
				startAngle = value;
				endAngle = value;
			}
		}
		#endregion

		#region Structs
		private struct CircularTarget {
			public Vector2 CenterAnchored;
			public float Radius;
			public float TargetAngleRad;
		}
		#endregion

		#region Methods
		public void HeadStartEditor() {
			if(!isActiveAndEnabled) return; // 無効時は処理しない

			bool previousSuppress = useAnimation; // 元の抑制状態を保存
			useAnimation = false;
			HeadStart();

			useAnimation = previousSuppress;
		}

		/// <summary>指定の子要素（未指定なら現在選択中）を開始位置に合わせて整列する</summary>
		public void HeadStart(RectTransform target = null) {
			int count = rectChildren.Count;
			if(count == 0) return;

			RectTransform ResolveSelectedChild() {
				var current = EventSystem.current?.currentSelectedGameObject;
				if(current == null) return null;
				Transform t = current.transform;
				while (t != null && t != transform) {
					if(t.parent == transform && t is RectTransform rt) {
						return rt;
					}
					t = t.parent;
				}
				return null;
			}

			target ??= headStartTarget;
			target ??= ResolveSelectedChild();
			if(target == null) return;

			int index = rectChildren.IndexOf(target);
			if(index < 0) return;
			if(count == 1) {
				AlignWithCollection();
				return;
			}

			bool clockwise = !reverseArrangement;
			float direction = clockwise ? 1f : -1f;
			float alignmentOffset = GetAlignmentAngleOffset();
			float alignedStartAngle = startAngle + alignmentOffset;
			float alignedEndAngle = endAngle + alignmentOffset;

			float angleStep;
			if(childForceExpand) {
				float span = direction > 0f
					? Mathf.Repeat(alignedEndAngle - alignedStartAngle, 360f)
					: Mathf.Repeat(alignedStartAngle - alignedEndAngle, 360f);
				bool fullCircle = Mathf.Approximately(span, 0f) || Mathf.Approximately(span, 360f);
				float spanToUse = fullCircle ? 360f : span;
				float divisor = fullCircle ? count : (count - 1);
				angleStep = (spanToUse / divisor) * direction;
			} else if(Mathf.Approximately(spacing, 0f)) {
				angleStep = 0f;
			} else {
				angleStep = Mathf.Abs(spacing) * direction;
			}

			float delta = -angleStep * index;
			startAngle += delta;
			endAngle += delta;

			AlignWithCollection();
		}

		private float GetAlignmentAngleOffset() {
			switch(childAlignment) {
				case TextAnchor.UpperLeft: return -45f;
				case TextAnchor.UpperCenter: return 0f;
				case TextAnchor.UpperRight: return 45f;
				case TextAnchor.MiddleLeft: return 270f;
				case TextAnchor.MiddleCenter: return 0f;
				case TextAnchor.MiddleRight: return 90f;
				case TextAnchor.LowerLeft: return 225f;
				case TextAnchor.LowerCenter: return 180f;
				case TextAnchor.LowerRight: return 135f;
				default: return 0f;
			}
		}
		/// <summary>
		/// 子要素を開始角度から終了角度まで等間隔で円周上に配置するレイアウト計算。
		/// パディング・アライメント・反転配置・スケールを考慮し、必要に応じてアニメーションも適用する。
		/// </summary>
		protected override void CalculateLayout() {
			if(RectTransform == null) return;

			int count = rectChildren.Count;
			if(count == 0) return;

			_circularTargets.Clear();
			System.Collections.Generic.List<RectTransform> navOrder = setNavigation ? new System.Collections.Generic.List<RectTransform>(count) : null;

			float width = RectTransform.rect.width;
			float height = RectTransform.rect.height;

			Vector2 pivot = RectTransform.pivot;
			Vector2 center = new Vector2(width * pivot.x, height * (1f - pivot.y)) + centerOffset;
			float alignmentOffset = GetAlignmentAngleOffset();
			float alignedStartAngle = startAngle + alignmentOffset;
			float alignedEndAngle = endAngle + alignmentOffset;

			float leftSpace = center.x - padding.left;
			float rightSpace = (width - padding.right) - center.x;
			float topSpace = center.y - padding.top;
			float bottomSpace = (height - padding.bottom) - center.y;
			float maxRadius = Mathf.Max(0f, Mathf.Min(Mathf.Min(leftSpace, rightSpace), Mathf.Min(topSpace, bottomSpace)));
			float actualRadius = radius > 0f ? Mathf.Min(radius, maxRadius) : maxRadius;


			bool clockwise = !reverseArrangement;
			float direction = clockwise ? 1f : -1f;
			float angleStep;
			if(count > 1) {
				if(childForceExpand) {
					float span = direction > 0f
						? Mathf.Repeat(alignedEndAngle - alignedStartAngle, 360f)
						: Mathf.Repeat(alignedStartAngle - alignedEndAngle, 360f);
					bool fullCircle = Mathf.Approximately(span, 0f) || Mathf.Approximately(span, 360f);
					float spanToUse = fullCircle ? 360f : span;
					float divisor = fullCircle ? count : (count - 1);
					angleStep = (spanToUse / divisor) * direction;
				} else if(Mathf.Approximately(spacing, 0f)) {
					angleStep = 0f;
				} else {
					angleStep = Mathf.Abs(spacing) * direction;
				}
			} else {
				angleStep = 0f;
			}

			for (int i = 0; i < count; i++) {
				var child = rectChildren[i];
				if(child == null) continue;

				GetChildSizes(child, 0, childControlWidth, childForceExpandWidth, out var sizeX);
				GetChildSizes(child, 1, childControlHeight, childForceExpandHeight, out var sizeY);
				float scaleX = childScaleWidth ? child.localScale.x : 1f;
				float scaleY = childScaleHeight ? child.localScale.y : 1f;

				float childWidth = childControlWidth ? sizeX.preferred : sizeX.preferred;
				float childHeight = childControlHeight ? sizeY.preferred : sizeY.preferred;

				float angle = alignedStartAngle + angleStep * i;
				float rad = (90f - angle) * Mathf.Deg2Rad;
				float posX = center.x + Mathf.Cos(rad) * actualRadius;
				float posY = center.y - Mathf.Sin(rad) * actualRadius;

				float alignedPosX = posX - childWidth * child.pivot.x * scaleX;
				float alignedPosY = posY - childHeight * (1f - child.pivot.y) * scaleY;

				_circularTargets[child] = new CircularTarget {
					CenterAnchored = new Vector2(center.x, -center.y),
					Radius = actualRadius,
					TargetAngleRad = rad
				};

				SetChildAlongBothAxes(child, alignedPosX, alignedPosY, childWidth, childHeight, scaleX, scaleY);

				if(navOrder != null) {
					navOrder.Add(child);
				}
			}

			if(navOrder != null) {
				ApplyNavigationCircular(navOrder, new Vector2(center.x, -center.y));
			}
		}

		/// <summary> 円形配置を考慮したNavigation設定を適用 </summary>
		private void ApplyNavigationCircular(System.Collections.Generic.List<RectTransform> order, Vector2 centerAnchored) {
			if(!setNavigation) return;

			int n = order.Count;
			var selectables = new Selectable[n];
			var angles = new float[n];

			for (int i = 0; i < n; i++) {
				var rect = order[i];
				if(rect == null) continue;
				selectables[i] = rect.GetComponent<Selectable>();

				if(_circularTargets.TryGetValue(rect, out var info)) {
					angles[i] = Mathf.Repeat(90f - info.TargetAngleRad * Mathf.Rad2Deg, 360f); // 0度=上, 時計回り
				} else {
					Vector2 pos = _lastTargetPositions.TryGetValue(rect, out var targetPos) ? targetPos : rect.anchoredPosition;
					Vector2 dir = pos - centerAnchored;
					float rad = Mathf.Atan2(-dir.y, dir.x);
					angles[i] = Mathf.Repeat(90f - rad * Mathf.Rad2Deg, 360f);
				}
			}

			Selectable FindAdjacent(int selfIndex, int step) {
				for (int k = 1; k < n; k++) {
					int idx = selfIndex + step * k;
					if(navigationLoop) {
						idx = (idx % n + n) % n;
					} else if(idx < 0 || idx >= n) {
						break;
					}

					var candidate = selectables[idx];
					if(candidate != null) return candidate;
				}
				return null;
			}

			bool InBand(float angleDeg, float centerDeg, float halfRange) {
				return Mathf.Abs(Mathf.DeltaAngle(angleDeg, centerDeg)) <= halfRange;
			}


			Selectable FindOpposite(int selfIndex, float currentAngle) {
				if(n <= 1) return null;
				float targetAngle = Mathf.Repeat(currentAngle + 180f, 360f);
				Selectable best = null;
				float bestDelta = float.MaxValue;
				for (int j = 0; j < n; j++) {
					if(j == selfIndex) continue;
					var candidate = selectables[j];
					if(candidate == null) continue;
					float delta = Mathf.Abs(Mathf.DeltaAngle(angles[j], targetAngle));
					if(delta < bestDelta) {
						bestDelta = delta;
						best = candidate;
					}
				}
				return best;
			}

			float halfRange = Mathf.Max(0f, navigationAxisRange);
			float rightMin = 90f - halfRange;
			float rightMax = 90f + halfRange;
			float downMin = 180f - halfRange;
			float downMax = 180f + halfRange;
			float leftMin = 270f - halfRange;
			float leftMax = 270f + halfRange;

			for (int i = 0; i < n; i++) {
				var selectable = selectables[i];
				if(selectable == null) continue;

				float angle = angles[i];
				var prev = FindAdjacent(i, -1);
				var next = FindAdjacent(i, 1);

				if(reverseArrangement) {
					(var tempPrev, var tempNext) = (prev, next);
					prev = tempNext;
					next = tempPrev;
				}

				Selectable up;
				Selectable down;
				Selectable left;
				Selectable right;

				switch(navigationType) {
					case NavigationType.TwoAxis:
						left = prev;
						right = next;
						up = FindOpposite(i, angle);
						down = up;
						break;
					case NavigationType.TwoAxisReverse:
						left = next;
						right = prev;
						up = FindOpposite(i, angle);
						down = up;
						break;
					default:
						if(InBand(angle, 0f, halfRange)) {
							up = null;
							down = null;
							left = prev;
							right = next;
						} else if(InBand(angle, 90f, halfRange)) {
							up = prev;
							down = next;
							left = null;
							right = null;
						} else if(InBand(angle, 180f, halfRange)) {
							up = null;
							down = null;
							left = next;
							right = prev;
						} else if(InBand(angle, 270f, halfRange)) {
							up = next;
							down = prev;
							left = null;
							right = null;
						} else if(angle < rightMin) {
							// Between Up band and Right band (例:11-79)
							up = prev;
							left = prev;
							right = next;
							down = next;
						} else if(angle < rightMax) {
							// Right band already handled, but keep safety
							up = prev;
							left = null;
							right = null;
							down = next;
						} else if(angle < downMin) {
							// Between Right band and Down band (例:101-169)
							up = prev;
							left = next;
							right = prev;
							down = next;
						} else if(angle < downMax) {
							up = null;
							left = next;
							right = prev;
							down = null;
						} else if(angle < leftMin) {
							// Between Down band and Left band (例:191-259)
							up = next;
							left = next;
							right = prev;
							down = prev;
						} else if(angle < leftMax) {
							up = next;
							left = null;
							right = null;
							down = prev;
						} else if(angle < 360f - halfRange) {
							// Between Left band and Up band (例:281-349)
							up = next;
							left = prev;
							right = next;
							down = prev;
						} else {
							// Wrap-around near 360 where Up band would have matched; treat as Up band gap if any
							up = prev;
							left = prev;
							right = next;
							down = next;
						}
						break;
				}

				Navigation nav = selectable.navigation;
				nav.mode = Navigation.Mode.Explicit;
				nav.selectOnUp = up;
				nav.selectOnDown = down;
				nav.selectOnLeft = left;
				nav.selectOnRight = right;
				selectable.navigation = nav;
			}
		}

		/// <summary> 必要に応じて円周移動のアニメーションを適用 </summary>
		protected override void ApplyPosition(RectTransform rect, Vector2 targetPos) {
			if(!useAnimation || !useCircularMove || !_circularTargets.TryGetValue(rect, out var info)) {
				base.ApplyPosition(rect, targetPos);
				return;
			}

			_lastTargetPositions[rect] = targetPos;

			Vector2 delta = rect.anchoredPosition - targetPos;
			float distance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
			bool shouldAnimate = !_positionTweens.ContainsKey(rect) && distance <= animationDistanceThreshold && animationDuration > 0f;
			if(IsAnimationSuppressed || !shouldAnimate) {
				base.ApplyPosition(rect, targetPos);
				return;
			}

			KillTween(rect);

			float currentAngleRad;
			{
				Vector2 currentVec = rect.anchoredPosition - info.CenterAnchored;
				currentAngleRad = Mathf.Atan2(-currentVec.y, currentVec.x);
			}

			float currentDeg = currentAngleRad * Mathf.Rad2Deg;
			float targetDeg = info.TargetAngleRad * Mathf.Rad2Deg;
			float endDeg;
			switch(circularMoveType) {
				case CircularMoveType.Clockwise:
					endDeg = currentDeg + Mathf.Repeat(targetDeg - currentDeg, 360f);
					break;
				case CircularMoveType.CounterClockwise:
					endDeg = currentDeg - Mathf.Repeat(currentDeg - targetDeg, 360f);
					break;
				default:
					float deltaDeg = Mathf.DeltaAngle(currentDeg, targetDeg);
					endDeg = currentDeg + deltaDeg;
					break;
			}

			Tween tween = DG.Tweening.DOTween.To(
				() => currentDeg,
				v => {
					float rad = v * Mathf.Deg2Rad;
					Vector2 pos = info.CenterAnchored + new Vector2(Mathf.Cos(rad) * info.Radius, Mathf.Sin(rad) * info.Radius);
					rect.anchoredPosition = pos;
				},
				endDeg,
				animationDuration
				);
			if(useAnimationCurve && animationCurve != null) {
				tween.SetEase(animationCurve);
			} else {
				tween.SetEase(animationEase);
			}
			tween.SetLink(rect.gameObject);
			tween.OnComplete(() => rect.anchoredPosition = targetPos);
			_positionTweens[rect] = tween;
		}
		#endregion
	}
}
