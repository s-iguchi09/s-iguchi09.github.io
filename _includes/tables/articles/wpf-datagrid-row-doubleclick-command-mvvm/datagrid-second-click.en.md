| double-clicked | ClickCount of the two mouse-downs | MouseLeftButtonDown (ClickCount=2) handled at the DataGrid | OriginalSource of DataGrid.MouseDoubleClick | its visual parent at that time | IsEditing of the double-clicked cell at that time |
|---|---|---|---|---|---|
| cell of gamma (row 3) | 1, 2 | False | TextBlock | ContentPresenter | - |
| Name column header | 1, 2 | True | DataGridHeaderBorder | Grid | - |
| vertical scroll bar thumb | 1, 2 | True | Rectangle | Thumb | - |
| vertical scroll bar track below the thumb | 1, 2 | True | Rectangle | RepeatButton | - |
| blank area below the last row (3 rows) | 1, 2 | False | ScrollViewer | Border | - |
| cell of beta (selected) with Ctrl held | 1, 2 | True | TextBlock | ContentPresenter | - |
| cell of gamma (not selected) with Ctrl held | 1, 2 | True | TextBlock | ContentPresenter | - |
| Name cell of gamma, DataGrid and columns IsReadOnly=False | 1, 2 | True | TextBlock | null | True |
| Name cell of beta (selected), DataGrid and columns IsReadOnly=False | 1 | (no ClickCount=2) | (not raised) | - | - |
| Name cell of gamma, DataGrid IsReadOnly=False, only the Name column IsReadOnly=True | 1, 2 | False | TextBlock | ContentPresenter | False |
| City cell of gamma, DataGrid IsReadOnly=False, only the Name column IsReadOnly=True | 1, 2 | True | TextBlock | null | True |
