---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/treeview.html
title: "TreeView"
badge: "List"
lead: "TreeView shows hierarchical data as nodes that the user expands and collapses. Nodes come from nested <code>TreeViewItem</code>s or from data with a <code>HierarchicalDataTemplate</code>."
description: "WPF TreeView control reference: overview, properties, XAML examples, and use cases. Part of the WPF Standard Control Demo App running on .NET 10."
---

## Overview

**TreeView** derives from `ItemsControl`, and each node is a `TreeViewItem`, which derives from `HeaderedItemsControl` and holds its own child nodes. One node at a time is selected. `SelectedItem` and `SelectedValue` are read-only: setting a binding on `SelectedItem` threw `ArgumentException`. Selection is changed through each node's `IsSelected` instead; [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/) covers that in detail. `SelectedItem` returned the `TreeViewItem` for nodes written in XAML, and the data object for nodes created from `ItemsSource`.

Unlike ListBox, a TreeView is not virtualized by default. With 1,000 root nodes in a TreeView 100 high, `VirtualizingPanel.IsVirtualizing` was `False`, the items were arranged in a `StackPanel`, and all 1,000 `TreeViewItem`s were created. With `VirtualizingPanel.IsVirtualizing="True"`, the panel became a `VirtualizingStackPanel` and 12 were created.

The demo app has a section for each property below. The "Show Code" link under each section displays its XAML.

## Screen Preview

![treeview demo screen](/images/wpf-standard-control-demo/treeview.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `IsExpanded (TreeViewItem)` | `bool` | Whether a node shows its children. Unlike `IsSelected`, it does not bind two-way by default. That matters in the demo app, where a check box is bound to Node 1's `IsExpanded` without a `Mode`. Collapsing Node 1 with its own expander button removed that binding. The node collapsed but the check box stayed checked, and unchecking and checking the box afterward no longer expanded the node. Write `Mode=TwoWay` for such a binding. Bound two-way through `ItemContainerStyle`, the state is kept in the data: a child whose source had `IsExpanded = true` under a collapsed parent had no container yet, and was expanded as soon as the parent was expanded. |
| `IsSelected / IsSelectionActive (TreeViewItem)` | `bool / bool (ReadOnly)` | `IsSelected` marks the selected node and binds two-way by default. Selecting a node deselects the previous one and writes `False` back to its source. With `IsSelected` bound through `ItemContainerStyle`, setting Desktop and then Mobile to `True` in the data left Mobile selected and Desktop's source `False`. `IsSelectionActive` (the attached `Selector.IsSelectionActive`) tells whether the selection has keyboard focus. Selected from code without focus, Node 2 had `IsSelectionActive` `False`. Once focused it was `True`. After focus moved to a TextBox, it was `False` again while `IsSelected` stayed `True`. |
| `SelectedValuePath / SelectedValue (TreeView)` | `string / object (ReadOnly)` | `SelectedValuePath` names the property of the selected node that `SelectedValue` returns. The demo app types the path into a text box over nodes written in XAML. With `Header`, selecting "Gaming PC" gave a `SelectedValue` of "Gaming PC". Like `SelectedItem`, `SelectedValue` is read-only. |
| `ItemTemplate (ItemsControl) / HierarchicalDataTemplate` | `DataTemplate` | A `HierarchicalDataTemplate` describes a node and, through its `ItemsSource`, where its children come from. Set as the TreeView's `ItemTemplate`, as in the demo app, it was applied at every level: a second-level node showed "Workstation PC" through the template. As an implicit template in resources, it applies only to items of its `DataType`. With a mismatched `DataType`, the nodes did not become empty: they showed the item's `ToString()` and could not be expanded. |
| `HorizontalScrollBarVisibility / VerticalScrollBarVisibility (ScrollViewer)` | `Disabled / Auto / Hidden / Visible` | Attached properties for the TreeView's scroll bars. The default style sets both to `Auto`. The demo app has a long first node with five children so both bars can be tried. |

## XAML Example

The following XAML is the `ItemTemplate` / `HierarchicalDataTemplate` section of the demo app (`TreeViewUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. The template, which the demo app keeps in the GroupBox's resources, is in the StackPanel's resources here. `DeviceTree` is a collection of items with `Name` and `Children`:

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

## Keyboard

With node A selected and focused (A has children, the first of which has its own child):

- The down arrow selected the next node.
- The right arrow expanded A, and the left arrow collapsed it again.
- The numpad `*` expanded A and everything under it.
- Space and Enter changed neither the selection nor the expansion.

## Common Use Cases

- **Folders and files:** a folder tree whose nodes load their children when expanded.
- **Settings categories:** a tree on the left whose selected node chooses the page on the right.
- **Nested data:** XML, JSON, or organization data shown with a `HierarchicalDataTemplate`.

## Tips and Best Practices

- **Turn on virtualization for large trees.** Set `VirtualizingPanel.IsVirtualizing="True"`; the default creates a container for every node.
- **Bind `IsExpanded` with `Mode=TwoWay`.** A binding without a mode is removed the first time the user uses the expander button.
- **Keep expansion and selection in the data.** Bound through `ItemContainerStyle`, a child's expansion was applied once its parent was expanded, and selecting a node cleared the previous node's flag.
- **Context menus in `ItemContainerStyle` get the node's data.** A `ContextMenu` set there had no `DataContext` before it opened; while open, its `DataContext` was the node's data object.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TreeViewDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TreeViewDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Keys were reproduced by sending key events to a displayed window, and the expander button by toggling it through UI Automation, which runs the same toggle handling as a click.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/treeview/treeview-structure.svg" alt="Table of TreeView results: the demo app's IsExpanded binding is removed when the node is collapsed with its expander, a child expanded in the data opens when its parent opens, a HierarchicalDataTemplate applies at every level, a mismatched DataType shows ToString, the tree is not virtualized by default, arrow keys and numpad asterisk expand or collapse while Space and Enter do nothing, and a ContextMenu gets the node as DataContext while open" width="1210" height="560" loading="lazy">
  <figcaption>Expansion, templates, virtualization, keys, and context menus. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/) — selecting nodes from code and when their containers exist.
- [ListBox](/apps/wpf-standard-control-demo/listbox.html) — a flat list, virtualized by default.
- [Expander](/apps/wpf-standard-control-demo/expander.html) — a single collapsible section.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View TreeView source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TreeViewUsage){: target="_blank" rel="noopener noreferrer"}
