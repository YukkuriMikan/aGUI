using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace ANest.UI {
	/// <summary> 横・縦方向の線形レイアウト用共通基底クラス </summary>
	public abstract class aLayoutGroupLinear : aLayoutGroupBase {
		#region SerializeField
		[Tooltip("要素間スペース")]
		[SerializeField] protected float spacing; // 要素間スペース
		#endregion

		#region Fields
		protected readonly List<RectTransform> m_orderBuffer = new(); // 子の並び順を都度newせず再利用するバッファ
		protected float[] m_allocatedSlotsScaled = System.Array.Empty<float>();
		protected float[] m_finalWidths = System.Array.Empty<float>();
		protected float[] m_finalHeights = System.Array.Empty<float>();
		protected float[] m_crossPositions = System.Array.Empty<float>();
		#endregion

		#region Methods
		/// <summary>破棄済みの子だけを除去する。再収集せず、明示的な子の選択と順序を維持する。</summary>
		protected void RemoveMissingChildren() {
			var remaining = 0;
			for(var i = 0; i < rectChildren.Count; i++) {
				var child = rectChildren[i];
				if(child == null) {
					KillTween(child);
					if(!ReferenceEquals(child, null)) m_lastTargetPositions.Remove(child);
					continue;
				}
				if(remaining != i) rectChildren[remaining] = child;
				remaining++;
			}
			if(remaining < rectChildren.Count) rectChildren.RemoveRange(remaining, rectChildren.Count - remaining);
			if(remaining == 0) m_orderBuffer.Clear();
		}

		protected void EnsureLayoutBufferCapacity(int count) {
			if(m_finalWidths.Length >= count) return;
			var capacity = Mathf.NextPowerOfTwo(count);
			m_allocatedSlotsScaled = new float[capacity];
			m_finalWidths = new float[capacity];
			m_finalHeights = new float[capacity];
			m_crossPositions = new float[capacity];
		}

		/// <summary> 線形方向のNavigationを設定 </summary>
		protected void ApplyNavigationLinear(List<RectTransform> order, bool isHorizontal) {
			if(!setNavigation) return;
			
			var n = order.Count;
			RectTransform prevSelectable = null;
			
			for (int i = 0; i < n; i++) {
				var current = order[i];
				if(current == null) continue;
				var selectable = GetSelectable(current);
				if(selectable == null) {
					continue;
				}

				Navigation nav = selectable.navigation;
				nav.mode = Navigation.Mode.Explicit;

				// Find next selectable
				Selectable nextSelectable = null;
				
				for (int j = i + 1; j < n; j++) {
					var next = order[j];
					if(next == null) continue;
					var s = GetSelectable(next);
					if(s != null) {
						nextSelectable = s;
						break;
					}
				}

				var prev = GetSelectable(prevSelectable);

				if(isHorizontal) {
					nav.selectOnLeft = prev;
					nav.selectOnRight = nextSelectable;
					nav.selectOnUp = null;
					nav.selectOnDown = null;
				} else {
					nav.selectOnUp = prev;
					nav.selectOnDown = nextSelectable;
					nav.selectOnLeft = null;
					nav.selectOnRight = null;
				}

				selectable.navigation = nav;
				prevSelectable = current;
			}

			if(navigationLoop) {
				Selectable first = null;
				Selectable last = null;
				
				for (int i = 0; i < n; i++) {
					var s = GetSelectable(order[i]);
					if(s != null) {
						first = s;
						break;
					}
				}
				
				for (int i = n - 1; i >= 0; i--) {
					var s = GetSelectable(order[i]);
					if(s != null) {
						last = s;
						break;
					}
				}
				
				if(first != null && last != null && first != last) {
					var navFirst = first.navigation;
					var navLast = last.navigation;
					if(isHorizontal) {
						navFirst.selectOnLeft = last;
						navLast.selectOnRight = first;
					} else {
						navFirst.selectOnUp = last;
						navLast.selectOnDown = first;
					}
					first.navigation = navFirst;
					last.navigation = navLast;
				}
			}
		}

		/// <summary>現在の並び設定（reverseArrangement含む）で order バッファを再構築して返す</summary>
		protected List<RectTransform> BuildOrderBuffer(int count) {
			m_orderBuffer.Clear();
			if(m_orderBuffer.Capacity < count) m_orderBuffer.Capacity = count;
			for (int i = 0; i < count; i++) {
				int idx = reverseArrangement ? (count - 1 - i) : i;
				m_orderBuffer.Add(rectChildren[idx]); // 表示順でバッファへ詰める
			}
			return m_orderBuffer;
		}
		#endregion
	}
}
