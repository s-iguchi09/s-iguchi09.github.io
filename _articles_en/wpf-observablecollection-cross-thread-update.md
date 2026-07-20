---
layout: article-en
title: "Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF"
date: 2026-07-20
category: WPF
excerpt: "Modifying a bound ObservableCollection off the UI thread throws a NotSupportedException from CollectionView affinity. This covers the cause and two fixes."
---

## Overview

Modifying an `ObservableCollection<T>` bound to an `ItemsControl` from a thread other than the UI thread throws a `NotSupportedException`.
The message reads along the lines of "This type of `CollectionView` does not support changes to its `SourceCollection` from a thread different from the `Dispatcher` thread" (the exact wording varies by .NET version and locale).
This article explains that the exception comes from the thread affinity of the `CollectionView` rather than the collection itself, and it organizes the fixes based on `BindingOperations.EnableCollectionSynchronization` and the `Dispatcher`, along with the criteria for choosing between them.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (the same applies to .NET Framework 4.5 and later)
- Language: C# / XAML (the code samples use a target-typed `new` (`= new();`, C# 9 or later); on C# 8 or earlier, use an explicit type such as `= new ObservableCollection<string>();`)
- Target controls: `ObservableCollection<T>`, `ItemsControl` (including `ListBox`, `DataGrid`, `ListView`), `CollectionView`
- Architecture: applicable to both MVVM and code-behind
- Assumption: the collection is updated on a background thread (`Task.Run` or a worker thread)

---

## Problem

Modifying a bound collection directly from a background thread raises an exception.
The following example adds items to an `ObservableCollection<T>` from work started with `Task.Run`.

```csharp
public ObservableCollection<string> Items { get; } = new();

private async Task LoadAsync()
{
    await Task.Run(() =>
    {
        foreach (var line in ReadHugeFile())
        {
            // Add from a non-UI thread throws NotSupportedException
            Items.Add(line);
        }
    });
}
```

When `Items` is bound to `ItemsControl.ItemsSource`, the `Add` call reaches the `CollectionView` through a `CollectionChanged` notification.
Because that notification arrives from a non-UI thread, the `CollectionView` throws the exception.

---

## Cause / Background

The cause is not `ObservableCollection<T>` itself but the `CollectionView` that WPF routes collection access through when displaying it.
The official documentation states that both the `ItemsControl` and the `CollectionView` have affinity to the thread on which the `ItemsControl` was created, that using them on a different thread is forbidden, and that doing so throws an exception.
In effect, this restriction extends to the bound collection as well.

Most WPF objects derive from `DispatcherObject` and carry thread affinity to their creating thread, which is normally the UI thread.
The `CollectionView` also derives from `DispatcherObject` and, by default, does not allow its bound collection to be changed from another thread.
As a result, when a `CollectionChanged` notification arrives from a non-UI thread, the `CollectionView` throws `NotSupportedException` because it does not permit cross-thread changes.
The root of the problem is therefore not that the collection was touched on another thread, but that the UI-thread-owned `CollectionView` cannot receive a change notification originating from a different thread.

---

## Solution

There are two approaches.

- **Marshal to the UI thread with the `Dispatcher`** — run the collection mutation itself on the UI thread. This is simple and easy to apply to existing code.
- **Use `BindingOperations.EnableCollectionSynchronization`** — provide a lock in the application and register it with WPF, which allows direct modification from a background thread. This is less likely to saturate the UI thread even under heavy updates.

The former moves changes onto the UI thread; the latter lets WPF safely take in changes made on another thread.

---

## Implementation

### Marshal to the UI thread with the Dispatcher

Move the collection mutation to the UI thread with `Dispatcher.Invoke` (or `InvokeAsync`).
Using `Application.Current.Dispatcher` obtains the UI thread `Dispatcher` even from a view model.

```csharp
private async Task LoadAsync()
{
    var dispatcher = Application.Current.Dispatcher;
    await Task.Run(() =>
    {
        foreach (var line in ReadHugeFile())
        {
            // Add runs on the UI thread, so no exception occurs
            dispatcher.Invoke(() => Items.Add(line));
        }
    });
}
```

