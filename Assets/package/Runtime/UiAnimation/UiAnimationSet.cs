using UnityEngine;

namespace ANest.UI {
	/// <summary>
	/// IUiAnimation の設定を共通化するための ScriptableObject
	/// </summary>
	[CreateAssetMenu(fileName = "UiAnimationSet", menuName = "ANest/UI/UiAnimation Set")]
	public class UiAnimationSet : ScriptableObject {
		[Header("Animations")]
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] showAnimations;
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] hideAnimations;
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] clickAnimations;
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] onAnimations;
		[SerializeReference, SerializeReferenceDropdown]
		public IUiAnimation[] offAnimations;
	}
}