| detail form bound to | source right after typing | source when Cancel runs | list after Cancel | detail form after Cancel |
|---|---|---|---|---|
| SelectedCustomer.Name (default trigger) | \"alpha\" | \"edited\" | \"edited\" | \"edited\" |
| SelectedCustomer.Name, UpdateSourceTrigger=PropertyChanged | \"edited\" | \"edited\" | \"edited\" | \"edited\" |
| Editing.Name (copy for editing) | \"alpha\" | \"alpha\" | \"alpha\" | \"alpha\" |
| SelectedCustomer.Name with IEditableObject | \"alpha\" | \"edited\" | \"alpha\" | \"alpha\" |
