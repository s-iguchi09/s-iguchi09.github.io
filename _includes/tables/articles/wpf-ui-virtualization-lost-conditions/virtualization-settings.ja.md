| 条件 | 実体化したコンテナー | 項目のパネル | VirtualizingStackPanel か | IsVirtualizing | CanContentScroll | 高さ | IsGrouping | 項目のパネルの子要素 | 自分自身がコンテナーの項目 |
|---|---|---|---|---|---|---|---|---|---|
| ScrollViewer.CanContentScroll=False | 1,000 | VirtualizingStackPanel | True | True | False | 200 | False | 1,000 | 0 |
| VirtualizingPanel.IsVirtualizing=False | 1,000 | VirtualizingStackPanel | True | False | True | 200 | False | 1,000 | 0 |
| ItemsPanel = StackPanel | 1,000 | StackPanel | False | True | True | 200 | False | 1,000 | 0 |
| ItemsPanel = WrapPanel | 1,000 | WrapPanel | False | True | True | 200 | False | 1,000 | 0 |
| グループ化、GroupStyle なし | 11 | VirtualizingStackPanel | True | True | True | 200 | False | 11 | 0 |
| グループ化、GroupStyle あり | 1,000 | StackPanel | False | True | False | 200 | True | 10 | 0 |
| DataGrid、EnableRowVirtualization=False | 1,000 | DataGridRowsPresenter | True | False | True | 200 | False | 1,000 | 0 |
| VirtualizingPanel.ScrollUnit=Pixel | 11 | VirtualizingStackPanel | True | True | True | 200 | False | 11 | 0 |
| VirtualizingPanel.CacheLength=0 | 10 | VirtualizingStackPanel | True | True | True | 200 | False | 10 | 0 |
| VirtualizingPanel.CacheLengthUnit=Page | 20 | VirtualizingStackPanel | True | True | True | 200 | False | 20 | 0 |
| ListBoxItem を Items に直接追加（1,000 個） | 11 | VirtualizingStackPanel | True | True | True | 200 | False | 11 | 1,000 |
