| ビューモデル | コードからの代入 | 利用者の操作 |
|---|---|---|
| INotifyDataErrorInfo、5 文字まで | \"abcdefgh\": HasErrors True、Validation.HasError True | \"abcdefgh\" を入力: \"abcde\"、HasErrors False |
| INotifyDataErrorInfo、上限を超えた値を読み込む | \"abcdefgh\" を読み込む: HasErrors True | 末尾に x: \"abcdefgh\"、Backspace: HasErrors True |
| 保存ボタン、IsEnabled を !HasErrors にバインド | \"abcdefgh\": False、\"abcde\": True | 読み込み後: False、Backspace 3 回で \"abcde\": True |
| setter で 0〜100 に収める | 150: ソース 100、Slider 100 | End キー: 送った値 200、ソース 100、Slider 100 |
| setter で 10 刻みに丸める | 23.4: ソース 20、Slider 20 | 50 から右矢印キー: 送った値 55、ソース 60、Slider 60 |
