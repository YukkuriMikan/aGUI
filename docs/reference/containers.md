# Container / Cursor Reference

## aGUI共通要素
aGUIのコンテナとSelectableコンポーネントは基本的にaGuiInfoを持つ。
| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aGuiInfo` | Rect/Graphic のキャッシュと初期値保持。 | `RectTransform`, `OriginalRectTransformValues`, `TargetGraphic`, `Refresh()` | [aGuiInfo.cs](../../Assets/package/Runtime/Containers/aGuiInfo.cs) |

## Containers
型引数は全てSelectable継承の制約有り。
継承クラスを作成する際は、基底クラスのメソッドを最初に呼ぶ事。

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aContainerBase` | すべてのコンテナの基底。表示状態、イベント、Show/Hide アニメーションを管理。 | `Initialize()`, `Show()`, `Hide()`, `Toggle()`, `IsVisible`, `OnShow`, `OnHide`, `ShowStartObservable` | [aContainerBase.cs](../../Assets/package/Runtime/Containers/Abstracts/aContainerBase.cs) |
| `aSelectableContainerBase<T>` | 子 `Selectable` の一覧・現在選択・初期選択を管理。<br>`EventSystem` は考慮されない。<br>子要素にButtonやImage等のSelectable継承クラスを使用したい場合、本クラスを継承して使用する事。<br>アイテムUIのカテゴリ選択等、サブ的な選択要素として使用する事を想定。| `ChildSelectables`, `CurrentSelectable`, `CurrentSelectableIndex`, `RefreshChildSelectables()`, `SelectNext()`, `SelectPrevious()`, `OnSelectChanged` | [aSelectableContainerBase.cs](../../Assets/package/Runtime/Containers/Abstracts/aSelectableContainerBase.cs) |
| `aNormalSelectableContainerBase<T>` | `EventSystem` を考慮した選択コンテナ。選択 null を禁止可能。<br>子要素にButtonやImage等のSelectable継承クラスを使用したい場合、本クラスを継承して使用する事。<br>アイテムUIのアイテムリスト本体等、メインとなる選択要素として使用する事を想定。 | `DisallowNullSelection`, `CurrentSelectable`, `CurrentSelectableIndex` | [aNormalSelectableContainerBase.cs](../../Assets/package/Runtime/Containers/Abstracts/aNormalSelectableContainerBase.cs) |
| `aScrollContainerBase<T>` | `ScrollRect` を参照として指定する事で、自動スクロール機能を追加。 | `ScrollToItem(...)`, `SmoothScrollAsync(...)` | [aScrollContainerBase.cs](../../Assets/package/Runtime/Containers/Abstracts/aScrollContainerBase.cs) |
| `aNormalSelectableContainer` | `Selectable` を子要素とする汎用的な標準コンテナ。 | (基底クラス API を利用) | [aNormalSelectableContainer.cs](../../Assets/package/Runtime/Containers/aNormalSelectableContainer.cs) |
| `aNormalScrollContainer` | `Selectable` を子要素とする汎用的な標準スクロールコンテナ。 | (基底クラス API を利用) | [aNormalScrollContainer.cs](../../Assets/package/Runtime/Containers/aNormalScrollContainer.cs) |
| `aCustomSelectableContainer` |  `Selectable` を子要素とする `EventSystem` を継承しないコンテナ。 | (基底クラス API を利用) | [aCustomSelectableContainer.cs](../../Assets/package/Runtime/Containers/aCustomSelectableContainer.cs) |
| `aStaticContainer` | 選択管理を持たないシンプルコンテナ。 | (基底クラス API を利用) | [aStaticContainer.cs](../../Assets/package/Runtime/Containers/aStaticContainer.cs) |
| `aSubContainer` | 親コンテナの表示/非表示に追従する子コンテナ。<br>親コンテナ表示時に子コンテナを個別に非表示にすることは可能だが、親コンテナが非表示時に子コンテナを表示することは出来ない。 | `MainContainer`, `Show()`, `Hide()`, `Initialize()` | [aSubContainer.cs](../../Assets/package/Runtime/Containers/aSubContainer.cs) |
| `IDisallowNullSelectionContainer` | 選択 null 禁止機能のインターフェース。 | `DisallowNullSelection` | [IDisallowNullSelectionContainer.cs](../../Assets/package/Runtime/Containers/Interfaces/IDisallowNullSelectionContainer.cs) |
| `aContainerManager` | 登録コンテナの全体管理。 | `Add()`, `Remove()`, `GetContainer()`, `GetContainers<T>()`, `Containers`, `Count`, `Clear()` | [aContainerManager.cs](../../Assets/package/Runtime/Containers/aContainerManager.cs) |

## Cursors

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aCursorBase` | 選択中のSelectable要素へ追従するカーソルの基底クラス。移動/サイズモード対応。 | `MoveMode`, `SizeMode`, `UpdateMode` | [aCursorBase.cs](../../Assets/package/Runtime/Containers/Abstracts/aCursorBase.cs) |
| `aNormalCursorBase<ContainerType, SelectableType>` | `aSelectableContainerBase` と連携するカーソルの基底クラス。<br>Containerの継承クラスでカーソルを使用する場合、基本的に本抽象クラスを継承して使用する。 | (基底クラス API を利用) | [aNormalCursorBase.cs](../../Assets/package/Runtime/Containers/Abstracts/aNormalCursorBase.cs) |
| `aCustomCursorBase<T>` | 任意 `Selectable` 向けカーソル基底。 | (基底クラス API を利用) | [aCustomCursorBase.cs](../../Assets/package/Runtime/Containers/Abstracts/aCustomCursorBase.cs) |
| `aSelectableCursor` | 標準のSelectableコンテナ向けカーソル。 | (基底クラス API を利用) | [aSelectableCursor.cs](../../Assets/package/Runtime/Containers/aSelectableCursor.cs) |
| `aCustomSelectableCursor` | カスタム選択コンテナ向けカーソル。 | (基底クラス API を利用) | [aCustomSelectableCursor.cs](../../Assets/package/Runtime/Containers/aCustomSelectableCursor.cs) |
