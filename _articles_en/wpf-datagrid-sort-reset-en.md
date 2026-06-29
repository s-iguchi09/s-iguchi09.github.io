---
layout: article-en
title: "How to Reset DataGrid Sorting in WPF"
date: 2026-06-29
category: WPF
excerpt: "Practical ways to reset WPF DataGrid sorting, including explicit clearing, Sorting-event control, CollectionView handling, and a reusable Behavior."
---

WPF `DataGrid` provides built-in sorting, but many applications need a way to restore the grid to its initial unsorted state.  
This article covers practical approaches to reset sorting depending on your architecture and UX requirements.

## Topics covered

- Default behavior of Shift+Click
- Explicitly clearing sort in code
- Auto-reset on the third click (custom behavior)
- Resetting sort from ViewModel via `CollectionView`
- Reusable Behavior class approach

## Shift+Click default behavior

In WPF `DataGrid`, `Shift + column header click` is used for **multi-column sorting** by default.  
It does **not** act as a built-in “clear sorting” shortcut.

- Regular click: toggles sort direction on that column
- Shift+Click: adds that column to existing sort criteria

So if you need a true reset to the initial state, you should implement it explicitly.

## Explicitly clear sorting in code

The most direct method is to clear both internal sort descriptors and UI sort indicators.

```csharp
using System.Windows.Controls;

public static class DataGridSortHelper
{
    public static void ClearDataGridSort(DataGrid dataGrid)
    {
        if (dataGrid == null) return;

        // Clear data-level sort descriptors
        dataGrid.Items.SortDescriptions.Clear();

        // Clear header arrows
        foreach (var column in dataGrid.Columns)
        {
            column.SortDirection = null;
        }

        // Refresh UI
        dataGrid.Items.Refresh();
    }
}
```

## Auto-reset on the third click (custom behavior)

If you want: `Unsorted → Ascending → Descending → Unsorted`, handle the `Sorting` event and intercept the descending-to-next transition.

### XAML

```xml
<DataGrid x:Name="MyDataGrid"
          Sorting="DataGrid_Sorting" />
```

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
        e.Handled = true; // Cancel default processing

        // Remove only this column's sort descriptor
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

## Reset from ViewModel using CollectionView

In MVVM, you typically control sorting through `ICollectionView` instead of touching the `DataGrid` directly.

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

```xml
<DataGrid ItemsSource="{Binding ItemsView}" />
```

## Build a reusable Behavior class

If you need the same tri-state sorting logic across multiple views, encapsulate it in a Behavior (`Microsoft.Xaml.Behaviors.Wpf`).

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

## Conclusion

To reset sorting in WPF `DataGrid`, choose the approach that matches your app design:

- Understand default behavior first (Shift+Click = multi-sort)
- Use explicit clearing for straightforward reset
- Use `Sorting` event for tri-state UX
- Use `CollectionView` for MVVM-friendly control
- Use Behavior for reusable, cross-screen consistency
