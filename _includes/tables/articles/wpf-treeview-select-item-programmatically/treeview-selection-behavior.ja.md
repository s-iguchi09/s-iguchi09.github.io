| 条件 | 計測値 |
|---|---|
| \'Program Files\' を選び、続けて \'Users\'（どちらも生成済み） | ViewModel: Program Files False, Users True |
| \'C:\' を選び、続けて \'drivers\'（まだコンテナーが無い） | ViewModel: C: True, drivers True、SelectedItem C: |
| TwoWay: container.IsSelected に代入し、続けて ViewModel を false に | 値の出どころ Style、container.IsSelected False |
| OneWay: container.IsSelected に代入し、続けて ViewModel を false に | 値の出どころ Local、container.IsSelected True |
| 選択中の背景、項目にフォーカス | \#FF0078D7 = SystemColors.HighlightColor |
| 選択中の背景、フォーカスは別の所 | \#FFF0F0F0 = SystemColors.InactiveSelectionHighlightBrush |
| 200 個のノードの最後: IsSelected、続けて BringIntoView | 選択 Folder 200、位置 0 -&gt; 3051.05 |
| IsVirtualizing: 200 個の最後を ViewModel で選択 | SelectedItem: コンテナーなし、null -&gt; スクロール後: コンテナーあり、Folder 200 |
