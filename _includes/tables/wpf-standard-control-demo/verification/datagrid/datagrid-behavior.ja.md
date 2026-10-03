| 条件 | 計測値 |
|---|---|
| Name のセルで F2: IsEditing / 編集用の要素 | True / TextBox |
| \"Changed\" を入力して Esc: IsEditing / ソース | False / \"Name 1\" |
| F2、\"Committed\" を入力して Enter: ソース | \"Committed\" |
| 列の種類ごとの編集用の要素 | Text: TextBox, CheckBox: CheckBox, ComboBox: ComboBox |
| Name の見出しを 3 回クリック: SortDirection | Ascending -&gt; Descending -&gt; Ascending |
| セルの編集中に Items.SortDescriptions.Add(\.\.\.) | InvalidOperationException |
| CommitEdit() の後 | InvalidOperationException |
| CancelEdit() の後 | InvalidOperationException |
| CommitEdit(DataGridEditingUnit.Row, true) の後 | 例外なし |
| CancelEdit(DataGridEditingUnit.Row) の後 | 例外なし |
| AlternatingRowBackground だけを指定: AlternationCount / 各行 | 2 / \#FFFFFFFF, \#FFD3D3D3, \#FFFFFFFF, \#FFD3D3D3 |
| 1,000 行: DataGridRow の数 / GroupItem の数 | 11 / 0 |
| 1,000 行をグループ化、GroupStyle あり: DataGridRow の数 / GroupItem の数 | 1000 / 10 |
| 1,000 行をグループ化、GroupStyle なし: DataGridRow の数 / GroupItem の数 | 11 / 0 |
| 1,000 行をグループ化、GroupStyle、IsVirtualizingWhenGrouping=True: DataGridRow の数 / GroupItem の数 | 18 / 1 |
| FrozenColumnCount=0、60 スクロール: 1 列目と 2 列目の x 座標 前 / 後 | 7, 107 / -53, 47 |
| FrozenColumnCount=1、60 スクロール: 1 列目と 2 列目の x 座標 前 / 後 | 7, 107 / 7, 47 |
