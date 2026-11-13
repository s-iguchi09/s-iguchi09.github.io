| 条件 | 計測値 |
|---|---|
| CommandParameter=\"&#123;Binding SecurePassword, ElementName=PasswordInput&#125;\": \"abc\" を入力して押す | SecureString.Length 0 |
| CommandParameter=\"&#123;Binding ElementName=PasswordInput&#125;\": \"abc\" を入力して押す（Execute で SecurePassword を読む） | SecureString.Length 3 |
| 同じ渡し方で CanExecute が SecurePassword.Length &gt; 0 を見る: ボタンの IsEnabled 入力の前 / 後 | False / False |
