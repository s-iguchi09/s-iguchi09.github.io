| 条件 | 計測値 |
|---|---|
| null を許さない DateTime にバインド、SelectedDate = null | ソース 2026-04-15、HasError True、Validation.HasError True |
| 表示する前に IsDropDownOpen = True: IsDropDownOpen / Popup.IsOpen | True / True |
| 表示中の例外 | 例外なし |
| IsTodayHighlighted 指定なし: DatePicker / ポップアップの Calendar の値（出どころ） | True (DefaultStyle) / True (Local) |
| 今日の日付のボタン: IsToday / IsEnabled / 今日の印 | True / True / DayStates=Today、TodayBackground の不透明度 1 |
| IsTodayHighlighted True: DatePicker / ポップアップの Calendar の値（出どころ） | True (Local) / True (Local) |
| 今日の日付のボタン: IsToday / IsEnabled / 今日の印 | True / True / DayStates=Today、TodayBackground の不透明度 1 |
| IsTodayHighlighted False: DatePicker / ポップアップの Calendar の値（出どころ） | False (Local) / False (Local) |
| 今日の日付のボタン: IsToday / IsEnabled / 今日の印 | True / True / DayStates=RegularDay、TodayBackground の不透明度 0 |
| FirstDayOfWeek=Sunday: ポップアップの Calendar の FirstDayOfWeek | Sunday |
| FirstDayOfWeek=Monday: ポップアップの Calendar の FirstDayOfWeek | Monday |
