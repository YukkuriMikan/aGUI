using ANest.UI;
using UnityEngine;

public class MouseShortcut : IShortCut {
	public bool IsPressed => Input.GetMouseButton(0);
}
