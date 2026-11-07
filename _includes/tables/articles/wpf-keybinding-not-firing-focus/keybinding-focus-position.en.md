| key | where the KeyBinding is | before pressing the key | keyboard focus when the key is pressed | isolation step that applies | Window\'s PreviewKeyDown raised | command that ran |
|---|---|---|---|---|---|---|
| Ctrl+S | Window | Keyboard.Focus(titleBox) | TextBox in the Window | 4: on the path | raised | SaveCommand |
| Ctrl+S | Window | Keyboard.Focus(okButton) | Button | 4: on the path | raised | SaveCommand |
| Ctrl+S | Window | show and Activate only | Window | 4: on the path | raised | SaveCommand |
| Ctrl+S | Window | Keyboard.ClearFocus() | none (Keyboard.FocusedElement is null) | 1: no focus | not raised | none |
| Ctrl+S | Window | Keyboard.ClearFocus(), then Keyboard.Focus(window) | Window | 4: on the path | raised | SaveCommand |
| Ctrl+S | Window | Keyboard.Focus(okButton), then okButton.IsEnabled = false | Window | 4: on the path | raised | SaveCommand |
| Ctrl+S | Window | Keyboard.Focus(titleBox), then titleBox.Visibility = Collapsed | Window | 4: on the path | raised | SaveCommand |
| Ctrl+S | Window | focus the TextBox in an owned window | TextBox in the owned window | 2: another window | not raised | none |
| Ctrl+S | Window and the owned window | focus the TextBox in an owned window | TextBox in the owned window | 4: on the path | not raised | SaveCommand |
| Ctrl+S | Window and the owned window (no DataContext on the owned window) | focus the TextBox in an owned window | TextBox in the owned window | 4: on the path | not raised | none |
| Ctrl+D | UserControl | Keyboard.Focus(panelBox) | TextBox in the UserControl | 4: on the path | raised | DeleteCommand |
| Ctrl+D | UserControl | Keyboard.Focus(titleBox) | TextBox in the Window | 3: outside the element | raised | none |
| Ctrl+D | UserControl | Keyboard.Focus(titleBox), then click the TextBlock in the UserControl | TextBox in the Window | 3: outside the element | raised | none |
| Ctrl+D | UserControl with Focusable=\"True\" | Keyboard.Focus(titleBox), then click the TextBlock in the UserControl | TextBox in the Window | 3: outside the element | raised | none |
| Ctrl+D | UserControl | Keyboard.Focus(panel) | Window | 3: outside the element | raised | none |
| Ctrl+D | UserControl with Focusable=\"True\" | Keyboard.Focus(panel) | UserControl | 4: on the path | raised | DeleteCommand |
| Ctrl+D | Window (removed from the UserControl) | Keyboard.Focus(titleBox) | TextBox in the Window | 4: on the path | raised | DeleteCommand |
