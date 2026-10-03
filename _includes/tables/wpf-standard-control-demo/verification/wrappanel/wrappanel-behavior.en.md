| case | measured |
|---|---|
| defaults: Orientation / ItemWidth / ItemHeight | Horizontal / NaN / NaN |
| the demo\'s 5 labels, Horizontal, 150 wide | 2 rows of 3, 2 |
| the demo\'s 5 labels, Vertical, 60 high | 5 columns of 1, 1, 1, 1, 1 |
| children 20 and 40 high in a row, lower one Stretch: its height | 40 |
| children 20 and 40 high in a row, lower one Top: its height | 20 |
| ItemWidth=100: demo label width / 150-wide child: x, layout clip | 96 / 300, 100 |
| hit test on the 150-wide child, 20 past its 100 | (nothing) |
| ItemHeight=100, 5 labels, 150 wide: label height / second row starts at y / its first label at y | 96 / 100 / 102 |
| 5 labels in a ScrollViewer, horizontal Disabled (default), 150 wide | 3 rows of 2, 2, 1 |
| 5 labels in a ScrollViewer, horizontal Auto, 150 wide | 1 rows of 5 |
| Background=null: hit test in the gap between two labels | (nothing) |
| Background=Transparent: hit test in the gap between two labels | Panel |
| two labels overlapping by 15, ZIndex 1 and 2: on top | Item2 |
| two labels overlapping by 15, ZIndex 3 and 2: on top | Item1 |
| ListBox 300 x 200, WrapPanel as ItemsPanel, 1000 items: items created | 1000 |
