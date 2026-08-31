---
layout: article-en
title: "How to Reset DataGrid Sorting in WPF"
date: 2026-06-29
category: WPF
excerpt: "Practical ways to reset WPF DataGrid sorting, including explicit clearing, Sorting-event control, CollectionView handling, and a reusable Behavior."
image: /images/articles/wpf-datagrid-sort-reset/datagrid-sort-three-states.png
---

WPF `DataGrid` provides built-in sorting, but some business workflows require an explicit operation to restore the initial unsorted state.  
This article organizes practical reset strategies for single-column and multi-column sorting scenarios.

## Overview

This article covers the following approaches for resetting WPF `DataGrid` sorting.  

- Explicitly clearing sorting in code.
- Controlling unsorted state through the `Sorting` event.
- Resetting sorting from ViewModel with `ICollectionView`.
- Encapsulating tri-state logic into a reusable Behavior.

## Prerequisites / Environment

- Framework: WPF `DataGrid`.
- Target versions: .NET Framework 4.8 / .NET 6 or later.
- Language: C# 9 or later.
- Architecture: MVVM or code-behind.
- Scope: single-column and multi-column sorting requirements.

## Problem

In WPF `DataGrid`, production requirements often include a command that resets current sorting back to the initial state.  
Default user interaction alone may not provide a consistent reset timing across screens.

## Cause / Background

In WPF `DataGrid`, `Shift + column-header click` is a default operation for adding multi-column sorting.  
It is not a built-in shortcut to clear sorting.

- Regular click: toggles ascending and descending on the selected column.
- Shift+Click: appends the selected column to the existing sort criteria.

For this reason, explicit reset logic is required when an application needs deterministic unsorted behavior.

The sort state lives in two places. The figure below records both after each operation.

<figure class="article-figure">
  <img src="/images/articles/wpf-datagrid-sort-reset/datagrid-sort-state.svg" alt="A table of the SortDescriptions count, the column SortDirection, and the row order after each operation. Adding a SortDescription from code leaves SortDirection null. Clearing SortDescriptions leaves SortDirection at Ascending. Only clearing both returns to the initial state. The last row, a column header click, updates both at once." width="827" height="290" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11. <code>SortDescriptions</code> is the number of sort conditions on the view; <code>column.SortDirection</code> is the property that drives the arrow in the column header. Every row but the last operates on them directly from code.</figcaption>
</figure>

**On the row that calls `SortDescriptions.Clear()`, the order is back to its initial state while `column.SortDirection` still reads `Ascending`.**
The header keeps showing its arrow in that state, making it look as though the sort is still applied.

The reverse holds on the row that only adds a `SortDescription`: the order changes while `SortDirection` stays `null`.
**Touching one of them from code never updates the other.** Both have to be set explicitly, in either direction.

The row adding two `SortDescriptions` produces a multi-column sort. Shift-clicking builds exactly that state; it does not clear anything.

The last row is the contrast: the standard sort that a click on the column header runs. **That path updates `SortDescriptions` and `SortDirection` together.**
This is why the arrow and the order never disagree when the user sorts; they diverge only once code touches one side alone.

---

## Four Ways to Reset

The reset strategy should be selected by architecture and UX requirements.  

- Clear both sort descriptors and header indicators explicitly.
- Use `Sorting` event logic when tri-state transition is required.
- Manage sorting in `ICollectionView` for MVVM-oriented implementations.
- Use a Behavior when the same rule must be shared across multiple screens.

### Explicitly clear sorting in code

The most direct implementation clears both data-level sort descriptors and visual sort arrows.

```csharp
using System.Windows.Controls;

public static class DataGridSortHelper
{
    public static void ClearDataGridSort(DataGrid dataGrid)
    {
        if (dataGrid == null) return;

        // Clear data-level sort descriptors.
        dataGrid.Items.SortDescriptions.Clear();

        // Clear header arrows.
        foreach (var column in dataGrid.Columns)
        {
            column.SortDirection = null;
        }

        // Refresh the view.
        dataGrid.Items.Refresh();
    }
}
```

This approach is effective for full reset commands and keeps UI indicators synchronized with actual sort state.

### Auto-reset on third click with custom sorting behavior

To implement `Ascending -> Descending -> Unsorted`, intercept the `Sorting` event at the transition after descending.  
The three states appear as follows.

<figure class="article-figure">
  <img src="/images/articles/wpf-datagrid-sort-reset/datagrid-sort-three-states.png" alt="Three DataGrid controls side by side. The left one shows an ascending arrow on the Name column with rows in name order, the middle one a descending arrow with the reverse order, and the right one has no arrow and shows the original data order." width="650" height="171" loading="lazy">
  <figcaption>The three states rendered over the same data. In the unsorted state on the right, both the column's <code>SortDescription</code> and the header arrow are removed. Because this example sorts on a single column, clearing it returns the rows to the order of the underlying collection; when several columns are sorted, the remaining descriptors still apply and the original order is not necessarily restored.</figcaption>
</figure>

#### XAML

```xml
<DataGrid x:Name="MyDataGrid"
          Sorting="DataGrid_Sorting" />
```

This event hook allows custom handling before the default sorting pipeline completes.
When columns such as `DataGridTemplateColumn` are used, `SortMemberPath` should be set explicitly for columns that need sort-reset handling.

#### C\#

