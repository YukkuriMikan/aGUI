using System;

namespace ANest.UI {
	/// <summary> テキスト差し替え用のステート値 </summary>
	[Serializable]
	public struct TextSwapState {
		public string normalText;      // 通常時テキスト
		public string highlightedText; // ハイライト時テキスト
		public string pressedText;     // 押下時テキスト
		public string selectedText;    // 選択時テキスト
		public string disabledText;    // 無効時テキスト
	}
}
