| implementation / what was called | before | after |
|---|---|---|
| RequerySuggested / (nothing) | False | False |
| RequerySuggested / InvalidateRequerySuggested | False | True |
| RequerySuggested / key typed in a TextBox | False | True |
| own event / (nothing) | False | False |
| own event / InvalidateRequerySuggested | False | False |
| own event / RaiseCanExecuteChanged | False | True |
| own event / key typed in a TextBox | False | False |
| Command = null | True | True |
