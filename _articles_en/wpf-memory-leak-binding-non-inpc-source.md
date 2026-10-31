---
layout: article-en
title: "WPF Binding Without INotifyPropertyChanged: Why the Source Stays in Memory"
seo_title: "WPF Binding Memory Leak Without INotifyPropertyChanged"
date: 2026-10-31
category: WPF
excerpt: "On .NET 10, a closed window bound to a non-INotifyPropertyChanged source is collected, but a PropertyDescriptor table keeps the source. Nine bindings compared."
image: /images/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-retention.svg
---

## Overview

After a report window is opened and closed, the view model of the closed window can remain uncollected by the GC.
If the remaining object holds large data, repeated opening and closing can look like a memory leak.
One cause is a binding to a property of an object that does not implement `INotifyPropertyChanged` (INPC below).

Since something survives the window, it is natural to assume that the whole window survives.
Counting over 100 open/close cycles on .NET 10, however, shows that the objects left uncollected are neither the windows nor the bindings, but the binding sources.
The number left behind fell and rose again between checkpoints.

This article traces the reference path that keeps the source alive by reading WPF's internal table.
It then compares, by measurement, which bindings leave the source behind and which do not, and shows view model implementations that avoid it.

---

## Prerequisites / Environment

- Framework: WPF (.NET). The code examples use collection expressions and therefore assume C# 12 or later (available by default when targeting .NET 8 or later)
- Verified on: .NET 10 (10.0.13) / Windows 11
- Architecture: MVVM (the view shows values through bindings only and has no code-behind)

The behavior was checked only on .NET 10; .NET 8, .NET 9, and .NET Framework 4.8 were not measured.
The internal table is observed through non-public fields, which may not be readable under the same names in other versions.

---

## Symptom

The view model does not implement INPC.
It holds a 1 MB array in place of the report data.

```csharp
public sealed class ReportViewModel
{
    public string Title { get; set; } = "Monthly report";

    public byte[] Data { get; } = new byte[1_000_000];
}
```

`Title` is an ordinary auto-implemented property with no way to announce changes.
For screens that only display values, view models or DTOs written like this are sometimes assigned to `DataContext` as is.

The view is a window that only shows `Title` in a `TextBlock`.

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Width="240" Height="100">
  <TextBlock Text="{Binding Title}" />
