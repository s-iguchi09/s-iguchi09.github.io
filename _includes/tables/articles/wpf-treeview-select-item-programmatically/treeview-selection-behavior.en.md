| case | measured |
|---|---|
| select \'Program Files\', then \'Users\' (both generated) | ViewModel: Program Files False, Users True |
| select \'C:\', then \'drivers\' (no container yet) | ViewModel: C: True, drivers True; SelectedItem C: |
| TwoWay: assign container.IsSelected, then ViewModel false | source Style; container.IsSelected False |
| OneWay: assign container.IsSelected, then ViewModel false | source Local; container.IsSelected True |
| selected background, item focused | \#FF0078D7 = SystemColors.HighlightColor |
| selected background, focus elsewhere | \#FFF0F0F0 = SystemColors.InactiveSelectionHighlightBrush |
| last of 200 nodes: IsSelected, then BringIntoView | selected Folder 200, offset 0 -&gt; 3051.05 |
| IsVirtualizing: last of 200 selected in ViewModel | SelectedItem: no container, null -&gt; scrolled: container, Folder 200 |
