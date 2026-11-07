| キー | KeyBinding の置き場所 | キーを押す前の操作 | キーを押したときのキーボードフォーカス | 切り分けの手順の判定 | 実行されたビューモデルのコマンド | キーの後の titleBox の文字列 |
|---|---|---|---|---|---|---|
| Ctrl+Z | Window | Keyboard.Focus(titleBox) | Window の TextBox | 4: 経路の上 | なし | \"\" |
| Ctrl+Z | Window | Keyboard.Focus(titleBox) の後、\"abc\" を入力 | Window の TextBox | 4: 経路の上 | なし | \"\" |
| Ctrl+Z | Window | Keyboard.Focus(okButton) | Button | 4: 経路の上 | UndoCommand | - |
| Ctrl+Z | Window と titleBox.InputBindings（TextUndoCommand） | Keyboard.Focus(titleBox) の後、\"abc\" を入力 | Window の TextBox | 4: 経路の上 | TextUndoCommand | \"abc\" |
| Ctrl+Z | Window と Window.PreviewKeyDown（TextUndoCommand） | Keyboard.Focus(titleBox) の後、\"abc\" を入力 | Window の TextBox | 4: 経路の上 | TextUndoCommand | \"abc\" |
| Enter | Window | Keyboard.Focus(titleBox) | Window の TextBox | 4: 経路の上 | SubmitCommand | \"\" |
| Enter | Window（titleBox は AcceptsReturn=\"True\"） | Keyboard.Focus(titleBox) | Window の TextBox | 4: 経路の上 | なし | \"\\r\\n\" |
| Enter | Window | Keyboard.Focus(okButton) | Button | 4: 経路の上 | OkCommand | - |
