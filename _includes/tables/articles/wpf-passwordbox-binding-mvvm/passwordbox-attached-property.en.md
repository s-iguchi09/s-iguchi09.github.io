| case | measured |
|---|---|
| subscribe in the change callback, ViewModel starts with \"\": type \"abc\" | ViewModel.Password \"\" |
| set Password back without comparing: type \"abc\" | PasswordBox \"cba\" / ViewModel \"cba\" |
| article\'s attached property: type \"abc\" | PasswordBox \"abc\" / ViewModel \"abc\" |
| then the ViewModel sets Password = \"\" | PasswordBox \"\" |
| button IsEnabled before / after typing | False / True |
