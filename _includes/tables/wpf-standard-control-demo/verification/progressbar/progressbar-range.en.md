| case | measured |
|---|---|
| base class / defaults: Minimum, Maximum, Value, IsIndeterminate, Orientation | RangeBase / 0, 100, 0, False, Horizontal |
| width 200, Minimum 0, Maximum 100, Value 30: indicator width | 60 |
| width 200, Minimum 20, Maximum 120, Value 70: indicator width | 100 |
| width 200, Minimum 0, Maximum 100, Value 0: indicator width | 0 |
| Minimum = Maximum = 50: indicator width (exception) | 200 (none) |
| Minimum 50, then Maximum = 10: Maximum / Value / indicator width | 50 / 50 / 200 |
| Value = 150 with Maximum 100: Value / indicator width; then Maximum = 200: Value | 100 / 200; 150 |
| Orientation=Vertical, 20 x 200, Value 75: indicator bounds | x=0 y=50 w=20 h=150 |
