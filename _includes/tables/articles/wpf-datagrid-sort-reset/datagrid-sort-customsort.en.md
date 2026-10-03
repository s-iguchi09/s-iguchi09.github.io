| step | CustomSort | SortDescriptions | order (Price) |
|---|---|---|---|
| initial | null | 0 | 21800, 2480, 4980 |
| CustomSort = Price descending | set | 0 | 21800, 4980, 2480 |
| SortDescriptions.Clear() + Refresh() | set | 0 | 21800, 4980, 2480 |
| CustomSort = null + Refresh() | null | 0 | 21800, 2480, 4980 |
