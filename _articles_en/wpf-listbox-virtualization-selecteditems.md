---
layout: article-en
title: "How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox"
date: 2026-04-24
category: WPF
excerpt: "Why ListBox selection appears to vanish under UI virtualization, and how an IsSelected-based MVVM pattern keeps it stable, including Shift-range selection."
---

## Overview

When a WPF `ListBox` displays a large number of items, UI virtualization is typically enabled through `VirtualizingStackPanel`.  
With virtualization, item containers (`ListBoxItem`) are created and destroyed based on the visible range.  

If selection management depends on containers, a previously selected item can appear to disappear from `SelectedItems` after scrolling and selecting another item.  

A robust approach is to keep selection state on the **data items**, not on the visual containers.  
Add an `IsSelected` property to each item ViewModel and bind `ListBoxItem.IsSelected` to it with two-way binding.  

## Prerequisites / Environment

- Framework / Language: .NET 6 or later / C# 10  
- Target control: WPF `ListBox` (`System.Windows.Controls`)  
- Architecture: MVVM (item ViewModels that expose `IsSelected`)  

The examples assume a collection of roughly 10,000 items bound to the `ListBox` with UI virtualization enabled, which is the default for `ListBox`.  
`SelectionMode` is set to `Extended` so multiple items can be selected.  

## Why the issue happens

With ListBox virtualization, containers are not guaranteed to stay alive.  
If selection state is managed in a way that depends on containers, it becomes vulnerable to the following patterns:

- storing or tracking selected state through `ListBoxItem` references
- rebuilding selection from the visual tree
- failing to restore selection when a container is realized again

In other words, the data is not necessarily lost — the **container-based synchronization** is.  

## Recommended fix: add IsSelected to each item

For stable MVVM-friendly multi-selection, add an `IsSelected` property to every item ViewModel.  

### Item ViewModel sample

Implement `IsSelected` with change notification on the ViewModel that represents each row.  

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class RowItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public RowItemViewModel(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

Without change notification on `IsSelected`, the two-way binding from `ListBoxItem.IsSelected` fails to synchronize on initial display and when containers are regenerated, so implementing `INotifyPropertyChanged` is mandatory.  

### Screen ViewModel sample

Hold the entire list and expose the selected items from the data side.  

```csharp
using System.Collections.ObjectModel;
using System.Linq;

public class MainViewModel
{
    public ObservableCollection<RowItemViewModel> Items { get; } = new();

    public MainViewModel()
    {
        for (int i = 1; i <= 10000; i++)
        {
            Items.Add(new RowItemViewModel(i, $"Row {i}"));
        }
    }

    public RowItemViewModel[] GetSelectedItems()
        => Items.Where(x => x.IsSelected).ToArray();
}
```

`GetSelectedItems` walks the data rather than the containers, so it always returns the correct selection regardless of scroll position or virtualization state.  

### XAML sample

Bind `ListBoxItem.IsSelected` to each item's `IsSelected` with two-way binding through `ItemContainerStyle`.  

```xml
<ListBox ItemsSource="{Binding Items}"
         SelectionMode="Extended"
         ScrollViewer.CanContentScroll="True"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Id}" Width="80"/>
                <TextBlock Text="{Binding Name}"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>

    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="IsSelected"
                    Value="{Binding IsSelected, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        </Style>
    </ListBox.ItemContainerStyle>
</ListBox>
```

Even when a container is regenerated, the binding re-reads the `IsSelected` value and restores the selection state.  

## Shift-range selection support

This pattern keeps `SelectionMode="Extended"`, so built-in multi-selection behavior (including `Shift` range selection and `Ctrl` additive selection) remains handled by WPF.  

When a range is selected with `Shift`, the `IsSelected` property for each item in the range is set to `true`.  
Even if containers are later virtualized away, the selection state remains on the data items, preventing selection from appearing lost.  

## Common pitfalls

### 1. Do not try to two-way bind SelectedItems directly

`SelectedItems` is a collection, but it is not easy to bind directly in a standard WPF `ListBox`.  
For multi-select MVVM scenarios, the `IsSelected` pattern is the most practical solution.  

### 2. Do not set CanContentScroll to false

`ScrollViewer.CanContentScroll="False"` often disables virtualization behavior and causes all items to be rendered.  
For large lists, keep it `True` unless pixel-based scrolling is explicitly required.  

### 3. Avoid container-dependent logic

Using `ItemContainerGenerator.ContainerFromIndex` or walking the visual tree makes selection logic fragile under virtualization and container recycling.  
Keep selection state in the data layer.  

### 4. Do not read the selection from containers

`ListBox.SelectedItems` reflects the selection even for items that virtualization has not realized.  
Code that enumerates containers to collect the selection, by contrast, misses off-screen items.  
Derive the selection from `IsSelected`, as `GetSelectedItems` above does.  

## Summary

If a WPF `ListBox` uses virtualization and selection is managed through visual containers, `SelectedItems` may appear to lose previously selected items after scrolling.  

A robust solution is:

- add `IsSelected` to each item ViewModel
- bind `ListBoxItem.IsSelected` to `IsSelected` with two-way binding
- keep `SelectionMode="Extended"` so built-in `Shift` / `Ctrl` selection continues to work
- keep `CanContentScroll="True"` so virtualization remains active

With this pattern, selection remains stable even in a virtualized ListBox, including Shift-range selection.  

This composition suits multi-selection over lists of thousands to tens of thousands of items.  
For a small list with few selections and no need for virtualization, using the standard `SelectedItems` directly is simpler.  
