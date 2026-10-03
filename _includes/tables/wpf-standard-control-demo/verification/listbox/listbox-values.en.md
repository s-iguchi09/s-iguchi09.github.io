| case | measured |
|---|---|
| SelectedIndex = 2 | SelectedIndex 2, SelectedItem Item 3, SelectionChanged 1 |
| then SelectedIndex = 10 (4 items) | no exception; SelectedIndex 2 |
| then SelectedIndex = -1 | SelectedIndex -1, SelectedItem null |
| class without Equals: SelectedItem = new instance with the same data | SelectedIndex -1, SelectedItem null |
| record (value equality): SelectedItem = new instance with the same data | SelectedIndex 1, same instance as the item: False |
| SelectedValue = 2 before ItemsSource, then ItemsSource set | SelectedValue 2 -&gt; SelectedIndex 1, SelectedValue 2 |
| DisplayMemberPath=\"Name\": element showing item 1 | TextBlock, Text \"Desktop\" |
| DisplayMemberPath=\"Nope\": element showing item 1 | TextBlock, Text \"\" |
| DisplayMemberPath and ItemTemplate both set | InvalidOperationException when set; not shown |
