| item | value |
|---|---|
| base class | Control |
| has PasswordProperty / PasswordCharProperty | False / True |
| PasswordChar (metadata default) | \'\*\' (U+002A) |
| MaxLength / SelectionOpacity / IsInactiveSelectionHighlightEnabled | 0 / 0.4 / False |
| PasswordChar with the default style (value source) | \'●\' (U+25CF, DefaultStyle) |
| CaretBrush / SelectionBrush (value source) | null (Default) / \#FF0078D7 (Default) |
| internal text container / its text fields | PasswordTextContainer / \_password: SecureString |
| Password = \"secret\": Password | String \"secret\" |
| SecurePassword | SecureString, Length 6, read-only False |
| SecurePassword twice: same instance | False |
| XAML Password=\"PASSWORD\" (the demo app\'s markup) | Password \"PASSWORD\" |
