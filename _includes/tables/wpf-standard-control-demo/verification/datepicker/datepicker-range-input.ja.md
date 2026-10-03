| 条件 | 結果 |
|---|---|
| SelectedDate = 04-15: DisplayDate / 続けて DisplayDate = 07-01: SelectedDate | 2026-04-15 / 2026-04-15 |
| 範囲外をコードから: SelectedDate = 04-05 | 例外なし; SelectedDate 2026-04-05 |
| 範囲外を Text から: Text = \"4/5/2026\" | 例外なし; SelectedDate 2026-04-05, Text \"4/5/2026\" |
| 範囲外を入力: \"4/5/2026\" + Enter | SelectedDate 2026-04-05, Text \"4/5/2026\", DateValidationError 0 |
| 範囲内を入力: \"4/18/2026\" + Enter | SelectedDate 2026-04-18, Text \"4/18/2026\", DateValidationError 0 |
| 解析できない文字列を入力: \"abc\" + Enter | SelectedDate 2026-04-15, Text \"4/15/2026\", DateValidationError 1 (\"abc\") |
| 文字列を消して Enter | SelectedDate null, Text \"\", DateValidationError 0 |
| カレンダーのポップアップ、日付ボタン 2026-04-05 | IsEnabled False, IsBlackedOut False, IsInactive False |
| カレンダーのポップアップ、日付ボタン 2026-04-12 | IsEnabled True, IsBlackedOut False, IsInactive False |
| BlackoutDates 04-12\.\.04-13、コードから SelectedDate = 04-12 | ArgumentOutOfRangeException; SelectedDate 2026-04-15 |
| カレンダーのポップアップ、日付ボタン 2026-04-12 | IsEnabled True, IsBlackedOut True |
| SelectedDate 04-10、続けて DisplayDateStart = 04-15 | 例外なし; DisplayDateStart 2026-04-10, SelectedDate 2026-04-10 |
| Text = \"4/15/2026\" | 例外なし; SelectedDate 2026-04-15, Text \"4/15/2026\" |
| Text = \"2026-04-15\" | 例外なし; SelectedDate 2026-04-15, Text \"4/15/2026\" |
| Text = \"April 15, 2026\" | 例外なし; SelectedDate 2026-04-15, Text \"4/15/2026\" |
| Text = \"15/4/2026\" | 例外なし; SelectedDate null, Text \"\" |
