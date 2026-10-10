| 列のバインド | アイテム | 入力中のソース | Esc を 2 回押した後のソース | CancelEdit |
|---|---|---|---|---|
| &#123;Binding Name&#125; | IEditableObject を実装 | \"alpha\" | \"alpha\" | 2 |
| &#123;Binding Name&#125; | INotifyPropertyChanged のみ | \"alpha\" | \"alpha\" | - |
| &#123;Binding Name, UpdateSourceTrigger=PropertyChanged&#125; | IEditableObject を実装 | \"edited\" | \"alpha\" | 2 |
| &#123;Binding Name, UpdateSourceTrigger=PropertyChanged&#125; | INotifyPropertyChanged のみ | \"edited\" | \"edited\" | - |
