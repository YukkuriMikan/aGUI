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
