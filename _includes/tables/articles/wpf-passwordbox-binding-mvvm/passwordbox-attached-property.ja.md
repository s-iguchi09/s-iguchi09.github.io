| 条件 | 計測値 |
|---|---|
| 変更コールバックの中で購読・ビューモデルの初期値 \"\": \"abc\" を入力 | ViewModel.Password \"\" |
| 値を比べずに Password を設定し直す: \"abc\" を入力 | PasswordBox \"cba\" / ViewModel \"cba\" |
| 添付プロパティなし: \"ab\" を入力してから \"c\" を入力（Password は設定しない） | PasswordBox \"abc\" |
| 添付プロパティなし: \"ab\" を入力し、コードで Password = \"ab\" を設定してから \"c\" を入力 | PasswordBox \"cab\" |
| 記事の添付プロパティ: \"abc\" を入力 | PasswordBox \"abc\" / ViewModel \"abc\" |
| 続けてビューモデルが Password = \"\" を設定 | PasswordBox \"\" |
| ボタンの IsEnabled: 入力の前 / 後 | False / True |
