| case | measured |
|---|---|
| Name cell, F2: IsEditing / editor | True / TextBox |
| typed \"Changed\", Esc: IsEditing / source | False / \"Name 1\" |
| F2, typed \"Committed\", Enter: source | \"Committed\" |
| editor by column type | Text: TextBox, CheckBox: CheckBox, ComboBox: ComboBox |
| Name header clicked 3 times: SortDirection | Ascending -&gt; Descending -&gt; Ascending |
| editing a cell, Items.SortDescriptions.Add(\.\.\.) | InvalidOperationException |
| after CommitEdit() | InvalidOperationException |
| after CancelEdit() | InvalidOperationException |
| after CommitEdit(DataGridEditingUnit.Row, true) | no exception |
| after CancelEdit(DataGridEditingUnit.Row) | no exception |
| only AlternatingRowBackground set: AlternationCount / rows | 2 / \#FFFFFFFF, \#FFD3D3D3, \#FFFFFFFF, \#FFD3D3D3 |
| 1,000 rows: DataGridRows / GroupItems | 11 / 0 |
| 1,000 rows grouped, with GroupStyle: DataGridRows / GroupItems | 1000 / 10 |
| 1,000 rows grouped, no GroupStyle: DataGridRows / GroupItems | 11 / 0 |
| 1,000 rows grouped, GroupStyle, IsVirtualizingWhenGrouping=True: DataGridRows / GroupItems | 18 / 1 |
| FrozenColumnCount=0, scrolled 60: x of columns 1, 2 before / after | 7, 107 / -53, 47 |
| FrozenColumnCount=1, scrolled 60: x of columns 1, 2 before / after | 7, 107 / 7, 47 |
