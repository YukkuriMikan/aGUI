# 作業方針

- 機能追加・不具合修正など、まとまった成果を完成させるごとに必要な検証を行い、Gitコミットを作成する。ユーザーから改めてコミットを指示されるのを待たない。
- 各成果のコミットには `Assets/package/package.json` のバージョン更新を含める。特に指定がなければパッチバージョンを1つ上げる。同じ成果について既に更新済みなら重ねて上げない。
- コミット対象はその成果に関係する変更に限定し、ユーザーの無関係な変更を混ぜない。
- 完了報告にはコミットID、更新後のバージョン、検証結果を簡潔に記載する。
- ユーザーが明示的に別の進め方を指定した場合は、その指示を優先する。
- RectTransformの実サイズは `rect.size` / `rect.width` / `rect.height` で取得し、`SetSizeWithCurrentAnchors` で設定する。`sizeDelta` はアンカー相対設定の保存・復元など、差分自体を扱う必然性がある場合に限る。
- 保留中の既知の不具合：`aScrollContainerBase.SmoothScrollAsync` が `Time.deltaTime` を使うため、`Time.timeScale = 0` のポーズ中は自動スクロールが進まない。ユーザーの指示で現状維持。今後、バグ一覧を報告する際もこの項目を引き続き挙げる。
- 別途確認済みの未修正問題：`aButton.OnPointerDown` は InitialGuard 中でも連打防止の時刻を更新するため、InitialGuard 解除直後のクリックを弾くことがある。既存テスト `InitialGuard_BlocksChildButtonClickTemporarily` が変更前の 0.7.6 でも失敗することを確認済み。
