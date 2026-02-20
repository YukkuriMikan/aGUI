using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>aButton・aToggle共通のSelectable探索ユーティリティ</summary>
	public static class aGuiSelectableUtils {
		/// <summary>指定方向にあるInteractableなSelectableを探索する</summary>
		public static Selectable FindInteractableSelectable(Selectable origin, MoveDirection direction) {
			if(direction == MoveDirection.None) return null;

			var visited = new HashSet<Selectable> { origin };
			var current = origin;

			while(true) {
				var next = FindSelectableInDirection(current, direction);

				if(next == null) return null;
				if(!visited.Add(next)) return null;

				if(next.IsActive() && next.IsInteractable()) {
					return next;
				}

				current = next;
			}
		}

		/// <summary>方向に応じて次のSelectableを取得する（非Interactableも対象）</summary>
		public static Selectable FindSelectableInDirection(Selectable current, MoveDirection direction) {
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
				MoveDirection.Left when (navigation.mode & Navigation.Mode.Horizontal) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.left),
				MoveDirection.Right when (navigation.mode & Navigation.Mode.Horizontal) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.right),
				MoveDirection.Up when (navigation.mode & Navigation.Mode.Vertical) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.up),
				MoveDirection.Down when (navigation.mode & Navigation.Mode.Vertical) != 0 => FindSelectableWithoutInteractableFilter(current, current.transform.rotation * Vector3.down),
				_ => null
			};
		}

		/// <summary>Interactable判定を除外したSelectable探索</summary>
		public static Selectable FindSelectableWithoutInteractableFilter(Selectable current, Vector3 dir) {
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

			var selectables = Selectable.allSelectablesArray;
			for(int i = 0; i < selectables.Length; ++i) {
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
