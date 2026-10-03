| 手順 | CustomSort | SortDescriptions | 並び順（Price） |
|---|---|---|---|
| 最初 | null | 0 | 21800, 2480, 4980 |
| CustomSort = Price の降順 | 設定あり | 0 | 21800, 4980, 2480 |
| SortDescriptions.Clear() + Refresh() | 設定あり | 0 | 21800, 4980, 2480 |
| CustomSort = null + Refresh() | null | 0 | 21800, 2480, 4980 |
