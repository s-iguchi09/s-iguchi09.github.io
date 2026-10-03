| case | measured |
|---|---|
| 2x2 columns 100,100 / rows 50,50 | measure calls 1 / 1 / 1 / 1 |
| 2x2 columns Auto,Auto / rows Auto,Auto | measure calls 1 / 1 / 1 / 1 |
| 2x2 columns 1\*,1\* / rows 1\*,1\* | measure calls 1 / 1 / 1 / 1 |
| 2x2 columns Auto,1\* / rows 1\*,Auto | measure calls 2 / 1 / 1 / 1 |
| 500 TextBlocks, vs flat Grid: each row in a nested Grid (+50 Grids) | x1.05, x1.01, x1.00 |
| 500 TextBlocks, vs flat Grid: each TextBlock in 3 nested Grids (+1,500) | x1.25, x1.08, x1.18 |
| 500 TextBlocks, vs flat Grid: Auto columns instead of 1\* | x1.00, x1.00, x0.97 |
