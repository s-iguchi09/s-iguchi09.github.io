| case | measured |
|---|---|
| base class / default IsChecked | ToggleButton / False |
| checked RadioButton clicked again: IsChecked | True |
| no GroupName, A-C in one StackPanel: all clicked in turn -&gt; checked | C |
| D-E in another StackPanel, clicked after A-C -&gt; checked in A-E | E; A-C: C |
| F and G each wrapped in a Border in one StackPanel: both clicked -&gt; checked | F, G |
| no GroupName, RadioButton in an ItemsControl\'s ItemTemplate: all clicked -&gt; checked (Parent) | H, I, J (null) |
| same parent: A, B without GroupName and X with GroupName=\"1\"; X, A, B clicked -&gt; checked | B, X |
| GroupName=\"shared\" in two different GroupBoxes: both clicked -&gt; checked | L |
| GroupName=\"shared\" in the window, another window and a Popup: all clicked -&gt; checked | M, N, P |
