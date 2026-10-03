| 条件 | 計測値 |
|---|---|
| setter で検証、LostFocus: 入力で空にする | HasError: フォーカス中 False（Text \"\"）、フォーカスが外れた後 True |
| setter で検証、Explicit: 入力で空にする | HasError: フォーカス中 False（Text \"\"）、フォーカスが外れた後 False、UpdateSource の後 True |
| OneWay + 規則、ソースは空 | HasError: 最初 True、\"abc\" と入力 True、ソースを \"abc\" に False |
| OneWay、コードで Text = \"x\" | バインドは外れる、HasError False |
| OneTime、コードで Text = \"x\" | バインドは外れる、HasError False |
| OneWay, SetCurrentValue(Text, \"x\") | バインドは残る、HasError True |
| エラーを追加、ErrorsChanged(\"Namee\")、パスは Name | HasErrors True, Validation.HasError False |
| エラーを追加、ErrorsChanged(\"Name\")、パスは Name | HasErrors True, Validation.HasError True |
| Validation.Error のハンドラー、NotifyOnValidationError=False | Validation.Error の発生 0 回 |
| Validation.Error のハンドラー、NotifyOnValidationError=True | Validation.Error の発生 2 回 |
| (Validation.Errors)\[0\].ErrorContent、エラーを解消 | HasError False, Error 17 の記録 1 |
| (Validation.Errors)/ErrorContent、エラーを解消 | HasError False, Error 17 の記録 0 |
| UI スレッドで ErrorsChanged を発生させる | Validation.HasError True（UI スレッドで発生） |
| バックグラウンドスレッドで ErrorsChanged を発生させる | Validation.HasError True（UI 以外のスレッドで発生） |
