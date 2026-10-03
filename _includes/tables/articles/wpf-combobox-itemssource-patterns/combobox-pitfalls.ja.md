| 条件 | 計測値 |
|---|---|
| DisplayMemberPath + ItemTemplate（コード） | InvalidOperationException |
| DisplayMemberPath + ItemTemplate（XAML） | XamlParseException（内側の例外 InvalidOperationException） |
| SelectedValuePath=Id（int）、ソースは string のプロパティで \"20\" | 表示時: SelectedIndex 1、インデックス 2 を選んだ後のソース = 30（String） |
| SelectedValuePath=Id（int）、ソースは object のプロパティで \"20\" を保持 | 表示時: SelectedIndex 1、インデックス 2 を選んだ後のソース = 30（Int32） |
| 列挙型の項目、SelectedValuePath=value\_\_ | SelectedItem High, SelectedValue null |
| ItemsSource より前に SelectedValue = 20（コード） | SelectedIndex 1 |
| SelectedValue を 30 にバインド、表示した後に ItemsSource を設定 | SelectedIndex 2、ソース 30 |
