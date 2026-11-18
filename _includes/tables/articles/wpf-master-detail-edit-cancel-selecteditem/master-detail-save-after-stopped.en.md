| list | how the change is stopped | row highlighted on screen | first row after Save | second row after Save |
|---|---|---|---|---|
| ListBox | reject in the setter, raise PropertyChanged | \"beta\" | \"edited\" | \"beta\" |
| ListBox | IsSynchronizedWithCurrentItem, e.Cancel = true in CurrentChanging | \"beta\" | \"edited\" | \"beta\" |
| DataGrid | reject in the setter, raise PropertyChanged | \"beta\" | \"edited\" | \"beta\" |
| DataGrid | IsSynchronizedWithCurrentItem, e.Cancel = true in CurrentChanging | \"beta\" | \"edited\" | \"beta\" |
