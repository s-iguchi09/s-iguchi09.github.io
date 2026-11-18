| list | how the change is stopped | SelectedCustomer (view model) | SelectedItem / SelectedIndex | row highlighted on screen | CurrentItem | detail form | first row in the list | list IsEnabled just before the mouse press |
|---|---|---|---|---|---|---|---|---|
| ListBox | nothing (copy for editing) | \"beta\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"beta\" | \"alpha\" | True |
| ListBox | nothing (IEditableObject) | \"beta\" | \"beta\" / 1 | \"beta\" | \"edited\" | \"beta\" | \"edited\" | True |
| ListBox | reject in the setter, raise PropertyChanged | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| ListBox | reject in the setter, raise PropertyChanged via Dispatcher.BeginInvoke (Normal priority) | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| ListBox | IsSynchronizedWithCurrentItem, e.Cancel = true in CurrentChanging | \"alpha\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| ListBox | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\", detail with UpdateSourceTrigger=PropertyChanged | \"alpha\" | \"alpha\" / 0 | \"alpha\" | \"alpha\" | \"edited\" | \"alpha\" | False |
| DataGrid | reject in the setter, raise PropertyChanged | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| DataGrid | reject in the setter, raise PropertyChanged via Dispatcher.BeginInvoke (Normal priority) | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| DataGrid | IsSynchronizedWithCurrentItem, e.Cancel = true in CurrentChanging | \"alpha\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| DataGrid | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\", detail with UpdateSourceTrigger=PropertyChanged | \"alpha\" | \"alpha\" / 0 | \"alpha\" | \"alpha\" | \"edited\" | \"alpha\" | False |
| ListBox | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\", detail with the default trigger | \"beta\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"beta\" | \"alpha\" | True |
| ListBox | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\", detail with UpdateSourceTrigger=PropertyChanged, click Cancel first | \"beta\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"beta\" | \"alpha\" | False → True (Cancel clicked in between) |
