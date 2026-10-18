| Click ハンドラでの待ち方 | UI スレッド | 内側の await の後 | 待っている間に UI スレッドへ積んだ処理 |
|---|---|---|---|
| LoadAsync().Result | 止まったまま | 走らない（Task.Delay は完了済み） | Pending のまま |
| LoadAsync().GetAwaiter().GetResult() | 止まったまま | 走らない（Task.Delay は完了済み） | Pending のまま |
| LoadAsync().Wait() | 止まったまま | 走らない（Task.Delay は完了済み） | Pending のまま |
| LoadAsync().Wait(TimeSpan.FromSeconds(1)) | 1,025 ms 止まり、Wait は false を返す | UI（Wait が戻った後） | Click が戻った後に走る |
| LoadAllConfigureAwaitAsync().Result | 325 ms 止まって戻る | スレッドプール | Click が戻った後に走る |
| LoadFirstSuspendConfigureAwaitAsync().Result | 630 ms 止まって戻る | スレッドプール | Click が戻った後に走る |
| LoadCachedFirstAsync().Result | 止まったまま | 走らない（Task.Delay は完了済み） | Pending のまま |
| LoadCachedFirstAllConfigureAwaitAsync().Result | 330 ms 止まって戻る | スレッドプール | Click が戻った後に走る |
| LoadOuterAsync().Result | 止まったまま | 走らない（Task.Delay は完了済み） | Pending のまま |
| Task.Run(() =&gt; LoadAsync()).Result | 325 ms 止まって戻る | スレッドプール | Click が戻った後に走る |
| Task.Run(() =&gt; LoadWithInvokeAsync()).Result | 止まったまま | スレッドプール（その後 Dispatcher.Invoke から戻らない） | Pending のまま |
| async void の Click で await LoadAsync() | 止まらない（積んだ処理は 17 ms 後に走る） | UI | 待っている間に走る |
