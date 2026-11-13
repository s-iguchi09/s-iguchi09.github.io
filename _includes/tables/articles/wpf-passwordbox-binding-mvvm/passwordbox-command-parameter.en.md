| case | measured |
|---|---|
| CommandParameter=\"&#123;Binding SecurePassword, ElementName=PasswordInput&#125;\": type \"abc\", then click | SecureString.Length 0 |
| CommandParameter=\"&#123;Binding Password, ElementName=PasswordInput&#125;\": type \"abc\", then click | String.Length 0 |
| article\'s SignInViewModel, CommandParameter=\"&#123;Binding ElementName=PasswordInput&#125;\": type \"abc\", then click | SecureString.Length 3 |
| same, with CanExecute checking SecurePassword.Length &gt; 0: button IsEnabled before / after typing | False / False |
