| 条件 | 計測値 |
|---|---|
| IsChecked を bool の A、B、C にバインド（Mode なし）、B をクリック: ソース / A のバインドが残っているか | A=False, B=True, C=False / True |
| 続けてソースの C を true に: ソース / チェックされたもの | A=False, B=False, C=True / C |
| A がチェック済みでフォーカスあり、下矢印キー: フォーカス / チェックされたもの | B / A |
| A にフォーカス、Tab: フォーカス | B |
| VerticalContentAlignment（値の出どころ） | Top (Default) |
| 3 行のラベル、VerticalContentAlignment=Top、高さ 200 の RadioButton: 丸印の y / ラベルの y | 1（高さ 12）/ -1（高さ 47.88） |
| 3 行のラベル、VerticalContentAlignment=Center、高さ 200 の RadioButton: 丸印の y / ラベルの y | 94（高さ 12）/ 75.56（高さ 47.88） |
| 3 行のラベル、VerticalContentAlignment=Center、高さ 46.88 の RadioButton: 丸印の y / ラベルの y | 17.44（高さ 12）/ -1（高さ 47.88） |
