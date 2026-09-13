using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aButton・aToggle共通のSelectable探索ユーティリティ</summary>
	public static class aGuiSelectableUtils {
		// 探索中に独自Selectableから再入されても、作業データを共有しない。
		private sealed class SearchBuffer {
			public readonly HashSet<Selectable> Visited = new();
			public Selectable[] Selectables = System.Array.Empty<Selectable>();
			public int Count = -1;
			public void Collect() {
				// フォーカス判定中の追加・有効化も、次の探索から反映する。
				var count = Selectable.allSelectableCount;
				if(Selectables.Length < count) Selectables = new Selectable[Mathf.NextPowerOfTwo(count)];
				var previousCount = Count;
				Count = Selectable.AllSelectablesNoAlloc(Selectables);
				if(previousCount > Count) System.Array.Clear(Selectables, Count, previousCount - Count);
			}
			public void Clear() {
				Visited.Clear();
				if(Count > 0) System.Array.Clear(Selectables, 0, Count);
				Count = -1;
			}
		}
		private static readonly ObjectPool<SearchBuffer> s_searchBuffers = new(
			() => new SearchBuffer(), actionOnRelease: buffer => buffer.Clear());

		/// <summary>指定したSelectableがフォーカスを取得できるかどうか</summary>
		public static bool CanReceiveFocus(Selectable selectable) {
			if(selectable == null) return false;
			return selectable is not IPreventFocusSelectable focusSelectable || !focusSelectable.PreventFocus;
		}

		/// <summary>指定方向にあるInteractableなSelectableを探索する</summary>
		public static Selectable FindInteractableSelectable(Selectable origin, MoveDirection direction) {
			if(direction == MoveDirection.None) return null;

			var buffer = s_searchBuffers.Get();
			try {
				var visited = buffer.Visited;
				visited.Add(origin);
				var current = origin;

				while(true) {
					var next = FindSelectableInDirection(current, direction, buffer);

					if(next == null) return null;
					if(!visited.Add(next)) return null;

					if(next.IsActive() && next.IsInteractable() && CanReceiveFocus(next)) {
						return next;
					}

					current = next;
				}
			} finally { s_searchBuffers.Release(buffer); }
		}

		/// <summary>方向に応じて次のSelectableを取得する（非Interactableも対象）</summary>
		public static Selectable FindSelectableInDirection(Selectable current, MoveDirection direction) {
			var buffer = s_searchBuffers.Get();
			try { return FindSelectableInDirection(current, direction, buffer); }
			finally { s_searchBuffers.Release(buffer); }
		}

		private static Selectable FindSelectableInDirection(Selectable current, MoveDirection direction, SearchBuffer buffer) {
			if(current == null) return null;
			var navigation = current.navigation;

			if(navigation.mode == Navigation.Mode.Explicit) {
				return direction switch {
					MoveDirection.Left => navigation.selectOnLeft,
					MoveDirection.Right => navigation.selectOnRight,
					MoveDirection.Up => navigation.selectOnUp,
					MoveDirection.Down => navigation.selectOnDown,
					_ => null
				};
			}

			return direction switch {
				MoveDirection.Left when (navigation.mode & Navigation.Mode.Horizontal) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.left, buffer),
				MoveDirection.Right when (navigation.mode & Navigation.Mode.Horizontal) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.right, buffer),
				MoveDirection.Up when (navigation.mode & Navigation.Mode.Vertical) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.up, buffer),
				MoveDirection.Down when (navigation.mode & Navigation.Mode.Vertical) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.down, buffer),
				_ => null
			};
		}

		/// <summary>Interactable判定を除外したSelectable探索</summary>
		public static Selectable FindSelectableWithoutInteractableFilter(Selectable current, Vector3 dir) {
			var buffer = s_searchBuffers.Get();
			try { return FindSelectableWithoutInteractableFilter(current, dir, buffer); }
			finally { s_searchBuffers.Release(buffer); }
		}

		private static Selectable FindSelectableWithoutInteractableFilter(Selectable current, Vector3 dir, SearchBuffer buffer) {
			dir = dir.normalized;
			Vector3 localDir = Quaternion.Inverse(current.transform.rotation) * dir;
			Vector3 pos = current.transform.TransformPoint(GetPointOnRectEdge(current.transform as RectTransform, localDir));
			float maxScore = Mathf.NegativeInfinity;
			float maxFurthestScore = Mathf.NegativeInfinity;
			float score = 0f;
			var navigation = current.navigation;
			bool wantsWrapAround = navigation.wrapAround && (navigation.mode == Navigation.Mode.Vertical || navigation.mode == Navigation.Mode.Horizontal);

			Selectable bestPick = null;
			Selectable bestFurthestPick = null;

			buffer.Collect();
			var selectables = buffer.Selectables;
			for(int i = 0; i < buffer.Count; ++i) {
				Selectable sel = selectables[i];
				if(sel == null || sel == current) continue;
				if(sel.navigation.mode == Navigation.Mode.None) continue;

				var selRect = sel.transform as RectTransform;
				Vector3 selCenter = selRect != null ? (Vector3)selRect.rect.center : Vector3.zero;
				Vector3 myVector = sel.transform.TransformPoint(selCenter) - pos;
				float dot = Vector3.Dot(dir, myVector);

				if(wantsWrapAround && dot < 0) {
					score = -dot * myVector.sqrMagnitude;
					if(score > maxFurthestScore) {
						maxFurthestScore = score;
						bestFurthestPick = sel;
					}
					continue;
				}

				if(dot <= 0) continue;
				score = dot / myVector.sqrMagnitude;
				if(score > maxScore) {
					maxScore = score;
					bestPick = sel;
				}
			}

			if(wantsWrapAround && bestPick == null) return bestFurthestPick;
			return bestPick;
		}

		/// <summary>RectTransformのエッジ上の点を取得する</summary>
		public static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir) {
			if(rect == null) return Vector3.zero;
			if(dir != Vector2.zero) {
				dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
			}
			dir = rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
			return dir;
		}
	}
}
