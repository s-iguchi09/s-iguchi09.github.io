| view model | value from code | user input |
|---|---|---|
| INotifyDataErrorInfo, at most 5 | \"abcdefgh\": HasErrors True, Validation.HasError True | typed \"abcdefgh\": \"abcde\", HasErrors False |
| INotifyDataErrorInfo, loaded over the limit | \"abcdefgh\" loaded: HasErrors True | x at the end: \"abcdefgh\"; Backspace: HasErrors True |
| Save button, IsEnabled bound to !HasErrors | \"abcdefgh\": False; \"abcde\": True | loaded: False; 3 Backspaces to \"abcde\": True |
| setter clamps to 0-100 | 150: source 100, Slider 100 | End key: sent 200, source 100, Slider 100 |
| setter rounds to 10 | 23.4: source 20, Slider 20 | 50, Right arrow: sent 55, source 60, Slider 60 |
