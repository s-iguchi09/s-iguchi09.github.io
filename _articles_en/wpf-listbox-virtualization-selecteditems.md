---
layout: article-en
title: "How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox"
date: 2026-04-24
category: WPF
excerpt: "Learn why ListBox selection can appear to disappear when UI virtualization is enabled, and how to keep selection state stable with an IsSelected-based MVVM pattern."
---

## Overview

When a WPF `ListBox` displays a large number of items, UI virtualization is typically enabled through `VirtualizingStackPanel`. With virtualization, item containers (`ListBoxItem`) are created, destroyed, or recycled as you scroll.

If your selection logic depends on containers, you may observe a situation where an item selected earlier seems to disappear from `SelectedItems` after scrolling and selecting another item. This becomes especially noticeable in multi-select scenarios and when using `Shift` range selection.

The safest approach is to keep selection state on the **data items**, not on the visual containers. Add an `IsSelected` property to each item ViewModel and bind `ListBoxItem.IsSelected` to it with a two-way binding. This keeps selection stable even when virtualization is active.

## Why the issue happens

With ListBox virtualization, containers are not guaranteed to stay alive. If selection state is managed in a way that depends on containers, it becomes vulnerable to the following patterns:

- storing or tracking selected state through `ListBoxItem` references
- rebuilding selection from the visual tree
- failing to restore selection when a container is realized again

In other words, the data is not necessarily lost — the **container-based synchronization** is.

## Recommended fix: add IsSelected to each item

For stable MVVM-friendly multi-selection, add an `IsSelected` property to every item ViewModel.

### Item ViewModel sample

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

### Screen ViewModel sample

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

### XAML sample

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

## Does this also fix Shift-range selection?

Yes.

If you keep `SelectionMode="Extended"`, WPF continues to handle normal multi-selection behavior, including `Ctrl` and `Shift` selection. The `IsSelected` binding above is only responsible for persisting the resulting selection state on the data items.

That means when a user selects a range with `Shift`, the selected items in that range are marked as selected on the ViewModel. Even if their containers are later virtualized away, the selection state remains in the data and is restored when the containers are realized again.

## Common pitfalls

### 1. Do not try to two-way bind SelectedItems directly

`SelectedItems` is a collection, but it is not easy to bind directly in a standard WPF `ListBox`. For multi-select MVVM scenarios, the `IsSelected` pattern is the most practical solution.

### 2. Do not set CanContentScroll to false

`ScrollViewer.CanContentScroll="False"` often disables virtualization behavior and causes all items to be rendered. For large lists, keep it `True` unless you specifically need pixel-based scrolling.

### 3. Avoid container-dependent logic

Using `ItemContainerGenerator.ContainerFromIndex` or walking the visual tree makes your selection logic fragile under virtualization and container recycling. Keep selection state in the data layer.

## Summary

If a WPF `ListBox` uses virtualization and selection is managed through visual containers, `SelectedItems` may appear to lose previously selected items after scrolling.

A robust solution is:

- add `IsSelected` to each item ViewModel
- bind `ListBoxItem.IsSelected` to `IsSelected` with two-way binding
- keep `SelectionMode="Extended"` so built-in `Shift` / `Ctrl` selection continues to work
- keep `CanContentScroll="True"` so virtualization remains active

With this pattern, selection remains stable even in a virtualized ListBox, including Shift-range selection.
