# Component Reference

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aTextMeshProUgui` | Localization とルビ表示対応の `TextMeshProUGUI` 拡張。 | `LocalizationKey`, `StringTable`, `RubySizeMode`, `RubyScale`, `RubySize`, `RubyOffset` | [aTextMeshProUgui.cs](../../Assets/package/Runtime/Components/aTextMeshProUgui.cs) |
| `RubySizeMode` | ルビサイズ指定方法。 | `Auto`, `Scale`, `Size` | [aTextMeshProUgui.cs](../../Assets/package/Runtime/Components/aTextMeshProUgui.cs) |
| `aUiLineRenderer` | uGUI でラインを描画。頂点列の編集、UV、コーナー/キャップ設定に対応。 | `Points`, `AddPoint()`, `AddPoints()`, `RemovePoint()`, `ReplacePoint()`, `InsertPoint()`, `ClearPoints()` | [aUiLineRenderer.cs](../../Assets/package/Runtime/Components/aUiLineRenderer.cs) |
| `aUiLineRendererSpace` | ライン座標空間。 | `Local`, `World` | [aUiLineRenderer.cs](../../Assets/package/Runtime/Components/aUiLineRenderer.cs) |
| `CornerType` / `CapType` | ラインの角・端点メッシュ方式。 | enum 定義を参照 | [aUiLineRenderer.cs](../../Assets/package/Runtime/Components/aUiLineRenderer.cs) |
| `aScrollStop` | 子領域がビューからはみ出さないよう `TargetRect` の位置を補正。 | `TryGetChildRegionRect(...)`, `TryGetChildRegionPolygon(...)`, `TargetRect`, `Method` | [aScrollStop.cs](../../Assets/package/Runtime/Components/aScrollStop.cs) |
| `aScrollStop.StopMethod` | 補正境界の方式。 | `Rect`, `Polygon` | [aScrollStop.cs](../../Assets/package/Runtime/Components/aScrollStop.cs) |
| `aScrollStop.UpdateTiming` | 補正タイミング。 | `Update`, `LateUpdate` | [aScrollStop.cs](../../Assets/package/Runtime/Components/aScrollStop.cs) |
| `RectSync` | 別 `RectTransform` の Anchor/Pivot/Position/Size を同期。 | `Sync()` | [RectSync.cs](../../Assets/package/Runtime/Utility/RectSync.cs) |
| `aImage` | `UnityEngine.UI.Image` の aGUI版(開発中)。 | (Image API を利用) | [aImage.cs](../../Assets/package/Runtime/Components/aImage.cs) |

## aTextMeshProUgui のルビ

`<ruby="かんじ">漢字</ruby>` と、従来の `<link="ruby:かんじ">漢字</link>` を使用できます。
Inspector の Text と `text` プロパティには入力したタグが残り、実行中の編集も反映されます。
本文が空のルビは表示しません。ルビ本文内の明示改行は無効化し、自動改行では本文全体を一単位として扱います。
本文だけで行幅を超える場合は警告を出し、分割せず横にはみ出して表示します。

0.7.14 から、TMP が生成したルビ文字の頂点を `OnPreRenderText` で本文の上へ配置します。
ルビごとの GameObject / TextMeshProUGUI は生成しません。フォールバックフォントなどで別マテリアルが必要な場合は、TMP 標準の `TMP_SubMeshUI` が生成されることがあります。
旧方式の生成物は、有効化時に `Ruby_` で始まる名前・`NotEditable`・`TextMeshProUGUI` を確認して除去します。

本文レイアウトの変更時は、本文を計算してからルビを含むメッシュを生成します。
通常の再描画では本文の計算結果と頂点バッファを再利用します。文字列変更やバッファ拡張時の割り当てはありますが、安定した再描画では GC Alloc が発生しないことをテストしています。
`textInfo` の文字・リンク・行情報と preferred size は本文を対象とし、`meshInfo` には本文とルビの両方の頂点が含まれます。
