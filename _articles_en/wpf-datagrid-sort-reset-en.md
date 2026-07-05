---
layout: article-en
title: "How to Reset DataGrid Sorting in WPF"
date: 2026-06-29
category: WPF
excerpt: "Practical ways to reset WPF DataGrid sorting, including explicit clearing, Sorting-event control, CollectionView handling, and a reusable Behavior."
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

## Solution

The reset strategy should be selected by architecture and UX requirements.  

- Clear both sort descriptors and header indicators explicitly.
- Use `Sorting` event logic when tri-state transition is required.
- Manage sorting in `ICollectionView` for MVVM-oriented implementations.
- Use a Behavior when the same rule must be shared across multiple screens.

## Implementation

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

### XAML

```xml
<DataGrid x:Name="MyDataGrid"
          Sorting="DataGrid_Sorting" />
```

This event hook allows custom handling before the default sorting pipeline completes.

### C\#

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

### Build a reusable Behavior class

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

## Notes

- Clearing `SortDescriptions` alone can leave header arrows out of sync with actual data order.
- For multi-column sorting, requirements should define whether reset means full clear or only target-column clear.
- Behavior-based reuse should provide extension points when screen-specific sorting rules differ.

## Alternatives / Comparison

| Method | Advantages | Disadvantages | Best fit |
|---|---|---|---|
| Explicit clear (`SortDescriptions.Clear` + `SortDirection = null`) | Simple implementation and fast adoption. | Requires direct `DataGrid` reference and lowers MVVM purity. | Screen-level commands that always perform full reset. |
| Tri-state with `Sorting` event | Provides consistent `Ascending -> Descending -> Unsorted` UX. | Event logic can become complex with column-specific requirements. | Header-driven interaction where reset should be available by click sequence. |
| `ICollectionView` in ViewModel | Better testability and minimal UI dependency. | Requires clear view-model responsibility boundaries. | Strict MVVM implementations with command-based reset. |
| Behavior-based reuse | Easy to roll out across multiple screens and reduces duplicate code. | Needs extension design for per-screen differences. | Shared sorting policy across many `DataGrid` instances. |

## Summary

WPF `DataGrid` sorting reset should be selected by requirement scope and architecture.

- Start with default behavior understanding: Shift+Click means multi-column sorting.
- Use explicit clear for straightforward full-reset scenarios.
- Use `Sorting` event handling for tri-state interaction.
- Use `ICollectionView` for MVVM-centered control.
- Use Behavior for cross-screen consistency and reuse.

For simple requirements, a helper method is sufficient.  
For shared screen behavior, Behavior-based design is more maintainable.  
For strict MVVM, `ICollectionView`-centric control is the most practical option.

## Related Articles

- [Implementing Column Sorting in WPF DataGrid](/articles/wpf-datagrid-sorting/)
- [Using DataGridTemplateColumn for Display and Edit Templates in WPF](/articles/wpf-datagrid-cell-editing-template/)
