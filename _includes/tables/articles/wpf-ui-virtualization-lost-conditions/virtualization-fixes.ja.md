| 条件 | 実体化したコンテナー | 項目のパネル | VirtualizingStackPanel か | IsVirtualizing | CanContentScroll | 高さ |
|---|---|---|---|---|---|---|
| TreeView: VirtualizingPanel.IsVirtualizing=True | 25 | VirtualizingStackPanel | True | True | True | 200 |
| TreeView: VirtualizingPanel.IsVirtualizing=True、CacheLength=0 | 13 | VirtualizingStackPanel | True | True | True | 200 |
| ItemsControl: VirtualizingStackPanel + テンプレートの ScrollViewer | 14 | VirtualizingStackPanel | True | True | True | 200 |
| ItemsControl: VirtualizingStackPanel だけ、CanContentScroll=True の外側の ScrollViewer に入れる | 1,000 | VirtualizingStackPanel | True | True | - | 15,960 |
| ItemsControl: VirtualizingStackPanel だけ、高さ 200 の Grid に直接置く | 1,000 | VirtualizingStackPanel | True | True | - | 200 |
| ComboBox: ItemsPanel = VirtualizingStackPanel、ドロップダウンを開く | 19 | VirtualizingStackPanel | True | True | True | 21.96 |
| GroupStyle ありのグループ化: VirtualizingPanel.IsVirtualizingWhenGrouping=True | 19 | VirtualizingStackPanel | True | True | True | 200 |
| 検索欄: Auto の行に TextBox、\* の行に ListBox | 10 | VirtualizingStackPanel | True | True | True | 182.04 |
| 検索欄: 上に寄せた TextBox、DockPanel の最後の子に ListBox | 10 | VirtualizingStackPanel | True | True | True | 182.04 |
| 縦の StackPanel: ListBox に MaxHeight=200 | 11 | VirtualizingStackPanel | True | True | True | 200 |
