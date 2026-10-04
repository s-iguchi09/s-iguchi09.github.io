| case | measured |
|---|---|
| base class of CheckBox / RadioButton | ToggleButton / ToggleButton |
| IsChecked: BindsTwoWayByDefault | True |
| Checked / Unchecked / Indeterminate routing | Bubble / Bubble / Bubble |
| VerticalContentAlignment (value source) | Top (Default) |
| IsThreeState=True, starting at false: IsChecked after 3 clicks | false -&gt; true -&gt; null -&gt; false |
| IsThreeState=False, starting at false: IsChecked after 3 clicks | false -&gt; true -&gt; false -&gt; true |
| IsThreeState=False, starting at null: IsChecked after 3 clicks | null -&gt; false -&gt; true -&gt; false |
| IsThreeState=True, starting at null: IsChecked after 3 clicks | null -&gt; false -&gt; true -&gt; null |
| IsThreeState, bound to bool? (true), clicked to null: IsChecked / source | null / null |
| IsThreeState, bound to bool (true), clicked to null: IsChecked / source / binding error | null / true / HasError True |
| Space pressed and released on a focused CheckBox (false before) | true |
| hit test in the middle of the label: element hit / inside the CheckBox | TextBlock / True |
| 3-line label, VerticalContentAlignment=Top, CheckBox 200 high: glyph y / label y | 1 (height 13.1) / -1 (height 47.88) |
| 3-line label, VerticalContentAlignment=Center, CheckBox 200 high: glyph y / label y | 93.45 (height 13.1) / 75.56 (height 47.88) |
| 3-line label, VerticalContentAlignment=Center, CheckBox 46.88 high: glyph y / label y | 16.89 (height 13.1) / -1 (height 47.88) |
| Checked handler on the parent StackPanel, child clicked: handler calls | 1 |
| Button IsEnabled bound to a CheckBox\'s IsChecked: before / after a real click on the CheckBox | False / True |
