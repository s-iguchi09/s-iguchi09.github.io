| 操作 | SortDescriptions | column.SortDirection | 並び順 |
|---|---|---|---|
| 最初 | 0 | null | carol, alice, bob |
| SortDescriptions.Add だけ | 1 | null | alice, bob, carol |
| + column.SortDirection | 1 | Ascending | alice, bob, carol |
| 続けて SortDescriptions.Clear() だけ | 0 | Ascending | carol, alice, bob |
| + column.SortDirection も消す | 0 | null | carol, alice, bob |
| SortDescription 1 つ（Score の降順）、同点あり | 1 | null | alice, carol, anna, bob |
| SortDescription 2 つ（Score の降順、Name の昇順）、同点あり | 2 | null | alice, anna, carol, bob |
| ItemsSource = ICollectionView、view.SortDescriptions.Clear() | 0 | Ascending | carol, alice, bob |
| 列ヘッダーをクリック（標準の並べ替え） | 1 | Ascending | alice, bob, carol |
