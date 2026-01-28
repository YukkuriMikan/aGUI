namespace ANest.UI {
	/// <summary>ショートカット入力を定義するインターフェース</summary>
	public interface IShortCut {
		/// <summary>ショートカットキーが押下されているかどうか</summary>
		public bool IsPressed { get; }
	}
}
