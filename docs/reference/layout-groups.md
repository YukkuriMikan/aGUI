# Layout Group Reference

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aLayoutGroupBase` | aGUI レイアウト基底。子収集、配置、ナビゲーション設定、レイアウトアニメーションを提供。 | `AlignWithCollection()`, `Align()`, `AddRectChild()`, `ClearRectChildren()`, `CompleteLayoutAsObservable`, `CalculateContentRect()` | [aLayoutGroupBase.cs](../../Assets/package/Runtime/LayoutGroups/aLayoutGroupBase.cs) |
| `aLayoutGroupLinear` | 横/縦の線形レイアウト基底。 | `spacing`（SerializeField）、線形ナビゲーション適用 | [aLayoutGroupLinear.cs](../../Assets/package/Runtime/LayoutGroups/aLayoutGroupLinear.cs) |
| `aLayoutGroupHorizontal` | 横方向レイアウト。 | (基底クラス API を利用) | [aLayoutGroupHorizontal.cs](../../Assets/package/Runtime/LayoutGroups/aLayoutGroupHorizontal.cs) |
| `aLayoutGroupVertical` | 縦方向レイアウト。 | (基底クラス API を利用) | [aLayoutGroupVertical.cs](../../Assets/package/Runtime/LayoutGroups/aLayoutGroupVertical.cs) |
| `aLayoutGroupGrid` | グリッドレイアウト。セルサイズ・開始位置・制約数を指定可能。 | `Corner`, `Axis`, `Constraint` | [aLayoutGroupGrid.cs](../../Assets/package/Runtime/LayoutGroups/aLayoutGroupGrid.cs) |
| `aLayoutGroupCircular` | 円形レイアウト。角度範囲、半径、円弧移動アニメーション、円形ナビゲーション対応。 | `StartAngle`, `EndAngle`, `Radius`, `AngleOffset`, `CircularMoveType`, `NavigationType` | [aLayoutGroupCircular.cs](../../Assets/package/Runtime/LayoutGroups/aLayoutGroupCircular.cs) |
| `aContentSizeFitter` | `aLayoutGroupBase` の計算結果 `Rect` に合わせて自分のサイズを調整。 | `ApplyFitting()`, `PivotType` | [aContentSizeFitter.cs](../../Assets/package/Runtime/LayoutGroups/aContentSizeFitter.cs) |
| `aTextMeshSizeFitter` | `TextMeshPro` のテキストサイズに合わせてサイズ調整。 | `ApplyFitting()`, `PivotType` | [aTextMeshSizeFitter.cs](../../Assets/package/Runtime/LayoutGroups/aTextMeshSizeFitter.cs) |

## 主要 Enum

- `aLayoutGroupBase.UpdateMode`: `Manual`, `InitializeOnly`, `OnTransformChildrenChanged`
- `aLayoutGroupBase.UpdateTiming`: `Immediate`, `Update`, `LateUpdate`
