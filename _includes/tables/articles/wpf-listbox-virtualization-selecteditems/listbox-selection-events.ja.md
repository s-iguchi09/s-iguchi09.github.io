|  |  | 計測値 |
|---|---|---|
| 両方 | SelectAll() の後に PageDown x10 | SelectionChanged 0（追加 0、削除 0） |
| 両方 | Row 5001.IsSelected = true（画面外） | SelectedItems 0、SelectionChanged 0（追加 0、削除 0） |
| 両方 | 続けて ScrollIntoView(Row 5001) | SelectedItems 1、SelectionChanged 1（追加 1、削除 0） |
| SelectionChanged | Row 1 を選択し、Shift+End | SelectedItems 10,000、IsSelected 10,000、SelectionChanged 1（追加 9,999、削除 0） |
| ItemContainerStyle | Row 1 を選択し、Shift+End | SelectedItems 10,000、IsSelected 31、SelectionChanged 1（追加 9,999、削除 0） |
