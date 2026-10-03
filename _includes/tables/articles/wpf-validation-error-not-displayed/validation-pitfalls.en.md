| case | measured |
|---|---|
| setter validation, LostFocus: cleared by typing | HasError: while focused False (Text \"\"); after focus leaves True |
| setter validation, Explicit: cleared by typing | HasError: while focused False (Text \"\"); after focus leaves False; after UpdateSource True |
| OneWay + rule, source empty | HasError: start True; typed \"abc\" True; source \"abc\" False |
| OneWay, Text = \"x\" in code | binding removed, HasError False |
| OneTime, Text = \"x\" in code | binding removed, HasError False |
| OneWay, SetCurrentValue(Text, \"x\") | binding kept, HasError True |
| error added, ErrorsChanged(\"Namee\"), path Name | HasErrors True, Validation.HasError False |
| error added, ErrorsChanged(\"Name\"), path Name | HasErrors True, Validation.HasError True |
| Validation.Error handler, NotifyOnValidationError=False | Validation.Error raised 0 times |
| Validation.Error handler, NotifyOnValidationError=True | Validation.Error raised 2 times |
| (Validation.Errors)\[0\].ErrorContent, error cleared | HasError False, Error 17 traced 1 |
| (Validation.Errors)/ErrorContent, error cleared | HasError False, Error 17 traced 0 |
| ErrorsChanged raised on the UI thread | Validation.HasError True (raised on thread UI) |
| ErrorsChanged raised on a background thread | Validation.HasError True (raised on thread other than UI) |
