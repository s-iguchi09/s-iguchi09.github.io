| case | measured |
|---|---|
| namespace / defaults: Rows, Columns, FirstColumn | System.Windows.Controls.Primitives / 0, 0, 0 |
| 5 labels, 300 x 200, Columns=1 (demo start) | 5 rows x 1 columns, cell 300 x 40 |
| 5 labels, 300 x 200, Columns=2 | 3 rows x 2 columns, cell 150 x 66.67 |
| 5 labels, 300 x 200, Rows=1 (demo start) | 1 rows x 5 columns, cell 60 x 200 |
| 5 labels, 300 x 200, Rows=2 | 2 rows x 3 columns, cell 100 x 100 |
| 5 labels, 300 x 200, neither set | 3 rows x 3 columns (children in 2 rows), cell 100 x 66.67 |
| 5 labels, Rows=2, Columns=2 (4 cells), 300 x 200: 4th / 5th label at | (150, 100) / (0, 200) |
| demo FirstColumn=1, Columns=3, 9 labels: Item1 / Item3 at, shape | (100, 0) / (0, 50), 4 rows x 3 columns |
| FirstColumn=3, Columns=3: Item1 at, shape; FirstColumn after layout | (0, 0), 3 rows x 3 columns; 0 |
| FirstColumn=4, Columns=3: Item1 at, shape; FirstColumn after layout | (0, 0), 3 rows x 3 columns; 0 |
| bound to text \"4\" (demo): FirstColumn, binding; text then \"1\": FirstColumn, Item1 at | 0, binding removed; 0, (0, 0) |
| 5 labels, Columns=2, Item2 Collapsed: Item3 at / shape | (150, 0) / 2 rows x 2 columns, cell 150 x 100 |
| Columns=3 with a child 150 wide: stretched to 300 / sized to content | 100 x 100 / 150 x 100 |
| Columns=2 with one label, Background=null: hit test in the empty cell | (nothing) |
| Columns=2 with one label, Background=Transparent: hit test in the empty cell | Grid |
| demo ZIndex labels (Item2 top margin -15), ZIndex 1 and 2: on top | Item2 |
| demo ZIndex labels (Item2 top margin -15), ZIndex 3 and 2: on top | Item1 |
