| 条件 | 計測値 |
|---|---|
| 0\.\.100, TickFrequency=30: 目盛り、左から右へ（反転していないトラックの値として） | 5: 0, 30, 60, 90, 100 |
| 0\.\.10, Ticks=1,3,5,7,9: 目盛り、左から右へ（反転していないトラックの値として） | 7: 0, 1, 3, 5, 7, 9, 10 |
| 0\.\.10, Ticks=1,2: 目盛り、左から右へ（反転していないトラックの値として） | 4: 0, 1, 2, 10 |
| 0\.\.10, Ticks=1,2, IsDirectionReversed: 目盛り、左から右へ（反転していないトラックの値として） | 4: 0, 8, 9, 10 |
| TickPlacement=None: 高さ | 18 |
| TickPlacement=BottomRight: 高さ | 24 |
| TickPlacement=Both: 高さ | 30 |
| 0\.\.10、Selection 2\.\.8、IsSelectionRangeEnabled=False | Hidden |
| 0\.\.10、Selection 2\.\.8、IsSelectionRangeEnabled=True | Visible、2〜8 の範囲 |
| 選択範囲の外の Value = 9.5 | 9.5 |
| AutoToolTipPlacement=None、Precision=0、Value 33.6 | ツールチップなし |
| AutoToolTipPlacement=TopLeft、Precision=0、Value 33.6 | \"34\", IsOpen=True |
| AutoToolTipPlacement=TopLeft、Precision=0、Value 33.4 | \"33\", IsOpen=True |
| AutoToolTipPlacement=TopLeft、Precision=2、Value 33.456 | \"33.46\", IsOpen=True |
| AutoToolTipPlacement=BottomRight、Precision=1、Value 1234.56 | \"1,234.6\", IsOpen=True |
| CurrentCulture en-US、Precision=1、Value 1234.56 | \"1,234.6\" |
| CurrentCulture de-DE、Precision=1、Value 1234.56 | \"1.234,6\" |
