| binding of the column | item | source while typing | source after Esc twice | CancelEdit |
|---|---|---|---|---|
| &#123;Binding Name&#125; | implements IEditableObject | \"alpha\" | \"alpha\" | 2 |
| &#123;Binding Name&#125; | INotifyPropertyChanged only | \"alpha\" | \"alpha\" | - |
| &#123;Binding Name, UpdateSourceTrigger=PropertyChanged&#125; | implements IEditableObject | \"edited\" | \"alpha\" | 2 |
| &#123;Binding Name, UpdateSourceTrigger=PropertyChanged&#125; | INotifyPropertyChanged only | \"edited\" | \"edited\" | - |
