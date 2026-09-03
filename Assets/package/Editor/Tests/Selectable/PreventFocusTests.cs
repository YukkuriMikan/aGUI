using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PreventFocusTests {
	private GameObject eventSystemObject;
	private EventSystem eventSystem;
	private GameObject previousObject;
	private Button previousButton;

	[SetUp]
	public void SetUp() {
		eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
		eventSystem = eventSystemObject.GetComponent<EventSystem>();
		previousObject = new GameObject("Previous", typeof(RectTransform), typeof(Button));
		previousButton = previousObject.GetComponent<Button>();
		eventSystem.SetSelectedGameObject(previousObject);
	}

	[TearDown]
	public void TearDown() {
		if(previousObject != null) Object.DestroyImmediate(previousObject);
		if(eventSystemObject != null) Object.DestroyImmediate(eventSystemObject);
	}

	[Test]
	public void Button_PreventFocus_KeepsSelectionAndClickBehavior() {
		var buttonObject = new GameObject("aButton", typeof(RectTransform), typeof(aButton));
		try {
			var button = buttonObject.GetComponent<aButton>();
			var configuredNavigation = new Navigation {
				mode = Navigation.Mode.Explicit,
				selectOnRight = previousButton
			};
			button.navigation = configuredNavigation;

			var clickCount = 0;
			button.onClick.AddListener(() => clickCount++);
			button.PreventFocus = true;

			Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));

			var pointer = new PointerEventData(eventSystem) {
				button = PointerEventData.InputButton.Left
			};
			button.OnPointerDown(pointer);
			button.OnPointerClick(pointer);

			Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(previousObject));
			Assert.That(clickCount, Is.EqualTo(1));

			button.Select();
			Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(previousObject));

			button.PreventFocus = false;
			Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
			Assert.That(button.navigation.selectOnRight, Is.EqualTo(previousButton));
		} finally {
			Object.DestroyImmediate(buttonObject);
		}
	}

	[Test]
	public void Toggle_PreventFocus_KeepsSelectionAndValueChangeBehavior() {
		var toggleObject = new GameObject("aToggle", typeof(RectTransform), typeof(aToggle));
		try {
			var toggle = toggleObject.GetComponent<aToggle>();
			var configuredNavigation = new Navigation {
				mode = Navigation.Mode.Explicit,
				selectOnLeft = previousButton
			};
			toggle.navigation = configuredNavigation;

			var valueChangedCount = 0;
			toggle.onValueChanged.AddListener(_ => valueChangedCount++);
			toggle.PreventFocus = true;

			Assert.That(toggle.navigation.mode, Is.EqualTo(Navigation.Mode.None));

			var pointer = new PointerEventData(eventSystem) {
				button = PointerEventData.InputButton.Left
			};
			toggle.OnPointerDown(pointer);
			toggle.OnPointerClick(pointer);

			Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(previousObject));
			Assert.That(toggle.isOn, Is.True);
			Assert.That(valueChangedCount, Is.EqualTo(1));

			toggle.Select();
			Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(previousObject));

			toggle.PreventFocus = false;
			Assert.That(toggle.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
			Assert.That(toggle.navigation.selectOnLeft, Is.EqualTo(previousButton));
		} finally {
			Object.DestroyImmediate(toggleObject);
		}
	}
}
