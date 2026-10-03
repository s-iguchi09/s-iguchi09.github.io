| binding target and format | culture | rendered text |
|---|---|---|
| FrameworkElement.Language default | - | en-us |
| CultureInfo.CurrentCulture | - | ja-JP |
| TextBlock.Text, C | no ConverterCulture | \$1,234.50 |
| TextBlock.Text, C | ConverterCulture=ja-JP | ¥1,235 |
| TextBlock.Text, d | no ConverterCulture | 7/17/2026 |
| TextBlock.Text, d | ConverterCulture=ja-JP | 2026/07/17 |
| Label.Content, StringFormat=C | - | 1234.5 |
| Label.Content, ContentStringFormat=C | - | \$1,234.50 |
