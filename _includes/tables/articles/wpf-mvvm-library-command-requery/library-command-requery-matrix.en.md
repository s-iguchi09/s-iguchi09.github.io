| command and view model | before typing | after setting the name | after InvalidateRequerySuggested | after moving focus |
|---|---|---|---|---|
| own RelayCommand, CanExecuteChanged =&gt; CommandManager.RequerySuggested (name typed) | False | True | True | True |
| same command, name assigned to the view model in code | False | False | True | True |
| Toolkit RelayCommand, setter calls SetProperty only | False | False | False | False |
| Toolkit RelayCommand, setter calls NotifyCanExecuteChanged | False | True | True | True |
| Toolkit \[ObservableProperty\] + \[NotifyCanExecuteChangedFor\] | False | True | True | True |
| Prism DelegateCommand, setter calls SetProperty only | False | False | False | False |
| Prism DelegateCommand, setter calls RaiseCanExecuteChanged | False | True | True | True |
| Prism DelegateCommand + ObservesProperty(() =&gt; Name) | False | True | True | True |
