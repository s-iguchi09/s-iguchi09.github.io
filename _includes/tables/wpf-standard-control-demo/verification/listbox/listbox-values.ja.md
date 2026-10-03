| 条件 | 計測値 |
|---|---|
| SelectedIndex = 2 | SelectedIndex 2, SelectedItem Item 3, SelectionChanged 1 |
| 続けて SelectedIndex = 10（項目は 4 つ） | 例外なし; SelectedIndex 2 |
| 続けて SelectedIndex = -1 | SelectedIndex -1, SelectedItem null |
| Equals の無いクラス: 同じデータの別のインスタンスを SelectedItem に | SelectedIndex -1, SelectedItem null |
| record（値で比べる）: 同じデータの別のインスタンスを SelectedItem に | SelectedIndex 1、項目と同じインスタンスか: False |
| ItemsSource より前に SelectedValue = 2、続けて ItemsSource を設定 | SelectedValue 2 -&gt; SelectedIndex 1, SelectedValue 2 |
| DisplayMemberPath=\"Name\": 項目 1 を表示している要素 | TextBlock, Text \"Desktop\" |
| DisplayMemberPath=\"Nope\": 項目 1 を表示している要素 | TextBlock, Text \"\" |
| DisplayMemberPath と ItemTemplate の両方を指定 | 設定時に InvalidOperationException、表示していない |
