---
layout: article-en
title: "How to Implement DataGrid Sorting in WPF"
date: 2026-04-20
category: WPF
excerpt: "Learn the basics of DataGrid sorting and practical implementation patterns for real-world WPF applications."
---

## Overview

The WPF `DataGrid` control provides built-in column sorting out of the box.  
When `CanUserSortColumns` is set to `true` (the default), clicking a column header sorts the rows by that column.  
Built-in sorting covers the common cases, but real applications frequently need programmatic sorting, custom comparison rules, or a way to reset the grid to its unsorted state.  
This article covers the essentials and explores patterns for each of those requirements.  

## Prerequisites / Environment

- Framework / Language: .NET 6 or later / C# 10  
- Target control: WPF `DataGrid` (`System.Windows.Controls`)  
- Architecture: applicable to both code-behind and MVVM  

The examples assume the grid is bound to an observable collection of a `Product` type that exposes `Name` and `Price` properties.  

## Enabling Default Sorting

By default, every column in a `DataGrid` is sortable as long as its `SortMemberPath` can be resolved against the bound data source:

```xml
<DataGrid ItemsSource="{Binding Products}"
          AutoGenerateColumns="False"
          CanUserSortColumns="True">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Name"  Binding="{Binding Name}"  SortMemberPath="Name" />
    <DataGridTextColumn Header="Price" Binding="{Binding Price}" SortMemberPath="Price" />
  </DataGrid.Columns>
</DataGrid>
```

Clicking the **Name** header once sorts ascending, and clicking again reverses to descending.  
The default behavior does not return to an unsorted state, so clear logic must be implemented explicitly when needed — see [How to Reset DataGrid Sorting in WPF](/articles/wpf-datagrid-sort-reset/) for that pattern.  

## Sorting via Code-Behind

Sorting can be triggered programmatically by manipulating `DataGrid.Items.SortDescriptions`:

```csharp
using System.ComponentModel;

dataGrid.Items.SortDescriptions.Clear();
dataGrid.Items.SortDescriptions.Add(
    new SortDescription(nameof(Product.Price), ListSortDirection.Descending));
dataGrid.Items.Refresh();
```

Reset the column-header sort glyph too so the UI stays in sync:

```csharp
foreach (var col in dataGrid.Columns)
    col.SortDirection = null;

var priceCol = dataGrid.Columns.First(c => c.SortMemberPath == nameof(Product.Price));
priceCol.SortDirection = ListSortDirection.Descending;
```

Without this glyph update the header arrow still points at the previous column, even though the rows are ordered correctly, which makes the sorted state look inconsistent to the user.  

## Custom Sort Logic with ListCollectionView

For complex scenarios — case-insensitive string sort, multi-level sort, or sorting computed properties — use `ListCollectionView.CustomSort`.  
`CollectionViewSource.GetDefaultView` returns an `ICollectionView`, which does not expose `CustomSort`, so cast to `ListCollectionView` first:

```csharp
var view = (ListCollectionView)CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
view.CustomSort = Comparer<Product>.Create((a, b) =>
    StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
```

`CustomSort` takes precedence over `SortDescriptions`, so clear `SortDescriptions` first when switching between the two approaches.  

## Notes

- `SortMemberPath` is required only when it differs from the column's `Binding` path. For a plain `DataGridTextColumn` bound to a simple property, sorting works without it, but specifying it explicitly avoids surprises when the binding path is complex.  
- Calling `Items.Refresh()` rebuilds the entire view and resets the current cell and selection. On large collections this is noticeable, so prefer setting `SortDescriptions` before the grid is populated when possible.  
- `CustomSort` is a property of `ListCollectionView`, the default view returned for in-memory collections. Views that wrap other sources — for example the `BindingListCollectionView` returned for a `DataView` — do not support it and throw `NotSupportedException` when it is assigned.  
- Sorting changes only the display order, not the underlying collection. Code that iterates the bound collection directly still sees the original order.  

## Summary

| Scenario              | Recommended approach                        |
| --------------------- | ------------------------------------------- |
| Simple column sorting | `CanUserSortColumns="True"` (default)       |
| Programmatic sort     | `SortDescriptions` + update `SortDirection` |
| Custom sort logic     | `ICollectionView.CustomSort`                |

For most line-of-business apps the default mechanism covers the common cases.  
Reach for `CustomSort` only when the data requires special ordering that `SortDescription` cannot express.  
