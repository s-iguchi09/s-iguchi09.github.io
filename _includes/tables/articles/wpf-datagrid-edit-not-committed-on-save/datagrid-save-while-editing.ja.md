| 保存の操作 | 保存処理で呼ぶもの | DataGrid から出たフォーカスの移り先 | 保存時のキーボードフォーカス | 戻り値 | ソースの値 | EndEdit | Refresh | 保存後の編集中: セル / 行 |
|---|---|---|---|---|---|---|---|---|
| ToolBar の Button をクリック | なし | ToolBar の Button | セルの TextBox | - | \"alpha\" | 0 | InvalidOperationException | True / True |
| Menu の MenuItem をクリック | なし | Menu の MenuItem | セルの TextBox | - | \"alpha\" | 0 | InvalidOperationException | True / True |
| Ctrl+S（Window の KeyBinding） | なし | 出ない | セルの TextBox | - | \"alpha\" | 0 | InvalidOperationException | True / True |
| ToolBar の外の Button をクリック | なし | ToolBar の外の Button | ToolBar の外の Button | - | \"edited\" | 2 | 成功 | False / False |
| FocusManager.IsFocusScope=\"False\" の ToolBar の Button をクリック | なし | ToolBar の Button | ToolBar の Button | - | \"edited\" | 2 | 成功 | False / False |
| FocusManager.IsFocusScope=\"False\" の Menu の MenuItem をクリック | なし | Menu の MenuItem | Menu の MenuItem | - | \"edited\" | 2 | 成功 | False / False |
| ToolBar の Button をクリック | CommitEdit() | ToolBar の Button | セルの TextBox | True | \"alpha\" | 0 | InvalidOperationException | False / True |
| ToolBar の Button をクリック | CommitEdit() を 2 回 | ToolBar の Button | セルの TextBox | True, True | \"edited\" | 2 | 成功 | False / False |
| ToolBar の Button をクリック | CommitEdit(DataGridEditingUnit.Row, true) | ToolBar の Button | セルの TextBox | True | \"edited\" | 2 | 成功 | False / False |
| Menu の MenuItem をクリック | CommitEdit(DataGridEditingUnit.Row, true) | Menu の MenuItem | セルの TextBox | True | \"edited\" | 2 | 成功 | False / False |
| Ctrl+S（Window の KeyBinding） | CommitEdit(DataGridEditingUnit.Row, true) | 出ない | セルの TextBox | True | \"edited\" | 2 | 成功 | False / False |
| ToolBar の外の Button をクリック | CommitEdit(DataGridEditingUnit.Row, true) | ToolBar の外の Button | ToolBar の外の Button | True | \"edited\" | 2 | 成功 | False / False |
| ToolBar の Button をクリック（IEditableObject の無いアイテム） | CommitEdit() | ToolBar の Button | セルの TextBox | True | \"alpha\" | - | InvalidOperationException | False / True |
| ToolBar の Button をクリック（IEditableObject の無いアイテム） | CommitEdit(DataGridEditingUnit.Row, true) | ToolBar の Button | セルの TextBox | True | \"edited\" | - | 成功 | False / False |
| ToolBar の Button をクリック（Name 列に UpdateSourceTrigger=PropertyChanged） | なし | ToolBar の Button | セルの TextBox | - | \"edited\" | 0 | InvalidOperationException | True / True |
| ToolBar の Button をクリック（int の Quantity 列に \"abc\" を入力） | CommitEdit(DataGridEditingUnit.Row, true) | ToolBar の Button | セルの TextBox | False | 1 | 0 | InvalidOperationException | True / True |
| ToolBar の Button をクリック（Quantity に \"abc\"、5 に直してもう一度クリック） | CommitEdit(DataGridEditingUnit.Row, true) | ToolBar の Button | セルの TextBox | False → True | 5 | 2 | 成功 | False / False |
| ToolBar の Button をクリック（セルを編集していない） | CommitEdit(DataGridEditingUnit.Row, true) | ToolBar の Button | DataGrid | True | \"alpha\" | 0 | 成功 | False / False |
