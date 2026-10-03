| case | measured |
|---|---|
| base class of ListBox / ListView / ComboBox | Selector / ListBox / Selector |
| ListBoxItem.IsSelected: BindsTwoWayByDefault | True |
| SelectionMode (default) | Single |
| ScrollViewer.HorizontalScrollBarVisibility / VerticalScrollBarVisibility | Auto (DefaultStyle) / Auto (DefaultStyle) |
| items panel | VirtualizingStackPanel |
| SelectionMode=Single: click Item 1, Item 2, Item 1 (no modifier keys) | Item 1  -&gt;  Item 2  -&gt;  Item 1 |
| SelectionMode=Multiple: click Item 1, Item 2, Item 1 (no modifier keys) | Item 1  -&gt;  Item 1, Item 2  -&gt;  Item 2 |
| SelectionMode=Extended: click Item 1, Item 2, Item 1 (no modifier keys) | Item 1  -&gt;  Item 2  -&gt;  Item 1 |
| SelectionMode=Single: SelectAll() | NotSupportedException; 0 selected |
| SelectionMode=Multiple: SelectAll() | no exception; 4 selected |
| SelectionMode=Extended: SelectAll() | no exception; 4 selected |
