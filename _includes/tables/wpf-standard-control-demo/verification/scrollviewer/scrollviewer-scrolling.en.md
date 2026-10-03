| case | measured |
|---|---|
| defaults: VerticalScrollBarVisibility / HorizontalScrollBarVisibility | Visible / Disabled |
| demo items, CanContentScroll=False: sizes / offset after LineDown | extent 251.64, viewport 100 / 16 |
| demo items, CanContentScroll=True: sizes / offset after LineDown | extent 9, viewport 3 / 1 |
| ListBox, 1000 items, CanContentScroll=True: items created | 10 |
| ListBox, 1000 items, CanContentScroll=False: items created | 1000 |
| IsDeferredScrollingEnabled=False: offset / content offset, dragging; released | 114.38 / 114.38; 114.38 / 114.38 |
| IsDeferredScrollingEnabled=True: offset / content offset, dragging; released | 0 / 0; 114.38 / 114.38 |
| HorizontalScrollBarVisibility inside ListBox / TextBox / TreeView / DataGrid | Auto / Hidden / Auto / Auto |
| inside ListBox: attached Disabled / outer ScrollViewer Disabled | Disabled / Auto |
| ExtentHeight / ViewportHeight before layout; after layout | 0 / 0; 251.64 / 100 |
