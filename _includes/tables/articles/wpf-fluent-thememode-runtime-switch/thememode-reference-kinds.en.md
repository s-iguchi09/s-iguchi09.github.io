| how the brush is referenced | ReadLocalValue | before | after | follows |
|---|---|---|---|---|
| &#123;StaticResource&#125; | SolidColorBrush | \#E4000000 | \#E4000000 | no |
| &#123;DynamicResource&#125; | ResourceReferenceExpression | \#E4000000 | \#FFFFFFFF | yes |
| Foreground = FindResource(\.\.\.) | SolidColorBrush | \#E4000000 | \#E4000000 | no |
| &#123;DynamicResource SystemColors.ControlTextBrushKey&#125; | ResourceReferenceExpression | \#FF000000 | \#FF000000 | no |
| Button (Fluent style) | (no local value) | \#E4000000 | \#FFFFFFFF | yes |
