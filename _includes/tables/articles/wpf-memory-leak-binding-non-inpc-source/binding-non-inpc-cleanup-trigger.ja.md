| 次に開いたウィンドウ | 開く前に登録あり | 開く前に最初の BindingExpression が残っている | 開いた直後（GC の前）に登録あり | 開いたまま GC した後に登録あり | 閉じて GC した後に登録あり | 最後に最初のソースが残っている |
|---|---|---|---|---|---|---|
| バインドの無いウィンドウ | True | False | True | True | True | True |
| INPC のビューモデルにバインドしたウィンドウ | True | False | False | False | False | False |
| 通知なしの型に Mode=OneTime でバインドしたウィンドウ | True | False | True | True | True | True |
| 別の型の通知なしのプロパティにバインドしたウィンドウ | True | False | False | False | False | False |
| 同じ通知なしの型の別のインスタンスにバインドしたウィンドウ | True | False | False | False | False | False |
| 同上。ただし最初のウィンドウを閉じた後、GC を呼ばずに開く | True | True | True | True | True | True |
