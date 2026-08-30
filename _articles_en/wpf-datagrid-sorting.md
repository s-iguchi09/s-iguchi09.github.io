---
layout: article-en
title: "How to Implement DataGrid Sorting in WPF"
date: 2026-04-20
category: WPF
excerpt: "Learn the basics of DataGrid sorting and practical implementation patterns for real-world WPF applications."
image: /images/articles/wpf-datagrid-sorting/datagrid-sort-glyph-sync.png
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

The figure below records what lands in `SortMemberPath` and `CanUserSort` for each way of declaring a column.

<figure class="article-figure">
  <img src="/images/articles/wpf-datagrid-sorting/datagrid-sortability.svg" alt="A table of SortMemberPath and CanUserSort per column declaration. A DataGridTextColumn with only a Binding takes the binding path as its SortMemberPath and is sortable. An explicit SortMemberPath takes precedence. Setting CanUserSort to False disables sorting. A template column with no binding ends up with an empty SortMemberPath and CanUserSort False." width="803" height="200" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11, varying only the column declaration. <code>order after sorting</code> is the order after sorting ascending by that column&#39;s <code>SortMemberPath</code>; a column that cannot sort keeps the original order.</figcaption>
</figure>

**`SortMemberPath` is filled in from the `Binding` path even when it is not written.** Stating it in the XAML above makes the intent explicit; omitting it produces the same result.

The last row is the one to note. A `DataGridTemplateColumn` without a `Binding` ends up with an empty `SortMemberPath`, and **`CanUserSort` becomes `False`** as well.
It stays out of sorting even with `CanUserSortColumns` set to `True`. Making a template column sortable requires stating `SortMemberPath` explicitly.

---

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

<figure class="article-figure">
  <img src="/images/articles/wpf-datagrid-sorting/datagrid-sort-glyph-sync.png" alt="Two DataGrid controls side by side. In the left one the rows are ordered by Price descending while an ascending arrow remains on the Name column. In the right one the descending arrow is on the Price column and matches the row order." width="460" height="171" loading="lazy">
  <figcaption>Both grids started from a state sorted by <code>Name</code> ascending, and then had their <code>SortDescriptions</code> replaced with <code>Price</code> descending. The left grid updated only <code>SortDescriptions</code>, so the rows follow <code>Price</code> descending while the arrow stays on the <code>Name</code> column. The right grid also updated <code>SortDirection</code>, so the arrow points at <code>Price</code> descending.</figcaption>
</figure>

## Custom Sort Logic with ListCollectionView

For comparison rules that `SortDescriptions` cannot express — such as a case-insensitive string sort or sorting by an expression not exposed as a public property — use `ListCollectionView.CustomSort`. (`SortDescriptions` can still sort by a public property whose getter computes a value; the limit is comparisons not exposed as a property.) Multi-level sorting does not need it: add several `SortDescription` entries instead.  
`CollectionViewSource.GetDefaultView` returns an `ICollectionView`, which does not expose `CustomSort`. For an in-memory collection the concrete type is `ListCollectionView`, but a view over another source (such as a `DataView`) is not, so narrow the type with a pattern match rather than an unconditional cast that could throw `InvalidCastException`:

```csharp
if (CollectionViewSource.GetDefaultView(dataGrid.ItemsSource) is ListCollectionView view)
{
    view.CustomSort = Comparer<Product>.Create((a, b) =>
        StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
}
```

`CustomSort` takes precedence over `SortDescriptions`. To switch back to `SortDescriptions`-based sorting, set `view.CustomSort = null` first — clearing `SortDescriptions` alone leaves `CustomSort` in effect — and then configure `SortDescriptions`.  

## Notes

- `SortMemberPath` is required only when it differs from the column's `Binding` path. For a plain `DataGridTextColumn` bound to a simple property, sorting works without it, but specifying it explicitly avoids surprises when the binding path is complex.  
- Calling `Items.Refresh()` rebuilds the entire view and resets the current cell and selection. On large collections this is noticeable, so prefer setting `SortDescriptions` before the grid is populated when possible.  
- `CustomSort` is a property of `ListCollectionView`, the default view returned for in-memory collections. A view over another source — such as the `BindingListCollectionView` returned for a `DataView` — is not a `ListCollectionView` and offers no `CustomSort`, which is why the pattern match above skips it. For those sources the ordering must come from another mechanism, such as `SortDescriptions` or sorting at the data source.  
- Sorting changes only the display order, not the underlying collection. Code that iterates the bound collection directly still sees the original order.  

## Summary

| Scenario              | Recommended approach                        |
| --------------------- | ------------------------------------------- |
| Simple column sorting | `CanUserSortColumns="True"` (default)       |
| Programmatic sort     | `SortDescriptions` + update `SortDirection` |
| Custom sort logic     | `ListCollectionView.CustomSort`             |

For most line-of-business apps the default mechanism covers the common cases.  
Reach for `CustomSort` only when the data requires special ordering that `SortDescription` cannot express.  