</Window>
```

Each time the window opens, a new `ReportViewModel` is created and assigned to `DataContext`, and the window is shown and closed.
This is repeated 100 times, and after running the GC, the objects that are still alive are counted through weak references.
Sources are counted at 10, 50, and 100 cycles; `BindingExpression` objects and windows are counted at 100 cycles.
The following table shows the results for nine variations of the binding.

{% include tables/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-retention.en.md %}

Table 1: A window was opened and closed 100 times.
At each checkpoint, the run drained the dispatcher, waited 100 ms, and ran the GC, five times in a row, before counting (.NET 10.0.13 / Windows 11, values from one run).
Every source holds a 1 MB array.
The rows marked "tracking the List&lt;T&gt;" and "tracking the collection" count the `Rows` object instead of the view model.
"Key in \_valueChangedHandlers while open" shows whether, while the first window was open, the owner of the bound property was a key in the registration table of that property's `PropertyDescriptor` (a non-public field read through reflection).
Whether the same numbers remain across runs was not checked.
{: .table-caption}

In the first three "No INPC" rows (the settable property with a TextBlock and with a TextBox, and the get-only property) and in the "List&lt;T&gt;.Count" row, sources were left uncollected at one or more checkpoints.
At 100 cycles, every one of these rows left fewer than the number opened (100).
On the other hand, no row left any `BindingExpression` or window alive after 100 cycles.
The INPC view model, `Mode=OneTime`, the `RowCount` property, and `ObservableCollection<T>.Count` left no source at any checkpoint.
Every row that left sources behind is a row whose source was a key in the registration table while the window was open.

---

## What Happens Internally

### How WPF Watches a Source That Does Not Notify

Microsoft Learn's "[Optimizing Performance: Data Binding](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/optimizing-performance-data-binding)" names two ways to provide property change notifications: INPC and the change notifications associated with `TypeDescriptor`.
In the measurement, when the source did not implement INPC, the source was in the registration table of the bound property's `PropertyDescriptor`.
That is the "Key in \_valueChangedHandlers while open" column in Table 1.
Sources that implement INPC and bindings with `Mode=OneTime` were not in that registration table.

A `PropertyDescriptor` is not created per object.
In the measurement, calling `TypeDescriptor.GetProperties` twice with the type, or once with an instance, returned the same instance for the same property.
Subscribers are registered in a table inside this single instance per property of a type, **keyed by the object being watched**.
The following table shows what the non-public field `_valueChangedHandlers` held, starting while the window was open.

{% include tables/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-cleanup-timeline.en.md %}

Table 2: How `_valueChangedHandlers` changed for one window, from while it was open until the registration was removed.
`TimelineViewModel` has the same shape as `ReportViewModel` in Table 1 and is used only for this measurement.
`_valueChangedHandlers` and the `_source` field of the record stored as its value were only read through reflection and never modified (.NET 10.0.13 / Windows 11).
{: .table-caption}

The `Target` of the handler stored as the value is `MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord`, not the binding itself.
This record also points to the source through its `_source` field.
After the window was closed and the GC was run, the `BindingExpression` was collected, but the registration and the source remained.

### Why the Window Is Collected While the Source Is Not

Microsoft Learn's "[Weak event patterns](https://learn.microsoft.com/dotnet/desktop/wpf/events/weak-event-patterns)" explains that many aspects of WPF data binding already apply the weak event pattern.
On this path too, what is registered directly in the `PropertyDescriptor` is not the binding but a `ValueChangedEventManager` record (Table 2).
The record and the binding are connected in a way that does not prevent the binding from being collected, and after the window closed, the `BindingExpression` was collected (Tables 1 and 2), and so was the window (Table 1).

The source, on the other hand, is held by ordinary references: as a key in the registration table and as the record's `_source`.
In Table 2, the source was not collected until that registration was cleaned up.

<figure class="article-figure">
  <img src="/images/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-reference-path.svg" alt="A solid arrow goes from PropertyDescriptor (ReportViewModel.Title) to _valueChangedHandlers. From there, a solid arrow labeled key goes to ReportViewModel (byte[] Data, 1 MB) and a solid arrow labeled value goes to ValueChangedRecord. From ValueChangedRecord, a solid arrow labeled _source goes to ReportViewModel and a dashed arrow goes to BindingExpression (TextBlock / Window)." width="760" height="270" loading="lazy">
  <figcaption>The reference path that keeps the source alive after the window closes. Solid arrows are references that prevent collection; the dashed arrow is one that does not. The registration key, the record type, and the record's _source were confirmed in Table 2 (with TimelineViewModel, which has the same shape as ReportViewModel); the collection of the BindingExpression was confirmed in Tables 1 and 2, and that of the window in Table 1. Red marks the source that stays after the window closes, gray marks .NET and WPF internal objects (the PropertyDescriptor, its registration table, and the record), and green marks what gets collected. The shape of the reference from the record to the BindingExpression (direct or through other objects) was not checked; the dashed arrow only shows that this part does not prevent collection (.NET 10.0.13 / Windows 11).</figcaption>
</figure>

### What Removes the Registration

As Table 2 shows, the registration was not removed after waiting 5 seconds with the GC run every 250 ms.
It was removed when another window, bound to a different type without notifications, was opened and the GC was run before closing it.
What triggered the removal was a window with a binding to a different type, whose `PropertyDescriptor` is a different instance.
This suggests that the cleanup happens not on the per-type registration table but on a side that works across types (such as `ValueChangedEventManager`).
That internal processing, and whether creating a binding to an INPC source or a `OneTime` binding also removes it, were not checked.

In Table 2, the next window is opened after the first window's `BindingExpression` has been collected by the GC.
In Table 1, the GC runs only at checkpoints, so when the next window opens, the previous window's binding might not have been collected yet.
Assuming that such registrations stay is consistent with only part of the opened sources remaining in Table 1, but neither this hypothesis nor how the registrations change after each open/close cycle was measured.

---

## Consequences of This Route

### Long-Lived Sources Rarely Cause Harm

What is retained is the source, not the window.
If the source lives for the whole application anyway, such as a single settings object, a remaining registration does not make that source live any longer than it would.
In the "one long-lived source shared by all windows" row of Table 1, that source was a key in the registration table, yet all 100 windows and `BindingExpression` objects were collected.

The problem arises when a source is created for each window, has no reason to outlive it, and holds large data.
Report data, images, and lists of search results stay together with the source.

### The Object That Owns the Bound Property Is What Stays

The key is not necessarily the `DataContext`; it is the object that owns the bound property.
If `Rows` in `{Binding Rows.Count}` is a `List<T>`, the `List<T>` that owns `Count` does not implement INPC, even when the view model does, so the `List<T>` becomes the key when `Count` is watched.
In Table 1, the `List<T>` was a key in the registration table while the window was open, and the `List<T>` and its contents stayed after the window closed.

`ObservableCollection<T>` implements INPC, so with the same path it did not become a key and did not stay.
Keeping the `List<T>` but reading the count from a `RowCount` property on the INPC view model kept the view model out of the registration table, and no `List<T>` was left behind either.

### Properties Without a Setter Are Watched Too

Having no way to change the value does not exclude a property from watching.
In Table 1, binding to a get-only property also made the source a key in the registration table and left sources behind.

### OneTime Stays Out of the Registration Table

`Mode=OneTime` only initializes the target from the source and does not propagate later changes ([Data binding overview](https://learn.microsoft.com/dotnet/desktop/wpf/data/#direction-of-the-data-flow)).
In Table 1, even a source without notifications did not become a key in the registration table and did not stay.

---

## Implementation Example

### Implement INPC on the View Model

The basic fix is to implement INPC on the class that owns the bound properties.
A minimal base class is prepared, and view models derive from it.

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class ReportViewModel : ViewModelBase
{
    private string _title = "Monthly report";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public byte[] Data { get; } = new byte[1_000_000];
}
```

