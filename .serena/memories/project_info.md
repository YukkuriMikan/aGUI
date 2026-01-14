# aGUI Project

## Purpose
Unity用のカスタムUIコンポーネントライブラリ。aLayoutGroupGridなどのレイアウトグループや、aButton, aToggleなどの選択可能なコンポーネントを含む。

## Tech Stack
- Unity (Unity 6000.1.17f1)
- C# 9.0
- UniRx
- DOTween

## Project Structure
- `Assets/package/Runtime`: コアロジックとコンポーネント
    - `Components`: 画像やラインレンダラーなどの描画系
    - `Containers`: コンテナ管理
    - `LayoutGroups`: グリッドやサイズフィッターなどのレイアウト系
    - `Selectables`: ボタン、トグルなどのインタラクティブ系
    - `UiAnimation`: アニメーション関連
    - `Utility`: 便利ツール

## Code Style and Conventions
- **Comments**:
    - クラス、プロパティ、メソッドなどはXMLコメント（`<summary> 内容 </summary>`）。
    - 1行に収まる場合はタグ含め1行。
    - フィールドとenum要素は末尾に `//` コメント。
- **Attributes**:
    - `[SerializeField]` なフィールドには `[Tooltip("説明")]` を付ける。
    - `Tooltip` は `SerializeField` とは別行。
- **Layout**:
    - 適切に `#region` 分けを行う。
    - フィールド間の空行は入れない。
- **Naming**:
    - クラス名は `a` プレフィックス（例: `aLayoutGroupGrid`）。

## Commands
- 特になし。Unityエディタ上で動作。
