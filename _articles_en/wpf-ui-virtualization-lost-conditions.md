---
layout: article-en
title: "Finding What Turns Off UI Virtualization in WPF"
date: 2026-10-03
category: WPF
excerpt: "WPF list virtualization turns off silently through placement, the items panel, or scroll settings. Measured with 1,000 items, with diagnosis steps and fixes."
image: /images/articles/wpf-ui-virtualization-lost-conditions/virtualization-placement.svg
---

## Overview

A `ListBox` virtualizes by default and creates item containers (`ListBoxItem`) only for the items in view and in the cache.
Depending on how it is placed or configured, however, showing 1,000 items creates all 1,000 containers.
When virtualization turns off, no exception is raised.
When the placement is the cause, or when `ItemsPanel` is replaced or the items are grouped with a `GroupStyle`, `IsVirtualizing` also stays `True`, so it tends to go unnoticed.

The main causes fall into three groups.
The placement does not limit the height, the items panel or the template does not support virtualization, or a setting turns off virtualization or logical scrolling, and each group needs a different fix.
In addition, when containers such as `ListBoxItem` are passed as the items, the number of containers alone cannot tell the list apart from one given data.
This article shows which conditions turn virtualization off, measured by counting the item containers realized for 1,000 items in an area 200 units high.
It also shows how to find the cause in an existing application, and the fix for each cause.

---

## Prerequisites / Environment

- Framework: WPF on .NET Framework 4.5 or later / .NET Core 3.0 or later
- Scope: controls derived from `ItemsControl` (`ListBox`, `ListView`, `DataGrid`, `TreeView`, `ComboBox`, `ItemsControl`)
- APIs involved: `VirtualizingStackPanel`, `VirtualizingPanel.IsVirtualizing`, `VirtualizingPanel.IsVirtualizingWhenGrouping`, `VirtualizingPanel.ScrollUnit`, `VirtualizingPanel.CacheLength`, `VirtualizingPanel.CacheLengthUnit`, `ScrollViewer.CanContentScroll`, `DataGrid.EnableRowVirtualization`, `ItemsControl.IsGrouping`, `ItemsControl.IsItemItsOwnContainer`
- Verification environment: .NET 10 / Windows 11 (default theme)
- Measurement: 1,000 items were shown in an area 300 wide and 200 high, and after layout completed, the positions with a container were counted with `ItemContainerGenerator.ContainerFromIndex`. The measurement is implemented as a scene in `tools/screenshot-capture`.

The number of containers depends on the item height, which changes with the font and display scaling.
This article reads whether virtualization works as "only the visible range and the cache" or "all 1,000".

---

## Problem

A `ListBox` was placed in a vertical `StackPanel` to put a search box above it.
With few items, there is no visible difference even when virtualization is off.

```xml
<StackPanel>
  <TextBox Text="{Binding Keyword}" />
  <ListBox ItemsSource="{Binding Items}" DisplayMemberPath="Name" />
</StackPanel>
```

This `ListBox` creates a container for every one of 1,000 items (see the placement table below).
Yet its items panel is still a `VirtualizingStackPanel`, and `VirtualizingPanel.IsVirtualizing` is still `True`.
The virtualization settings alone do not explain why it is off.

---

## What the Symptom Narrows Down

