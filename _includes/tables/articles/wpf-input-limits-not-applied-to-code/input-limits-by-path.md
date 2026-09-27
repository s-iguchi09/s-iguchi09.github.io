| restriction | user input | value from code | value from a binding |
|---|---|---|---|
| TextBox MaxLength=5, \"abcdefgh\" | \"abcde\" | \"abcdefgh\" | \"abcdefgh\" |
| TextBox CharacterCasing=Upper, \"hello\" | \"HELLO\" | \"hello\" | \"hello\" |
| PasswordBox MaxLength=8, 10 letters | 8 characters | 10 characters | no PasswordProperty to bind |
| DatePicker 04-10 to 04-20, 04-05 | calendar: day disabled, typed: 2026-04-05 | 2026-04-05 | 2026-04-05 |
| DatePicker, DisplayDateStart afterwards | after typing: 2026-04-05 | 2026-04-05, 04-07 day enabled | 2026-04-05 |
| Slider snap to ticks of 10 | 50, Right arrow: 60 | 23.4: 23.4 | 23.4: 23.4 |
| Slider Maximum=100 | End key: 100 | 150: 100, Maximum 200: 150 | 150: Slider 100, source 150 |
| 2nd TabItem IsEnabled=False | UIA Select(): ElementNotEnabledException, index 0 | index 1, \"Page 2\" | index 1, \"Page 2\" |
