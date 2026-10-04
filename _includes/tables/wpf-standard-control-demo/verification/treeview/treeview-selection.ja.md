| 条件 | 計測値 |
|---|---|
| TreeView / TreeViewItem の基底クラス | ItemsControl / HeaderedItemsControl |
| SelectedItem / SelectedValue が読み取り専用か | True / True |
| IsExpanded / IsSelected: BindsTwoWayByDefault | False / True |
| SetBinding(TreeView.SelectedItemProperty, \.\.\.) | ArgumentException |
| XAML の TreeViewItem、SelectedValuePath=\"Header\"、\"Gaming PC\" を選択: SelectedItem / SelectedValue | TreeViewItem / Gaming PC |
| ItemsSource のデータ、ソースの Desktop.IsSelected = true: SelectedItem | ToString:Desktop |
| 続けてソースの Mobile.IsSelected = true: SelectedItem / Desktop.IsSelected / Mobile.IsSelected | ToString:Mobile / False / True |
| Node 2 の IsSelectionActive: 選択なし、フォーカスなし | False |
| コードで Node 2 を選択（IsSelected / IsSelectionActive） | True / False |
| Node 2 にフォーカス | True / True |
| フォーカスを TextBox に移す | True / False |
