| key | where the KeyBinding is | before pressing the key | keyboard focus when the key is pressed | command that ran | titleBox text after the key |
|---|---|---|---|---|---|
| Ctrl+S | Window | Keyboard.Focus(titleBox) | TextBox in the Window | SaveCommand | - |
| Ctrl+S | Window | Keyboard.Focus(panelBox) | TextBox in the UserControl | SaveCommand | - |
| Ctrl+D | Window | Keyboard.Focus(titleBox) | TextBox in the Window | DeleteCommand | - |
| Ctrl+D | Window | Keyboard.Focus(panelBox) | TextBox in the UserControl | DeleteCommand | - |
| Ctrl+Z | titleBox.InputBindings | Keyboard.Focus(titleBox), then type \"abc\" | TextBox in the Window | UndoCommand | \"abc\" |
| Enter | Window | Keyboard.Focus(titleBox) | TextBox in the Window | SubmitCommand | \"\" |
| Enter | Window | Keyboard.Focus(okButton) | Button | OkCommand | - |
