| 条件 | 計測値 |
|---|---|
| 基底クラス / Focusable、IsTabStop / Padding / 内容の配置 | ContentControl / False, False / 5,5,5,5 / Left, Top |
| TextBox、Label、TextBox の順: 最初の TextBox から Tab で移る先 | 2 つ目の TextBox |
| デモの Target 付きの Label: アクセスキー N / A の後のフォーカス | Name の TextBox / Age の TextBox |
| Name の TextBox を UI オートメーションのクライアントから読む: 名前 / LabeledBy | （空） / なし |
| AutomationProperties.LabeledBy を手で指定した Age の TextBox: 名前 / LabeledBy | Age(Press Alt+A) / Age(Press Alt+A) |
| Target の無い Label \"\_Plain\": アクセスキー P の後のフォーカス | Other（変わらない） |
| Target が ToolBar の中の TextBox（独自のフォーカススコープ）: アクセスキー S の後のフォーカス | ToolBar の中の TextBox |
| 文字列の Content（1 行）: 高さ | 25.96 |
| 文字列の Content（改行あり）: 高さ | 41.92 |
| アクセスキー、Target が ComboBox / 編集できる ComboBox / DatePicker: フォーカスの移る先 | ComboBox / TextBox / DatePickerTextBox |
