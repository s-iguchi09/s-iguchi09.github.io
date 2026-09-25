---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/datagrid.html
title: "DataGrid"
badge: "List"
lead: "DataGrid shows a collection as a table of rows and columns, with built-in column sorting, in-place editing, and adding and deleting rows."
description: "WPF DataGrid control reference: columns, sorting, editing, adding rows, validation, virtualization, and frozen columns, measured on .NET 10 with the demo app."
---

## Overview

**DataGrid** derives from `MultiSelector`, `Selector`, and `ItemsControl`. With the default `AutoGenerateColumns="True"`, it creates one column per public property, in the order the properties are declared: a class with `Zeta`, `Alpha`, and `Mid` gave the columns in that order, not alphabetically. A property without a setter became a read-only column. Columns can also be declared as `DataGridTextColumn`, `DataGridCheckBoxColumn`, `DataGridComboBoxColumn`, and so on. In edit mode their cells hold a `TextBox`, a `CheckBox`, and a `ComboBox`.

Sorting is built in: clicking a column header sorted ascending, then descending, then ascending again. A third click does not remove the sort. [How to Reset DataGrid Sorting in WPF](/articles/wpf-datagrid-sort-reset/) shows how to clear it from code. Grouping comes from the collection view, and the DataGrid shows groups only when it has a `GroupStyle`. Without one, a grouped view produced no group headers.

The demo app has a section for each of its twenty property groups, some of which are listed below. The "Show Code" link under each section displays its XAML.

## Screen Preview

