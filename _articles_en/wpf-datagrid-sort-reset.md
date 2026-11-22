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
- Verification environment: .NET 10 / Windows 11

The tables in this article were measured in the environment above.
Each caption states how its table was measured.
The following points were confirmed in that environment:

- The sort state is held in two places: `SortDescriptions` on the `ICollectionView` and `SortDirection` on the column.
- Clearing `SortDescriptions` alone leaves `SortDirection` on the column, so the header arrow stays.
- Adding to `SortDescriptions` alone does not set `SortDirection` on the column.
- Adding two entries to `SortDescriptions` produces a multi-column sort: rows that tie on the first condition are ordered by the second.
- During a multi-column sort, clicking a descending column again without Shift removes the other columns' arrows.
  If the `Sorting` handler then removes only the clicked column's descriptor, the other descriptors remain without an arrow.

## Problem

In WPF `DataGrid`, production requirements often include a command that resets current sorting back to the initial state.  
Default user interaction alone may not provide a consistent reset timing across screens.

## Cause / Background

In WPF `DataGrid`, `Shift + column-header click` is a default operation for adding multi-column sorting.  
It is not a built-in shortcut to clear sorting.

- Regular click: toggles ascending and descending on the selected column.
- Shift+Click: appends the selected column to the existing sort criteria.

For this reason, explicit reset logic is required when an application needs deterministic unsorted behavior.

The sort state lives in two places. The table below records both after each operation.

{% include tables/articles/wpf-datagrid-sort-reset/datagrid-sort-state.en.md %}

Measured on .NET 10 / Windows 11. <code>SortDescriptions</code> is the number of sort conditions on the view; <code>column.SortDirection</code> is the property that drives the arrow in the column header. Every row but the last operates on them directly from code. The two rows marked <code>with a tie</code> add a row, anna, with the same <code>Score</code> as carol.
{: .table-caption}

**On the row that calls `SortDescriptions.Clear()`, the order is back to its initial state while `column.SortDirection` still reads `Ascending`.**
The header keeps showing its arrow in that state, making it look as though the sort is still applied.

The reverse holds on the row that only adds a `SortDescription`: the order changes while `SortDirection` stays `null`.
**Touching one of them from code never updates the other.** Both have to be set explicitly, in either direction.

The rows marked `with a tie` show the multi-column sort: with only `Score`, carol and anna, who tie, keep their original order; adding `Name` puts anna first. Shift-clicking builds exactly that state; it does not clear anything.

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
  <figcaption>The three states rendered over the same data. In the unsorted state on the right, both the column's <code>SortDescription</code> and the header arrow are removed. Because this example sorts on a single column, clearing it returns the rows to the order of the underlying collection; when several columns are sorted and one is cleared with Shift held, the remaining descriptors still apply and the original order is not necessarily restored.</figcaption>
</figure>

#### XAML

```xml
<DataGrid x:Name="MyDataGrid"
          Sorting="DataGrid_Sorting" />
```

This event hook allows custom handling before the default sorting pipeline completes.
When columns such as `DataGridTemplateColumn` are used, `SortMemberPath` should be set explicitly for columns that need sort-reset handling.

#### C\#

The reset logic goes into a static class so that the Behavior later in this article can reuse it.

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

public static class TriStateSort
{
    // Call this from the DataGrid Sorting event. It replaces the default processing only when a descending column is clicked again, and resets the sort instead.
    public static void OnSorting(DataGrid dataGrid, DataGridSortingEventArgs e)
    {
        if (e.Column.SortDirection != ListSortDirection.Descending) return;

        e.Handled = true; // Cancel default sorting behavior.

        SortDescriptionCollection sorts = dataGrid.Items.SortDescriptions;
        int groupingCount = CountGroupingSorts(dataGrid.Items);

        using (dataGrid.Items.DeferRefresh()) // Re-sort once after all removals.
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                // Shift+click: remove only the clicked column's descriptor. Other descriptors and arrows stay.
                for (int i = groupingCount; i < sorts.Count; i++)
                {
                    if (sorts[i].PropertyName == e.Column.SortMemberPath)
                    {
                        sorts.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                // Plain click: DataGrid has already cleared the other columns' arrows before this event.
                // Remove every descriptor except the grouping ones so that none is left without an arrow.
                while (sorts.Count > groupingCount)
                {
                    sorts.RemoveAt(sorts.Count - 1);
                }
            }
        }

        e.Column.SortDirection = null;
    }

