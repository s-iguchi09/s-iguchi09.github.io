| 条件 | 計測値 |
|---|---|
| MaxLength=8、\"1234567890\" を入力 | Password \"12345678\" |
| MaxLength=8、コードから Password = \"1234567890\" | Password \"1234567890\" |
| PasswordChanged の回数: \"abc\" を 1 文字ずつ入力 / Password = \"xyz\" / Clear() | 3 / 1 / 1 |
| Clear() の後の Password | \"\" |
| 全選択: Copy / Cut / Paste の CanExecute | False / False / True |
| IsSelectionActive: フォーカスを移す前 | False |
| フォーカスあり、選択なし | True |
| フォーカスあり、全選択 | True |
| フォーカスを別のコントロールへ移す（選択は残る） | False |