![datagrid demo screen](/images/wpf-standard-control-demo/datagrid.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `AutoGenerateColumns` | `bool` | Creates a column for every public property, in declaration order; the default is `True`. The demo app's items implement `IDataErrorInfo`, so the generated columns were `Name`, `Value`, and a read-only `Error` column taken from the interface. Set it to `False` and declare columns when some properties should not be shown. |
| `IsReadOnly` | `bool` | Turns off editing for the whole grid. With `IsReadOnly="True"`, `CanUserAddRows` became `False`. A column cannot opt back in: a column with `IsReadOnly="False"` set explicitly still reported `False`, but `BeginEdit()` on its cell returned `False` and no editing started. |
| `SelectionMode / SelectionUnit` | `Single / Extended`, `Cell / FullRow / CellOrRowHeader` | How many items can be selected and whether a row or a cell is the unit of selection. The defaults are `Extended` and `FullRow`. |
| `ColumnWidth / MaxColumnWidth / MinColumnWidth` | `DataGridLength / double / double` | The default width of columns that do not set their own, and its limits. The defaults were `SizeToHeader`, `Infinity`, and 20. |
| `CanUserAddRows / CanUserDeleteRows` | `bool` | Whether the user can add a row through the empty row at the bottom and delete selected rows with the Delete key. Both default to `True`. The new-item row depends on the source: it appeared for a `List<T>` and an `ObservableCollection<T>`. It did not appear for an array or for a type without a parameterless constructor. In those cases `CanUserAddRows` became `False`, without an exception. With the second of three rows selected, the Delete key removed it from the source collection. |
| `CanUserReorderColumns / CanUserResizeColumns / CanUserResizeRows / CanUserSortColumns` | `bool` | Switches for what the user may do with columns and rows. All four default to `True`. |
| `RowDetailsVisibilityMode / AreRowDetailsFrozen` | `Collapsed / Visible / VisibleWhenSelected`, `bool` | When the `RowDetailsTemplate` is shown under a row, and whether it stays in place during horizontal scrolling. The defaults are `VisibleWhenSelected` and `False`. |
| `RowValidationErrorTemplate` | `ControlTemplate` | What a row shows when its validation fails. A row only fails validation when a rule checks it, and `RowValidationRules` is empty by default. With the demo app's data, whose third row returns an `IDataErrorInfo.Error`, that row never had `Validation.HasError` set, even after it was edited. So the template never appears. After adding a `DataErrorValidationRule` to `RowValidationRules`, the third row had a validation error. |
| `GridLinesVisibility / HorizontalGridLinesBrush / VerticalGridLinesBrush` | `None / Horizontal / Vertical / All`, `Brush` | Which grid lines are drawn and their brushes; the default `GridLinesVisibility` is `All`. |
| `AlternatingRowBackground` | `Brush` | The background of every other row. Setting only this property was enough: `AlternationCount` became 2, and the four rows alternated between white and the alternating brush. |
| `FrozenColumnCount` | `int` | The number of leftmost columns that stay in place during horizontal scrolling; the default is 0. With four 100-wide columns in a DataGrid 200 wide, scrolling 60 to the right moved the first column from x = 7 to -53. With `FrozenColumnCount="1"`, it stayed at 7 while the second column moved. The demo app's slider goes up to 3 for two columns; a value of 3 with two columns raised no exception and was reduced to 2. |
| `HeadersVisibility` | `None / Column / Row / All` | Which headers are shown; the default is `All`. |
| `EnableRowVirtualization / EnableColumnVirtualization` | `bool` | Whether rows and columns outside the view are created. The defaults are `True` for rows and `False` for columns. With 1,000 rows, 11 `DataGridRow`s existed. By default, grouping turns row virtualization off, because `VirtualizingPanel.IsVirtualizingWhenGrouping` is `False`: grouped with a `GroupStyle`, all 1,000 rows were created. With it set to `True`, 18 rows were created. |

## XAML Example

The following XAML is the `FrozenColumnCount` section of the demo app (`DataGridUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `Items` holds items with `Name` and `Value`:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Slider x:Name="PropertyFrozenColumnCount"
          IsSnapToTickEnabled="True" Maximum="3" Minimum="0" />

  <DataGrid x:Name="ResultFrozen" Height="150"
            AutoGenerateColumns="False"
            FrozenColumnCount="{Binding Value, ElementName=PropertyFrozenColumnCount}"
            ItemsSource="{Binding Items}">
    <DataGrid.Columns>
      <DataGridTextColumn Width="150" Binding="{Binding Name}" Header="Frozen" />
      <DataGridTextColumn Width="600" Binding="{Binding Value}" Header="Scrollable" />
    </DataGrid.Columns>
  </DataGrid>
</StackPanel>
```

## Editing

Keyboard editing was measured on a focused `Name` cell:

- F2 started editing, with a `TextBox` in the cell.
- Typing "Changed" and pressing Esc ended editing and left the source unchanged.
- Pressing F2 again, typing "Committed", and pressing Enter wrote "Committed" to the source.

While a cell was being edited, adding a `SortDescription` to the items threw `InvalidOperationException`. `CommitEdit()` and `CancelEdit()` without arguments did not prevent it: sorting still threw after either. After `CommitEdit(DataGridEditingUnit.Row, true)` or `CancelEdit(DataGridEditingUnit.Row)`, sorting worked. End the row edit this way before sorting, filtering, or grouping from code.

## Tips and Best Practices

- **Declare columns when the item type has members that should not be shown.** Automatic columns include every public property, such as `IDataErrorInfo.Error`.
- **Add a validation rule if rows should show errors.** `RowValidationErrorTemplate` has no effect while `RowValidationRules` is empty.
- **Check the new-item row.** If users cannot add rows, check the source: an array or an item type without a parameterless constructor quietly turns `CanUserAddRows` off.
- **Set `VirtualizingPanel.IsVirtualizingWhenGrouping="True"` on grouped grids.** By default, grouping creates every row.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`DataGridDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/DataGridDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Header clicks were reproduced by calling the column header's click handling (`OnClick`), and keys by sending key events to a displayed window. Positions are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/datagrid/datagrid-columns-rows.svg" alt="Table of DataGrid column and row results: automatic columns follow declaration order and include the demo data's Error column, a read-only grid blocks editing even for a column set to IsReadOnly False, the new-item row appears for List and ObservableCollection but not for an array or a type without a parameterless constructor, row validation needs a DataErrorValidationRule, and the Delete key removes the selected row" width="1218" height="440" loading="lazy">
  <figcaption>Columns, read-only settings, validation, and adding and deleting rows. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/datagrid/datagrid-behavior.svg" alt="Table of DataGrid behavior: F2, Esc, and Enter start, cancel, and commit editing, each column type uses its own editor, three header clicks give Ascending, Descending, Ascending, sorting during an edit throws InvalidOperationException even after CommitEdit() or CancelEdit() but not after ending the row edit, AlternatingRowBackground alone sets AlternationCount to 2, grouping with a GroupStyle creates all 1,000 rows by default and 18 with IsVirtualizingWhenGrouping, and a frozen first column keeps its position while scrolling" width="1203" height="590" loading="lazy">
  <figcaption>Editing, sorting, alternating rows, virtualization, and frozen columns. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [How to Implement DataGrid Sorting in WPF](/articles/wpf-datagrid-sorting/) — which columns can be sorted and how.
- [How to Reset DataGrid Sorting in WPF](/articles/wpf-datagrid-sort-reset/) — clearing a sort from code.
- [Switching Controls Between Display and Edit Modes in WPF DataGrid Cells](/articles/wpf-datagrid-cell-editing-template/) — cell templates for display and editing.
- [ListView](/apps/wpf-standard-control-demo/listview.html) — columns through `GridView`, without editing.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View DataGrid source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/DataGridUsage){: target="_blank" rel="noopener noreferrer"}
