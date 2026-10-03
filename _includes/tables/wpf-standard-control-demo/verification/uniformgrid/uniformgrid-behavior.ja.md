| 条件 | 計測値 |
|---|---|
| 名前空間 / 既定値: Rows、Columns、FirstColumn | System.Windows.Controls.Primitives / 0, 0, 0 |
| Label 5 個、300 x 200、Columns=1（デモの初期値） | 5 行 x 1 列、セル 300 x 40 |
| Label 5 個、300 x 200、Columns=2 | 3 行 x 2 列、セル 150 x 66.67 |
| Label 5 個、300 x 200、Rows=1（デモの初期値） | 1 行 x 5 列、セル 60 x 200 |
| Label 5 個、300 x 200、Rows=2 | 2 行 x 3 列、セル 100 x 100 |
| Label 5 個、300 x 200、どちらも指定なし | 3 行 x 3 列（子は 2 行）、セル 100 x 66.67 |
| Label 5 個、Rows=2、Columns=2（セル 4 つ）、300 x 200: 4 個目 / 5 個目の位置 | (150, 100) / (0, 200) |
| デモの FirstColumn=1、Columns=3、Label 9 個: Item1 / Item3 の位置、形 | (100, 0) / (0, 50)、4 行 x 3 列 |
| FirstColumn=3、Columns=3: Item1 の位置、形、レイアウト後の FirstColumn | (0, 0)、3 行 x 3 列、0 |
| FirstColumn=4、Columns=3: Item1 の位置、形、レイアウト後の FirstColumn | (0, 0)、3 行 x 3 列、0 |
| 文字列 \"4\" にバインド（デモ）: FirstColumn、バインド、続けて文字列を \"1\" に: FirstColumn、Item1 の位置 | 0、バインドは外れる、0、(0, 0) |
| Label 5 個、Columns=2、Item2 が Collapsed: Item3 の位置 / 形 | (150, 0) / 2 行 x 2 列、セル 150 x 100 |
| Columns=3、幅 150 の子: 300 に広げる / 内容に合わせる | 100 x 100 / 150 x 100 |
| Columns=2 に Label 1 個、Background=null: 空いたセルのヒットテスト | （なし） |
| Columns=2 に Label 1 個、Background=Transparent: 空いたセルのヒットテスト | Grid |
| デモの ZIndex の Label（Item2 の上の Margin -15）、ZIndex 1 と 2: 上にあるもの | Item2 |
| デモの ZIndex の Label（Item2 の上の Margin -15）、ZIndex 3 と 2: 上にあるもの | Item1 |
