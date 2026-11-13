| コードが行ったこと | メモリ上に見つかったか |
|---|---|
| PasswordBox に入力しただけ | なし |
| SecurePassword を読んだ（Dispose した） | なし |
| SecurePassword を非マネージドのメモリへ展開し、ZeroFreeGlobalAllocUnicode で消した | なし |
| Password を 1 回読んだ（string。参照は捨てた） | あり |
| 添付プロパティでビューモデルの string に同期した | あり |
| 同じ同期の後、ViewModel.Password = \"\" にして GC.Collect を実行した | あり |
