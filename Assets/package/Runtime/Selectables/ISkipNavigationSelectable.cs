namespace ANest.UI {
	/// <summary>ナビゲーションスキップ設定を外部から注入可能なSelectableのインターフェース</summary>
	public interface ISkipNavigationSelectable {
		/// <summary> 非Interactableをスキップして次のSelectableに移動するかどうか </summary>
		bool SkipNonInteractableNavigation { get; set; }
	}
}
