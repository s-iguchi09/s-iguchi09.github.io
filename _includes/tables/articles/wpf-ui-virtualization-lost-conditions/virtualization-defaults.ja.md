| 条件 | 実体化したコンテナー | 項目のパネル | VirtualizingStackPanel か | IsVirtualizing | CanContentScroll | 高さ |
|---|---|---|---|---|---|---|
| ListBox | 11 | VirtualizingStackPanel | True | True | True | 200 |
| ListView + GridView | 10 | VirtualizingStackPanel | True | True | True | 200 |
| DataGrid | 11 | DataGridRowsPresenter | True | True | True | 200 |
| ScrollViewer に入れた ItemsControl | 1,000 | StackPanel | False | True | - | 15,960 |
| 高さ 200 の Grid に直接置いた ItemsControl | 1,000 | StackPanel | False | True | - | 200 |
| ComboBox、ドロップダウンを開く前 | 0 | - | False | True | - | 21.96 |
| ComboBox、ドロップダウンを開く | 1,000 | StackPanel | False | True | True | 21.96 |
| ComboBox、ドロップダウンを開いて閉じた後 | 1,000 | StackPanel | False | True | True | 21.96 |
| TreeView、ルートのノード 1,000 個 | 1,000 | StackPanel | False | False | False | 200 |
