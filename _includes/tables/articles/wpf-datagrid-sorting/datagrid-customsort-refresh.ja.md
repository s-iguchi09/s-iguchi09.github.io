| 操作 | SortDescriptions | CustomSort | 並び順 / 選択 |
|---|---|---|---|
| SortDescriptions: Name の昇順 | 1 | null | alice, bob, carol |
| 続けて CustomSort = 名前の長さ順 | 0 | 設定あり | bob, alice, carol |
| 続けて SortDescriptions.Add(Name の降順) | 1 | null | carol, bob, alice |
| 行を選んだ状態で Items.Refresh() | - | - | SelectedItem bob, CurrentCell bob |
