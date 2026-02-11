namespace ANest.UI {
	/// <summary>Null選択防止が有効なコンテナの共通インターフェース</summary>
	public interface IDisallowNullSelectionContainer {
		/// <summary>CurrentSelectableがNullになる事を許可しないかどうか</summary>
		bool DisallowNullSelection { get; }
	}
}
