using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UI;

namespace ANest.UI.Editor {
	/// <summary> uGUI Button を aButton へ移行するコンテキストメニュー </summary>
	public static class ButtonMigrationMenu {
		/// <summary> Button を aButton に変換 </summary>
		[MenuItem("CONTEXT/Button/Migrate to aButton")]
		private static void MigrateButton(MenuCommand command) {
			if(command.context is not Button src) return;
			var go = src.gameObject;
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Migrate to aButton");

			var snapshot = ButtonSnapshot.Create(src);

			Undo.DestroyObjectImmediate(src);

			var dst = Undo.AddComponent<aButton>(go);
			CopyButtonValues(snapshot, dst);

			EditorUtility.SetDirty(go);
			Undo.CollapseUndoOperations(group);
		}

		private static void CopyButtonValues(ButtonSnapshot snapshot, aButton dst) {
			if(dst == null) return;

			Undo.RecordObject(dst, "Copy Button Values");
			dst.interactable = snapshot.interactable;
			dst.targetGraphic = snapshot.targetGraphic;
			dst.transition = snapshot.transition;
			dst.colors = snapshot.colors;
			dst.spriteState = snapshot.spriteState;
			dst.animationTriggers = snapshot.animationTriggers;
			dst.navigation = snapshot.navigation;
			dst.onClick = snapshot.onClick;
		}

		private readonly struct ButtonSnapshot {
			public readonly bool interactable;
			public readonly Graphic targetGraphic;
			public readonly Selectable.Transition transition;
			public readonly ColorBlock colors;
			public readonly SpriteState spriteState;
			public readonly AnimationTriggers animationTriggers;
			public readonly Navigation navigation;
			public readonly Button.ButtonClickedEvent onClick;

			private ButtonSnapshot(Button src) {
				interactable = src.interactable;
				targetGraphic = src.targetGraphic;
				transition = src.transition;
				colors = src.colors;
				spriteState = src.spriteState;
				animationTriggers = src.animationTriggers;
				navigation = src.navigation;
				onClick = src.onClick;
			}

			public static ButtonSnapshot Create(Button src) {
				return src == null ? default : new ButtonSnapshot(src);
			}
		}
	}
}