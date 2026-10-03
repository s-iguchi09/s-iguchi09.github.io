| case | result |
|---|---|
| SelectedDate = 04-15: DisplayDate / then DisplayDate = 07-01: SelectedDate | 2026-04-15 / 2026-04-15 |
| out of range from code: SelectedDate = 04-05 | no exception; SelectedDate 2026-04-05 |
| out of range through Text: Text = \"4/5/2026\" | no exception; SelectedDate 2026-04-05, Text \"4/5/2026\" |
| out of range typed: \"4/5/2026\" + Enter | SelectedDate 2026-04-05, Text \"4/5/2026\", DateValidationError 0 |
| in range typed: \"4/18/2026\" + Enter | SelectedDate 2026-04-18, Text \"4/18/2026\", DateValidationError 0 |
| unparsable typed: \"abc\" + Enter | SelectedDate 2026-04-15, Text \"4/15/2026\", DateValidationError 1 (\"abc\") |
| text cleared + Enter | SelectedDate null, Text \"\", DateValidationError 0 |
| calendar popup, day button 2026-04-05 | IsEnabled False, IsBlackedOut False, IsInactive False |
| calendar popup, day button 2026-04-12 | IsEnabled True, IsBlackedOut False, IsInactive False |
| BlackoutDates 04-12\.\.04-13; SelectedDate = 04-12 from code | ArgumentOutOfRangeException; SelectedDate 2026-04-15 |
| calendar popup, day button 2026-04-12 | IsEnabled True, IsBlackedOut True |
| SelectedDate 04-10, then DisplayDateStart = 04-15 | no exception; DisplayDateStart 2026-04-10, SelectedDate 2026-04-10 |
| Text = \"4/15/2026\" | no exception; SelectedDate 2026-04-15, Text \"4/15/2026\" |
| Text = \"2026-04-15\" | no exception; SelectedDate 2026-04-15, Text \"4/15/2026\" |
| Text = \"April 15, 2026\" | no exception; SelectedDate 2026-04-15, Text \"4/15/2026\" |
| Text = \"15/4/2026\" | no exception; SelectedDate null, Text \"\" |
