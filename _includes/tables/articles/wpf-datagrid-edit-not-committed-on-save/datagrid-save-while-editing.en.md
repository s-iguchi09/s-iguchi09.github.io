| save input | called in the save handler | where focus went when it left the DataGrid | keyboard focus when saving | return value | source value | EndEdit | Refresh | after saving: cell / row editing |
|---|---|---|---|---|---|---|---|---|
| click the Button in the ToolBar | nothing | Button in the ToolBar | TextBox in the cell | - | \"alpha\" | 0 | InvalidOperationException | True / True |
| click the MenuItem in the Menu | nothing | MenuItem in the Menu | TextBox in the cell | - | \"alpha\" | 0 | InvalidOperationException | True / True |
| Ctrl+S (KeyBinding on the Window) | nothing | did not leave | TextBox in the cell | - | \"alpha\" | 0 | InvalidOperationException | True / True |
| click a Button outside the ToolBar | nothing | Button outside the ToolBar | Button outside the ToolBar | - | \"edited\" | 2 | succeeds | False / False |
| click the Button in a ToolBar with FocusManager.IsFocusScope=\"False\" | nothing | Button in the ToolBar | Button in the ToolBar | - | \"edited\" | 2 | succeeds | False / False |
| click the MenuItem in a Menu with FocusManager.IsFocusScope=\"False\" | nothing | MenuItem in the Menu | MenuItem in the Menu | - | \"edited\" | 2 | succeeds | False / False |
| click the Button in the ToolBar | CommitEdit() | Button in the ToolBar | TextBox in the cell | True | \"alpha\" | 0 | InvalidOperationException | False / True |
| click the Button in the ToolBar | CommitEdit() twice | Button in the ToolBar | TextBox in the cell | True, True | \"edited\" | 2 | succeeds | False / False |
| click the Button in the ToolBar | CommitEdit(DataGridEditingUnit.Row, true) | Button in the ToolBar | TextBox in the cell | True | \"edited\" | 2 | succeeds | False / False |
| click the MenuItem in the Menu | CommitEdit(DataGridEditingUnit.Row, true) | MenuItem in the Menu | TextBox in the cell | True | \"edited\" | 2 | succeeds | False / False |
| Ctrl+S (KeyBinding on the Window) | CommitEdit(DataGridEditingUnit.Row, true) | did not leave | TextBox in the cell | True | \"edited\" | 2 | succeeds | False / False |
| click a Button outside the ToolBar | CommitEdit(DataGridEditingUnit.Row, true) | Button outside the ToolBar | Button outside the ToolBar | True | \"edited\" | 2 | succeeds | False / False |
| click the Button in the ToolBar (item without IEditableObject) | CommitEdit() | Button in the ToolBar | TextBox in the cell | True | \"alpha\" | - | InvalidOperationException | False / True |
| click the Button in the ToolBar (item without IEditableObject) | CommitEdit(DataGridEditingUnit.Row, true) | Button in the ToolBar | TextBox in the cell | True | \"edited\" | - | succeeds | False / False |
| click the Button in the ToolBar (Name column with UpdateSourceTrigger=PropertyChanged) | nothing | Button in the ToolBar | TextBox in the cell | - | \"edited\" | 0 | InvalidOperationException | True / True |
| click the Button in the ToolBar (\"abc\" typed into the int Quantity column) | CommitEdit(DataGridEditingUnit.Row, true) | Button in the ToolBar | TextBox in the cell | False | 1 | 0 | InvalidOperationException | True / True |
| click the Button in the ToolBar (\"abc\" in Quantity, then corrected to 5 and clicked again) | CommitEdit(DataGridEditingUnit.Row, true) | Button in the ToolBar | TextBox in the cell | False → True | 5 | 2 | succeeds | False / False |
| click the Button in the ToolBar (no cell has been edited) | CommitEdit(DataGridEditingUnit.Row, true) | Button in the ToolBar | DataGrid | True | \"alpha\" | 0 | succeeds | False / False |
