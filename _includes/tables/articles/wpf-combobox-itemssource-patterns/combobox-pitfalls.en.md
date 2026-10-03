| case | measured |
|---|---|
| DisplayMemberPath + ItemTemplate (code) | InvalidOperationException |
| DisplayMemberPath + ItemTemplate (XAML) | XamlParseException (inner InvalidOperationException) |
| SelectedValuePath=Id (int), source string property \"20\" | shown: SelectedIndex 1; after selecting index 2, source = 30 (String) |
| SelectedValuePath=Id (int), source object property holding \"20\" | shown: SelectedIndex 1; after selecting index 2, source = 30 (Int32) |
| enum items, SelectedValuePath=value\_\_ | SelectedItem High, SelectedValue null |
| SelectedValue = 20 set before ItemsSource (code) | SelectedIndex 1 |
| SelectedValue bound to 30, ItemsSource assigned after display | SelectedIndex 2, source 30 |
