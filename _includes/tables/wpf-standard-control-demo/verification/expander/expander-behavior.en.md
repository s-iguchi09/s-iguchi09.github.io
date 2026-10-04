| case | measured |
|---|---|
| base class / IsExpanded default, two-way by default / ExpandDirection | HeaderedContentControl / False, True / Down |
| default template: header element / VisualStateGroups | ToggleButton named HeaderSite / 0 |
| triggers | IsExpanded=True; ExpandDirection=Right, Up, Left; IsEnabled=False |
| IsExpanded set to False: content Visibility at once / after 50 ms | Collapsed / Collapsed |
| real mouse click on header: IsExpanded / bound CheckBox / events | True / True / Expanded |
| then the CheckBox cleared: IsExpanded / events | False / Collapsed |
| collapsed: child Loaded / measured / items in a 1000-item ListBox | 1 / 0 / 0 |
| after expanding | 2 / 1 / 11 |
