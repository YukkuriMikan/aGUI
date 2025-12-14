using System;
using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>
	/// aButton の設定を共通化するための ScriptableObject
	/// </summary>
	[CreateAssetMenu(fileName = "aButtonSharedParameters", menuName = "ANest/UI/aButton Shared Parameters")]
	public class aButtonSharedParameters : ScriptableObject {
		[Header("Initial Guard")]
		public bool useInitialGuard = true;
		public float initialGuardDuration = 0.5f;

		[Header("Long Press")]
		public bool enableLongPress = false;
		public float longPressDuration = 0.5f;

		[Header("Multiple Input Guard")]
		public bool useMultipleInputGuard = true;
		public float multipleInputGuardInterval = 0.5f;

		[Header("Text Transition")]
		public TextTransitionType textTransition = TextTransitionType.TextColor;
		public ColorBlock textColors = ColorBlock.defaultColorBlock;
		public TextSwapState textSwapState;
		public AnimationTriggers textAnimationTriggers = new();

		[Header("Animation")]
		public bool useCustomAnimation;
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] clickAnimations;
	}
}