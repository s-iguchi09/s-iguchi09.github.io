| コマンドの書き方 | DispatcherUnhandledException（回数） | UnobservedTaskException（GC を明示的に呼ばずに 1 秒） | UnobservedTaskException（GC の後） | UnobservedTaskException（2 回目を押して GC の後） | ImportCommand.ExecutionTask.Status | 画面のメッセージ | 失敗の後のボタンの IsEnabled |
|---|---|---|---|---|---|---|---|
| Toolkit、async Task に \[RelayCommand\]（既定） | 1 | 0 | 0 | 0 | Faulted | （なし） | True |
| Toolkit、\[RelayCommand(FlowExceptionsToTaskScheduler = true)\] | 0 | 0 | 0 | 1 | Faulted | （なし） | True |
| Prism、DelegateCommand に async のラムダ | 1 | 0 | 0 | 0 | -（プロパティが無い） | （なし） | True |
| Toolkit の既定、ImportAsync の中で await を try / catch | 0 | 0 | 0 | 0 | RanToCompletion | The import file was not found. | True |
| Toolkit の既定、別のコマンドから ImportCommand.ExecuteAsync を await して try / catch | 0 | 0 | 0 | 0 | Faulted | The import file was not found. | True |
