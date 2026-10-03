| case | measured |
|---|---|
| IsChecked bound to bool A, B, C (Mode not set); B clicked: source / A still bound | A=False, B=True, C=False / True |
| then C set to true in the source: source / checked | A=False, B=False, C=True / C |
| A checked and focused, Down arrow: focus / checked | B / A |
| focus on A, Tab: focus | B |
| VerticalContentAlignment (value source) | Top (Default) |
| 3-line label, VerticalContentAlignment=Top, RadioButton 200 high: glyph y / label y | 1 (height 12) / -1 (height 47.88) |
| 3-line label, VerticalContentAlignment=Center, RadioButton 200 high: glyph y / label y | 94 (height 12) / 75.56 (height 47.88) |
| 3-line label, VerticalContentAlignment=Center, RadioButton 46.88 high: glyph y / label y | 17.44 (height 12) / -1 (height 47.88) |
