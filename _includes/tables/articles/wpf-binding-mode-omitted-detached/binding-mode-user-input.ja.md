| バインドと操作 | 操作後: 表示 / ソース | 操作後のバインド | 操作後の値の供給元 | ソースに代入した後の表示 | バインドエラー |
|---|---|---|---|---|---|
| ItemContainerStyle で &#123;Binding IsExpanded&#125;、展開ボタンをクリック | True / False | 外れる | Local | True | 0 |
| ItemContainerStyle で &#123;Binding IsExpanded&#125;、ノードを選んで右矢印キー（→） | True / False | 残る | Style、式、現在値 | False | 0 |
| ItemContainerStyle で &#123;Binding IsExpanded&#125;、見出しをダブルクリック | True / False | 残る | Style、式、現在値 | False | 0 |
| ItemContainerStyle で &#123;Binding IsExpanded&#125;、展開ボタンの IsChecked にコードで True を設定 | True / False | 外れる | Local | True | 0 |
| ItemContainerStyle で &#123;Binding IsExpanded&#125;、展開ボタンをクリックした後に ClearValue(IsExpanded) | False / False | 残る | Style、式 | True | 0 |
| ItemContainerStyle で &#123;Binding IsExpanded, Mode=TwoWay&#125;、展開ボタンをクリック | True / True | 残る | Style、式 | False | 0 |
| ItemContainerStyle で &#123;Binding IsExpanded, Mode=TwoWay&#125;、ノードを選んで右矢印キー（→） | True / True | 残る | Style、式 | False | 0 |
| ItemContainerStyle で &#123;Binding IsExpanded, Mode=TwoWay&#125;、見出しをダブルクリック | True / True | 残る | Style、式 | False | 0 |
| TreeViewItem に IsExpanded=\"&#123;Binding Value&#125;\" を直接書く、展開ボタンをクリック | True / False | 外れる | Local | True | 0 |
| TreeViewItem に IsExpanded=\"&#123;Binding Value&#125;\" を直接書く、クリックした後に ClearValue(IsExpanded) | False / False | 外れる | Default | False | 0 |
| Expander IsExpanded=\"&#123;Binding Value&#125;\"、見出しをクリック | True / True | 残る | Local、式 | False | 0 |
| Expander IsExpanded=\"&#123;Binding Value, Mode=OneWay&#125;\"、見出しをクリック | True / False | 外れる | Local | True | 0 |
| MenuItem IsCheckable IsChecked=\"&#123;Binding Value&#125;\"、クリック | True / True | 残る | Local、式 | False | 0 |
| CheckBox IsChecked=\"&#123;Binding Value&#125;\"、クリック | True / True | 残る | Local、式 | False | 0 |
| ColumnDefinition Width=\"&#123;Binding Value&#125;\"、GridSplitter を右へ 60 DIP ドラッグ | 160 / 100 | 外れる | Local | 160 | 0 |
| ColumnDefinition Width=\"&#123;Binding Value, Mode=TwoWay&#125;\"、GridSplitter を右へ 60 DIP ドラッグ | 160 / 160 | 残る | Local、式 | 100 | 0 |
| エラーの数え方の確認: TreeViewItem IsExpanded=\"&#123;Binding Missing&#125;\"（存在しないパス）、操作なし | False / False | 残る | Local、式 | False | 1 |
