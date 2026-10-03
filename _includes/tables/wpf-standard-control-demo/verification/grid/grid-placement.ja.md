| 条件 | 計測値 |
|---|---|
| Grid.Row / Grid.Column の既定値（メタデータ） | 0 / 0 |
| 2x2、Grid.Row/Column を指定しない子が 2 つ | x=0 y=0 w=150 h=100 \| x=0 y=0 w=150 h=100 |
| 2x2、Grid.Column=2（範囲外） | x=150 y=0 w=150 h=100 |
| 2x2、Grid.Column=5、Grid.Row=5 | x=150 y=100 w=150 h=100 |
| 3x2、Grid.ColumnSpan=5 | x=0 y=0 w=300 h=100 |
| 3x2、Grid.Column=1、ColumnSpan=5 | x=100 y=0 w=200 h=100 |
| 3x2、Grid.RowSpan=5 | x=0 y=0 w=100 h=200 |
| Grid.SetColumn(child, -1) | ArgumentException |
| Grid.SetColumnSpan(child, 0) | ArgumentException |
| 定義なし、子 A と子 B（Column=1、50x30） | x=0 y=0 w=300 h=200 \| x=125 y=85 w=50 h=30 |
