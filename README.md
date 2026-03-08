# aGUI

`aGUI` は Unity UI（uGUI）向けの拡張ライブラリです。  
Namespace は `ANest.UI` です。

主な目的:
- UIコンテナの表示/非表示管理
- Selectable の選択制御とナビゲーション改善
- カーソル追従表示
- レイアウト拡張（Linear / Grid / Circular）
- DOTween ベースの UI アニメーション統一

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

## クイックスタート

### 1. シーン構成

1. Canvas 配下に Panel を作成
2. Panel に `aNormalSelectableContainer` を追加
3. 子要素に `aButton` / `aToggle` を配置
4. 必要に応じて `aSelectableCursor` を追加

### 2. 表示制御コード

```csharp
using ANest.UI;
using UnityEngine;

public class MenuController : MonoBehaviour {
    [SerializeField] private aNormalSelectableContainer menu;

    public void Open() => menu.Show();
    public void Close() => menu.Hide();
    public void Toggle() => menu.Toggle();
}
```

## 主なコンポーネント

### Container

- `aContainerBase`: `Show/Hide/Toggle`、表示イベント、表示アニメーション
- `aSelectableContainerBase<T>`: 子Selectable管理、選択状態管理
- `aNormalSelectableContainer`: Null選択禁止に対応した通常コンテナ
- `aNormalScrollContainer`: 選択変更時に ScrollRect を自動追従
- `aSubContainer`: 親コンテナの表示状態に追従

### Selectable

- `aButton`:
- 右クリックイベント
- 長押し (`onLongPress` / `onLongPressCancel`)
- 連打ガード（Multiple Input Guard）
- テキスト遷移（色/文言/アニメーション）
- `aToggle`:
- ON/OFF 切り替えアニメーション
- テキスト遷移
- `aSelectablesSharedParameters`: Selectable 設定を ScriptableObject で共有

### Cursor

- `aCursorBase`: 選択中要素への追従カーソル
- `aSelectableCursor` / `aCustomSelectableCursor`: コンテナ連携カーソル

### Layout

- `aLayoutGroupHorizontal`
- `aLayoutGroupVertical`
- `aLayoutGroupGrid`
- `aLayoutGroupCircular`
- `aContentSizeFitter`: レイアウト結果に合わせて親Rectサイズ調整
- `aTextMeshSizeFitter`: TMPの内容に合わせてRectサイズ調整

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
- `RectSync`: RectTransform 同期

## マネージャー/ユーティリティ

- `aGuiManager`: EventSystem 管理、選択履歴、`GoBack()`
- `aContainerManager`: 登録コンテナ管理、取得
- `aGuiUtils`: アニメーション再生、テキスト遷移ユーティリティ
- `aGuiExtensions`: `Tween.AwaitCompletion()`（UniTask）

## 既存UIからの移行（Editor）

コンテキストメニューから移行可能:
- `Button` → `aButton`
- `Toggle` → `aToggle`
- `HorizontalLayoutGroup` → `aLayoutGroupHorizontal`
- `VerticalLayoutGroup` → `aLayoutGroupVertical`
- `GridLayoutGroup` → `aLayoutGroupGrid`

## 注意事項

- `gameObject.SetActive` 直呼びではなく、コンテナは `Show/Hide` 利用を推奨
- EventSystem が複数ある場合、`aGuiManager` の優先ロジックに従います
- 移行メニューは Prefab 上では制限される場合があります
