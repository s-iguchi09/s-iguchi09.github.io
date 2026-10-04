| case | measured |
|---|---|
| base class of TreeView / TreeViewItem | ItemsControl / HeaderedItemsControl |
| SelectedItem / SelectedValue read-only | True / True |
| IsExpanded / IsSelected: BindsTwoWayByDefault | False / True |
| SetBinding(TreeView.SelectedItemProperty, \.\.\.) | ArgumentException |
| XAML TreeViewItems, SelectedValuePath=\"Header\"; select \"Gaming PC\": SelectedItem / SelectedValue | TreeViewItem / Gaming PC |
| ItemsSource data; source Desktop.IsSelected = true: SelectedItem | ToString:Desktop |
| then source Mobile.IsSelected = true: SelectedItem / Desktop.IsSelected / Mobile.IsSelected | ToString:Mobile / False / True |
| Node 2 IsSelectionActive: nothing selected, no focus | False |
| Node 2 selected from code (IsSelected / IsSelectionActive) | True / False |
| Node 2 focused | True / True |
| focus moved to a TextBox | True / False |
