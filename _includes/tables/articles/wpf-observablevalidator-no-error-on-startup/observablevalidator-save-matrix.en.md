| how Save is guarded | Save button IsEnabled on startup | HasError on startup | times the save went through after clicking Save | HasError after clicking Save | Save button IsEnabled after typing a valid name |
|---|---|---|---|---|---|
| CanExecute: !HasErrors, \[NotifyDataErrorInfo\] only | True | False | 1 | False | True |
| CanExecute: !HasErrors, plus ValidateAllProperties() in the constructor | False | True | - (cannot click) | - (cannot click) | True |
| no CanExecute, ValidateAllProperties() at the start of Save | True | False | 0 | True | True |
