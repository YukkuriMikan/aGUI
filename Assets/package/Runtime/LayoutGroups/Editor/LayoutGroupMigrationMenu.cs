using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	/// <summary> uGUI LayoutGroup を aLayoutGroup 系へ移行するコンテキストメニュー </summary>
	public static class LayoutGroupMigrationMenu {
		#region Menu Entries
		/// <summary> HorizontalLayoutGroup を aLayoutGroupHorizontal へ変換 </summary>
		[MenuItem("CONTEXT/HorizontalLayoutGroup/Migrate to aLayoutGroupHorizontal")]
		private static void MigrateHorizontal(MenuCommand command) {
			if(command.context is not HorizontalLayoutGroup src) return;
			var go = src.gameObject;
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Migrate to aLayoutGroupHorizontal");

			var dst = Undo.AddComponent<aLayoutGroupHorizontal>(go);
			CopyCommon(src, dst);
			CopyHorizontalVertical(src, dst);

			Undo.DestroyObjectImmediate(src);
			EditorUtility.SetDirty(go);
			Undo.CollapseUndoOperations(group);
		}

		/// <summary> VerticalLayoutGroup を aLayoutGroupVertical へ変換 </summary>
		[MenuItem("CONTEXT/VerticalLayoutGroup/Migrate to aLayoutGroupVertical")]
		private static void MigrateVertical(MenuCommand command) {
			if(command.context is not VerticalLayoutGroup src) return;
			var go = src.gameObject;
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Migrate to aLayoutGroupVertical");

			var dst = Undo.AddComponent<aLayoutGroupVertical>(go);
			CopyCommon(src, dst);
			CopyHorizontalVertical(src, dst);

			Undo.DestroyObjectImmediate(src);
			EditorUtility.SetDirty(go);
			Undo.CollapseUndoOperations(group);
		}

		/// <summary> GridLayoutGroup を aLayoutGroupGrid へ変換 </summary>
		[MenuItem("CONTEXT/GridLayoutGroup/Migrate to aLayoutGroupGrid")]
		private static void MigrateGrid(MenuCommand command) {
			if(command.context is not GridLayoutGroup src) return;
			var go = src.gameObject;
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Migrate to aLayoutGroupGrid");

			var dst = Undo.AddComponent<aLayoutGroupGrid>(go);
			CopyCommon(src, dst);
			CopyGrid(src, dst);

			Undo.DestroyObjectImmediate(src);
			EditorUtility.SetDirty(go);
			Undo.CollapseUndoOperations(group);
		}
		#endregion

		#region Copy Helpers
		/// <summary> 共通プロパティをコピー </summary>
		private static void CopyCommon(LayoutGroup src, aLayoutGroupBase dst) {
			if(src == null || dst == null) return;
			var so = new SerializedObject(dst);
			var paddingProp = so.FindProperty("padding");
			if(paddingProp != null && src.padding != null) {
				SetRectOffset(paddingProp, src.padding);
			}
			var childAlignmentProp = so.FindProperty("childAlignment");
			if(childAlignmentProp != null) childAlignmentProp.enumValueIndex = (int)src.childAlignment;

			if(src is HorizontalOrVerticalLayoutGroup hv) {
				var spacingProp = so.FindProperty("spacing");
				if(spacingProp != null) spacingProp.floatValue = hv.spacing;
				var reverseProp = so.FindProperty("reverseArrangement");
				if(reverseProp != null) reverseProp.boolValue = hv.reverseArrangement;
			} else if(src is GridLayoutGroup g) {
				var spacingProp = so.FindProperty("spacingXY");
				if(spacingProp != null) {
					var x = spacingProp.FindPropertyRelative("x");
					var y = spacingProp.FindPropertyRelative("y");
					if(x != null) x.floatValue = g.spacing.x;
					if(y != null) y.floatValue = g.spacing.y;
				}
			}
			so.ApplyModifiedPropertiesWithoutUndo();
		}

		/// <summary> Horizontal/VerticalLayoutGroup 固有のプロパティをコピー </summary>
		private static void CopyHorizontalVertical(HorizontalOrVerticalLayoutGroup src, aLayoutGroupBase dst) {
			if(src == null || dst == null) return;
			var so = new SerializedObject(dst);
			SetBool(so, "childControlWidth", src.childControlWidth);
			SetBool(so, "childControlHeight", src.childControlHeight);
			SetBool(so, "childScaleWidth", src.childScaleWidth);
			SetBool(so, "childScaleHeight", src.childScaleHeight);
			SetBool(so, "childForceExpandWidth", src.childForceExpandWidth);
			SetBool(so, "childForceExpandHeight", src.childForceExpandHeight);
			so.ApplyModifiedPropertiesWithoutUndo();
		}

		/// <summary> GridLayoutGroup 固有のプロパティをコピー </summary>
		private static void CopyGrid(GridLayoutGroup src, aLayoutGroupGrid dst) {
			if(src == null || dst == null) return;
			var so = new SerializedObject(dst);
			// reverseArrangement は uGUI GridLayoutGroup に存在しないのでスキップ

			var cornerProp = so.FindProperty("startCorner");
			if(cornerProp != null) cornerProp.enumValueIndex = (int)src.startCorner;

			var axisProp = so.FindProperty("startAxis");
			if(axisProp != null) axisProp.enumValueIndex = (int)src.startAxis;

			var cellSizeProp = so.FindProperty("cellSize");
			if(cellSizeProp != null) {
				var x = cellSizeProp.FindPropertyRelative("x");
				var y = cellSizeProp.FindPropertyRelative("y");
				if(x != null) x.floatValue = src.cellSize.x;
				if(y != null) y.floatValue = src.cellSize.y;
			}

			var spacingProp = so.FindProperty("spacingXY");
			if(spacingProp != null) {
				var x = spacingProp.FindPropertyRelative("x");
				var y = spacingProp.FindPropertyRelative("y");
				if(x != null) x.floatValue = src.spacing.x;
				if(y != null) y.floatValue = src.spacing.y;
			}

			var constraintProp = so.FindProperty("constraint");
			if(constraintProp != null) constraintProp.enumValueIndex = (int)src.constraint;

			var countProp = so.FindProperty("constraintCount");
			if(countProp != null) countProp.intValue = src.constraintCount;

			so.ApplyModifiedPropertiesWithoutUndo();
		}
		#endregion

		#region Utility
		/// <summary> bool プロパティを安全に設定 </summary>
		private static void SetBool(SerializedObject so, string propertyName, bool value) {
			var prop = so.FindProperty(propertyName);
			if(prop != null) prop.boolValue = value;
		}

		/// <summary> RectOffset を SerializedProperty にコピー </summary>
		private static void SetRectOffset(SerializedProperty prop, RectOffset value) {
			if(prop == null || value == null) return;
			var left = prop.FindPropertyRelative("m_Left");
			var right = prop.FindPropertyRelative("m_Right");
			var top = prop.FindPropertyRelative("m_Top");
			var bottom = prop.FindPropertyRelative("m_Bottom");
			if(left != null) left.intValue = value.left;
			if(right != null) right.intValue = value.right;
			if(top != null) top.intValue = value.top;
			if(bottom != null) bottom.intValue = value.bottom;
		}
		#endregion
	}
}
