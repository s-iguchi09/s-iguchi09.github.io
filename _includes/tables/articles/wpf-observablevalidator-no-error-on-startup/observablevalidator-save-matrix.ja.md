| 保存の止め方 | 起動直後の保存ボタンの IsEnabled | 起動直後の HasError | 保存ボタンを押した後に保存まで進んだ回数 | 保存ボタンを押した後の HasError | 正しい名前を入力した後の保存ボタンの IsEnabled |
|---|---|---|---|---|---|
| CanExecute は !HasErrors、\[NotifyDataErrorInfo\] だけ | True | False | 1 | False | True |
| CanExecute は !HasErrors、コンストラクターで ValidateAllProperties() | False | True | -（押せない） | -（押せない） | True |
| CanExecute なし、Save の先頭で ValidateAllProperties() | True | False | 0 | True | True |
