|  | Application.Exit | Run() returns | process ends |
|---|---|---|---|
| visible window only | raised | returns | 3.6 s |
| no window at all | never | never | never |
| Window created, never closed | never | never | never |
| new Thread(\.\.\.) | raised | returns | 6.3 s |
| + IsBackground = true | raised | returns | 2.9 s |
| Task.Run(\.\.\.) | raised | returns | 3.2 s |
| 2nd UI thread + Dispatcher.Run() | raised | returns | never |
| + InvokeShutdown() on Exit | raised | returns | 3.0 s |
