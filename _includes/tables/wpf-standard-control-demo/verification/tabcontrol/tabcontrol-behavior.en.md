| case | measured |
|---|---|
| base class / default TabStripPlacement / first Dock value (demo start) | Selector / Top / Left |
| SelectedIndex before showing / after showing | -1 / 0 |
| demo \"Format: &#123;0&#125;.\": text / SelectedContent / SelectedContentStringFormat | Format: Item1. / Item1 / Format: &#123;0&#125;. |
| switched to Tab2 (its own \"Own &#123;0&#125;\"): text / SelectedContentStringFormat | Format: Item2. / Own &#123;0&#125; |
| Tab2 selected before showing: text / SelectedContentStringFormat | Own Item2 / Own &#123;0&#125; |
| 2 tabs, SelectedIndex = 5: exception / SelectedIndex | no exception / 0 |
| SelectedIndex = -2: exception / SelectedIndex | ArgumentException / 0 |
| Tab2 selected, SelectedItem = a TabItem not in the list | 1 |
| SelectedIndex = -1: SelectedContent / text shown | null / (nothing) |
| 3 tabs, Tab2 selected and removed: SelectedIndex / selected tab | 1 / Tab3 |
| Items filled, then ItemsSource / ItemsSource, then Items.Add | InvalidOperationException / InvalidOperationException |
| focus on Tab1\'s header, Right arrow: SelectedIndex / focused header | 1 / Tab2 |
