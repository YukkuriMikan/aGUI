using UnityEngine;

namespace ANest.UI {
	/// <summary>
	/// IUiAnimation の設定を共通化するための ScriptableObject
	/// </summary>
	[CreateAssetMenu(fileName = "UiAnimationSet", menuName = "ANest/UI/UiAnimation Set")]
	public class UiAnimationSet : ScriptableObject {
		[Header("Animations")]
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] showAnimations; // 表示時のアニメーション
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] hideAnimations; // 非表示時のアニメーション
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] clickAnimations; // クリック時のアニメーション
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] onAnimations; // ON時のアニメーション
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] offAnimations; // OFF時のアニメーション
	}
}
