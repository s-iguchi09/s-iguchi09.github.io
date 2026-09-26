---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/treeview.html
title: "TreeView"
badge: "List"
lead: "TreeView shows hierarchical data as nodes that the user expands and collapses. Nodes come from nested <code>TreeViewItem</code>s or from data with a <code>HierarchicalDataTemplate</code>."
description: "WPF TreeView measured on .NET 10: no virtualization by default, an IsExpanded binding the expander removes unless TwoWay, HierarchicalDataTemplate, and keys."
---

## Not virtualized by default

**TreeView** derives from `ItemsControl`, and each node is a `TreeViewItem`, which derives from `HeaderedItemsControl` and holds its own child nodes. Unlike ListBox, a TreeView is not virtualized by default. With 1,000 root nodes in a TreeView 100 high, `VirtualizingPanel.IsVirtualizing` was `False`, the items were arranged in a `StackPanel`, and all 1,000 `TreeViewItem`s were created. With `VirtualizingPanel.IsVirtualizing="True"`, the panel became a `VirtualizingStackPanel` and 12 were created. Turn it on for large trees.

## An IsExpanded binding is removed unless it is TwoWay

`IsExpanded` on a TreeViewItem tells whether a node shows its children. Unlike `IsSelected`, it does not bind two-way by default. With a check box bound to Node 1's `IsExpanded` without a `Mode`, collapsing Node 1 with its own expander button removed that binding. The node collapsed but the check box stayed checked, and unchecking and checking the box afterward no longer expanded the node. Write `Mode=TwoWay` for such a binding.

Bound two-way through `ItemContainerStyle`, the state is kept in the data: a child whose source had `IsExpanded = true` under a collapsed parent had no container yet, and was expanded as soon as the parent was expanded. Keep expansion in the data.

For a folder tree that loads children when a node is expanded, a placeholder child that the `Expanded` handler replaced worked: a real click on the expander button showed the two real children.

## Selection goes through IsSelected, not SelectedItem

One node at a time is selected. `SelectedItem` and `SelectedValue` are read-only: setting a binding on `SelectedItem` threw `ArgumentException`. Selection is changed through each node's `IsSelected` instead; [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/) covers that in detail. `SelectedItem` returned the `TreeViewItem` for nodes written in XAML, and the data object for nodes created from `ItemsSource`.

`IsSelected` binds two-way by default. Selecting a node deselects the previous one and writes `False` back to its source. With `IsSelected` bound through `ItemContainerStyle`, setting Desktop and then Mobile to `True` in the data left Mobile selected and Desktop's source `False`.

`IsSelectionActive` (the attached `Selector.IsSelectionActive`) is read-only and tells whether the selection has keyboard focus. Selected from code without focus, Node 2 had `IsSelectionActive` `False`. Once focused it was `True`. After focus moved to a TextBox, it was `False` again while `IsSelected` stayed `True`.

`SelectedValuePath` names the property of the selected node that `SelectedValue` returns. With `Header`, selecting "Gaming PC" gave a `SelectedValue` of "Gaming PC".

## HierarchicalDataTemplate, and what a node's data reaches

A `HierarchicalDataTemplate` describes a node and, through its `ItemsSource`, where its children come from. Set as the TreeView's `ItemTemplate`, it was applied at every level: a second-level node showed "Workstation PC" through the template. As an implicit template in resources, it applies only to items of its `DataType`. With a mismatched `DataType`, the nodes did not become empty: they showed the item's `ToString()` and could not be expanded.

A `ContextMenu` set in `ItemContainerStyle` had no `DataContext` before it opened; while open, its `DataContext` was the node's data object.

## Keyboard

With node A selected and focused (A has children, the first of which has its own child), the down arrow selected the next node. The right arrow expanded A, and the left arrow collapsed it again. The numpad `*` expanded A and everything under it. Space and Enter changed neither the selection nor the expansion.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/treeview/treeview-structure.svg" alt="Table of TreeView results: the demo app's IsExpanded binding is removed when the node is collapsed with its expander, a child expanded in the data opens when its parent opens, a HierarchicalDataTemplate applies at every level, a mismatched DataType shows ToString, the tree is not virtualized by default, arrow keys and numpad asterisk expand or collapse while Space and Enter do nothing, a ContextMenu gets the node as DataContext while open, and a placeholder child replaced in the Expanded handler shows the real children after a real click on the expander button" width="1210" height="590" loading="lazy">
  <figcaption>Expansion, templates, virtualization, keys, and context menus. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The TreeView page of the demo app, with the control list on the left and the first section, IsExpanded(TreeViewItem)](/images/wpf-standard-control-demo/treeview.png){: .screenshot-img}

The TreeView page of the demo app has sections for `IsExpanded`, `IsSelected` and `IsSelectionActive`, `SelectedValuePath` and `SelectedValue`, `ItemTemplate` with a `HierarchicalDataTemplate`, and the scroll bars. The `IsExpanded` section binds a check box to Node 1's `IsExpanded` without a `Mode`, so the binding is removed as described above. The `SelectedValuePath` section takes the path from a text box, over nodes written in XAML. The scroll bar section has a long first node with five children so both bars can be tried; the default style sets both `ScrollViewer.HorizontalScrollBarVisibility` and `ScrollViewer.VerticalScrollBarVisibility` to `Auto`. The "Show Code" link under each section displays its XAML. The following XAML is the `ItemTemplate` / `HierarchicalDataTemplate` section (`TreeViewUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. The template, which the demo app keeps in the GroupBox's resources, is in the StackPanel's resources here. `DeviceTree` is a collection of items with `Name` and `Children`:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <StackPanel.Resources>
    <HierarchicalDataTemplate x:Key="MyTreeViewTemplate" x:Name="MyTreeViewTemplate"
                              ItemsSource="{Binding Children}">
      <StackPanel Orientation="Horizontal">
        <Path Margin="0,0,5,0" VerticalAlignment="Center"
              Data="M0,0 L8,4 L0,8 Z" Fill="Orange" />
        <TextBlock VerticalAlignment="Center" Text="{Binding Name}" />
      </StackPanel>
    </HierarchicalDataTemplate>
  </StackPanel.Resources>

  <TreeView x:Name="ItemTemplateTreeView" Height="150"
            ItemTemplate="{StaticResource MyTreeViewTemplate}"
            ItemsSource="{Binding DeviceTree}" />
</StackPanel>
```

## Related controls and articles

- [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/) — selecting nodes from code and when their containers exist.
- [ListBox](/apps/wpf-standard-control-demo/listbox.html) — a flat list, virtualized by default.
- [Expander](/apps/wpf-standard-control-demo/expander.html) — a single collapsible section.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TreeViewDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TreeViewDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Keys were reproduced by sending key events to a displayed window, and the expander button by toggling it through UI Automation, which runs the same toggle handling as a click. Only the expander of the node with a placeholder child was clicked with the real mouse.

[View TreeView source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TreeViewUsage){: target="_blank" rel="noopener noreferrer"}
