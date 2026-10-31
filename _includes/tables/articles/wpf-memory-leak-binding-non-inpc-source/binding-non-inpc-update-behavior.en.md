| Target and mode | Operation | Before | After step 1 | After step 2 |
|---|---|---|---|---|
| TextBlock, OneWay | Displayed text: set Title (raises PropertyChanged), then replace DataContext | Monthly report | Changed | Replaced |
| TextBlock, OneTime | Displayed text: set Title (raises PropertyChanged), then replace DataContext | Monthly report | Monthly report | Replaced |
| TextBox, TwoWay (default) | Source Title: type \"Edited\", then call UpdateSource | Monthly report | Edited | - |
| TextBox, OneTime | Source Title: type \"Edited\", then call UpdateSource | Monthly report | Monthly report | - |