    // When grouped, DataGrid inserts ascending descriptors for grouping at the start. Count them.
    // Reordering GroupDescriptions later does not reinsert them, so match by property name, not by position.
    private static int CountGroupingSorts(ItemCollection items)
    {
        var groupProperties = new List<string>();
        foreach (GroupDescription group in items.GroupDescriptions)
        {
            if (group is PropertyGroupDescription property
                && property.Converter == null
                && property.StringComparison == StringComparison.Ordinal)
            {
                groupProperties.Add(property.PropertyName);
            }
        }

        int count = 0;
        while (count < items.SortDescriptions.Count
            && items.SortDescriptions[count].Direction == ListSortDirection.Ascending
            && groupProperties.Remove(items.SortDescriptions[count].PropertyName))
        {
            count++;
        }

        return count;
    }
}
```

The code-behind handler only calls it.

```csharp
private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
{
    if (sender is DataGrid dataGrid) TriStateSort.OnSorting(dataGrid, e);
}
```

This handler cancels the default processing only when a descending column is clicked again.
How much it clears depends on whether Shift is held.

On a click without Shift, `DataGrid` sets `SortDirection` of every other column to `null` before it raises `Sorting` ([PerformSort](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7084-L7099) and [PrepareForSort](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7119-L7136) in DataGrid.cs).
Removing only the clicked column's descriptor at that point would leave the other descriptors in effect with no arrow.
A plain click therefore removes all column descriptors.
A Shift+click keeps the other arrows, so only the clicked column's descriptor is removed.

When the view is grouped and a column header is clicked, `DataGrid` inserts an ascending descriptor at the start of `SortDescriptions` for each `PropertyGroupDescription` that has no `Converter` and uses `Ordinal` comparison ([AddGroupingSortDescriptions](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7334-L7361)).
Sorting a column on the same property as the grouping produces two descriptors with the same `PropertyName`.
`CountGroupingSorts` counts the grouping descriptors at the start, matching them by property name, and excludes them from the reset.

The following table compares this handler with one that removes the first descriptor whose `PropertyName` matches the column (`FirstOrDefault`).

{% include tables/articles/wpf-datagrid-sort-reset/datagrid-tristate-sort.en.md %}

Measured on .NET 10 / Windows 11 by clicking the column headers with real mouse input.
Clicks were spaced longer than the double-click time, and Shift was pressed as real keyboard input.
"Column arrows" lists the columns whose <code>SortDirection</code> is set.
{: .table-caption}

**With the first-match handler, a plain click leaves the `Name` descriptor in place, so the rows stay in name order while no column shows an arrow.**
When grouped, the descriptor that gets removed is the grouping one, and the descending order remains.
With `TriStateSort`, apart from the grouping descriptor, the descriptors and the arrows agree in both cases.
A Shift+click keeps the `Name` descriptor and its arrow with either handler.

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

**This `ClearSort` releases the view order only.** `DataGridColumn.SortDirection` stays on the `DataGrid` side, so the rows return to their initial order while the header arrow remains displayed (the `ItemsSource = ICollectionView` row of the table above measures exactly that: clearing the view's `SortDescriptions` left `SortDirection` at `Ascending`).
This `ClearSort` also clears only `SortDescriptions`, so it does not undo sorting through `ListCollectionView.CustomSort`. Running the same steps on a view with `CustomSort` set left `CustomSort` in place and the order unchanged. On a screen that uses `CustomSort`, have `ClearSort` also assign `null` to `CustomSort`.

{% include tables/articles/wpf-datagrid-sort-reset/datagrid-sort-customsort.en.md %}

Measured on .NET 10 / Windows 11 by applying each step in turn to a <code>ListCollectionView</code> over the sample data. The <code>SortDescriptions</code> column is the number of conditions.
{: .table-caption}

Clearing the arrow as well needs a separate step that sets `SortDirection` to `null` on the `DataGrid`, since the ViewModel cannot reach the column state. Folding that step into a Behavior lets a single command reset both.

```xml
<DataGrid ItemsSource="{Binding ItemsView}" />
```

With this binding, sorting reset responsibility remains in ViewModel commands.

### Share it as a Behavior

When the same tri-state rule is required in multiple screens, encapsulate the logic as a Behavior (`Microsoft.Xaml.Behaviors.Wpf`).

```csharp
using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;

namespace YourApp.Behaviors
{
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

        // Delegate the reset to TriStateSort from the third-click section.
        private void OnSorting(object sender, DataGridSortingEventArgs e) =>
            TriStateSort.OnSorting(AssociatedObject, e);
    }
}
```

This design reduces duplicated event handlers and centralizes behavior-level customization.

```xml
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
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
Ascending, descending and unsorted all live in one gesture. `TriStateSort` in this article uses Shift to choose between clearing one column and clearing all of them; change that branch if your requirement differs.

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

- Clearing `SortDescriptions` alone leaves header arrows out of sync with actual data order.
- A column with an empty `SortMemberPath` is not sorted by the default sorting and gets no arrow ([DefaultSort](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7145-L7209)).
  It therefore never reaches the reset either.
  Set `SortMemberPath` on a `DataGridTemplateColumn`.
- `TriStateSort` in this article removes all column descriptors on a plain click and only the clicked column's descriptor on a Shift+click.
  If your requirement differs, change that branch.

---

## Summary

Sort state in a WPF `DataGrid` lives in two places — `ICollectionView.SortDescriptions` and `DataGridColumn.SortDirection` — and touching only one of them from code leaves them disagreeing. A reset has to restore both.

The deciding factors are what triggers the reset and how many screens share the behavior.
Use the explicit clear for a button-driven reset, the `Sorting` event to keep it on the header, `ICollectionView` for strict MVVM, and a Behavior to carry one rule across screens.

## Related Articles

- [Implementing Column Sorting in WPF DataGrid](/articles/wpf-datagrid-sorting/)
- [WPF DataGrid Row Double-Click to a Command: MouseDoubleClick Also Fires on Column Headers](/articles/wpf-datagrid-row-doubleclick-command-mvvm/): measures how a common approach opens the previously selected row while a column header double-click also sorts the column
- [Using DataGridTemplateColumn for Display and Edit Templates in WPF](/articles/wpf-datagrid-cell-editing-template/)
- [DataGrid (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/datagrid.html): measures that a third header click keeps the sort, and how to end an edit before sorting from code
