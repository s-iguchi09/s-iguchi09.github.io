| 条件 | 計測値 |
|---|---|
| 子に渡される大きさ / TextBlock の幅 / Stretch の Border の幅（幅 300 の Canvas） | Infinity x Infinity / 60.23 / 37.29 |
| (20, 20) に子を置いた Canvas、横向きの StackPanel の中: DesiredSize / ClipToBounds の既定値 | 0 x 0 / False |
| デモの初期値 Top 20、Left 20 | x=20 y=20 w=100 h=100 |
| Left 20 と Right 20、Top 20 と Bottom 20（300 x 200 の Canvas） | x=20 y=20 w=100 h=100 |
| Right 20 と Bottom 20 だけ、300 x 200 に広げた Canvas（Width の指定なし） | x=180 y=80 w=100 h=100 |
| 同じ指定で大きさ 0 の Canvas（StackPanel の中） | x=-120 y=-120 w=100 h=100 |
| 位置の指定なし / Left -30 | x=0 y=0 w=100 h=100 / x=-30 y=150 w=100 h=100 |
| 幅 200 の Canvas、子を Left 150（250 まで）、ClipToBounds=False: x=230 のヒットテスト | A |
| 幅 200 の Canvas、子を Left 150（250 まで）、ClipToBounds=True: x=230 のヒットテスト | （なし） |
| デモの長方形 A (20, 20) と B (50, 50)、ZIndex A=0、B=1: (85, 85) で上にあるもの | B |
| デモの長方形 A (20, 20) と B (50, 50)、ZIndex A=2、B=1: (85, 85) で上にあるもの | A |
| デモの長方形 A (20, 20) と B (50, 50)、ZIndex A=0、B=0: (85, 85) で上にあるもの | B |
| デモの FallbackValue=NaN のバインド: 文字列 \"\" / \"30\" のときの Canvas.Right | NaN / 30 |
