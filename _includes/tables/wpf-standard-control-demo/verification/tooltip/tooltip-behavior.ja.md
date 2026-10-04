| 条件 | 計測値 |
|---|---|
| 既定値: InitialShowDelay / ShowDuration / BetweenShowDelay | 1000 / 2147483647 / 100 |
| 既定値: Placement / ShowsToolTipOnKeyboardFocus / ShowOnDisabled / HasDropShadow | Mouse / null / False / False |
| 実際のマウスをボタンに乗せる: InitialShowDelay 500 / 2000 | 600 ms 後に開いた / 2050 ms 後に開いた |
| ShowDuration=1000、ポインターをボタンに乗せたまま: 2.5 秒後に開いているか | False |
| 文字列の ToolTip を 2 回開く: 同じ ToolTip のインスタンスか | False |
| 無効なボタン: ShowOnDisabled False / True | 開かなかった / 350 ms 後に開いた |
| Placement=Bottom: ボタンの左上から見たツールチップの位置 / ポインターから見た位置 | (0, 30) / (-80, 15) |
| Bottom、HorizontalOffset 50: ボタンの左上から見たツールチップの位置 / ポインターから見た位置 | (50, 30) / (-30, 15) |
| Placement=Mouse（既定値）: ボタンの左上から見たツールチップの位置 / ポインターから見た位置 | (80, 32) / (0, 17) |
| 実際に Tab（マウスはもう一方のボタンの上）、OnKeyboardFocus=True: フォーカス / マウスが上にあるか / 開いたか | True / False / True |
| 実際に Tab（マウスはもう一方のボタンの上）、OnKeyboardFocus=False: フォーカス / マウスが上にあるか / 開いたか | True / False / False |
| 実際に Tab（マウスはもう一方のボタンの上）、OnKeyboardFocus=null: フォーカス / マウスが上にあるか / 開いたか | True / False / True |
