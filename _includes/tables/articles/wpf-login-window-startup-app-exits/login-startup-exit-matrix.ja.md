|  | ログイン中の MainWindow | ShowDialog() の戻り値 | ログイン画面を閉じた後の MainWindow | ShowDialog() 直後の終了処理 | Show() 直後の IsVisible | Show() 後の MainWindow | メイン画面 | Application.Exit のきっかけ | プロセス | 未処理の例外 |
|---|---|---|---|---|---|---|---|---|---|---|
| OnLastWindowClose（既定）、ログイン後に Show | LoginWindow | True | null | 始まっている | false | MainWindow | 表示されない | ログイン画面を閉じたこと | 終わる | — |
| 起動時から OnMainWindowClose | LoginWindow | True | null | 始まっている | false | MainWindow | 表示されない | ログイン画面を閉じたこと | 終わる | — |
| StartupUri を残し、OnStartup でログイン | LoginWindow | True | null | 始まっている | — | — | 表示されない | ログイン画面を閉じたこと | 終わる | — |
| ログイン前にメイン画面を生成 | MainWindow | True | MainWindow | 始まっていない | true | MainWindow | 1.5 秒後も開いている | メイン画面を閉じたこと | 終わる | — |
| ログイン前にメイン画面を生成、取り消し、Shutdown() なし | MainWindow | False | MainWindow | 始まっていない | — | — | 表示されない | 発生しない | 終わらない | — |
| ログイン前にメイン画面を生成、取り消し → Shutdown() | MainWindow | False | MainWindow | 始まっていない | — | — | 表示されない | Shutdown() | 終わる | — |
| OnExplicitShutdown → OnMainWindowClose（推奨） | LoginWindow | True | null | 始まっていない | true | MainWindow | 1.5 秒後も開いている | メイン画面を閉じたこと | 終わる | — |
| 推奨形で StartupUri を残す | LoginWindow | True | null | 始まっていない | true | MainWindow | 1.5 秒後に 2 つ開いている | メイン画面を閉じたこと | 終わる | — |
| 推奨形、取り消し → Shutdown() | LoginWindow | False | null | 始まっていない | — | — | 生成されない | Shutdown() | 終わる | — |
| 推奨形、× で閉じる（WM\_CLOSE）→ Shutdown() | LoginWindow | False | null | 始まっていない | — | — | 生成されない | Shutdown() | 終わる | — |
| 推奨形から MainWindow の代入を省く | LoginWindow | True | null | 始まっていない | true | MainWindow | 1.5 秒後も開いている | メイン画面を閉じたこと | 終わる | — |
| 代入を省き、メイン画面の前に別の Window を生成 | LoginWindow | True | null | 始まっていない | true | Window | 1.5 秒後も開いている | 発生しない | 終わらない | — |
| 代入し、メイン画面の前に別の Window を生成 | LoginWindow | True | null | 始まっていない | true | MainWindow | 1.5 秒後も開いている | メイン画面を閉じたこと | 終わる | — |
| OnExplicitShutdown → OnLastWindowClose | LoginWindow | True | null | 始まっていない | true | MainWindow | 1.5 秒後も開いている | メイン画面を閉じたこと | 終わる | — |
| OnExplicitShutdown → OnLastWindowClose、別の Window を生成 | LoginWindow | True | null | 始まっていない | true | MainWindow | 1.5 秒後も開いている | 発生しない | 終わらない | — |
| ログイン後も OnExplicitShutdown のまま | LoginWindow | True | null | 始まっていない | true | MainWindow | 1.5 秒後も開いている | 発生しない | 終わらない | — |
| ShowDialog が戻った後に OnExplicitShutdown を設定 | LoginWindow | True | null | 始まっている | — | — | 生成されない | ログイン画面を閉じたこと | 終わる | InvalidOperationException |