A source that implements INPC does not become a key in the `PropertyDescriptor` registration table (Table 1).
The source did not stay even when `PropertyChanged` was never raised.
For `OneWay` and `TwoWay` bindings to detect source changes, however, the source must provide change notifications ([What are binding declarations?](https://learn.microsoft.com/dotnet/desktop/wpf/data/binding-declarations-overview#binding-direction)).
The setter therefore notifies through `SetProperty`.

The XAML of the view does not need to change.
`{Binding Title}` stays as it is, and the type of the source alone changes the path.

### Show Counts Through ObservableCollection or a View Model Property

To show the number of rows in a list, make `Rows` an `ObservableCollection<T>`.

```csharp
using System.Collections.ObjectModel;

public sealed class RowsViewModel : ViewModelBase
{
    public ObservableCollection<byte[]> Rows { get; } = [];
}
```

The XAML can stay `{Binding Rows.Count}`.

To keep a `List<T>` for read-only data whose rows do not change, give the view model a property that returns the count.

```csharp
using System.Collections.Generic;

public sealed class ReadOnlyRowsViewModel : ViewModelBase
{
    public List<byte[]> Rows { get; } = [];

    public int RowCount => Rows.Count;
}
```

The XAML becomes `{Binding RowCount}`.
The binding reads `RowCount` from the view model, which implements INPC, and in Table 1 neither the view model nor the `List<T>` stayed.
`RowCount` does not announce changes to `Rows`, so lists whose items are added or removed after the window opens should use `ObservableCollection<T>`.

### Use OneTime for Values That Do Not Change

When a value does not change while the window is open and the source class cannot be changed (a DTO from an external library, for example), specify `Mode=OneTime`.

```xml
<TextBlock Text="{Binding Title, Mode=OneTime}" />
```

A `OneTime` binding stays out of the registration table, so the source does not stay even if it does not implement INPC (Table 1).
In exchange, the screen is not updated if the source value changes after it is displayed.
`TextBox.Text`, which accepts input, is `TwoWay` by default ([What are binding declarations?](https://learn.microsoft.com/dotnet/desktop/wpf/data/binding-declarations-overview#default-behaviors); "[How User Input Silently Removes a WPF Binding Written Without Mode](/articles/wpf-binding-mode-omitted-detached/)" covers how to check a property's default direction).
Only `TwoWay` and `OneWayToSource` bindings write back to the source ([Binding.UpdateSourceTrigger](https://learn.microsoft.com/dotnet/api/system.windows.data.binding.updatesourcetrigger)), so `OneTime` stops input from reaching the source.
This approach is limited to display-only elements.

---

## Notes

- **Use the internal type names for observation only.** `_valueChangedHandlers`, `ValueChangedRecord`, and `_source` are non-public implementation details whose names and structure may change in other versions. Do not touch them from application code.
- **Check the retention path in a memory profiler.** When the view model of a closed window stays alive and its retention path contains a type derived from `PropertyDescriptor` or a `ValueChangedEventManager` record, it is likely the path described here. How it appears in a profiler was not checked for this article.
- **Do not judge by the number left behind.** Even in Table 1, only part of the opened sources stayed. "Opening and closing a few times did not increase it" does not prove that this path is absent. Check the binding itself: whether the source implements INPC or the binding is `OneTime`.
- **The object without INPC is not always the view model.** Objects in the middle of the path (`List<T>`, DTOs, settings classes, and so on) are affected as well. For a path like `{Binding A.B.C}`, check the type of the object at each step.
- **A shared source does not need an urgent fix.** For a binding to a source that lives to the end, the windows and `BindingExpression` objects are collected (Table 1).

---

## Summary

When a binding's source does not implement INPC, the source becomes a key in the registration table of a `PropertyDescriptor` that is reused per property of a type.
On .NET 10, the binding and the window are collected, but the source stays referenced by the registration key and by the record's `_source`.
In the measurement, 5 seconds of waiting and GC did not remove it; it was removed after another window, bound to a different type without notifications, was opened and the GC was run.
Because the trigger was a window with a binding to a different type, the cleanup appears to happen not on the per-type registration table but on a side that works across types.

When binding to view models created per window or to objects that hold large data, implement INPC on that class.
Show list counts through `ObservableCollection<T>` or through a count property on the view model.
For values that do not change and whose class cannot be modified, use `Mode=OneTime`.
For a single long-lived source shared across the application, the windows are not retained, so the priority is low.

---

## Related Articles

- [How User Input Silently Removes a WPF Binding Written Without Mode](/articles/wpf-binding-mode-omitted-detached/)
- [Reading WPF Binding Errors and Diagnosing Them with the Output Window](/articles/wpf-binding-error-debugging-output-window/)
- [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/)
