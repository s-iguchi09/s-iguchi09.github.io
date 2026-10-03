| case | measured |
|---|---|
| size given to a child / width of a TextBlock / width of a Stretch Border (Canvas 300 wide) | Infinity x Infinity / 60.23 / 37.29 |
| Canvas with a child at (20, 20), in a horizontal StackPanel: DesiredSize / ClipToBounds default | 0 x 0 / False |
| demo\'s initial values Top 20, Left 20 | x=20 y=20 w=100 h=100 |
| Left 20 and Right 20, Top 20 and Bottom 20 (Canvas 300 x 200) | x=20 y=20 w=100 h=100 |
| only Right 20, Bottom 20, Canvas stretched to 300 x 200 (no Width set) | x=180 y=80 w=100 h=100 |
| same in a Canvas of size 0 (inside StackPanels) | x=-120 y=-120 w=100 h=100 |
| no position set / Left -30 | x=0 y=0 w=100 h=100 / x=-30 y=150 w=100 h=100 |
| Canvas 200 wide, child at Left 150 (to 250), ClipToBounds=False: hit test at x=230 | A |
| Canvas 200 wide, child at Left 150 (to 250), ClipToBounds=True: hit test at x=230 | (nothing) |
| demo\'s rectangles A (20, 20) and B (50, 50), ZIndex A=0, B=1: on top at (85, 85) | B |
| demo\'s rectangles A (20, 20) and B (50, 50), ZIndex A=2, B=1: on top at (85, 85) | A |
| demo\'s rectangles A (20, 20) and B (50, 50), ZIndex A=0, B=0: on top at (85, 85) | B |
| demo binding with FallbackValue=NaN: Canvas.Right for text \"\" / \"30\" | NaN / 30 |
