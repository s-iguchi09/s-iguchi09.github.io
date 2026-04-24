---
layout: article-en
title: "How to Implement DataGrid Sorting in WPF"
date: 2026-04-20
category: WPF
excerpt: "Learn the basics of DataGrid sorting and practical implementation patterns for real-world WPF applications."
---

## Overview

The WPF `DataGrid` control provides built-in column sorting out of the box. When `CanUserSortColumns` is set to `true` (the default), clicking a column header sorts the rows by that column. In this article we cover the essentials and explore patterns for customising sort behaviour.

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

Clicking the **Name** header once sorts ascending; clicking again reverses to descending; a third click clears the sort.

## Sorting via Code-Behind

You can trigger sorting programmatically by manipulating `DataGrid.Items.SortDescriptions`:

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

## Custom Sort Logic with ICollectionView

For complex scenarios — case-insensitive string sort, multi-level sort, or sorting computed properties — use `ICollectionView.CustomSort`:

```csharp
var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
view.CustomSort = Comparer<Product>.Create((a, b) =>
    StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
```

`CustomSort` takes precedence over `SortDescriptions`, so clear `SortDescriptions` first if you switch between the two approaches.

## Summary

| Scenario | Recommended approach |
|---|---|
| Simple column sorting | `CanUserSortColumns="True"` (default) |
| Programmatic sort | `SortDescriptions` + update `SortDirection` |
| Custom sort logic | `ICollectionView.CustomSort` |

For most line-of-business apps the default mechanism covers the common cases. Reach for `CustomSort` only when the data requires special ordering that `SortDescription` cannot express.
