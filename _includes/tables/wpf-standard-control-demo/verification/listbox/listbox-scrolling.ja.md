| 条件 | 計測値 |
|---|---|
| 最初に作られた ListBoxItem の数 | 6 |
| コードから SelectedIndex = 500: VerticalOffset / 項目 500 のコンテナー | 0 / 作られていない |
| 続けて ScrollIntoView(SelectedItem): VerticalOffset / 項目 500 のコンテナー | 497 / 作られている |
| VerticalScrollBarVisibility=Auto: スクロールできる量 / バー、下矢印キー 20 回の後 | 996 / Visible、SelectedIndex 20、位置 17 |
| VerticalScrollBarVisibility=Disabled: スクロールできる量 / バー、下矢印キー 20 回の後 | 996 / Collapsed、SelectedIndex 20、位置 17 |
