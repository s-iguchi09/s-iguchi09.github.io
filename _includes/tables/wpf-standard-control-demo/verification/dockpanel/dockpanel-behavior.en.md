| case | measured |
|---|---|
| defaults: LastChildFill / Dock of a child | True / Left |
| demo\'s two labels, LastChildFill=False: Item1 / Item2 | x=0 y=0 w=42.34 h=100 / x=42.34 y=0 w=42.34 h=100 |
| demo\'s two labels, LastChildFill=True: Item1 / Item2 | x=0 y=0 w=42.34 h=100 / x=42.34 y=0 w=257.66 h=100 |
| LastChildFill=True, last label with Dock=Top: its rect | x=42.34 y=0 w=257.66 h=100 |
| demo\'s single label, LastChildFill=False, Dock=Left | x=0 y=0 w=42.34 h=100 |
| demo\'s single label, LastChildFill=False, Dock=Top | x=0 y=0 w=300 h=27.96 |
| demo\'s single label, LastChildFill=False, Dock=Right | x=257.66 y=0 w=42.34 h=100 |
| demo\'s single label, LastChildFill=False, Dock=Bottom | x=0 y=72.04 w=300 h=27.96 |
| Top, Bottom, Left, content: Left / content | x=0 y=27.96 w=31.75 h=44.08 / x=31.75 y=27.96 w=268.25 h=44.08 |
| Left, Top, Bottom, content: Left / content | x=0 y=0 w=31.75 h=100 / x=31.75 y=27.96 w=268.25 h=44.08 |
| LastChildFill=False, Background=null: hit test in the empty area | (nothing) |
| LastChildFill=False, Background=AliceBlue: hit test in the empty area | Panel |
| demo\'s ZIndex labels (Item2 margin -30), ZIndex 1 and 2: on top where they overlap | Item2 |
| demo\'s ZIndex labels (Item2 margin -30), ZIndex 3 and 2: on top where they overlap | Item1 |
