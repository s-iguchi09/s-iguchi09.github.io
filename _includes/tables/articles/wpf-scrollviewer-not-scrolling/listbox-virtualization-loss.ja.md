| 構成 | 実体化した ListBoxItem | CanContentScroll（値の出どころ） |
|---|---|---|
| ListBox Height=180 | 10 | True (DefaultStyle) |
| ScrollViewer.CanContentScroll=False | 2,000 | False (Local) |
| VirtualizingPanel.IsVirtualizing=False | 2,000 | True (DefaultStyle) |
| CollectionView.GroupDescriptions だけ（GroupStyle なし） | 10 | True (DefaultStyle) |
| CollectionView.GroupDescriptions + GroupStyle | 2,000 | False (TemplateTrigger) |
| 外側の ScrollViewer の中（Height=180） | 2,000 | True (DefaultStyle) |
| StackPanel の中（Height=180） | 2,000 | True (DefaultStyle) |
