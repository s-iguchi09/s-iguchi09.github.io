| where the exception is thrown | throwing thread | DispatcherUnhandledException | AppDomain.UnhandledException | UnobservedTaskException | process |
|---|---|---|---|---|---|
| Click handler (Handled = true) | UI | called | — | — | keeps running |
| Click handler (Handled not set) | UI | called | called (IsTerminating = true) | — | terminated |
| DispatcherTimer.Tick | UI | called | — | — | keeps running |
| new Thread(\.\.\.) | dedicated | — | called (IsTerminating = true) | — | terminated |
| 2nd UI thread (Dispatcher.Run) | 2nd UI | — | called (IsTerminating = true) | — | terminated |
| + its own Dispatcher.UnhandledException | 2nd UI | — (its own Dispatcher.UnhandledException is called) | — | — | keeps running |
| Task.Run(\.\.\.), not awaited (no GC) | thread pool | — | — | — | keeps running |
| + GC.Collect() | thread pool | — | — | called | keeps running |
| async Task called from Click, not awaited (no GC) | UI | — | — | — | keeps running |
| System.Timers.Timer.Elapsed (sync) | thread pool | — | — | — | keeps running |
| System.Timers.Timer.Elapsed (async) | thread pool | — | called (IsTerminating = true) | — | terminated |
| async void Click, await Task.Run | thread pool | called | — | — | keeps running |
| async void Click, after ConfigureAwait(false) | thread pool | called | — | — | keeps running |
| async void Click, await async Task | UI | called | — | — | keeps running |
| async void Click, try / catch around await | thread pool | — (caught by catch) | — | — | keeps running |
| async void started on the thread pool | thread pool | — | called (IsTerminating = true) | — | terminated |
