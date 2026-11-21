| double-clicked | SelectedItem after | other effects | EventTrigger on DataGrid + SelectedItem | MouseBinding on DataGrid + SelectedItem | EventTrigger + EventArgsConverter (row item) | RowStyle + attached property (row item) |
|---|---|---|---|---|---|---|
| cell of gamma (row 3) | \"gamma\" | - | \"gamma\" | \"gamma\" | \"gamma\" | \"gamma\" |
| Name column header | \"beta\" | SortDirection: Descending | \"beta\" | not executed | not executed | not executed |
| vertical scroll bar thumb | \"beta\" | - | \"beta\" | not executed | not executed | not executed |
| vertical scroll bar track below the thumb | \"beta\" | VerticalOffset &gt; 0: True | \"beta\" | not executed | not executed | not executed |
| blank area below the last row (3 rows) | \"beta\" | - | \"beta\" | \"beta\" | not executed | not executed |
| blank area below the last row (3 rows), nothing selected beforehand | null | - | not executed | not executed | not executed | not executed |
| cell of beta (selected) | \"beta\" | - | \"beta\" | \"beta\" | \"beta\" | \"beta\" |
| cell of beta (selected) with Ctrl held | \"beta\" | - | \"beta\" | not executed | \"beta\" | \"beta\" |
| cell of gamma (not selected) with Ctrl held | \"beta\" | - | \"beta\" | not executed | \"gamma\" | \"gamma\" |
| cell of gamma, IsReadOnly=False | \"gamma\" | IsEditing: True | \"gamma\" | not executed | not executed | \"gamma\" |
| cell of gamma with the right button | \"gamma\" | - | \"gamma\" | not executed | not executed | not executed |
