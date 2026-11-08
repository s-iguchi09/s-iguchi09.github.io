---
layout: article-en
title: "Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF"
seo_title: "Fixing Cross-Thread ObservableCollection Updates in WPF"
date: 2026-07-20
category: WPF
excerpt: "Modifying a bound ObservableCollection off the UI thread throws a NotSupportedException from CollectionView affinity. This covers the cause and two fixes."
---

## Overview

By default, modifying an `ObservableCollection<T>` bound to an `ItemsControl` from a thread other than the UI thread throws a `NotSupportedException` (registering `BindingOperations.EnableCollectionSynchronization`, described below, lifts this restriction).
The message reads along the lines of "This type of `CollectionView` does not support changes to its `SourceCollection` from a thread different from the `Dispatcher` thread" (the exact wording varies by .NET version and locale).
This article explains that the exception comes from the thread affinity of the `CollectionView` rather than the collection itself, and it organizes the fixes based on `BindingOperations.EnableCollectionSynchronization` and the `Dispatcher`, along with the criteria for choosing between them.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (the same applies to .NET Framework 4.5 and later)
- Language: C# / XAML (the code samples use a target-typed `new` (`= new();`, C# 9 or later); on C# 8 or earlier, use an explicit type such as `= new ObservableCollection<string>();`)
- Target controls: `ObservableCollection<T>`, `ItemsControl` (including `ListBox`, `DataGrid`, `ListView`), `CollectionView`
- Architecture: applicable to both MVVM and code-behind
- Assumption: the collection is updated on a background thread (`Task.Run` or a worker thread)
- Verification environment: .NET 10 / Windows 11 (the measured results in this article were obtained here)

---

## Problem

Modifying a bound collection directly from a background thread raises an exception.
The following example adds items to an `ObservableCollection<T>` from work started with `Task.Run`.
`File.ReadLines` (from `System.IO`) is used as a stand-in data source; it enumerates a file's lines lazily.
The snippets in this article, except `RangeObservableCollection<T>` shown later, are members of a `ViewModel` class and assume the namespaces `System.Collections.ObjectModel`, `System.IO`, `System.Threading.Tasks`, `System.Windows`, and `System.Windows.Data`.

```csharp
public ObservableCollection<string> Items { get; } = new();

private async Task LoadAsync(string path)
{
    await Task.Run(() =>
    {
        foreach (var line in File.ReadLines(path))
        {
            // Add from a non-UI thread throws NotSupportedException
            Items.Add(line);
        }
    });
}
```

When `Items` is bound to `ItemsControl.ItemsSource`, the `Add` call reaches the `CollectionView` through a `CollectionChanged` notification.
Because that notification arrives from a non-UI thread, the `CollectionView` throws the exception.

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-observablecollection-cross-thread-update/collectionview-thread-affinity.svg" alt="A three-lane diagram of the data flow between threads. The first lane shows a CollectionChanged notification arriving directly at the CollectionView from a background thread and raising an exception. The second shows EnableCollectionSynchronization: the change stays on the background thread while the notification is queued and applied asynchronously to the UI thread's shadow copy. The third shows the Dispatcher moving the collection operation itself onto the UI thread." width="900" height="556" loading="lazy">
  <figcaption>The exception is raised not when the collection is touched, but when the change notification reaches the <code>CollectionView</code>. The two fixes work differently. <code>EnableCollectionSynchronization</code> makes the <code>CollectionView</code> participate in the same synchronization mechanism and applies queued notifications asynchronously to the shadow copy it keeps for the UI thread — the mutation itself may stay on the background thread. The <code>Dispatcher</code> instead marshals the collection operation onto the UI thread.</figcaption>
</figure>

---

## Cause / Background

`ObservableCollection<T>` itself is not thread-safe, but that is a separate matter: the direct cause of this `NotSupportedException` is the `CollectionView` that WPF routes collection access through when displaying it.
The official documentation states that both the `ItemsControl` and the `CollectionView` have affinity to the thread on which the `ItemsControl` was created, that using them on a different thread is forbidden, and that doing so throws an exception.
In effect, this restriction extends to the bound collection as well.