```csharp
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
{
    if (sender is not DataGrid dataGrid) return;

    if (e.Column.SortDirection == ListSortDirection.Descending)
    {
        e.Handled = true; // Cancel default sorting behavior.

        // Remove only the sort descriptor for the target column.
        var target = dataGrid.Items.SortDescriptions
            .FirstOrDefault(sd => sd.PropertyName == e.Column.SortMemberPath);

        if (!string.IsNullOrEmpty(target.PropertyName))
        {
            dataGrid.Items.SortDescriptions.Remove(target);
        }

        e.Column.SortDirection = null;
        dataGrid.Items.Refresh();
    }
}
```

This logic removes only the clicked column from sorting, so existing sort conditions on other columns can remain intact.

### Reset from ViewModel using `ICollectionView`

In MVVM, sorting should generally be controlled in the view model layer instead of manipulating `DataGrid` directly.

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

public class SampleViewModel
{
    public ObservableCollection<RowItem> Items { get; } = new();
    public ICollectionView ItemsView { get; }

    public SampleViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
    }

    public void ClearSort()
    {
        ItemsView.SortDescriptions.Clear();
        ItemsView.Refresh();
    }
}

public class RowItem
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}
```

Managing `SortDescriptions` in `ItemsView` improves testability and keeps view logic thin.

```xml
<DataGrid ItemsSource="{Binding ItemsView}" />
```

With this binding, sorting reset responsibility remains in ViewModel commands.

### Share it as a Behavior

When the same tri-state rule is required in multiple screens, encapsulate the logic as a Behavior (`Microsoft.Xaml.Behaviors.Wpf`).

```csharp
using Microsoft.Xaml.Behaviors;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

public class TriStateSortBehavior : Behavior<DataGrid>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Sorting += OnSorting;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Sorting -= OnSorting;
        base.OnDetaching();
    }

    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        if (e.Column.SortDirection == ListSortDirection.Descending)
        {
            e.Handled = true;

            var sd = grid.Items.SortDescriptions
                .FirstOrDefault(x => x.PropertyName == e.Column.SortMemberPath);

            if (!string.IsNullOrEmpty(sd.PropertyName))
            {
                grid.Items.SortDescriptions.Remove(sd);
            }

            e.Column.SortDirection = null;
            grid.Items.Refresh();
        }
    }
}
```

This design reduces duplicated event handlers and centralizes behavior-level customization.

```xml
<Window
    xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
    xmlns:local="clr-namespace:YourApp.Behaviors">
    <DataGrid>
        <i:Interaction.Behaviors>
            <local:TriStateSortBehavior />
        </i:Interaction.Behaviors>
    </DataGrid>
</Window>
```

XAML usage remains compact, which helps apply identical sorting rules consistently across screens.

## How to Choose

Which approach applies is settled by what triggers the reset and by how many screens share the behavior.

**Resetting everything at once from a button or menu calls for the explicit clear.**
Placing `SortDescriptions.Clear()` next to `SortDirection = null` is all it takes, which makes it the fastest to adopt. It does reference the `DataGrid` from code-behind, so MVVM separation loosens.

**Keeping the whole interaction on the header calls for tri-state handling in the `Sorting` event.**
Ascending, descending and unsorted all live in one gesture. Decide up front whether a reset clears one column or all of them, or the event handling grows complicated.

**Strict MVVM with a command-driven reset calls for `ICollectionView`.**
Sort state lives on the ViewModel side, which makes it testable without the UI. It assumes the View and ViewModel responsibilities are already separated.

**Applying the same rule across several `DataGrid`s calls for a Behavior.**
One line of XAML per screen carries it across, cutting duplicated code. Provide extension points for the parts that differ per screen.

---

## Comparing the Approaches

| Approach | Pros | Cons | Best suited for |
|---|---|---|---|
| Explicit clear (`SortDescriptions.Clear` + `SortDirection = null`) | Simple to implement and quick to adopt. | Requires a `DataGrid` reference, loosening MVVM purity. | Clearing everything at once per screen. |
| Tri-state handling in the `Sorting` event | Unifies the UX into ascending, descending and unsorted. | Event handling grows complex; per-column requirements need sorting out. | Prioritizing an interaction that lives entirely on the header. |
| `ICollectionView` managed by the ViewModel | Testable, with minimal UI dependency. | Presumes a design that separates View and ViewModel responsibilities. | Strict MVVM with a command-driven reset. |
| Behavior | Carries across screens easily and cuts duplication. | Needs extension points for per-screen differences. | Applying one sorting rule to several `DataGrid`s. |

---

## Notes

- Clearing `SortDescriptions` alone can leave header arrows out of sync with actual data order.
- In the `Sorting` event example, if reset logic uses `SortMemberPath` as the key, columns without `SortMemberPath` can fail to reset as expected.
- For multi-column sorting, requirements should define whether reset means full clear or only target-column clear.

---

## Summary

Sort state in a WPF `DataGrid` lives in two places — `ICollectionView.SortDescriptions` and `DataGridColumn.SortDirection` — and touching only one of them from code leaves them disagreeing. A reset has to restore both.

The deciding factors are what triggers the reset and how many screens share the behavior.
Use the explicit clear for a button-driven reset, the `Sorting` event to keep it on the header, `ICollectionView` for strict MVVM, and a Behavior to carry one rule across screens.

## Related Articles

- [Implementing Column Sorting in WPF DataGrid](/articles/wpf-datagrid-sorting/)
- [Using DataGridTemplateColumn for Display and Edit Templates in WPF](/articles/wpf-datagrid-cell-editing-template/)
