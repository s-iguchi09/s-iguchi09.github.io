| how the Click handler waits | UI thread | code after the inner await | UI work queued during the wait |
|---|---|---|---|
| LoadAsync().Result | stays blocked | does not run (Task.Delay already completed) | stays Pending |
| LoadAsync().GetAwaiter().GetResult() | stays blocked | does not run (Task.Delay already completed) | stays Pending |
| LoadAsync().Wait() | stays blocked | does not run (Task.Delay already completed) | stays Pending |
| LoadAsync().Wait(TimeSpan.FromSeconds(1)) | blocked 1,025 ms, Wait returns false | UI (after Wait returned) | runs after the Click returns |
| LoadAllConfigureAwaitAsync().Result | blocked 325 ms, then returns | thread pool | runs after the Click returns |
| LoadFirstSuspendConfigureAwaitAsync().Result | blocked 630 ms, then returns | thread pool | runs after the Click returns |
| LoadCachedFirstAsync().Result | stays blocked | does not run (Task.Delay already completed) | stays Pending |
| LoadCachedFirstAllConfigureAwaitAsync().Result | blocked 330 ms, then returns | thread pool | runs after the Click returns |
| LoadOuterAsync().Result | stays blocked | does not run (Task.Delay already completed) | stays Pending |
| Task.Run(() =&gt; LoadAsync()).Result | blocked 325 ms, then returns | thread pool | runs after the Click returns |
| Task.Run(() =&gt; LoadWithInvokeAsync()).Result | stays blocked | thread pool (then never returns from Dispatcher.Invoke) | stays Pending |
| await LoadAsync() in async void Click | not blocked (queued work ran after 17 ms) | UI | runs while waiting |
