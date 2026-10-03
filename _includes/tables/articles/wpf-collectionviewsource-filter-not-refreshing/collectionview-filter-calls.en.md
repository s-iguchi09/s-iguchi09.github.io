| operation | filter calls | CollectionChanged | items in view |
|---|---|---|---|
| Add 1 item (passes filter) | 1 | 1 | 501 |
| Add 1 item (fails filter) | 1 | 0 | 500 |
| Remove 1 item | 0 | 1 | 499 |
| item.Stock = 0 (leaves filter) | 0 | 0 | 500 |
| item.Stock = 1 (enters filter) | 0 | 0 | 500 |
| view.Refresh() | 1000 | 1 | 500 |
