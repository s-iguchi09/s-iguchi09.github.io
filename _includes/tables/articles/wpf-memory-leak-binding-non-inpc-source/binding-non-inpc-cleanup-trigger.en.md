| Window opened next | Registered before opening | First BindingExpression alive before opening | Registered right after opening (before GC) | Registered after GC while open | First BindingExpression alive after that GC | Registered after closing and GC | Registered right after opening one more OneWay window | First source alive at the end |
|---|---|---|---|---|---|---|---|---|
| Window without bindings | True | False | True | True | False | True | False | False |
| Window bound to an INPC view model | True | False | False | False | False | False | False | False |
| Window bound with Mode=OneTime to a non-INPC type | True | False | True | True | False | True | False | False |
| Window bound to a non-INPC property of another type | True | False | False | False | False | False | False | False |
| Window bound to another instance of the same non-INPC type | True | False | False | False | False | False | False | False |
| Same as above, but opened before the first BindingExpression is collected | True | True | True | True | False | True | False | False |
