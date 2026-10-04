| case | measured |
|---|---|
| defaults: IsMainMenu; IsCheckable, IsChecked, StaysOpenOnClick, InputGestureText | True; False, False, False, (empty) |
| demo\'s Role section: the four items | TopLevelHeader, SubmenuHeader, SubmenuItem, TopLevelItem |
| a child added to TopLevelItem: its Role | TopLevelHeader |
| IsCheckable True, StaysOpenOnClick True: opened; click: checked / CheckBox / open; again | True; True / True / True; False / True |
| the CheckBox checked in code: item IsChecked | True |
| IsCheckable True, StaysOpenOnClick False: opened; click: checked / CheckBox / open; again | True; True / True / False; - |
| IsCheckable False, StaysOpenOnClick True: opened; click: checked / CheckBox / open; again | True; False / False / True; False / True |
| two checkable items, both clicked: IsChecked | True, True |
| IsMainMenu True, real Alt: highlighted / submenu open / TextBox keeps focus | True / False / False |
| IsMainMenu True, real F10: highlighted / submenu open / TextBox keeps focus | False / False / True |
| IsMainMenu True, real Alt+M: highlighted / submenu open / TextBox keeps focus | True / True / False |
| IsMainMenu False, real Alt: highlighted / submenu open / TextBox keeps focus | False / False / True |
| IsMainMenu False, real F10: highlighted / submenu open / TextBox keeps focus | False / False / True |
| IsMainMenu False, real Alt+M: highlighted / submenu open / TextBox keeps focus | True / True / False |
| IsMainMenu True, real F10, Button: highlighted / open / focus in menu | True / False / True |
| IsMainMenu True, F10 via InputManager, TextBox: highlighted / open / focus in menu | True / False / True |
| InputGestureText \"Ctrl+O\": shown / InputGestureText of the Copy item | shown (35.83 wide) / Ctrl+C |
| real Ctrl+O with a TextBox focused: Click count | 0 |
| Copy item, menu closed, TextBox focused, no selection: IsEnabled / CanExecute | True / False |
| opened by real click: no selection; all selected (focus); Button focused | False; True (MenuItem); False |
| Observation Target: Highlighted / Pressed / SubmenuOpen / Suspending: before; hover | False / False / False / False; True / False / False / False |
| real button down; up | True / True / True / True; True / False / True / True |
| click File: open / suspending; hover Edit: File open; Edit open / suspending | True / True; False; True / True |
| window KeyBinding Ctrl+O, real Ctrl+O: executed / item\'s InputGestureText | 1 / (empty) |
