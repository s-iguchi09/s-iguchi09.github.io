| キー | KeyBinding の書き方 | 結果 |
|---|---|---|
| A | Key=\"A\" | DeleteCommand |
| F2 | Key=\"F2\" | DeleteCommand |
| Enter | Key=\"Enter\" | SubmitCommand |
| A | Gesture=\"A\" | XAML の読み込みで XamlParseException |
| F2 | Gesture=\"F2\" | DeleteCommand |
| A | new KeyGesture(Key.A) | NotSupportedException |
| A | new KeyBinding(command, Key.A, ModifierKeys.None) | NotSupportedException |
