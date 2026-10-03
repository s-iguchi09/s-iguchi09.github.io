| what was measured | result |
|---|---|
| TreeView.SelectedItemProperty.ReadOnly | True |
| SetValue(SelectedItemProperty) from outside | throws InvalidOperationException |
| child container before expanding | null |
| right after IsExpanded = true, before a layout pass | null |
| after IsExpanded = true and UpdateLayout() | TreeViewItem |
| TreeView.SelectedItem after child.IsSelected = true | the \'child\' item |
