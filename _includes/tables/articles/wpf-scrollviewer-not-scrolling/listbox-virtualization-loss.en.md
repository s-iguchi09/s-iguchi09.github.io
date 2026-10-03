| configuration | ListBoxItem realized | CanContentScroll (value source) |
|---|---|---|
| ListBox Height=180 | 10 | True (DefaultStyle) |
| ScrollViewer.CanContentScroll=False | 2,000 | False (Local) |
| VirtualizingPanel.IsVirtualizing=False | 2,000 | True (DefaultStyle) |
| CollectionView.GroupDescriptions only, no GroupStyle | 10 | True (DefaultStyle) |
| CollectionView.GroupDescriptions + GroupStyle | 2,000 | False (TemplateTrigger) |
| inside an outer ScrollViewer (Height=180) | 2,000 | True (DefaultStyle) |
| inside a StackPanel (Height=180) | 2,000 | True (DefaultStyle) |
