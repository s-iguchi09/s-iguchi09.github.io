| Window opened next | Registered before opening | First BindingExpression alive before opening | Registered right after opening (before GC) | Registered after GC while open | Registered after closing and GC | First source alive at the end |
|---|---|---|---|---|---|---|
| Window without bindings | True | False | True | True | True | True |
| Window bound to an INPC view model | True | False | False | False | False | False |
| Window bound with Mode=OneTime to a non-INPC type | True | False | True | True | True | True |
| Window bound to a non-INPC property of another type | True | False | False | False | False | False |
| Window bound to another instance of the same non-INPC type | True | False | False | False | False | False |
| Same as above, but opened without running the GC after closing the first window | True | True | True | True | True | True |
