| 条件 | 計測値 |
|---|---|
| Orientation の既定値 / Spacing プロパティ | Vertical / なし |
| Vertical、200 x 100 のパネル: 子に渡される大きさ / 配置された子の大きさ（希望は 40 x 20） | 200 x Infinity / 200 x 20 |
| Horizontal、200 x 100 のパネル: 子に渡される大きさ / 配置された子の大きさ（希望は 40 x 20） | Infinity x 100 / 40 x 100 |
| Horizontal、デモの 3 つの Label、Width 80: 最後の Label の x / ClipToBounds / レイアウトのクリップの幅 | 94.69 / False / 80 |
| 80 の外にある最後の Label のヒットテスト | （なし） |
| 高さ 200 の ScrollViewer の中の StackPanel に 1000 個の子: 測定された子の数 | 1000 |
| 高さ 200 に 1000 項目の ListBox、直接: 高さ / ScrollableHeight / 作られた項目の数 | 200 / 991 / 10 |
| 高さ 200 に 1000 項目の ListBox、縦の StackPanel の中: 高さ / ScrollableHeight / 作られた項目の数 | 19964 / 0 / 1000 |
| 高さ 30 の空の StackPanel、Background=null: 中央のヒットテスト | （なし） |
| 高さ 30 の空の StackPanel、Background=Transparent: 中央のヒットテスト | Panel |
| 高さ 30 の空の StackPanel、Background=AliceBlue: 中央のヒットテスト | Panel |
| デモの Label、ZIndex Item1=1、Item2=2: 重なった部分で上にある要素 | Item2 |
| デモの Label、ZIndex Item1=3、Item2=2: 重なった部分で上にある要素 | Item1 |
| ZIndex=100 の内側のパネルの子と、内側のパネルの兄弟（ZIndex 0）: 上にあるもの | OuterSibling |
