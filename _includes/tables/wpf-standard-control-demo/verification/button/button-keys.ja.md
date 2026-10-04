| 条件 | 計測値 |
|---|---|
| UserControl の中、TextBox にフォーカス: Esc で押されたボタン / ウィンドウが開いたままか | IsCancel / True |
| Enter で押されたボタン（キーを押す前の IsDefaulted） | IsDefault（True） |
| AcceptsReturn の TextBox にフォーカス: Enter で押されたボタン / 文字列の改行の数 | （なし） / 1 |
| 別の Button にフォーカス: Enter で押されたボタン（IsDefault のボタンの IsDefaulted） | Other（False） |
| IsCancel のボタンが 2 つ: Esc で押されたボタン / キーボードフォーカス | （なし） / Cancel 1 |
| もう一度 Esc: 押されたボタン / キーボードフォーカス | （なし） / Cancel 2 |
| ShowDialog で開いたウィンドウの IsCancel: Esc でウィンドウが閉じたか / ShowDialog の戻り値 | True / False |
