| case | measured |
|---|---|
| bound to a DateTime (not nullable); SelectedDate = null | source 2026-04-15, HasError True, Validation.HasError True |
| IsDropDownOpen = True before showing: IsDropDownOpen / Popup.IsOpen | True / True |
| exception while showing | no exception |
| IsTodayHighlighted not set: DatePicker / popup Calendar value (source) | True (DefaultStyle) / True (Local) |
| today\'s day button: IsToday / IsEnabled / today marker | True / True / DayStates=Today, TodayBackground opacity 1 |
| IsTodayHighlighted True: DatePicker / popup Calendar value (source) | True (Local) / True (Local) |
| today\'s day button: IsToday / IsEnabled / today marker | True / True / DayStates=Today, TodayBackground opacity 1 |
| IsTodayHighlighted False: DatePicker / popup Calendar value (source) | False (Local) / False (Local) |
| today\'s day button: IsToday / IsEnabled / today marker | True / True / DayStates=RegularDay, TodayBackground opacity 0 |
| FirstDayOfWeek=Sunday: the popup Calendar\'s FirstDayOfWeek | Sunday |
| FirstDayOfWeek=Monday: the popup Calendar\'s FirstDayOfWeek | Monday |
