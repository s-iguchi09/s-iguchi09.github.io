| condition | containers realized | items panel | VirtualizingStackPanel? | IsVirtualizing | CanContentScroll | height |
|---|---|---|---|---|---|---|
| TreeView: VirtualizingPanel.IsVirtualizing=True | 25 | VirtualizingStackPanel | True | True | True | 200 |
| TreeView: VirtualizingPanel.IsVirtualizing=True, CacheLength=0 | 13 | VirtualizingStackPanel | True | True | True | 200 |
| ItemsControl: VirtualizingStackPanel + ScrollViewer in the template | 14 | VirtualizingStackPanel | True | True | True | 200 |
| ItemsControl: VirtualizingStackPanel only, in an outer ScrollViewer with CanContentScroll=True | 1,000 | VirtualizingStackPanel | True | True | - | 15,960 |
| ItemsControl: VirtualizingStackPanel only, directly in the 200-high Grid | 1,000 | VirtualizingStackPanel | True | True | - | 200 |
| ComboBox: ItemsPanel = VirtualizingStackPanel, drop-down open | 19 | VirtualizingStackPanel | True | True | True | 21.96 |
| grouped with GroupStyle: VirtualizingPanel.IsVirtualizingWhenGrouping=True | 19 | VirtualizingStackPanel | True | True | True | 200 |
| search box: TextBox in an Auto row, ListBox in a \* row | 10 | VirtualizingStackPanel | True | True | True | 182.04 |
| search box: TextBox docked at the top, ListBox as the last child of a DockPanel | 10 | VirtualizingStackPanel | True | True | True | 182.04 |
| vertical StackPanel: ListBox MaxHeight=200 | 11 | VirtualizingStackPanel | True | True | True | 200 |
