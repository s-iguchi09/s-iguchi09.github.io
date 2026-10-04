| case | measured |
|---|---|
| 0\.\.100, dragged to 23.4, no snapping | 23.4 |
| TickPlacement=BottomRight only | 23.4 |
| IsSnapToTickEnabled, TickFrequency=10 | 20 |
| IsSnapToTickEnabled, TickFrequency=1 | 23 |
| 0\.\.100, TickFrequency=30, snapping, dragged to 94 | 90 |
| 0\.\.100, TickFrequency=30, snapping, dragged to 97 | 100 |
| 0\.\.10, Ticks=1,3,5,7,9, snapping, dragged to 5.8 | 5 |
| 0\.\.10, Ticks=1,3,5,7,9, snapping, dragged to 0.2 | 0 |
| Value = 23.4 set from code, IsSnapToTickEnabled, TickFrequency=10 | 23.4 |
| Value bound with default settings: source during the drag (before release) | 40 |