A `VirtualizingStackPanel` creates containers only for the items in view and in the cache, when the `ScrollViewer` that wraps the `ItemsPresenter` inside the control's template scrolls logically (`CanContentScroll=True`).
In logical scrolling, the items panel handles the scrolling instead of the `ScrollViewer`.
In a `VirtualizingStackPanel`, `VirtualizingPanel.ScrollUnit` decides whether it scrolls per item or per pixel.
A `StackPanel` also scrolls logically, but it does not virtualize.
The size of the cache is set by `VirtualizingPanel.CacheLength` and `VirtualizingPanel.CacheLengthUnit` ([VirtualizingPanel.CacheLength](https://learn.microsoft.com/dotnet/api/system.windows.controls.virtualizingpanel.cachelength)).
The measured size is shown in the settings table below.
When an `ItemsControl` with only its panel replaced by a `VirtualizingStackPanel` was placed in an outer `ScrollViewer`, the items panel did not scroll logically, even with `CanContentScroll=True` (see the table of fixes below).
Virtualization therefore needs three conditions at once.
The view of the `ScrollViewer` inside the template must be limited, the items panel must be a `VirtualizingStackPanel`, and `IsVirtualizing` and `CanContentScroll` must be `True`.
These three conditions apply when the items are passed as data.
When item containers such as `ListBoxItem` are added directly to `Items`, the official documentation states that they are not virtualized ([Optimizing performance: Controls](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/optimizing-performance-controls#displaying-large-data-sets)).
Passing `ListBoxItem` objects through `ItemsSource` gave the same result in the settings table below.
How to tell these cases apart is covered after that table.

Which of the three conditions is missing can be read from the following values.

| Value to read | Group of causes |
|---|---|
| The `ActualHeight` of the `ItemsControl` is larger than the visible area | The placement does not limit the height |
| The items panel is not a `VirtualizingStackPanel` (or a class derived from it) | The items panel does not support virtualization: a control that does not virtualize by default, a replaced `ItemsPanel`, or grouping with a `GroupStyle` |
| `CanContentScroll` is "-" (no `ScrollViewer` around the items panel inside the template) | The template does not support virtualization: an `ItemsControl` with only its panel replaced by a `VirtualizingStackPanel` |
| `IsVirtualizing` is `False`, or `CanContentScroll` is `False` | A setting turns off virtualization or logical scrolling |

The items panel of a `DataGrid` shows up as `DataGridRowsPresenter`, which derives from `VirtualizingStackPanel` ([DataGridRowsPresenter](https://learn.microsoft.com/dotnet/api/system.windows.controls.primitives.datagridrowspresenter)).
The deciding factor is whether the panel is a `VirtualizingStackPanel`, not the type name.
The "VirtualizingStackPanel?" column in the tables below also counts derived classes as `True`.

---

## Diagnosis Procedure

1. The list is shown with far more items than the visible range and the cache hold, and after layout completes, `VirtualizationReport.Write` from the implementation example below is called. With few items, the number of containers equals the number of items even with virtualization on (here, about 10 are in view for 1,000 items).
2. The number of realized containers and the items that are their own container (`own containers` in the output) are read. If `own containers` is not 0, containers such as `ListBoxItem` are passed as the items (see "Containers are passed as the items" under Fixes by Cause). If it is 0 and the number of containers is smaller than the number of items, virtualization works, and the diagnosis ends here.
3. If it equals the number of items, the values are compared with the table above, row by row in order.
4. If more than one row matches, all of them are fixed. After a fix, the diagnosis starts again from step 1.

Causes can overlap.
The default `ItemsControl` placed in an outer `ScrollViewer` matches all three of the height, panel, and "-" rows (see the defaults table below).
Moving it to a place that limits the height alone still leaves every container created (see the row placed directly in the 200-high `Grid` in the defaults table).

The measurements for each condition follow.
The first table changes only where the `ListBox` is placed.

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-placement.en.md %}

Measured on .NET 10 / Windows 11 (default theme) by showing a `ListBox` of 1,000 items in an area 300 wide and 200 high, changing only where it is placed. Containers were counted with `ItemContainerGenerator.ContainerFromIndex` after layout completed. "height" is the `ActualHeight` of the `ListBox`, and `CanContentScroll` is the value of the `ScrollViewer` inside the `ListBox` template.
{: .table-caption}

In a vertical `StackPanel`, in a `ScrollViewer`, and in a `Height=Auto` row of a `Grid`, the `ListBox` grew to the height of all its items and created all 1,000 containers.
In all three, the items panel, `IsVirtualizing`, and `CanContentScroll` are the same as in the rows where virtualization works.
Only the height tells this group apart.

Inside an `Expander`, the result depends on where the `Expander` itself is placed.
In a `Grid` the list was virtualized, and in a vertical `StackPanel` it created every container.

The next table keeps the placement in the `Grid` and changes the settings (the `DataGrid` row changes the control, and the last two rows change how the items are passed).

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-settings.en.md %}

Measured on .NET 10 / Windows 11 (default theme) with a `ListBox` of 1,000 items in a `Grid` 300 wide and 200 high, changing one setting at a time. The `DataGrid` row places a `DataGrid` instead of the `ListBox`. The two "grouped" rows split the items into 10 groups of 100 with `GroupDescriptions` on a `ListCollectionView`. The last two rows pass `ListBoxItem` objects instead of data, added directly to `Items` and through `ItemsSource`. The items panel is the top-level panel of the control, and `IsGrouping` is the control's `ItemsControl.IsGrouping`. "Children of the items panel" counts the children of the top-level panel, which are the groups (`GroupItem`) when grouping with a `GroupStyle`. "Items that are their own container" counts the items for which `ItemsControl.IsItemItsOwnContainer` returns `true`.
{: .table-caption}

The height stays 200 in every row.
`CanContentScroll=False` and `IsVirtualizing=False` created every container while the items panel was still a `VirtualizingStackPanel`.
Replacing `ItemsPanel` with a `StackPanel` or a `WrapPanel` also created every container.
`EnableRowVirtualization="False"` on a `DataGrid` set `IsVirtualizing` to `False` and created all 1,000 rows ([DataGrid.EnableRowVirtualization](https://learn.microsoft.com/dotnet/api/system.windows.controls.datagrid.enablerowvirtualization)).

Grouping depended on `GroupStyle`.
Without a `GroupStyle`, `IsGrouping` stayed `False` despite the `GroupDescriptions`, so grouping was not in effect.
Virtualization stayed on because the list was in the same state as an ungrouped one.
With a `GroupStyle`, `IsGrouping` became `True`, the top-level panel became a `StackPanel`, `CanContentScroll` became `False`, and every container was created.

`VirtualizingPanel.ScrollUnit=Pixel` kept virtualization on.
That `ScrollUnit=Pixel` makes scrolling per pixel is measured in [Why WPF Slows Down with Many Labels and When to Switch to TextBlock](/articles/wpf-label-vs-textblock-performance/).
Per-pixel scrolling does not need `CanContentScroll=False`.

The `CacheLength=0` and `CacheLengthUnit=Page` rows change only the cache settings.
With `CacheLength=0`, the default 11 containers became 10.
The default `ListBox` therefore created one extra container beyond the 10 in view.
With `CacheLengthUnit=Page`, there were 20, one page beyond the visible range.

The last two rows create 1,000 `ListBoxItem` objects and add them directly to `Items` or pass them through `ItemsSource`.
In both, the counted containers and the children of the items panel were 11, the same as for a `ListBox` given data.
The panel holds only the visible range and the cache.
What cannot be saved is the 1,000 `ListBoxItem` objects the application created.
The official documentation states that when containers are created and added, a `VirtualizingStackPanel` offers no performance advantage over a `StackPanel` ([VirtualizingStackPanel](https://learn.microsoft.com/dotnet/api/system.windows.controls.virtualizingstackpanel)).
In this measurement, however, the panel had 11 children, unlike the 1,000 in the row with `ItemsPanel` replaced by a `StackPanel` (given data).
Time and memory were not measured.

In this case, neither the number of containers nor the number of panel children tells it apart from a `ListBox` given data.
What does tell it apart is the number of items for which `ItemsControl.IsItemItsOwnContainer` returns `true`: 1,000 in the two rows given `ListBoxItem` objects, and 0 in every row of the settings table given data ([ItemsControl.IsItemItsOwnContainer](https://learn.microsoft.com/dotnet/api/system.windows.controls.itemscontrol.isitemitsowncontainer)).

The last table shows the defaults of each control.

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-defaults.en.md %}

Measured on .NET 10 / Windows 11 (default theme) by showing 1,000 items with each control in an area 300 wide and 200 high. The three `ComboBox` rows were counted before the drop-down opened, after it opened, and after it was opened and closed. The "height" of the `ComboBox` is that of the closed control. The maximum height of the drop-down was fixed with `MaxDropDownHeight=360`, because its default is derived from a third of the screen height, so the visible range would differ by screen ([ComboBox.MaxDropDownHeight](https://learn.microsoft.com/dotnet/api/system.windows.controls.combobox.maxdropdownheight)). A `CanContentScroll` of "-" means there is no `ScrollViewer` around the items panel inside the control's template. Before the drop-down opens, the `ComboBox` has no items panel at all, so "items panel" is also "-" and "VirtualizingStackPanel?" is `False`.
{: .table-caption}

`ListBox`, `ListView` (with `GridView`), and `DataGrid` virtualized by default.
The items panel of `ItemsControl` is a `StackPanel`, and its template has no `ScrollViewer`.
Put inside an outer `ScrollViewer`, it created every container.
The `ItemsControl` grew to the height of all its items, and the outer `ScrollViewer` only scrolled the whole stretched `ItemsControl`.
Placed directly in the 200-high `Grid`, it stayed 200 high but still created every container.
A `TreeView` with 1,000 root nodes had `IsVirtualizing` and `CanContentScroll` set to `False`, and a `StackPanel` as its items panel.
`ComboBox` created neither containers nor an items panel before its drop-down opened, created a container for every item when it opened, and kept all 1,000 after it closed.

`IsVirtualizing` is `True` in the `ItemsControl` row.
`IsVirtualizing` is an instruction to a panel that can virtualize.
While the panel stays a `StackPanel`, as in `ItemsControl`, it has no effect.
`IsVirtualizing` alone does not show that a list is virtualized.

---

## Fixes by Cause

### The placement does not limit the height

The fix is to move the `ListBox` to a place that decides its height.
For the search box example from the beginning, the `ListBox` goes into a `Height="*"` row of a `Grid`.

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
  </Grid.RowDefinitions>
  <TextBox Grid.Row="0" Text="{Binding Keyword}" />
  <ListBox Grid.Row="1" ItemsSource="{Binding Items}" DisplayMemberPath="Name" />
</Grid>
```

The row of the `ListBox` is `*`, so the height left in the `Grid` becomes the height of the `ListBox`.
A `DockPanel` with the `TextBox` docked at the top and the `ListBox` as the last child (`LastChildFill`) gave the same result (see the table of fixes below).
If the layout cannot change, setting `MaxHeight` on the `ListBox` keeps it virtualized even in a vertical `StackPanel`.
The `ListBox` then never grows taller than `MaxHeight`.

Why a `StackPanel` does not limit the height of its children, and how the same thing stops a `ScrollViewer` from scrolling, is covered in [Why a WPF ScrollViewer Does Not Scroll and How to Fix It](/articles/wpf-scrollviewer-not-scrolling/).

### The items panel or the template does not support virtualization

For `TreeView`, the fix is `VirtualizingPanel.IsVirtualizing="True"`.

```xml
<TreeView ItemsSource="{Binding Roots}"
          VirtualizingPanel.IsVirtualizing="True" />
```

With this setting alone, the items panel changed from a `StackPanel` to a `VirtualizingStackPanel`, and `CanContentScroll` became `True`, reducing the containers from 1,000 to 25 (see the table of fixes below).
The count is higher than for `ListBox` mainly because the cache is larger.
Adding `VirtualizingPanel.CacheLength="0"` brought it to 13, so about as many nodes as fit in view were created as the cache.
The measurement used root nodes only, and nodes in expanded levels below were not counted.
In a virtualized `TreeView`, nodes outside the visible range have no container.
Selecting such a node from code is covered in [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/).

`ItemsControl` needs its items panel replaced and its `ItemsPresenter` wrapped in a logically scrolling `ScrollViewer` inside its template.
The same fix applies when step 3 shows a `CanContentScroll` of "-" for an `ItemsControl`.

```xml
<ItemsControl ItemsSource="{Binding Items}">
  <ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel />
    </ItemsPanelTemplate>
  </ItemsControl.ItemsPanel>
  <ItemsControl.Template>
    <ControlTemplate TargetType="ItemsControl">
      <ScrollViewer CanContentScroll="True" Focusable="False">
        <ItemsPresenter />
      </ScrollViewer>
    </ControlTemplate>
  </ItemsControl.Template>
</ItemsControl>
```

This `ItemsControl` created 14 containers (the default `ItemsControl` in an outer `ScrollViewer` created 1,000).
With only the panel replaced by a `VirtualizingStackPanel`, the `ItemsControl` created every container both inside an outer `ScrollViewer` with `CanContentScroll=True` and directly in the 200-high `Grid` (see the table of fixes).
In both cases there is no `ScrollViewer` around the panel inside the template (`CanContentScroll` is "-").
For a large list without selection, a `ListBox` still takes less markup.

For `ComboBox`, the fix is to replace `ItemsPanel` with a `VirtualizingStackPanel`.

```xml
<ComboBox ItemsSource="{Binding Items}" DisplayMemberPath="Name">
  <ComboBox.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel />
    </ItemsPanelTemplate>
  </ComboBox.ItemsPanel>
</ComboBox>
```

With the drop-down open, the containers went from 1,000 to 19 (see the table of fixes).
This value was measured with `MaxDropDownHeight=360` and changes with the height of the drop-down.

When grouping with a `GroupStyle`, the fix is `VirtualizingPanel.IsVirtualizingWhenGrouping="True"`.
`GroupedItems` is a `ListCollectionView` with `GroupDescriptions` (or the view of a `CollectionViewSource`).

```xml
<ListBox ItemsSource="{Binding GroupedItems}"
         VirtualizingPanel.IsVirtualizingWhenGrouping="True">
  <ListBox.GroupStyle>
    <GroupStyle />
  </ListBox.GroupStyle>
</ListBox>
```

Adding a `GroupStyle` alone trades virtualization for grouping (see the settings table above).
With this setting, the containers went from 1,000 to 19 (see the table of fixes).

A `ListBox` whose `ItemsPanel` is replaced with a `StackPanel` or a `WrapPanel` trades away virtualization.
With many items, a presentation that keeps the default `ItemsPanel` is the safer choice.
Writing a panel that supports virtualization was not tested in this article.

### A setting turns off virtualization or logical scrolling

Removing `ScrollViewer.CanContentScroll="False"` or `VirtualizingPanel.IsVirtualizing="False"` brings the list back to the virtualized default of `ListBox`.
The same holds for `EnableRowVirtualization="False"` on a `DataGrid`: removing it brings back the virtualized default of `DataGrid`.
If `CanContentScroll="False"` is there for per-pixel scrolling, `VirtualizingPanel.ScrollUnit="Pixel"` is the setting to use instead.

### Containers are passed as the items

When step 2 shows an `own containers` other than 0, containers such as `ListBoxItem` are passed as the items.
This is the same whether they are added directly to `Items` or passed through `ItemsSource`.
Where the containers are created is found in the code that supplies the items.
Even with a `VirtualizingStackPanel` as the panel, the containers the application created do not go away (see the last two rows of the settings table above).
The fix is to stop creating `ListBoxItem` objects and pass a collection of data to `ItemsSource`.

### Measurements after the fixes

The next table measures the conditions with the fixes under Fixes by Cause applied.

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-fixes.en.md %}

Measured on .NET 10 / Windows 11 (default theme) by showing 1,000 items in an area 300 wide and 200 high. The `TreeView`, `ItemsControl`, `ComboBox`, and search box rows were measured by loading XAML. Of these, the `TreeView` row with `CacheLength=0`, the two `ItemsControl` rows with only the panel replaced, and the `DockPanel` search box row put the configurations described in the text into XAML, which the text does not show. The grouping and `MaxHeight` rows set the same values in code. The `ComboBox` was given `MaxDropDownHeight=360` and counted after its drop-down opened.
{: .table-caption}

---

## Implementation Example

The following is the code used to find the cause, in a form that can be added to an application.
It is called after the window is shown and layout has completed (from a button click, for example), and it writes to the debug output.

```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public static class VirtualizationReport
{
    public static void Write(ItemsControl items)
    {
        // Realized containers, counted by position.
        // Counts item containers even when the items are grouped.
        int realized = Enumerable.Range(0, items.Items.Count)
            .Count(index => items.ItemContainerGenerator.ContainerFromIndex(index) != null);

        // Items that are their own container, such as ListBoxItem objects passed as the items.
        int ownContainers = items.Items.Cast<object>().Count(items.IsItemItsOwnContainer);

        // The items of a ComboBox are inside its popup, outside the ComboBox's visual tree.
        DependencyObject root = items;
        if (items is ComboBox combo && combo.Template?.FindName("PART_Popup", combo) is Popup popup && popup.Child != null)
        {
            root = popup.Child;
        }

        var host = Descendants(root).OfType<Panel>()
            .FirstOrDefault(p => p.IsItemsHost && ItemsControl.GetItemsOwner(p) == items);

        // The ScrollViewer around the items panel. Not necessarily the attached value set on the control.
        var viewer = host == null ? null : Ancestors(host).TakeWhile(d => d != items).OfType<ScrollViewer>().FirstOrDefault();

        Debug.WriteLine(
            $"{items.Name}: {realized.ToString("#,0", CultureInfo.InvariantCulture)}/{items.Items.Count.ToString("#,0", CultureInfo.InvariantCulture)} realized, " +
            $"own containers {ownContainers.ToString("#,0", CultureInfo.InvariantCulture)}, " +
            $"panel {host?.GetType().Name ?? "-"} (VirtualizingStackPanel: {host is VirtualizingStackPanel}), " +
            $"IsVirtualizing {VirtualizingPanel.GetIsVirtualizing(items)}, " +
            $"CanContentScroll {(viewer == null ? "-" : viewer.CanContentScroll.ToString())}, " +
            $"IsGrouping {items.IsGrouping}, " +
            $"height {items.ActualHeight.ToString("#,0.##", CultureInfo.InvariantCulture)}");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject node)
    {
        for (DependencyObject current = VisualTreeHelper.GetParent(node); current != null; current = VisualTreeHelper.GetParent(current))
        {
            yield return current;
        }
    }
}
```

`host is VirtualizingStackPanel` is also `True` for derived classes, so the `DataGridRowsPresenter` of a `DataGrid` is judged correctly.
For a `TreeView`, `Items` holds only the top-level nodes, so only their containers are counted.

For a `ComboBox`, the report is useful only after the drop-down opens.
Before that, neither the items panel nor the containers exist, and the output reads "0/count" (the "before the drop-down is opened" row of the defaults table).
That is smaller than the number of items, so step 2 would wrongly conclude that virtualization works.

The call is one line.
`OrdersList` is a `ListBox` with an `x:Name`.

```csharp
VirtualizationReport.Write(OrdersList);
```

The output is read with the diagnosis procedure and the table in "What the Symptom Narrows Down".

---

## Caveats

- **It is hard to notice with few items.** When virtualization turns off, the number of containers simply equals the number of items, and no exception is raised. With small sample data during development, the problem appears only with large data. Calling `VirtualizationReport` once with many items catches it early.
- **`IsVirtualizing` being `True` does not mean the list is virtualized.** It has no effect unless the panel is a `VirtualizingStackPanel` inside a logically scrolling `ScrollViewer` in the template. The number of containers is what decides.
- **Containers passed as the items cannot be detected by the number of containers.** The panel holds only the visible range and the cache, but the application has created every container. When `own containers` in the output is not 0, the fix is to pass data instead of containers.
- **Once its drop-down opens, a `ComboBox` keeps a container for every item.** Its default items panel is a `StackPanel`. With many candidates, replacing `ItemsPanel` or switching to an input box that filters the candidates is the better choice.
- **With virtualization on, items outside the visible range have no container.** Code that relies on container state such as `IsSelected` should keep that state in the data. Selection in a `ListBox` is covered in [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/).
- **The number of containers depends on the environment.** The 10 to 25 containers in the tables depend on the visible range, decided by the item height, and on the cache, and they change with the font and display scaling. For the drop-down of a `ComboBox`, they also change with the screen height, through the default of `MaxDropDownHeight`. For `ListBox` and `TreeView`, removing the cache left 10 and 13, which confirms that the rest was the cache.

---

## Summary

When virtualization seems off, the first values to read are the number of realized containers and `own containers`, followed by the height, the panel, a `CanContentScroll` of "-", and the values of `IsVirtualizing` and `CanContentScroll`, in that order.
When more than one matches, all of them need fixing.

- **`own containers` is not 0:** containers are passed as the items. The fix is to pass data instead of containers.
- **Height larger than the visible area:** the placement is the cause. The fix is a `*` row of a `Grid` or the last child of a `DockPanel`, or `MaxHeight` if the list cannot move.
- **Panel other than `VirtualizingStackPanel` (or a derived class):** the items panel is the cause. The fixes are `IsVirtualizing="True"` for `TreeView`, a replaced panel and template for `ItemsControl`, a replaced `ItemsPanel` for `ComboBox`, and `IsVirtualizingWhenGrouping="True"` for grouping with a `GroupStyle`.
- **`CanContentScroll` is "-":** the template is the cause, with no logically scrolling `ScrollViewer`. For `ItemsControl`, the template is replaced together with the panel.
- **`IsVirtualizing` or `CanContentScroll` is `False`:** the setting should be removed, including `EnableRowVirtualization="False"` on a `DataGrid`, and `ScrollUnit="Pixel"` covers per-pixel scrolling.

A `ListBox` in a `StackPanel` shows no visible difference on screens with few items, so it tends to stay unnoticed.
For a list whose items can grow, counting its containers once when its placement is decided is the reliable check.

---

## Related Articles

- [Why a WPF ScrollViewer Does Not Scroll and How to Fix It](/articles/wpf-scrollviewer-not-scrolling/)
- [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/)
- [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/)
- [Why WPF Slows Down with Many Labels and When to Switch to TextBlock](/articles/wpf-label-vs-textblock-performance/)
