---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/listview.html
title: "ListView"
badge: "List"
lead: "ListView is a ListBox that can show its items through a view. With a GridView, the items become rows with column headers."
description: "WPF ListView measured on .NET 10: Extended selection by default, auto-width columns that do not grow, column reordering and header clicks, and scrolling."
---

## What differs from ListBox: Extended selection by default

**ListView** derives from `ListBox`, and its items are wrapped in `ListViewItem` containers. Its `View` is `null` by default, which shows a plain list without column headers.

One difference from ListBox is the selection: the default `SelectionMode` of a ListView is `Extended`, while a ListBox's is `Single`. Set `SelectionMode="Single"` if only one item may be selected. How the selection modes, `SelectedIndex` (including invalid indexes), and `IsSelected` behave is measured on the ListBox page.

## GridView columns: width, order, and sorting

With a `GridView` as the `View`, the items become rows with column headers. Each column in `Columns` shows a value with `DisplayMemberBinding` or a `CellTemplate`. Setting both threw no exception, and `DisplayMemberBinding` was used: the first row showed `AliceBlue`, not the template's text.

A column `Width` of `Auto` (`NaN`) sized the Name column to the rows shown at the start, 84.89 wide, and it did not grow later: scrolling `LightGoldenrodYellow`, the longest name, into view and inserting a longer name at the top both left it at 84.89. Give columns a fixed width when the longest value is not in the first rows. A bound width follows its source: with the Value column's width bound to a slider, moving the slider to 300 made the column 300 wide.

`AllowsColumnReorder` decides whether the user can drag a header to move its column; the default is `True`. A real drag of the Name header past the Value header changed the order to Value, Name with `True` and left Name, Value with `False`. The order is that of the `Columns` collection itself.

A GridView does not sort. A real click on the Name header left the first item as `AliceBlue`, and no sort description was added. Sort in code: handle the header click and add a `SortDescription` to the items' view.

## Scrolling, and what Disabled cuts off

A GridView keeps rows virtualized: with 1,000 items in a ListView 150 high, 8 item containers were created.

In a list 300 wide and 100 high with one column 500 wide, `Disabled` scroll bars left no horizontal scrolling, so the right part of the column could not be reached, but the rows still scrolled vertically: `ScrollToBottom` and the Down key to the last row both moved the vertical offset to 2 rows (the horizontal offset stayed at 0). With `Auto`, the offsets went to 227 DIPs horizontally and 3 rows vertically. Leave the horizontal bar on `Auto` when columns are wide.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listview/listview-behavior.svg" alt="Table of ListView results: it derives from ListBox with Extended selection and no view by default, an auto-width column stays 84.89 wide after the longest name is scrolled in or a longer one is inserted, a bound width follows the slider, a real header drag reorders the columns only with AllowsColumnReorder, a header click does not sort, DisplayMemberBinding wins over CellTemplate without an exception, Disabled scroll bars leave no horizontal scrolling but still 2 rows vertically, and 1,000 items create 8 containers" width="1132" height="470" loading="lazy">
  <figcaption>Defaults, GridView columns, reordering, sorting, and scrolling. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The ListView page of the demo app, with the control list on the left and the first section, SelectionMode](/images/wpf-standard-control-demo/listview.png){: .screenshot-img}

The ListView page of the demo app has sections for `SelectionMode`, `View` with the columns and `AllowsColumnReorder`, a column's `Width`, `SelectedIndex` and `SelectedItem`, `IsSelected`, and the scroll bars. The `SelectionMode` combo box starts at `Single`, its first value, and `SelectedItems.Count` is shown under the list. The `View` section sets a `GridView` with a Name column and a Value column over the list of brushes, with `AllowsColumnReorder` checked; the `Width` section binds the Value column's width to a slider starting at 150; the `SelectedIndex` section starts at `SelectedIndex="0"` (Item A) and binds a text box to the index two-way; the `IsSelected` section binds the first item's `IsSelected` to a check box; and the scroll bar section has one column 500 wide, with its combo boxes starting at `Disabled`. The "Show Code" link under each section displays its XAML. The following XAML is the `View` section (`ListViewUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <CheckBox x:Name="AllowsColumnReorderCheckBox" Content="AllowsColumnReorder" IsChecked="True" />

  <ListView x:Name="ViewColumnsListView"
            Height="150"
            ItemsSource="{markupextensions:StaticBindingSource TargetType=Brushes}">
    <ListView.View>
      <GridView AllowsColumnReorder="{Binding IsChecked, ElementName=AllowsColumnReorderCheckBox}">
        <GridViewColumn Width="auto" DisplayMemberBinding="{Binding Name}" Header="Name" />
        <GridViewColumn Width="auto" DisplayMemberBinding="{Binding Value}" Header="Value" />
      </GridView>
    </ListView.View>
  </ListView>
</StackPanel>
```

## Related controls and articles

- [ListBox](/apps/wpf-standard-control-demo/listbox.html) — ListView's base class, with the selection measured in detail.
- [DataGrid](/apps/wpf-standard-control-demo/datagrid.html) — a table with editing and sorting.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ListViewDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ListViewDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with the demo app's list of brushes (`Name` and `Value` of each). Headers were dragged and clicked with the real mouse. Widths are the values on the measuring machine.

[View ListView source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ListViewUsage){: target="_blank" rel="noopener noreferrer"}
