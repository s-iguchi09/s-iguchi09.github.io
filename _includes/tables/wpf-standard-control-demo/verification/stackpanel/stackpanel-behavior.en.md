| case | measured |
|---|---|
| default Orientation / Spacing property | Vertical / none |
| Vertical, panel 200 x 100: size given to the child / child\'s arranged size (desired 40 x 20) | 200 x Infinity / 200 x 20 |
| Horizontal, panel 200 x 100: size given to the child / child\'s arranged size (desired 40 x 20) | Infinity x 100 / 40 x 100 |
| Horizontal, the demo\'s 3 labels, Width 80: last label x / ClipToBounds / layout clip width | 94.69 / False / 80 |
| hit test on the last label, outside the 80 | (nothing) |
| 1000 children in a StackPanel in a ScrollViewer 200 high: children measured | 1000 |
| 1000-item ListBox in 200 high, directly: height / ScrollableHeight / items created | 200 / 991 / 10 |
| 1000-item ListBox in 200 high, inside a vertical StackPanel: height / ScrollableHeight / items created | 19964 / 0 / 1000 |
| empty StackPanel 30 high, Background=null: hit test in the middle | (nothing) |
| empty StackPanel 30 high, Background=Transparent: hit test in the middle | Panel |
| empty StackPanel 30 high, Background=AliceBlue: hit test in the middle | Panel |
| demo labels, ZIndex Item1=1, Item2=2: element on top where they overlap | Item2 |
| demo labels, ZIndex Item1=3, Item2=2: element on top where they overlap | Item1 |
| child of an inner panel with ZIndex=100 vs the inner panel\'s sibling (ZIndex 0): on top | OuterSibling |
