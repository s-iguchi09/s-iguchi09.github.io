| key | how the KeyBinding is written | result |
|---|---|---|
| A | Key=\"A\" | DeleteCommand |
| F2 | Key=\"F2\" | DeleteCommand |
| Enter | Key=\"Enter\" | SubmitCommand |
| A | Gesture=\"A\" | XamlParseException when loading the XAML |
| F2 | Gesture=\"F2\" | DeleteCommand |
| A | new KeyGesture(Key.A) | NotSupportedException |
| A | new KeyBinding(command, Key.A, ModifierKeys.None) | NotSupportedException |
