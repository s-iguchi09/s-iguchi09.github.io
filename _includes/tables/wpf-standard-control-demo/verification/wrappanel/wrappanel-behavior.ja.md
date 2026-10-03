| 条件 | 計測値 |
|---|---|
| 既定値: Orientation / ItemWidth / ItemHeight | Horizontal / NaN / NaN |
| デモの Label 5 個、Horizontal、幅 150 | 2 行（3、2） |
| デモの Label 5 個、Vertical、高さ 60 | 5 列（1、1、1、1、1） |
| 同じ行に高さ 20 と 40 の子、低い方が Stretch: その高さ | 40 |
| 同じ行に高さ 20 と 40 の子、低い方が Top: その高さ | 20 |
| ItemWidth=100: デモの Label の幅 / 幅 150 の子: x、レイアウトのクリップ | 96 / 300、100 |
| 幅 150 の子の、100 を 20 過ぎた位置のヒットテスト | （なし） |
| ItemHeight=100、Label 5 個、幅 150: Label の高さ / 2 行目の始まりの y / その最初の Label の y | 96 / 100 / 102 |
| 横が Disabled（既定）の ScrollViewer の中に Label 5 個、幅 150 | 3 行（2、2、1） |
| 横が Auto の ScrollViewer の中に Label 5 個、幅 150 | 1 行（5） |
| Background=null: 2 つの Label の間のヒットテスト | （なし） |
| Background=Transparent: 2 つの Label の間のヒットテスト | Panel |
| 15 重なった 2 つの Label、ZIndex 1 と 2: 上にあるもの | Item2 |
| 15 重なった 2 つの Label、ZIndex 3 と 2: 上にあるもの | Item1 |
| 300 x 200 の ListBox、ItemsPanel に WrapPanel、1000 項目: 作られた項目の数 | 1000 |
