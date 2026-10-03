| ブラシの参照のしかた | ReadLocalValue | 切り替え前 | 切り替え後 | 追従するか |
|---|---|---|---|---|
| &#123;StaticResource&#125; | SolidColorBrush | \#E4000000 | \#E4000000 | しない |
| &#123;DynamicResource&#125; | ResourceReferenceExpression | \#E4000000 | \#FFFFFFFF | する |
| Foreground = FindResource(\.\.\.) | SolidColorBrush | \#E4000000 | \#E4000000 | しない |
| &#123;DynamicResource SystemColors.ControlTextBrushKey&#125; | ResourceReferenceExpression | \#FF000000 | \#FF000000 | しない |
| Button（Fluent のスタイル） | （ローカル値なし） | \#E4000000 | \#FFFFFFFF | する |
