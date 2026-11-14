| コードが行ったこと | メモリ上に見つかったか |
|---|---|
| PasswordBox に入力しただけ | なし |
| SecurePassword を読んだ（Dispose した） | なし |
| SecurePassword を非マネージドのメモリへ展開した直後（ZeroFreeGlobalAllocUnicode の前） | あり |
| SecurePassword を非マネージドのメモリへ展開し、ZeroFreeGlobalAllocUnicode で消した | なし |
| Password を 1 回読んだ（string。参照は捨てた） | あり |
| 添付プロパティでビューモデルの string に同期した | あり |
| 同じ同期の後、PasswordBox.Clear() で空にして（ビューモデルも \"\" になる）GC.Collect を実行した | あり |
