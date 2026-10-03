| 条件 | 計測値 |
|---|---|
| 既定のバインド、\"sato\" と入力し、IsDefault のボタンで Enter | Click の中: UserName = suzuki、TextBox のフォーカス True |
| UI スレッドで ErrorsChanged を発生させる | Validation.HasError True（UI スレッドで発生） |
| バックグラウンドスレッドで ErrorsChanged を発生させる | Validation.HasError True（UI 以外のスレッドで発生） |
