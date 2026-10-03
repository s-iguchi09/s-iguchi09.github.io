| condition | containers realized | items panel | VirtualizingStackPanel? | IsVirtualizing | CanContentScroll | height |
|---|---|---|---|---|---|---|
| ListBox | 11 | VirtualizingStackPanel | True | True | True | 200 |
| ListView + GridView | 10 | VirtualizingStackPanel | True | True | True | 200 |
| DataGrid | 11 | DataGridRowsPresenter | True | True | True | 200 |
| ItemsControl in a ScrollViewer | 1,000 | StackPanel | False | True | - | 15,960 |
| ItemsControl directly in the 200-high Grid | 1,000 | StackPanel | False | True | - | 200 |
| ComboBox, before the drop-down is opened | 0 | - | False | True | - | 21.96 |
| ComboBox, drop-down open | 1,000 | StackPanel | False | True | True | 21.96 |
| ComboBox, after the drop-down is opened and closed | 1,000 | StackPanel | False | True | True | 21.96 |
| TreeView, 1,000 root nodes | 1,000 | StackPanel | False | False | False | 200 |
