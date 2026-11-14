| 条件 | 計測値 |
|---|---|
| CommandParameter=\"&#123;Binding SecurePassword, ElementName=PasswordInput&#125;\": \"abc\" を入力して押す | SecureString.Length 0 |
| CommandParameter=\"&#123;Binding Password, ElementName=PasswordInput&#125;\": \"abc\" を入力して押す | String.Length 0 |
| 記事の SignInViewModel・CommandParameter=\"&#123;Binding ElementName=PasswordInput&#125;\": \"abc\" を入力して押す | SecureString.Length 3 |
| RelayCommand&lt;PasswordBox&gt; の CanExecute が SecurePassword.Length &gt; 0 を見る: ボタンの IsEnabled: 入力の前 / 後、(CommandParameter の型; RaiseCanExecuteChanged を呼んだ後) | False / False (PasswordBox; True) |
