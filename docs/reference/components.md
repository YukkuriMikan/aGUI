# Component Reference

| 型 | 概要 | 主な API | Source |
| --- | --- | --- | --- |
| `aTextMeshProUgui` | Localization・ルビ表示・横幅縮小対応の `TextMeshProUGUI` 拡張。 | `LocalizationKey`, `StringTable`, `RubySizeMode`, `RubyScale`, `RubySize`, `RubyOffset`, `ShrinkWidth` | [aTextMeshProUgui.cs](../../Assets/package/Runtime/Components/aTextMeshProUgui.cs) |
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

## aTextMeshProUgui の横幅縮小

Inspector の **Shrink Width**、または `text.ShrinkWidth = true` で有効にします（初期値OFF）。
左右のマージンを除いた RectTransform の幅へ、本文・ルビ・装飾を横方向だけ縮小して収めます。
フォントサイズ・縦位置・行間・RectTransform・Transform のスケールは変更しません。
文字列が短い場合に拡大することはありません。ルビや斜体の張り出しは枠内へ位置補正します。
明示改行は維持し、複数行にも共通の縮小率を適用します。縦方向のはみ出しは補正しません。

有効時は **Wrap Mode = No Wrap / Overflow = Overflow / Auto Size = OFF** で固定し、Inspectorでは無効表示します。
Auto Sizeから切り替えると、設定された基準フォントサイズを使用します。
OFFにすると切り替え前の3設定に戻ります。保存・Prefab複製・再読み込み後も復元用の設定を保持します。
`OnPreRenderText` には縮小後の文字・リンク座標を渡します。
preferred sizeは縮小前の本文を基準にするため、横幅をContentSizeFitterで文字に追従させる場合はそのレイアウトが優先されます。
