| binding | result |
|---|---|
| DisplayDateStart, default (TwoWay) | DisplayDateStart 2026-04-05, source 2026-04-10, binding kept; source set to 04-12: 2026-04-12 |
| DisplayDateStart, Mode=OneWay | DisplayDateStart 2026-04-05, source 2026-04-10, binding kept; source set to 04-12: 2026-04-12 |
| DisplayDateStart, get-only, Source set | SetBinding: InvalidOperationException (read-only property) |
| DisplayDateStart, get-only, DataContext set while shown | InvalidOperationException (read-only property) |
| DisplayDateStart, get-only, DataContext set before showing | at DataContext: no exception; at Show: InvalidOperationException (read-only property) |
| DisplayDateStart, get-only, parent\'s DataContext before showing | at DataContext: no exception; at Show: InvalidOperationException (read-only property) |
| DisplayDateEnd, get-only, Source set | SetBinding: InvalidOperationException (read-only property) |
| DisplayDateEnd, get-only, DataContext set while shown | InvalidOperationException (read-only property) |
| DisplayDateEnd, get-only, DataContext set before showing | at DataContext: no exception; at Show: InvalidOperationException (read-only property) |
| DisplayDateEnd, get-only, parent\'s DataContext before showing | at DataContext: no exception; at Show: InvalidOperationException (read-only property) |
