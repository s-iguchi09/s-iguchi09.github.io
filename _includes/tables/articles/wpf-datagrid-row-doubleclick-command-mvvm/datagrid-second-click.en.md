| double-clicked | MouseLeftButtonDown (ClickCount=2) handled at the DataGrid | OriginalSource of DataGrid.MouseDoubleClick | its visual parent at that time | IsEditing of the double-clicked cell at that time |
|---|---|---|---|---|
| cell of gamma (row 3) | False | TextBlock | ContentPresenter | - |
| Name column header | True | DataGridHeaderBorder | Grid | - |
| vertical scroll bar thumb | True | Rectangle | Thumb | - |
| vertical scroll bar track below the thumb | True | Rectangle | RepeatButton | - |
| blank area below the last row (3 rows) | False | ScrollViewer | Border | - |
| cell of beta (selected) with Ctrl held | True | TextBlock | ContentPresenter | - |
| cell of gamma (not selected) with Ctrl held | True | TextBlock | ContentPresenter | - |
| cell of gamma, IsReadOnly=False | True | TextBlock | null | True |
| cell of gamma, DataGrid IsReadOnly=False, Name column IsReadOnly=True | False | TextBlock | ContentPresenter | False |
