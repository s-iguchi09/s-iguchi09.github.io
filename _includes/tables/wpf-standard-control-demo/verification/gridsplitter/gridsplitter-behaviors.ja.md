| 条件 | 計測値 |
|---|---|
| ShowsPreview=False: 前 / ドラッグ中 / 離した後 | 197.5 / 5 / 197.5  \|  237.5 / 5 / 157.5  \|  237.5 / 5 / 157.5 |
| ShowsPreview=False: 10 回のドラッグの間に左の内容が測り直された回数 | 10 |
| ShowsPreview=True: 装飾レイヤーに置かれたプレビューの要素 | Control（Style がスプリッターの PreviewStyle か: True） |
| ShowsPreview=True: プレビューの要素の中の visual | Rectangle Fill=\#80000000 Opacity=1 |
| ShowsPreview=True: 前 / ドラッグ中 / 離した後 | 197.5 / 5 / 197.5  \|  197.5 / 5 / 197.5  \|  237.5 / 5 / 157.5 |
| ShowsPreview=True: 10 回のドラッグの間に左の内容が測り直された回数 | 0 |
| DragIncrement=1、27 ドラッグ | 左の列の移動量 27 |
| DragIncrement=20、9 ドラッグ | 左の列の移動量 0 |
| DragIncrement=20、11 ドラッグ | 左の列の移動量 20 |
| DragIncrement=20、27 ドラッグ | 左の列の移動量 20 |
| DragIncrement=20、31 ドラッグ | 左の列の移動量 40 |
| KeyboardIncrement=既定値、右矢印キーを 1 回 | 左の列の移動量 10 |
| KeyboardIncrement=25、右矢印キーを 1 回 | 左の列の移動量 25 |
| Focusable=True: Focus() / IsKeyboardFocused | True / True |
| Focusable=False: Focus() / IsKeyboardFocused | False / False |
| 左の MinWidth=なし、-1000 ドラッグ | 0 / 5 / 395 |
| 左の MinWidth=50、-1000 ドラッグ | 50 / 5 / 345 |
| 40 ドラッグし、離す前に Esc: 前 / 途中 / 後 | 197.5 / 5 / 197.5  \|  237.5 / 5 / 157.5  \|  197.5 / 5 / 197.5 |
| \* \| \*: ドラッグした後の ColumnDefinition.Width | 227.5\* \| 167.5\* |
| 200 \| \*: ドラッグした後の ColumnDefinition.Width | 230 \| \* |
| Auto \| \*: ドラッグした後の ColumnDefinition.Width | 90 \| \* |
| Width をソースのプロパティにバインド、Mode=指定なし: ドラッグ後のソース / バインド | \* / 外れる |
| Width をソースのプロパティにバインド、Mode=TwoWay: ドラッグ後のソース / バインド | 227.5\* / 残る |
| IsDragging: 前 / 左ボタンを押した後 / CancelDrag() の後 | False / True / False |
