| case | measured |
|---|---|
| base class / derived: CheckBox, RadioButton | ButtonBase / ToggleButton, ToggleButton |
| IsChecked: BindsTwoWayByDefault / GroupName property on ToggleButton | True / False |
| IsThreeState=False, start null: 3 clicks | null -&gt; false -&gt; true -&gt; false |
| IsThreeState=False, start false: 3 clicks | false -&gt; true -&gt; false -&gt; true |
| IsThreeState=False, start true: 3 clicks | true -&gt; false -&gt; true -&gt; false |
| IsThreeState=True, start false: 3 clicks | false -&gt; true -&gt; null -&gt; false |
| default template: VisualStateGroups / triggers on IsChecked (values) | 0 / true |
| UI Automation ToggleState | false -&gt; Off, true -&gt; On, null -&gt; Indeterminate |
| ClickMode=Press: IsChecked after button down / after button up (false before) | true / true |
| Command, CommandParameter bound to its own IsChecked; clicked (OnClick) from false: in Execute | IsChecked true, parameter True |
| the same button toggled through UI Automation\'s Toggle: Command | not executed |
| Popup.IsOpen bound to IsChecked (Mode not set): after click / after the popup closes | IsChecked true, IsOpen True / IsChecked false |
| Popup.IsOpen: BindsTwoWayByDefault | True |
| StaysOpen=False, real clicks (IsOpen, IsChecked): button / empty area | True, true / False, false |
| button again / button while open | True, true / True, true |
