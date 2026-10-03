| 条件 | 計測値 |
|---|---|
| ListBox / ListView / ComboBox の基底クラス | Selector / ListBox / Selector |
| ListBoxItem.IsSelected: BindsTwoWayByDefault | True |
| SelectionMode（既定値） | Single |
| ScrollViewer.HorizontalScrollBarVisibility / VerticalScrollBarVisibility | Auto (DefaultStyle) / Auto (DefaultStyle) |
| 項目のパネル | VirtualizingStackPanel |
| SelectionMode=Single: Item 1、Item 2、Item 1 の順にクリック（修飾キーなし） | Item 1  -&gt;  Item 2  -&gt;  Item 1 |
| SelectionMode=Multiple: Item 1、Item 2、Item 1 の順にクリック（修飾キーなし） | Item 1  -&gt;  Item 1, Item 2  -&gt;  Item 2 |
| SelectionMode=Extended: Item 1、Item 2、Item 1 の順にクリック（修飾キーなし） | Item 1  -&gt;  Item 2  -&gt;  Item 1 |
| SelectionMode=Single: SelectAll() | NotSupportedException、選択 0 件 |
| SelectionMode=Multiple: SelectAll() | 例外なし、選択 4 件 |
| SelectionMode=Extended: SelectAll() | 例外なし、選択 4 件 |
