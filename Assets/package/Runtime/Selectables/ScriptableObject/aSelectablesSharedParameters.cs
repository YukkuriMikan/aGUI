using UnityEngine;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>
	/// aButton の設定を共通化するための ScriptableObject
	/// </summary>
	[CreateAssetMenu(fileName = "aSelectablesSharedParameters", menuName = "ANest/UI/aSelectables Shared Parameters")]
	public class aSelectablesSharedParameters : ScriptableObject {
		[Header("Transition")]
		public Selectable.Transition transition = Selectable.Transition.ColorTint; // 遷移タイプ
		public ColorBlock transitionColors = ColorBlock.defaultColorBlock;         // 遷移カラー設定
		public SpriteState spriteState;                                            // スプライト遷移設定
		public AnimationTriggers selectableAnimationTriggers = new();              // アニメーショントリガー

		[Header("Text Transition")]
		public TextTransitionType textTransition = TextTransitionType.TextColor; // テキスト遷移タイプ
		public ColorBlock textColors = ColorBlock.defaultColorBlock;             // テキストカラー設定
		public TextSwapState textSwapState;                                      // テキスト差し替え設定
		public AnimationTriggers textAnimationTriggers = new();                  // テキストアニメーショントリガー

		[Header("Long Press")]
		public bool enableLongPress = false;   // 長押しを有効にするか
		public float longPressDuration = 0.5f; // 長押し成立時間（秒）

		[Header("Multiple Input Guard")]
		public bool useMultipleInputGuard = true;       // 連打ガードを使用するか
		public float multipleInputGuardInterval = 0.5f; // 連打ガード間隔（秒）
	}
}
