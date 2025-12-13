using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace ANest.UI {
	/// <summary> 横・縦方向の線形レイアウト用共通基底クラス </summary>
	public abstract class aLayoutGroupLinear : aLayoutGroupBase {
		#region SerializeField
		[SerializeField] protected float spacing; // 要素間スペース
		#endregion

		#region Methods
		/// <summary> 線形方向のNavigationを設定 </summary>
		protected void ApplyNavigationLinear(List<RectTransform> order, bool isHorizontal) {
			if(!setNavigation) return;
			int n = order.Count;
			RectTransform prevSelectable = null;
			for (int i = 0; i < n; i++) {
				var current = order[i];
				if(current == null) continue;
				var selectable = current.GetComponent<Selectable>();
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
					var s = next.GetComponent<Selectable>();
					if(s != null) {
						nextSelectable = s;
						break;
					}
				}

				Selectable prev = prevSelectable != null ? prevSelectable.GetComponent<Selectable>() : null;

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
					var s = order[i] != null ? order[i].GetComponent<Selectable>() : null;
					if(s != null) {
						first = s;
						break;
					}
				}
				for (int i = n - 1; i >= 0; i--) {
					var s = order[i] != null ? order[i].GetComponent<Selectable>() : null;
					if(s != null) {
						last = s;
						break;
					}
				}
				if(first != null && last != null && first != last) {
					Navigation navFirst = first.navigation;
					Navigation navLast = last.navigation;
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
		#endregion
	}
}
