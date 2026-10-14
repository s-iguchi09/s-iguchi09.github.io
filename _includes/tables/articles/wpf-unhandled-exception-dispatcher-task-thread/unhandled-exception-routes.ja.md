| 例外が発生する場所 | 投げたスレッド | DispatcherUnhandledException | AppDomain.UnhandledException | UnobservedTaskException | プロセス |
|---|---|---|---|---|---|
| Click ハンドラ（Handled = true） | UI | 呼ばれる | — | — | 動き続ける |
| Click ハンドラ（Handled を立てない） | UI | 呼ばれる | 呼ばれる（IsTerminating = true） | — | 終了する |
| DispatcherTimer.Tick | UI | 呼ばれる | — | — | 動き続ける |
| new Thread(\.\.\.) | 専用スレッド | — | 呼ばれる（IsTerminating = true） | — | 終了する |
| 2 つ目の UI スレッド（Dispatcher.Run） | 2 つ目の UI | — | 呼ばれる（IsTerminating = true） | — | 終了する |
| + そのスレッドの Dispatcher.UnhandledException | 2 つ目の UI | —（そのスレッドの Dispatcher.UnhandledException が呼ばれる） | — | — | 動き続ける |
| Task.Run(\.\.\.) を待たない（GC なし） | スレッドプール | — | — | — | 動き続ける |
| + GC.Collect() | スレッドプール | — | — | 呼ばれる | 動き続ける |
| Click から呼んだ async Task を待たない（GC なし） | UI | — | — | — | 動き続ける |
| System.Timers.Timer.Elapsed（同期） | スレッドプール | — | — | — | 動き続ける |
| System.Timers.Timer.Elapsed（async） | スレッドプール | — | 呼ばれる（IsTerminating = true） | — | 終了する |
| async void の Click で await Task.Run | スレッドプール | 呼ばれる | — | — | 動き続ける |
| async void の Click で ConfigureAwait(false) の後 | スレッドプール | 呼ばれる | — | — | 動き続ける |
| async void の Click で async Task を await | UI | 呼ばれる | — | — | 動き続ける |
| async void の Click で await を try / catch | スレッドプール | —（catch で受ける） | — | — | 動き続ける |
| スレッドプールで開始した async void | スレッドプール | — | 呼ばれる（IsTerminating = true） | — | 終了する |
