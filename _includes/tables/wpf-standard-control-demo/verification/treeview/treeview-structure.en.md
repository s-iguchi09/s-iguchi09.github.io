| case | measured |
|---|---|
| demo IsExpanded section, node collapsed by its expander: IsExpanded / CheckBox / binding | False / True / removed |
| then the CheckBox unchecked and checked again: IsExpanded | False |
| child IsExpanded=true (source) under a collapsed parent: container before / after expanding | not created / created, IsExpanded True, leaf created |
| explicit HierarchicalDataTemplate: text of a second-level node | \"Workstation PC\" |
| implicit HierarchicalDataTemplate, DataType=Node (items are Node) | first node shows \"Desktop\", expandable True |
| implicit HierarchicalDataTemplate, DataType=String (items are Node) | first node shows \"ToString:Desktop\", expandable False |
| 1,000 root nodes, IsVirtualizing not set: value, panel, TreeViewItems | False, StackPanel, 1000 |
| 1,000 root nodes, IsVirtualizing True: value, panel, TreeViewItems | True, VirtualizingStackPanel, 12 |
| key Down on A (selected, focused) | selected B; A closed, A1 closed |
| key Right on A (selected, focused) | selected A; A open, A1 closed |
| key Left (after Right) on A (selected, focused) | selected A; A closed, A1 closed |
| key Space on A (selected, focused) | selected A; A closed, A1 closed |
| key Enter on A (selected, focused) | selected A; A closed, A1 closed |
| key numpad \* on A (selected, focused) | selected A; A open, A1 open |
| ContextMenu from ItemContainerStyle (Mobile): DataContext before / while open | null / ToString:Mobile |
| ScrollViewer.HorizontalScrollBarVisibility / VerticalScrollBarVisibility (value source) | Auto (DefaultStyle) / Auto (DefaultStyle) |
| placeholder replaced in Expanded: before; after real click on expander (loads) | (loading); Child 1, Child 2 (1) |
