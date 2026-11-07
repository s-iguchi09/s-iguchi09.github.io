| key | where the KeyBinding is | before pressing the key | keyboard focus when the key is pressed | isolation step that applies | view-model command that ran | titleBox text after the key |
|---|---|---|---|---|---|---|
| Ctrl+Z | Window | Keyboard.Focus(titleBox) | TextBox in the Window | 4: on the path | none | \"\" |
| Ctrl+Z | Window | Keyboard.Focus(titleBox), then type \"abc\" | TextBox in the Window | 4: on the path | none | \"\" |
| Ctrl+Z | Window | Keyboard.Focus(okButton) | Button | 4: on the path | UndoCommand | - |
| Ctrl+Z | Window and titleBox.InputBindings (TextUndoCommand) | Keyboard.Focus(titleBox), then type \"abc\" | TextBox in the Window | 4: on the path | TextUndoCommand | \"abc\" |
| Ctrl+Z | Window and Window.PreviewKeyDown (TextUndoCommand) | Keyboard.Focus(titleBox), then type \"abc\" | TextBox in the Window | 4: on the path | TextUndoCommand | \"abc\" |
| Enter | Window | Keyboard.Focus(titleBox) | TextBox in the Window | 4: on the path | SubmitCommand | \"\" |
| Enter | Window (titleBox with AcceptsReturn=\"True\") | Keyboard.Focus(titleBox) | TextBox in the Window | 4: on the path | none | \"\\r\\n\" |
| Enter | Window | Keyboard.Focus(okButton) | Button | 4: on the path | OkCommand | - |
