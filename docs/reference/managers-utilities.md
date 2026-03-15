# Manager / Utility Reference

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aGuiManager` | `EventSystem` の解決と選択履歴管理。 | `EventSystem`, `SetSelectedSelectable(...)`, `GoBack()`, `ClearSelectionHistory()`, `UpdateEventSystem()` | [aGuiManager.cs](../../Assets/package/Runtime/aGuiManager.cs) |
| `aContainerManager` | コンテナの登録・取得・優先度判定。 | `Add()`, `Remove()`, `GetContainer(...)`, `GetContainers<T>()`, `IsHighestPriorityDisallowNullSelectionContainer(...)` | [aContainerManager.cs](../../Assets/package/Runtime/Containers/aContainerManager.cs) |
| `aGuiUtils` | アニメーション再生とテキスト遷移ユーティリティ。 | `CloneAnimations(...)`, `PlayAnimation(...)`, `ApplyTextColorTransition(...)`, `ApplyTextSwapTransition(...)`, `ApplyTextAnimationTransition(...)` | [aGuiUtils.cs](../../Assets/package/Runtime/aGuiUtils.cs) |
| `aGuiExtensions` | aGUIで使用する拡張メソッド群。 | `AwaitCompletion(this Tween, ...)` | [aGuiExtensions.cs](../../Assets/package/Runtime/aGuiExtensions.cs) |
| `RectTransformValues` | RectTransform のスナップショット値。 | `CreateValues(...)`, `Apply(...)` | [RectTransformValues.cs](../../Assets/package/Runtime/RectTransformValues.cs) |
| `RectUtils` | `Rect` 用ユーティリティ。 | `Union(...)`, `TryUnion(...)` | [RectUtils.cs](../../Assets/package/Runtime/RectUtils.cs) |
| `SerializeReferenceDropdownAttribute` | `SerializeReference` 用の型選択補助 Attribute。 | `BaseType` | [SerializeReferenceDropdownAttribute.cs](../../Assets/package/Runtime/SerializeReferenceDropdownAttribute.cs) |
