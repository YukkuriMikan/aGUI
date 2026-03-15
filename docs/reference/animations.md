# Animation Reference

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `IUiAnimation` | aGUI の UI アニメーション共通インターフェース。 | `Delay`, `Duration`, `IsYoYo`, `Curve`, `Ease`, `UseCurve`, `DoAnimate(...)` | [IUiAnimation.cs](../../Assets/package/Runtime/UiAnimation/Interfaces/IUiAnimation.cs) |
| `Fade` | `Graphic` の Alpha フェード。 | `DoAnimate(...)` | [Fade.cs](../../Assets/package/Runtime/UiAnimation/Fade.cs) |
| `FadeCanvasGroup` | `CanvasGroup` の Alpha フェード。 | `DoAnimate(...)` | [FadeCanvasGroup.cs](../../Assets/package/Runtime/UiAnimation/FadeCanvasGroup.cs) |
| `Move` | `RectTransform` の位置移動。 | `DoAnimate(...)` | [Move.cs](../../Assets/package/Runtime/UiAnimation/Move.cs) |
| `MoveTarget` | 任意ターゲット座標へ移動。 | `DoAnimate(...)` | [MoveTarget.cs](../../Assets/package/Runtime/UiAnimation/MoveTarget.cs) |
| `Rotate` | 回転アニメーション。 | `DoAnimate(...)` | [Rotate.cs](../../Assets/package/Runtime/UiAnimation/Rotate.cs) |
| `RotateTarget` | 任意ターゲット角度へ回転。 | `DoAnimate(...)` | [RotateTarget.cs](../../Assets/package/Runtime/UiAnimation/RotateTarget.cs) |
| `UiAnimationSet` | Show/Hide/Click/On/Off のアニメーション配列をまとめる ScriptableObject。 | `showAnimations`, `hideAnimations`, `clickAnimations`, `onAnimations`, `offAnimations` | [UiAnimationSet.cs](../../Assets/package/Runtime/UiAnimation/ScriptableObjects/UiAnimationSet.cs) |
