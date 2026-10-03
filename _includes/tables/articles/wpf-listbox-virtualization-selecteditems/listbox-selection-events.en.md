|  |  | measured |
|---|---|---|
| both | SelectAll(), then PageDown x10 | SelectionChanged 0 (added 0, removed 0) |
| both | Row 5001.IsSelected = true (off screen) | SelectedItems 0, SelectionChanged 0 (added 0, removed 0) |
| both | then ScrollIntoView(Row 5001) | SelectedItems 1, SelectionChanged 1 (added 1, removed 0) |
| SelectionChanged | Row 1 selected, then Shift+End | SelectedItems 10,000, IsSelected 10,000, SelectionChanged 1 (added 9,999, removed 0) |
| ItemContainerStyle | Row 1 selected, then Shift+End | SelectedItems 10,000, IsSelected 31, SelectionChanged 1 (added 9,999, removed 0) |
