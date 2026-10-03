| case | measured |
|---|---|
| ListBoxItems created at start | 6 |
| SelectedIndex = 500 from code: VerticalOffset / container for item 500 | 0 / not created |
| then ScrollIntoView(SelectedItem): VerticalOffset / container for item 500 | 497 / created |
| VerticalScrollBarVisibility=Auto: scrollable / bar; after 20 Down keys | 996 / Visible; SelectedIndex 20, offset 17 |
| VerticalScrollBarVisibility=Disabled: scrollable / bar; after 20 Down keys | 996 / Collapsed; SelectedIndex 20, offset 17 |