Most WPF objects derive from `DispatcherObject` and carry thread affinity to their creating thread, which is normally the UI thread.
The `CollectionView` also derives from `DispatcherObject` and, by default, does not allow its bound collection to be changed from another thread.
As a result, when a `CollectionChanged` notification arrives from a non-UI thread, the `CollectionView` throws `NotSupportedException` because it does not permit cross-thread changes.
The root of the problem is therefore not that the collection was touched on another thread, but that the UI-thread-owned `CollectionView` cannot receive a change notification originating from a different thread.

### Measured: the Outcome Depends on Whether the Collection Is Bound

The distinction is visible by performing the same operation on an `ObservableCollection<T>` that is not bound to anything.
The table below records the result of calling `Add` from a background thread, varying whether the collection is bound and which countermeasure is applied.

{% include tables/articles/wpf-observablecollection-cross-thread-update/collection-cross-thread-matrix.en.md %}

Measured on .NET 10 / Windows 11 by calling <code>ObservableCollection&lt;string&gt;.Add</code> from inside <code>Task.Run</code>. The first row is a collection bound to nothing; the remaining rows are bound to <code>ItemsControl.ItemsSource</code> and displayed in a window. The last column gives the collection's <code>Count</code> after the <code>Add</code>, the <code>ItemsControl</code>'s <code>Items.Count</code>, and the number of <code>CollectionChanged</code> notifications the view (<code>Items</code>) received.
{: .table-caption}

**An unbound collection can be modified from a background thread without an exception.**
If `ObservableCollection<T>` itself carried thread affinity, that row would fail as well.
The exception appears only once the collection is bound to `ItemsControl.ItemsSource` and a `CollectionView` sits in between, which confirms that the constraint belongs to the `CollectionView`.

Note that the absence of an exception on the unbound row **does not mean the collection is thread-safe**.
`ObservableCollection<T>` provides no protection against concurrent access, so competing updates still corrupt it in other ways.
What the row establishes is narrower: the source of `NotSupportedException` is the binding target.

The last column gives the collection's count after the `Add`, the `ItemsControl`'s `Items.Count`, and the number of change notifications the view received.
With a single `Add`, the two counts were 1 in every row, but a matching `Items.Count` does not show that the change reached the view: without synchronization the view can read the count from the source list. In the row that threw `NotSupportedException`, the view received no notification; with `Dispatcher.Invoke` and with `EnableCollectionSynchronization`, it received one.
The `EnableCollectionSynchronization` row was measured with the application wrapping the `Add` in the same lock it registered. Registering alone does not put the `Add` inside the lock; wrapping it in the same lock is the application's responsibility.

---

## Two Core Approaches

There are two approaches.

- **Marshal to the UI thread with the `Dispatcher`** — run the collection mutation itself on the UI thread. This is simple and easy to apply to existing code.
- **Use `BindingOperations.EnableCollectionSynchronization`** — provide a lock in the application and register it with WPF, which allows direct modification from a background thread. Change notifications are applied to the view asynchronously on the UI thread, so a change does not wait for them to be applied; access to the collection is still serialized by the registered lock, so the background thread can wait for the lock while the UI thread holds it.

The former moves changes onto the UI thread; the latter lets WPF safely take in changes made on another thread.

### Marshal to the UI thread with the Dispatcher

Move the collection mutation to the UI thread with `Dispatcher.Invoke` (or `InvokeAsync`).
Using `Application.Current.Dispatcher` obtains the UI thread `Dispatcher` even from a view model.
This assumes a single UI thread; in an application with multiple UI threads, `Application.Current.Dispatcher` refers to the main thread and may not own the bound `CollectionView`, so capture the `Dispatcher` associated with the bound `ItemsControl` (or its `CollectionView`) instead.

```csharp
private async Task LoadAsync(string path)
{
    var dispatcher = Application.Current.Dispatcher;
    await Task.Run(() =>
    {
        foreach (var line in File.ReadLines(path))
        {
            // Add runs on the UI thread, so no exception occurs
            dispatcher.Invoke(() => Items.Add(line));
        }
    });
}
```

