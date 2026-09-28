| 制限 | 利用者の操作 | コードからの代入 | バインドした値 |
|---|---|---|---|
| TextBox MaxLength=5、\"abcdefgh\" | \"abcde\" | \"abcdefgh\" | \"abcdefgh\" |
| TextBox CharacterCasing=Upper、\"hello\" | \"HELLO\" | \"hello\" | \"hello\" |
| PasswordBox MaxLength=8、10 文字 | 8 文字 | 10 文字 | バインドできる PasswordProperty が無い |
| DatePicker 04-10〜04-20、04-05 | カレンダー: 日付ボタンは無効、入力: 2026-04-05 | 2026-04-05 | 2026-04-05 |
| DatePicker、その後の DisplayDateStart | 入力の後: 2026-04-05 | 2026-04-05、04-07 の日付ボタンは有効 | 2026-04-05 |
| Slider 10 刻みの目盛りに合わせる | 50 から右矢印キー: 60 | 23.4: 23.4 | 23.4: 23.4 |
| Slider Maximum=100 | End キー: 100 | 150: 100、Maximum を 200 にすると 150 | 150: Slider 100、ソース 150 |
| 2 番目の TabItem IsEnabled=False | UIA Select(): ElementNotEnabledException、選択 0 | 選択 1、\"Page 2\" | 選択 1、\"Page 2\" |
