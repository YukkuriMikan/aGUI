# aGUI

`aGUI` は Unity UI（uGUI）向けの拡張ライブラリです。  
Namespace は `ANest.UI` です。

主な目的:
- UIコンテナの表示/非表示管理
- Selectable の選択制御とナビゲーション
- カーソル表示
- 手動レイアウト（Linear / Grid / Circular）
- 各種UIの簡易アニメーション

## 対応環境

- Unity: `6000.x`
- 依存ライブラリ/パッケージ:
  - DOTween
  - UniRx
  - UniTask
  - TextMeshPro
  - Unity Input System
  - Unity Localization（`aTextMeshProUgui` 利用時）

## インストール

### UPM (Git URL)

Unity Package Manager で `Add package from git URL...` を選び、以下を指定してください。

```text
https://github.com/YukkuriMikan/aGUI.git?path=/Assets/package
```

## 詳細

- [aGUI Reference Index](docs/reference/README.md)
- [Container / Cursor](docs/reference/containers.md)
- [Selectable](docs/reference/selectables.md)
- [Layout Group](docs/reference/layout-groups.md)
- [Component](docs/reference/components.md)
- [Animation](docs/reference/animations.md)
- [Manager / Utility](docs/reference/managers-utilities.md)

## クイックスタート
### 1. シーン構成

1. Canvas 配下に Panel を作成
2. Panel に `aNormalSelectableContainer` を追加
3. 子要素に `aButton` / `aToggle` を配置
4. カーソルが必要な場合は `aSelectableCursor` を追加
5. `aNormalSelectableContainer` のインスペクタに「Show/Hide」のボタンで動作を確認

## 主なコンポーネント
### Container

- `aContainerBase`: `Show/Hide/Toggle`、表示イベント、表示アニメーション
- `aSelectableContainerBase<T>`: 子のSelectableコンポーネントを管理する、選択状態管理コンテナ(継承してカテゴリ選択等、サブとなるUIに使用)
- `aNormalSelectableContainerBase<T>`: EventSysteの制御を含んだ選択状態管理コンテナ(継承してアイテムリスト等、メインとなるUIに使用)
- `aScrollContainerBase<T>`: 選択変更時に ScrollRect のスクロール位置を自動追従(継承してアイテムリスト等、スクロールが必要なメインとなるUIに使用)
- `aSubContainer`: 親コンテナの表示状態に追従

### Selectable
コンポーネントのコンテキストメニューに移行機能有り。選択することで、通常のButtonやToggleを出来るだけ値を保ったまま本コンポーネントに変更可能。
- `aButton`:
  - 右クリックイベント
  - 長押し (`onLongPress` / `onLongPressCancel`)
  - 連打ガード（Multiple Input Guard）
  - テキスト遷移（色/文言/アニメーション）
- `aToggle`:
  - ON/OFF 切り替えアニメーション
- `aSelectablesSharedParameters`: Selectable の設定を ScriptableObject で共有

### Cursor

- `aCursorBase`: 選択中要素への追従カーソル
- `aSelectableCursor` / `aCustomSelectableCursor`: コンテナ連携カーソル

### Layout
コンポーネントのコンテキストメニューに移行機能有り。選択することで、通常のButtonやToggleを出来るだけ値を保ったまま本コンポーネントに変更可能。
整列プレビュー機能付き。設定値はuGUIのLayoutGroupと概ね互換性有り。
- `aLayoutGroupHorizontal`
- `aLayoutGroupVertical`
- `aLayoutGroupGrid`
- `aLayoutGroupCircular`
- `aContentSizeFitter`: レイアウト結果に合わせて親Rectサイズ調整
- `aTextMeshSizeFitter`: TMPの文字に合わせてRectサイズ調整

### Animation

- `IUiAnimation` 実装:
- `Fade`
- `FadeCanvasGroup`
- `Move`
- `MoveTarget`
- `Rotate`
- `RotateTarget`
- `UiAnimationSet`: Show/Hide/Click/On/Off アニメーションを共有

### その他

- `aTextMeshProUgui`: Localization + ルビ表示対応 TMP 拡張
- `aUiLineRenderer`: uGUI向けライン描画
- `aScrollStop`: スクロール領域のはみ出し補正
- `RectSync`: 異なるRectTransform同士の位置を同期

## マネージャー/ユーティリティ

- `aGuiManager`: EventSystem 管理、選択履歴、`GoBack()`
- `aContainerManager`: 登録コンテナ管理、取得
- `aGuiUtils`: アニメーション再生、テキスト遷移ユーティリティ
- `aGuiExtensions`: `Tween.AwaitCompletion()`（UniTask）

## uGUI標準コンポーネントからの移行
コンテキストメニューから移行可能。
プレハブモード中は使用不可。
- `Button` → `aButton`
- `Toggle` → `aToggle`
- `HorizontalLayoutGroup` → `aLayoutGroupHorizontal`
- `VerticalLayoutGroup` → `aLayoutGroupVertical`
- `GridLayoutGroup` → `aLayoutGroupGrid`

## 注意事項
- `gameObject.SetActive` 直呼びではなく、コンテナは `Show/Hide` 利用を強く推奨
- EventSystem が複数ある場合、`aGuiManager` の優先ロジックに従います
