| case | measured |
|---|---|
| subscribe in the change callback, ViewModel starts with \"\": type \"abc\" | ViewModel.Password \"\" |
| set Password back without comparing: type \"abc\" | PasswordBox \"cba\" / ViewModel \"cba\" |
| no attached property: type \"ab\", then type \"c\" (Password not set) | PasswordBox \"abc\" |
| no attached property: type \"ab\", set Password = \"ab\" in code, then type \"c\" | PasswordBox \"cab\" |
| article\'s attached property: type \"abc\" | PasswordBox \"abc\" / ViewModel \"abc\" |
| then the ViewModel sets Password = \"\" | PasswordBox \"\" |
| button IsEnabled before / after typing | False / True |
