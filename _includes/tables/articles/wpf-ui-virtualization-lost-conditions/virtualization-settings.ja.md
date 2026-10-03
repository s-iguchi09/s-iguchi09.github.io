| 条件 | 実体化したコンテナー | 項目のパネル | VirtualizingStackPanel か | IsVirtualizing | CanContentScroll | 高さ | IsGrouping |
|---|---|---|---|---|---|---|---|
| ScrollViewer.CanContentScroll=False | 1,000 | VirtualizingStackPanel | True | True | False | 200 | False |
| VirtualizingPanel.IsVirtualizing=False | 1,000 | VirtualizingStackPanel | True | False | True | 200 | False |
| ItemsPanel = StackPanel | 1,000 | StackPanel | False | True | True | 200 | False |
| ItemsPanel = WrapPanel | 1,000 | WrapPanel | False | True | True | 200 | False |
| グループ化、GroupStyle なし | 11 | VirtualizingStackPanel | True | True | True | 200 | False |
| グループ化、GroupStyle あり | 1,000 | StackPanel | False | True | False | 200 | True |
| DataGrid、EnableRowVirtualization=False | 1,000 | DataGridRowsPresenter | True | False | True | 200 | False |
| VirtualizingPanel.ScrollUnit=Pixel | 11 | VirtualizingStackPanel | True | True | True | 200 | False |
| VirtualizingPanel.CacheLength=0 | 10 | VirtualizingStackPanel | True | True | True | 200 | False |
| VirtualizingPanel.CacheLengthUnit=Page | 20 | VirtualizingStackPanel | True | True | True | 200 | False |
