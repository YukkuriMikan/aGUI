using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>
	/// aButton の設定を共通化するための ScriptableObject
	/// </summary>
	[CreateAssetMenu(fileName = "aSelectablesSharedParameters", menuName = "ANest/UI/aSelectables Shared Parameters")]
	public class aSelectablesSharedParameters : ScriptableObject {
		[Header("Transition")]
		public Selectable.Transition transition = Selectable.Transition.ColorTint;
		public ColorBlock transitionColors = ColorBlock.defaultColorBlock;
		public SpriteState spriteState;
		public AnimationTriggers selectableAnimationTriggers = new();

		[Header("Text Transition")]
		public TextTransitionType textTransition = TextTransitionType.TextColor;
		public ColorBlock textColors = ColorBlock.defaultColorBlock;
		public TextSwapState textSwapState;
		public AnimationTriggers textAnimationTriggers = new();

		[Header("Initial Guard")]
		public bool useInitialGuard = true;
		public float initialGuardDuration = 0.5f;

		[Header("Long Press")]
		public bool enableLongPress = false;
		public float longPressDuration = 0.5f;

		[Header("Multiple Input Guard")]
		public bool useMultipleInputGuard = true;
		public float multipleInputGuardInterval = 0.5f;
	}
}