Because the mutation runs on the UI thread, no affinity violation occurs in the `CollectionView`.
Invoking per item causes many round-trips to the UI thread, however, so processing several items within a single `Invoke` is preferable when items can be added in batches.
Even so, within a single operation on the UI thread (measured in a `DispatcherTimer` `Tick`, not in an `Invoke`), adding 50,000 items one at a time pushed the median longest frame interval past five times that with no updates.
Collapsing them into one `Reset` notification kept the median under a third of the per-item value (see "Measured: How Rendering Stalls Under a Burst of Updates" below).

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

private async Task LoadAsync(string path)
{
    await Task.Run(() =>
    {
        foreach (var line in File.ReadLines(path))
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

## How to Choose

The two core approaches above, plus a variant and a way to avoid the problem entirely, make four in all. Which one applies is settled by the volume and frequency of the updates.

**Occasional updates in small numbers call for `Dispatcher`.**
It needs no extra setup and touches little of the existing code. The cost of a per-item round-trip to the UI thread does not matter at low counts.

**Heavy, frequent updates from another thread call for `EnableCollectionSynchronization`.**
A per-item synchronous `Invoke` waits for the UI thread to run each item. Sharing a lock allows direct modification from the background and removes those round-trips, although the background thread still waits whenever the UI thread holds the lock.
Adding 5,000 items one at a time, the loop took 992 ms with `Dispatcher.Invoke`.
With `EnableCollectionSynchronization` the loop finished in 6 ms, and the view had received notifications for all items after 64 ms.
When control returned to the UI thread after the loop, though, the view had received only 746 notifications; the rest were applied later on the UI thread. That count was read after the `await`, not at the instant the loop ended, and the UI thread can process notifications in between. Applying the changes remains work for the UI thread.

{% include tables/articles/wpf-observablecollection-cross-thread-update/collection-cross-thread-bulk.en.md %}

Measured on .NET 10 / Windows 11 by adding 5,000 items one at a time from inside <code>Task.Run</code> to a collection bound to a virtualized <code>ListBox</code>. The third column is the number of <code>CollectionChanged</code> notifications the view had received when control returned to the UI thread after the loop, and the last column is the time from just before <code>Task.Run</code> was called (including the wait for the task to start) until the view had received 5,000.
{: .table-caption}

**A custom synchronization mechanism such as a semaphore calls for the callback overload.**
It lets WPF wait on something other than a lock. This is the most complex to implement.

**Work that can apply all its changes at once calls for batching on the UI thread.**
This avoids producing a cross-thread modification in the first place. The benefit of doing the work in the background shrinks, but no synchronization design is needed.
Even when 50,000 items were applied in one batch, adding them one at a time stalled rendering, as measured in the next section.

### Measured: How Rendering Stalls Under a Burst of Updates

To check whether rendering stalls when many updates arrive at once, 50,000 items were produced at once in the background, and the longest interval between rendered frames was measured for each way of applying them.

{% include tables/articles/wpf-observablecollection-cross-thread-update/collection-cross-thread-stall.en.md %}

Measured on .NET 10 / Windows 11 by producing 50,000 items at once inside <code>Task.Run</code> for a collection bound to a virtualized <code>ListBox</code>. The approaches were run in turn, one run each per round; after one warm-up round, five rounds were measured (the starting approach was rotated each round, and a GC was completed before each run). The table shows the median and the min–max across runs. The frame interval is the time between <code>CompositionTarget.Rendering</code> callbacks, measured from the start of the burst until the later of 100 ms after all items were applied and 1,000 ms after the start (the baseline measured the same 1,000 ms without a burst). The length of the measured span differs by approach, since it depends on how long the items take to apply. In the "Dispatcher.Invoke per item" and "EnableCollectionSynchronization + lock" rows, the producer waits for the UI thread or the lock, so producing the items also takes time. The two queue rows drain the queue every 100 ms with a default <code>DispatcherTimer</code> and apply the items on the UI thread. Every item is the same short string, and the window is 240×160. "Until all applied" is the time from the start of the burst until the view reflected all 50,000 items; for the two queue rows it includes a wait of up to 100 ms for the first timer tick. Completion was checked on the UI thread after each <code>Task.Delay(1)</code>. "Most items applied in one timer tick" is, for each run, the largest number of items applied in a single tick; it is shown as one value when all five rounds agree and as a range otherwise.
{: .table-caption}

Rendering stalled longest when the items were queued and then added one at a time on the UI thread.
Its median longest interval was more than five times the no-update baseline.
It was longer than every other approach in all five rounds.
In every round, the longest interval of each of the other three approaches was under half of even the shortest run with one `Add` per item.

- **Queuing stalled the longest with one `Add` per item, and came in line with the other approaches only once collapsed into one `Reset` notification.** In all five rounds, the timer applied all 50,000 items in a single tick. With one `Add` per item, the UI thread processes 50,000 notifications back to back inside that tick and cannot render in the meantime. Collapsing them into one `Reset` notification cut the median longest interval to under a third of that, and applied everything in the shortest time.
- **With `EnableCollectionSynchronization`, the median longest interval was under a third of that with one `Add` per item.** WPF first records cross-thread changes in a change log, then applies them on the UI thread in work items at `ContextIdle` priority. Each pass stops after a fixed time slice, and the rest is posted again at `ContextIdle` (see [`ProcessChangeLog` in `CollectionView`](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Data/CollectionView.cs) and [`Marshal` in `DataBindEngine`](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/Data/DataBindEngine.cs)). Rendering, which has a higher priority, runs in the gaps between passes.
- **`Dispatcher.Invoke` did not stall rendering for long, but applying the items took more than 10 times as long as with `EnableCollectionSynchronization` at the median.** Each item waits for its own run on the UI thread, so the background side cannot move ahead.

To collapse the changes into one `Reset` notification, derive from `ObservableCollection<T>` and add a method that adds items without raising notifications.
The measurement used this class.

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> source)
    {
        CheckReentrancy();
        foreach (T item in source)
        {
            Items.Add(item); // adding to Items raises no notification
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
```

Call `AddRange` on the UI thread; the measurement called it from the `DispatcherTimer` `Tick` handler.
It raises `Reset` even when there is nothing to add, so do not call it with an empty source (the measurement skipped the call when the queue was empty).
The measurement added 50,000 items to an empty collection in one call.

The effect of a `Reset` notification on a non-empty collection was measured as well.
In a `ListBox` holding 1,000 items, the item at index 500 (item 500) was selected and scrolled into view, and then 1,000 items were appended.

{% include tables/articles/wpf-observablecollection-cross-thread-update/collection-cross-thread-reset-effects.en.md %}

Measured on .NET 10 / Windows 11 with a virtualized <code>ListBox</code> holding 1,000 items: after selecting the item at index 500 and calling <code>ScrollIntoView</code>, 1,000 items were appended on the UI thread. <code>VirtualizationMode</code> was measured with the default <code>Standard</code> and with <code>Recycling</code>. The window is 240×240. The arrow columns show the value before and after the items were added; the vertical offset is in items because <code>ScrollUnit</code> is the default <code>Item</code>. "Indices realized before" counts, for each index realized before the items were added, whether its container afterwards was the same instance, another instance, or not realized. "Containers realized after" splits every container realized after the items were added into those created before the add (including any kept for reuse) and those created after it; creation was recorded by overriding <code>GetContainerForItemOverride</code>. "Tag set on the selected container" tells whether a value set directly on the <code>ListBoxItem</code> at index 500 before the items were added was still on the container at index 500 afterwards. "View notifications" is the number of <code>CollectionChanged</code> events received by <code>ListBox.Items</code>. Each condition was measured once.
{: .table-caption}

`SelectedIndex`, `SelectedItem`, and the vertical scroll position were kept with both ways of adding and in both virtualization modes.
With the `Reset` notification, however, no index realized before the items were added kept the same container instance, in either virtualization mode.
Every container realized afterwards was created after the add in both modes, with no reuse.
The `Tag` set directly on the selected item's container was not on the container afterwards.
Before collapsing updates into one `Reset` notification, keep the state of visible items in the data (the ViewModel) and bind it back.
These results are for a virtualized `ListBox` showing strings.

---

## Comparing the Approaches

| Approach | Pros | Cons | Best suited for |
| --- | --- | --- | --- |
| `Dispatcher.Invoke` / `InvokeAsync` | No extra setup; simple and easy to retrofit | With the synchronous `Invoke`, each item waits for its own run on the UI thread, so large volumes apply slowly (measured with `Invoke` for 50,000 items, more than 10 times slower than `EnableCollectionSynchronization` at the median) | Low update frequency and volume; occasional add or remove |
| `EnableCollectionSynchronization` (simple lock) | Direct modification from the background; notifications are applied on the UI thread asynchronously (access still waits for the shared lock) | Requires consistent locking; slightly more design effort | High-volume, high-frequency updates on another thread |
| `EnableCollectionSynchronization` (callback) | Allows non-lock mechanisms such as semaphores | Most complex to implement | A design that already has a custom synchronization mechanism |
| Batch on the UI thread | Avoids the threading issue entirely; with one `Reset` notification, the median longest frame interval for 50,000 items was under a third of that with one `Add` per item | Loses the benefit of background work; adding 50,000 items one at a time stalled rendering while they were applied; with a `Reset` notification on a non-empty collection, every container realized afterwards was newly created and no earlier instance was used (as measured with a virtualized `ListBox`) | Work that can apply all changes at once after gathering |

---

## Notes

- **Protect all application access with the same lock:** the lock passed to `EnableCollectionSynchronization` must guard every read and write in the application, not only WPF's access. Leaving any path unlocked can race with the `CollectionView`.
- **Atomicity of change and notification:** a change (such as `Add`) and its `CollectionChanged` notification must be atomic. `ObservableCollection<T>` guarantees this as long as all changes are protected by the same synchronization.
- **Timing of registration and disabling:** call both `EnableCollectionSynchronization` and `DisableCollectionSynchronization` on the UI thread. To use the same collection on multiple UI threads, register it separately on each.
- **UI elements remain UI-thread-only:** this fix relaxes only access to the bound collection. Manipulating a `DependencyObject`, such as a control, directly from another thread remains disallowed.
- **A worker started with `new Thread` keeps the process alive:** unlike `Task.Run`, it is a foreground thread by default, so a worker left updating the collection prevents the process from exiting even after the window closes. Diagnosis and remedies are covered in [Diagnosing a WPF Process That Stays Alive After the Window Closes — ShutdownMode and Foreground Threads](/articles/wpf-application-not-exiting-shutdownmode-threads/).

---

## Summary

The exception raised when a bound `ObservableCollection<T>` is modified from another thread comes from the thread affinity of the `CollectionView`, not the collection.

The deciding factor is the volume and frequency of the updates. Marshal to the UI thread with `Dispatcher` for occasional ones; share a lock with `EnableCollectionSynchronization` for heavy, frequent ones.
When a burst is applied in one batch on the UI thread, collapse it into one `Reset` notification instead of adding the items one at a time (when 50,000 items were added to an empty collection at once, this cut the median longest frame interval to under a third of that with one `Add` per item).
When items were added to a non-empty collection with a `Reset` notification, every container realized afterwards was newly created and no earlier instance was used (measured with a virtualized `ListBox`), so keep item state in the data and bind it back.
In every case, design with the understanding that UI elements themselves remain UI-thread-only, and that only collection access is relaxed.

---

<!-- Related articles -->
- [WPF ComboBox ItemsSource Binding Patterns and Selected Value Retrieval](/articles/wpf-combobox-itemssource-patterns/)
- [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/)
- [Releasing the Image File Locked by BitmapImage in WPF with BitmapCacheOption.OnLoad](/articles/wpf-bitmapimage-file-lock-cacheoption/)
- [WPF Binding Without INotifyPropertyChanged: Why the Source Stays in Memory](/articles/wpf-memory-leak-binding-non-inpc-source/)
