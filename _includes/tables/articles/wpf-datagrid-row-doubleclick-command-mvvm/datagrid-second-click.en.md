| double-clicked | ClickCount of the two mouse-downs | MouseLeftButtonDown (ClickCount=2) handled at the DataGrid | OriginalSource of DataGrid.MouseDoubleClick | its visual parent at that time | IsEditing of the double-clicked cell at that time | mouse-downs reaching the window | MouseDoubleClick of the double-clicked row |
|---|---|---|---|---|---|---|---|
| cell of gamma (row 3) | 1, 2 | False | TextBlock | ContentPresenter | - | 2 | raised |
| Name column header | 1, 2 | True | DataGridHeaderBorder | Grid | - | 2 | - |
| vertical scroll bar thumb | 1, 2 | True | Rectangle | Thumb | - | 2 | - |
| vertical scroll bar track below the thumb | 1, 2 | True | Rectangle | RepeatButton | - | 2 | - |
| blank area below the last row (3 rows) | 1, 2 | False | ScrollViewer | Border | - | 2 | - |
| cell of beta (selected) with Ctrl held | 1, 2 | True | TextBlock | ContentPresenter | - | 2 | raised |
| cell of gamma (not selected) with Ctrl held | 1, 2 | True | TextBlock | ContentPresenter | - | 2 | raised |
| Name cell of gamma, DataGrid and columns IsReadOnly=False | 1, 2 | True | TextBlock | null | True | 2 | raised |
| Name cell of beta (selected), DataGrid and columns IsReadOnly=False | 1, 2 | True | TextBoxView | ScrollContentPresenter | True | 2 | raised |
| Name cell of gamma, DataGrid IsReadOnly=False, only the Name column IsReadOnly=True | 1, 2 | False | TextBlock | ContentPresenter | False | 2 | raised |
| City cell of gamma, DataGrid IsReadOnly=False, only the Name column IsReadOnly=True | 1, 2 | True | TextBlock | null | True | 2 | raised |
