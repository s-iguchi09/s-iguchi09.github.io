| case | measured |
|---|---|
| MaxLength=8, typed \"1234567890\" | Password \"12345678\" |
| MaxLength=8, Password = \"1234567890\" from code | Password \"1234567890\" |
| PasswordChanged count: typing \"abc\" one character at a time / Password = \"xyz\" / Clear() | 3 / 1 / 1 |
| Password after Clear() | \"\" |
| all text selected: CanExecute of Copy / Cut / Paste | False / False / True |
| IsSelectionActive: before focusing | False |
| focused, nothing selected | True |
| focused, all text selected | True |
| focus moved to another control (selection kept) | False |
