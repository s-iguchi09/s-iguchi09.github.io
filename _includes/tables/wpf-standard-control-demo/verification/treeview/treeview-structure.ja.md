| 条件 | 計測値 |
|---|---|
| デモの IsExpanded の欄、展開ボタンでノードを折りたたむ: IsExpanded / CheckBox / バインド | False / True / 外れる |
| 続けて CheckBox を外してもう一度チェック: IsExpanded | False |
| 折りたたまれた親の下の子が IsExpanded=true（ソース）: コンテナー（展開前 / 展開後） | 作られていない / 作られている、IsExpanded True、葉 作られている |
| 明示した HierarchicalDataTemplate: 2 階層目のノードの文字 | \"Workstation PC\" |
| 暗黙の HierarchicalDataTemplate、DataType=Node（項目は Node） | 最初のノードの表示 \"Desktop\"、展開できるか True |
| 暗黙の HierarchicalDataTemplate、DataType=String（項目は Node） | 最初のノードの表示 \"ToString:Desktop\"、展開できるか False |
| ルートのノード 1,000 個、IsVirtualizing 指定なし: 値, パネル, TreeViewItem の数 | False, StackPanel, 1000 |
| ルートのノード 1,000 個、IsVirtualizing True: 値, パネル, TreeViewItem の数 | True, VirtualizingStackPanel, 12 |
| A（選択、フォーカスあり）で Down キー | 選択 B; A 折りたたみ, A1 折りたたみ |
| A（選択、フォーカスあり）で Right キー | 選択 A; A 展開, A1 折りたたみ |
| A（選択、フォーカスあり）で Left（Right の後） キー | 選択 A; A 折りたたみ, A1 折りたたみ |
| A（選択、フォーカスあり）で Space キー | 選択 A; A 折りたたみ, A1 折りたたみ |
| A（選択、フォーカスあり）で Enter キー | 選択 A; A 折りたたみ, A1 折りたたみ |
| A（選択、フォーカスあり）で テンキーの \* キー | 選択 A; A 展開, A1 展開 |
| ItemContainerStyle の ContextMenu（Mobile）: DataContext（開く前 / 開いている間） | null / ToString:Mobile |
| ScrollViewer.HorizontalScrollBarVisibility / VerticalScrollBarVisibility（値の出どころ） | Auto (DefaultStyle) / Auto (DefaultStyle) |
| Expanded で仮の項目を置き換える: 前; 展開ボタンを実際にクリックした後（読み込みの回数） | (loading); Child 1, Child 2 (1) |
