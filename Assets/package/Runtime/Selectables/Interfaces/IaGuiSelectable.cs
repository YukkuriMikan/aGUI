namespace ANest.UI {
	/// <summary>フォーカス抑制に対応するSelectable向けインターフェース</summary>
	public interface IPreventFocusSelectable {
		/// <summary>入力方法を問わずフォーカスを取得しないかどうか</summary>
		bool PreventFocus { get; set; }
	}

	/// <summary>Selectable向けの共通インターフェース（ガード付きクリック実行/InitialGuard/ナビゲーションスキップ）</summary>
	public interface IaGuiSelectable {
		/// <summary> 非Interactableをスキップして次のSelectableに移動するかどうか </summary>
		bool SkipNonInteractableNavigation { get; set; }

		/// <summary>InitialGuard中かどうか</summary>
		bool InitialGuardActive { get; set; }

		/// <summary>多重入力ガードを適用してクリック相当処理を実行する</summary>
		/// <returns>実行された場合はtrue、ガード等でブロックされた場合はfalse</returns>
		bool InvokeClickWithGuard();
	}
}
