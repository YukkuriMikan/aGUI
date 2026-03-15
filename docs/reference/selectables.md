# Selectable Reference

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aButton` | 右クリック、長押し、連打ガード、テキスト遷移、ショートカット入力対応の拡張 Button。 | `OnRightClick`, `OnLongPress`, `OnLongPressCancel`, `LongPressProgress`, `TextSwapState`, `OnMove(...)` | [aButton.cs](../../Assets/package/Runtime/Selectables/aButton.cs) |
| `aToggle` | ON/OFF アニメーション、連打ガード、テキスト遷移、ショートカット入力対応の拡張 Toggle。 | `OnPointerClick(...)`, `OnSubmit(...)`, `OnMove(...)` | [aToggle.cs](../../Assets/package/Runtime/Selectables/aToggle.cs) |
| `aSelectablesSharedParameters` | `aButton` / `aToggle` の見た目・入力設定共有用 ScriptableObject。 | `transition`, `textTransition`, `enableLongPress`, `useMultipleInputGuard` など | [aSelectablesSharedParameters.cs](../../Assets/package/Runtime/Selectables/ScriptableObject/aSelectablesSharedParameters.cs) |
| `aGuiSelectableUtils` | Selectable ナビゲーション探索ユーティリティ。 | `FindInteractableSelectable(...)`, `FindSelectableInDirection(...)` | [aGuiSelectableUtils.cs](../../Assets/package/Runtime/Selectables/aGuiSelectableUtils.cs) |
| `IShortCut` | Selectable 用ショートカット入力インターフェース。 | `IsPressed` | [IShortCut.cs](../../Assets/package/Runtime/Selectables/Interfaces/IShortCut.cs) |
| `ISkipNavigationSelectable` | 非 Interactable をスキップするナビゲーション設定。 | `SkipNonInteractableNavigation` | [ISkipNavigationSelectable.cs](../../Assets/package/Runtime/Selectables/ISkipNavigationSelectable.cs) |
| `TextTransitionType` | テキスト遷移モード。 | `TextColor`, `TextSwap`, `TextAnimation` | [TextTransitionType.cs](../../Assets/package/Runtime/Selectables/TextTransitionType.cs) |
| `TextSwapState` | 選択状態ごとの差し替え文字列。 | `normalText`, `highlightedText`, `pressedText`, `selectedText`, `disabledText` | [TextSwapState.cs](../../Assets/package/Runtime/Selectables/TextSwapState.cs) |
