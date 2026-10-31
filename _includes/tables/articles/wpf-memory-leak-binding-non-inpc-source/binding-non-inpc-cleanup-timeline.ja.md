| 手順 | \_valueChangedHandlers の登録数 | キーが最初のウィンドウのソース | 記録の \_source がそのソース | ハンドラーの Target の型 | ソースが残っている | BindingExpression が残っている |
|---|---|---|---|---|---|---|
| ウィンドウを開いている間 | 1 | True | True | MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord | True | True |
| ウィンドウを閉じて GC | 1 | True | True | MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord | True | False |
| 250 ms ごとに GC しながら 5 秒待つ | 1 | True | True | MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord | True | False |
| 別の型の通知なしのソースにバインドしたウィンドウを開き、閉じる前に GC | 0 | - | - | - | False | False |
