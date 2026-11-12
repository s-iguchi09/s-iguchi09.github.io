| 条件 | Closing の回数 | 判定の開始 / 完了 | Closed の発生 | 未処理の例外 | 1.5 秒後のウィンドウ |
|---|---|---|---|---|---|
| Application.Shutdown()・判定が false | 1 | 1 / 0 | 判定の完了より前 | なし | 閉じてプロセスも終了 |
| OnMainWindowClose で別のメインウィンドウを閉じる・false | 1 | 1 / 0 | 判定の完了より前 | なし | 閉じてプロセスも終了 |
| Owner のウィンドウを閉じる・false | 0 | 0 / 0 | 発生する（判定は始まらない） | なし | 閉じてプロセスも終了 |
| 終了ボタン（ApplicationCommands.Close）・false | 1 | 1 / 1 | 発生しない | なし | 開いたまま |
| 終了ボタン（ApplicationCommands.Close）・true | 2 | 1 / 1 | 判定の完了後 | なし | 閉じてプロセスも終了 |
