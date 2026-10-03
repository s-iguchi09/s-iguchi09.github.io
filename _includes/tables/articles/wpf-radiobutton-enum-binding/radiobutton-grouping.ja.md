| 条件 | 計測値 |
|---|---|
| ランタイム | net48: .NET Framework 4.8.9345.0 / net10.0-windows: .NET 10.0.10 |
| GroupName の既定値 | （空） |
| GroupName なし: チェックされたもの | Single |
| GroupName なし: ConvertBack(false) | 1 (Standard) |
| GroupName なし: ViewModel | Standard / Single |
| GroupName あり: チェックされたもの | Standard + Single |
| GroupName あり: ConvertBack(false) | 0 |
| GroupName あり: ViewModel | Standard / Single |
| GroupName あり: Fine を選択 | ConvertBack(true) 1, (false) 0、チェック Fine + Single、ViewModel Fine / Single |
| GroupName あり: Draft を選択 | チェック Draft + Single、ViewModel Draft / Single |
| GroupName なし、列挙型ごとに StackPanel を 1 つ | チェック Standard + Single、ConvertBack(false) 0 |
| GroupName=\'quality\' を 2 つの Border に | チェック B:Standard |
| ConverterParameter=Draft（string）: チェックされたもの | Single |
| ConverterParameter=Draft（string）: Fine を選択 | チェック Single、ViewModel Fine / Single |
| 同じ GroupName、ViewModel が 2 つ: ConvertBack(false) | 1 (Standard) |
| グループの中のバインドしていない RadioButton: ConvertBack(false) | 1 (Standard) |
| ConvertBack が NotImplementedException を投げる | ConvertBack(false) 1、NotImplementedException が呼び出し元へ送出される |
| ConvertBack が UnsetValue を返す（FallbackValue=True） | Standard はチェックなし、エラー \"Value \'False\' could not be converted.\"、ソース Standard / Single |
| ConvertBack が false のときに parameter を返す（GroupName なし） | チェック Standard + Single、ConvertBack(false) 1 |
| bool のラッパープロパティ（GroupName なし） | チェック Standard + Single、setter(false) 1 |
| Grid、別々のセル | チェック B（両方を True に設定） |
| GroupBox の Header と Content | チェック B（両方を True に設定） |
| ItemsControl、Items の中の RadioButton | チェック B（両方を True に設定） |
