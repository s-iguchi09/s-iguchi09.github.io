| 構成 | HasError | Errors | 装飾（adorner）の数 |
|---|---|---|---|
| IDataErrorInfo だけ | False | 0 | 0 |
| + ValidatesOnDataErrors=True | True | 1 | 1 |
| INotifyDataErrorInfo だけ | True | 1 | 1 |
| ValidationRules | True | 1 | 1 |
| ValidationRules, ErrorTemplate=&#123;x:Null&#125; | True | 1 | 0 |