Because the mutation runs on the UI thread, no affinity violation occurs in the `CollectionView`.
Invoking per item causes many round-trips to the UI thread, however, so processing several items within a single `Invoke` is preferable when items can be added in batches.

### Share a lock with EnableCollectionSynchronization

Provide a lock object and register it with WPF by calling `EnableCollectionSynchronization` on the UI thread.
From then on, all application-side modifications must be protected by that same lock.

```csharp
private readonly object _lock = new();
public ObservableCollection<string> Items { get; } = new();

public ViewModel()
{
    // Call on the UI thread and before using the collection on another thread
    BindingOperations.EnableCollectionSynchronization(Items, _lock);
}

private async Task LoadAsync()
{
    await Task.Run(() =>
    {
        foreach (var line in ReadHugeFile())
        {
            lock (_lock)
            {
                Items.Add(line);
            }
        }
    });
}
```

Once `EnableCollectionSynchronization` is called, the `CollectionView` accesses the collection using the registered lock and maintains a "shadow copy" for the UI thread.
Change notifications are queued as they arrive and applied when the UI thread has the opportunity to do so.
This allows `Add` to be called directly from a background thread.
As required by the documentation, the call must occur on the UI thread and before the collection is used on another thread (or attached to the control), whichever is later.

---

## Notes

- **Protect all application access with the same lock:** the lock passed to `EnableCollectionSynchronization` must guard every read and write in the application, not only WPF's access. Leaving any path unlocked can race with the `CollectionView`.
- **Atomicity of change and notification:** a change (such as `Add`) and its `CollectionChanged` notification must be atomic. `ObservableCollection<T>` guarantees this as long as all changes are protected by the same synchronization.
- **Timing of registration and disabling:** call both `EnableCollectionSynchronization` and `DisableCollectionSynchronization` on the UI thread. To use the same collection on multiple UI threads, register it separately on each.
- **Overusing `Dispatcher.Invoke` strains the UI:** running a large number of per-item synchronous `Invoke` calls saturates the UI thread and reduces responsiveness. For high item counts, consider `EnableCollectionSynchronization` or a batched add on the UI thread.
- **UI elements remain UI-thread-only:** this fix relaxes only access to the bound collection. Manipulating a `DependencyObject`, such as a control, directly from another thread remains disallowed.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best suited for |
| --- | --- | --- | --- |
| `Dispatcher.Invoke` / `InvokeAsync` | No extra setup; simple and easy to retrofit | Per-item round-trips can strain the UI thread | Low update frequency and volume; occasional add or remove |
| `EnableCollectionSynchronization` (simple lock) | Direct modification from the background; less UI pressure | Requires consistent locking; slightly more design effort | High-volume, high-frequency updates on another thread |
| `EnableCollectionSynchronization` (callback) | Allows non-lock mechanisms such as semaphores | Most complex to implement | A design that already has a custom synchronization mechanism |
| Batch on the UI thread | Avoids the threading issue entirely | Loses the benefit of background work | Work that can apply all changes at once after gathering |

---

## Summary

The exception raised when a bound `ObservableCollection<T>` is modified from another thread comes from the thread affinity of the `CollectionView`, not the collection.
The selection criteria are as follows.

- **When updates are occasional and few:** marshal changes to the UI thread with `Dispatcher.Invoke` / `InvokeAsync`. This needs no extra setup and is the simplest option.
- **When updating heavily and frequently on another thread:** share a lock with `EnableCollectionSynchronization` and allow direct modification from the background, avoiding UI thread pressure.
- **When a custom synchronization mechanism such as a semaphore exists:** use the callback overload.

In every case, design with the understanding that UI elements themselves remain UI-thread-only, and that only collection access is relaxed.

---

<!-- Related articles -->
- [WPF ComboBox ItemsSource Binding Patterns and Selected Value Retrieval](/articles/wpf-combobox-itemssource-patterns/)
- [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/)
