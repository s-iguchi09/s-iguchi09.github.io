| 計測したもの | 結果 |
|---|---|
| TreeView.SelectedItemProperty.ReadOnly | True |
| 外から SetValue(SelectedItemProperty) | InvalidOperationException が発生 |
| 展開する前の子のコンテナー | null |
| IsExpanded = true の直後、レイアウトの前 | null |
| IsExpanded = true と UpdateLayout() の後 | TreeViewItem |
| child.IsSelected = true の後の TreeView.SelectedItem | \'child\' の項目 |
