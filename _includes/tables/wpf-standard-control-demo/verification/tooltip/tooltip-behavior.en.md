| case | measured |
|---|---|
| defaults: InitialShowDelay / ShowDuration / BetweenShowDelay | 1000 / 2147483647 / 100 |
| defaults: Placement / ShowsToolTipOnKeyboardFocus / ShowOnDisabled / HasDropShadow | Mouse / null / False / False |
| real mouse over a button: InitialShowDelay 500 / 2000 | opened after 600 ms / opened after 2050 ms |
| ShowDuration=1000, pointer kept on the button: open 2.5 s later | False |
| string ToolTip opened twice: same ToolTip instance | False |
| disabled button: ShowOnDisabled False / True | did not open / opened after 350 ms |
| Placement=Bottom: tooltip from the button\'s top-left / from the pointer | (0, 30) / (-80, 15) |
| Bottom, HorizontalOffset 50: tooltip from the button\'s top-left / from the pointer | (50, 30) / (-30, 15) |
| Placement=Mouse (default): tooltip from the button\'s top-left / from the pointer | (80, 32) / (0, 17) |
| real Tab (mouse on the other button), OnKeyboardFocus=True: focused / mouse over / opened | True / False / True |
| real Tab (mouse on the other button), OnKeyboardFocus=False: focused / mouse over / opened | True / False / False |
| real Tab (mouse on the other button), OnKeyboardFocus=null: focused / mouse over / opened | True / False / True |
