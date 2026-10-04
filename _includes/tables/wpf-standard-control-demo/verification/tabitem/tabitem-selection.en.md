| case | measured |
|---|---|
| base class | HeaderedContentControl |
| IsSelected: BindsTwoWayByDefault | True |
| TabStripPlacement on TabItem: read-only | True |
| TabControl.TabStripPlacement=Left: TabItem.TabStripPlacement of both tabs | Left / Left |
| Tab3.IsSelected = true: SelectedIndex / IsSelected of Tab1\.\.3 | 2 / False, False, True |
| XAML: IsSelected=\"True\" on Tab2 and Tab3: SelectedIndex / IsSelected of Tab1\.\.3 | 1 / False, True, False |
| code: Tab2 then Tab3 set to IsSelected=true: SelectedIndex | 2 |
| IsSelected bound without Mode; SelectedIndex = 1: source values of Tab1 / Tab2 | False / True |
| then Tab1\'s source set to True: SelectedIndex / source values of Tab1 / Tab2 | 0 / True / False |
| then Tab2\'s header clicked with the real mouse: SelectedIndex / sources of Tab1 / Tab2 | 1 / False / True |
| Tab2 IsEnabled=False; SelectedIndex = 1 from code: SelectedIndex / content shown | 1 / Item2 |
| Tab2 IsEnabled=False; UI Automation ISelectionItemProvider.Select(): result / SelectedIndex | ElementNotEnabledException / 0 |
| Tab2 IsEnabled=False; header clicked with the real mouse: SelectedIndex | 0 |
