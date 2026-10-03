| 条件 | 計測値 |
|---|---|
| 既定値: LastChildFill / 子の Dock | True / Left |
| デモの 2 つの Label、LastChildFill=False: Item1 / Item2 | x=0 y=0 w=42.34 h=100 / x=42.34 y=0 w=42.34 h=100 |
| デモの 2 つの Label、LastChildFill=True: Item1 / Item2 | x=0 y=0 w=42.34 h=100 / x=42.34 y=0 w=257.66 h=100 |
| LastChildFill=True、最後の Label に Dock=Top: その位置と大きさ | x=42.34 y=0 w=257.66 h=100 |
| デモの 1 つの Label、LastChildFill=False、Dock=Left | x=0 y=0 w=42.34 h=100 |
| デモの 1 つの Label、LastChildFill=False、Dock=Top | x=0 y=0 w=300 h=27.96 |
| デモの 1 つの Label、LastChildFill=False、Dock=Right | x=257.66 y=0 w=42.34 h=100 |
| デモの 1 つの Label、LastChildFill=False、Dock=Bottom | x=0 y=72.04 w=300 h=27.96 |
| Top、Bottom、Left、内容 の順: Left / 内容 | x=0 y=27.96 w=31.75 h=44.08 / x=31.75 y=27.96 w=268.25 h=44.08 |
| Left、Top、Bottom、内容 の順: Left / 内容 | x=0 y=0 w=31.75 h=100 / x=31.75 y=27.96 w=268.25 h=44.08 |
| LastChildFill=False、Background=null: 空いた部分のヒットテスト | （なし） |
| LastChildFill=False、Background=AliceBlue: 空いた部分のヒットテスト | Panel |
| デモの ZIndex の Label（Item2 の Margin -30）、ZIndex 1 と 2: 重なった部分で上にあるもの | Item2 |
| デモの ZIndex の Label（Item2 の Margin -30）、ZIndex 3 と 2: 重なった部分で上にあるもの | Item1 |
