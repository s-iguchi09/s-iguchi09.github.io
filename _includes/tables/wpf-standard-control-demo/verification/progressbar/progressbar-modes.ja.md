| 条件 | 計測値 |
|---|---|
| IsIndeterminate=True、Value 30: 進捗の部分の幅 / 表示状態 | 200 / CommonStates: Indeterminate |
| アニメーション中（300 ミリ秒でテンプレートを変える）: 表示 / Collapsed / 確定モード | True / False / False |
| ワーカースレッドから Value を設定 | InvalidOperationException |
| UI スレッドで作った Progress&lt;double&gt;、ワーカーから Report(40): UI スレッドでのコールバックか / Value | True / 40 |
| UI オートメーションの RangeValue パターン: 確定モード / IsIndeterminate=True | 値 30、IsReadOnly True / 対応していない |
