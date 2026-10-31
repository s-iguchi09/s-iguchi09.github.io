| ターゲットとモード | 操作 | 操作の前 | 1 つ目の操作の後 | 2 つ目の操作の後 |
|---|---|---|---|---|
| TextBlock, OneWay | 表示: Title を変更（PropertyChanged を発行）→ DataContext を差し替え | Monthly report | Changed | Replaced |
| TextBlock, OneTime | 表示: Title を変更（PropertyChanged を発行）→ DataContext を差し替え | Monthly report | Monthly report | Replaced |
| TextBox, TwoWay (default) | ソースの Title: 「Edited」と入力して UpdateSource を呼ぶ | Monthly report | Edited | - |
| TextBox, OneTime | ソースの Title: 「Edited」と入力して UpdateSource を呼ぶ | Monthly report | Monthly report | - |
