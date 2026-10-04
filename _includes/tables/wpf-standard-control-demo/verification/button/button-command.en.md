| case | measured |
|---|---|
| CanExecute false: IsEnabled / with IsEnabled=\"True\" set | False / False |
| CanExecute turns true: IsEnabled / after InvalidateRequerySuggested | False / True |
| 20 buttons, same command: CanExecute calls per focus change | 20 |
| demo XAML: parameters passed to CanExecute while loading | ShowMessageText, ShowMessageText |
| TextBox.Text set to empty from code: CanExecute calls / IsEnabled | 1 / False |
| TextBox.Text set to Hello, then clicked: parameter of Execute | Hello |
| item template, CommandParameter=\"&#123;Binding&#125;\": real click on the second row: Execute received | Row 2 |
