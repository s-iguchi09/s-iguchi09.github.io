| キー | KeyBinding の置き場所 | キーを押す前の操作 | キーを押したときのキーボードフォーカス | 実行されたコマンド | キーの後の titleBox の文字列 |
|---|---|---|---|---|---|
| Ctrl+S | Window | Keyboard.Focus(titleBox) | Window の TextBox | SaveCommand | - |
| Ctrl+S | Window | Keyboard.Focus(panelBox) | UserControl の TextBox | SaveCommand | - |
| Ctrl+D | Window | Keyboard.Focus(titleBox) | Window の TextBox | DeleteCommand | - |
| Ctrl+D | Window | Keyboard.Focus(panelBox) | UserControl の TextBox | DeleteCommand | - |
| Ctrl+Z | titleBox.InputBindings | Keyboard.Focus(titleBox) の後、\"abc\" を入力 | Window の TextBox | UndoCommand | \"abc\" |
| Enter | Window | Keyboard.Focus(titleBox) | Window の TextBox | SubmitCommand | \"\" |
| Enter | Window | Keyboard.Focus(okButton) | Button | OkCommand | - |
