| case | measured |
|---|---|
| 0\.\.100, TickFrequency=30: tick marks, left to right (as values of a non-reversed track) | 5: 0, 30, 60, 90, 100 |
| 0\.\.10, Ticks=1,3,5,7,9: tick marks, left to right (as values of a non-reversed track) | 7: 0, 1, 3, 5, 7, 9, 10 |
| 0\.\.10, Ticks=1,2: tick marks, left to right (as values of a non-reversed track) | 4: 0, 1, 2, 10 |
| 0\.\.10, Ticks=1,2, IsDirectionReversed: tick marks, left to right (as values of a non-reversed track) | 4: 0, 8, 9, 10 |
| TickPlacement=None: height | 18 |
| TickPlacement=BottomRight: height | 24 |
| TickPlacement=Both: height | 30 |
| 0\.\.10, Selection 2\.\.8, IsSelectionRangeEnabled=False | Hidden |
| 0\.\.10, Selection 2\.\.8, IsSelectionRangeEnabled=True | Visible, spans 2 to 8 |
| Value = 9.5 outside the selection | 9.5 |
| AutoToolTipPlacement=None, Precision=0, Value 33.6 | no tooltip |
| AutoToolTipPlacement=TopLeft, Precision=0, Value 33.6 | \"34\", IsOpen=True |
| AutoToolTipPlacement=TopLeft, Precision=0, Value 33.4 | \"33\", IsOpen=True |
| AutoToolTipPlacement=TopLeft, Precision=2, Value 33.456 | \"33.46\", IsOpen=True |
| AutoToolTipPlacement=BottomRight, Precision=1, Value 1234.56 | \"1,234.6\", IsOpen=True |
| CurrentCulture en-US, Precision=1, Value 1234.56 | \"1,234.6\" |
| CurrentCulture de-DE, Precision=1, Value 1234.56 | \"1.234,6\" |
