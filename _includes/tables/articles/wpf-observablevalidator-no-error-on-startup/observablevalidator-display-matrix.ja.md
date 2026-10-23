| ビューモデルの検証の書き方 | 起動直後（名前は空） | 21 文字を入力した後 | 入力を消して空にした後 |
|---|---|---|---|
| Toolkit、setter で SetProperty(ref \_name, value, true) | False | True | True |
| Toolkit の \[ObservableProperty\] に \[Required\] / \[MaxLength\] だけ | False | False | False |
| Toolkit の \[ObservableProperty\] に \[NotifyDataErrorInfo\] | False | True | True |
| 同じ書き方で、コンストラクターで ValidateAllProperties() | True | True | True |
| Prism の BindableBase と ErrorsContainer、setter で検証 | False | True | True |
| 同じ書き方で、コンストラクターでも検証 | True | True | True |
