namespace ANest.UI {
	/// <summary>InitialGuard中の入力ブロック状態を外部から注入可能なSelectableのインターフェース</summary>
	public interface IInitialGuardSelectable {
		/// <summary>InitialGuard中かどうか</summary>
		bool InitialGuardActive { get; set; }
	}
}