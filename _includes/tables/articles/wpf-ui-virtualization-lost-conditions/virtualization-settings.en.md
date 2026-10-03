| condition | containers realized | items panel | VirtualizingStackPanel? | IsVirtualizing | CanContentScroll | height | IsGrouping | children of the items panel | items that are their own container |
|---|---|---|---|---|---|---|---|---|---|
| ScrollViewer.CanContentScroll=False | 1,000 | VirtualizingStackPanel | True | True | False | 200 | False | 1,000 | 0 |
| VirtualizingPanel.IsVirtualizing=False | 1,000 | VirtualizingStackPanel | True | False | True | 200 | False | 1,000 | 0 |
| ItemsPanel = StackPanel | 1,000 | StackPanel | False | True | True | 200 | False | 1,000 | 0 |
| ItemsPanel = WrapPanel | 1,000 | WrapPanel | False | True | True | 200 | False | 1,000 | 0 |
| grouped, no GroupStyle | 11 | VirtualizingStackPanel | True | True | True | 200 | False | 11 | 0 |
| grouped, with GroupStyle | 1,000 | StackPanel | False | True | False | 200 | True | 10 | 0 |
| DataGrid, EnableRowVirtualization=False | 1,000 | DataGridRowsPresenter | True | False | True | 200 | False | 1,000 | 0 |
| VirtualizingPanel.ScrollUnit=Pixel | 11 | VirtualizingStackPanel | True | True | True | 200 | False | 11 | 0 |
| VirtualizingPanel.CacheLength=0 | 10 | VirtualizingStackPanel | True | True | True | 200 | False | 10 | 0 |
| VirtualizingPanel.CacheLengthUnit=Page | 20 | VirtualizingStackPanel | True | True | True | 200 | False | 20 | 0 |
| ListBoxItem added directly to Items (1,000) | 11 | VirtualizingStackPanel | True | True | True | 200 | False | 11 | 1,000 |
| ListBoxItem passed through ItemsSource (1,000) | 11 | VirtualizingStackPanel | True | True | True | 200 | False | 11 | 1,000 |
