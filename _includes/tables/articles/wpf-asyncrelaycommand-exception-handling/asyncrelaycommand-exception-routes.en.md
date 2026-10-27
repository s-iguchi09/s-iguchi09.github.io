| how the command is written | DispatcherUnhandledException (calls) | UnobservedTaskException, 1 s without an explicit GC | UnobservedTaskException after GC | UnobservedTaskException after a 2nd click and GC | ImportCommand.ExecutionTask.Status | message on screen | button IsEnabled after the failure |
|---|---|---|---|---|---|---|---|
| Toolkit \[RelayCommand\] on async Task (default) | 1 | 0 | 0 | 0 | Faulted | (none) | True |
| Toolkit \[RelayCommand(FlowExceptionsToTaskScheduler = true)\] | 0 | 0 | 0 | 1 | Faulted | (none) | True |
| Prism DelegateCommand with an async lambda | 1 | 0 | 0 | 0 | - (no such property) | (none) | True |
| Toolkit default, try / catch around the await in ImportAsync | 0 | 0 | 0 | 0 | RanToCompletion | The import file was not found. | True |
| Toolkit default, another command awaits ImportCommand.ExecuteAsync in try / catch | 0 | 0 | 0 | 0 | Faulted | The import file was not found. | True |
