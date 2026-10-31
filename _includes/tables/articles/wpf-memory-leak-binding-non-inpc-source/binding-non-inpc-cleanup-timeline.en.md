| Step | Entries in \_valueChangedHandlers | Key is the first window\'s source | Record\'s \_source is that source | Handler target type | Source alive | BindingExpression alive |
|---|---|---|---|---|---|---|
| While the window is open | 1 | True | True | MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord | True | True |
| Closed the window, then GC | 1 | True | True | MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord | True | False |
| Waited 5 s, GC every 250 ms | 1 | True | True | MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord | True | False |
| Opened another window bound to a different non-INPC type, then GC before closing it | 0 | - | - | - | False | False |
