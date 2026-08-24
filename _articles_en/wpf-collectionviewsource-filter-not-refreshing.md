---
layout: article-en
title: "Causes of a Stale ICollectionView Filter in WPF and Choosing Between Refresh and Live Filtering"
date: 2026-08-24
category: WPF
excerpt: "The filter is correct right after assignment, yet an item property change leaves the list stale. Refresh and live filtering compared on .NET 10."
image: /images/articles/wpf-collectionviewsource-filter-not-refreshing/collectionview-filter-refresh.png
---

## Overview

Screens that narrow a list by a condition, such as an inventory grid or a list filtered through a search box, are commonly built with `ICollectionView.Filter`.
The display is correct immediately after the filter is assigned.
Yet later changes to item values or to the search condition leave the list showing stale contents.

The predicate is not at fault.
An `ICollectionView` holds the filter, but **the triggers that cause it to be re-evaluated are limited, and by default a property change on an item is not among them**.

This article isolates which operations re-evaluate the filter and which do not.
It then compares three approaches — full re-evaluation through `ICollectionView.Refresh`, incremental re-evaluation through `ICollectionViewLiveShaping`, and building a filtered collection by hand — in terms of re-evaluation cost, notification granularity, and the effect on selection state.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF
- Verified on: .NET 10 / Windows 11 (all measurements and figures in this article were taken there)
- Cross-checked: the re-evaluation triggers, the `DeferRefresh` behavior, the `BindingListCollectionView` restrictions, and the XAML resolution produced identical results on .NET Framework 4.8
- Language: C# 12 or later / XAML (the samples use collection expressions; on .NET 6 and .NET 7, whose default language versions are lower, raise `LangVersion` or rewrite them as collection initializers)
- The samples assume nullable reference types are enabled
- Types involved: `System.ComponentModel.ICollectionView`, `System.ComponentModel.ICollectionViewLiveShaping`, `System.Windows.Data.CollectionViewSource`, `System.Windows.Data.ListCollectionView`
- Architecture: MVVM (the collection lives in the view model and reaches an `ItemsControl` through a view)
- Namespaces: `System.Collections.ObjectModel`, `System.ComponentModel`, `System.Windows.Data`
- Item types are assumed to implement `INotifyPropertyChanged`

The live filtering API — `ICollectionViewLiveShaping`, `ListCollectionView.IsLiveFiltering`, and `CollectionViewSource.IsLiveFilteringRequested` — was added in .NET Framework 4.5 and is unavailable on earlier versions.
The runtime behavior is identical on WPF for .NET Framework, but the samples below do not compile as written on .NET Framework 4.8, for three distinct reasons.
`string.Contains(string, StringComparison)` is an API that exists only on .NET Core 2.1 and later, so replace it with `IndexOf(_keyword, StringComparison.OrdinalIgnoreCase) >= 0`.
The `init` accessor requires `System.Runtime.CompilerServices.IsExternalInit`, a type absent from .NET Framework, so define it manually or fall back to `set`.
The rest is a language version question, and projects targeting .NET Framework default to C# 7.3.
Nullable reference annotations require C# 8.0, target-typed `new()` requires C# 9.0, and collection expressions require C# 12, so raise `LangVersion` to 12 or later.
Setting a `LangVersion` newer than the one associated with the target framework is not officially supported, however.
The features used in this article do work on .NET Framework 4.8, but rewriting the syntax as described above avoids relying on that.

---

## Problem

Consider a screen that lists only products that are in stock.
Each product carries a `Stock` count that changes as goods are restocked or shipped.

```csharp
public sealed class Product : INotifyPropertyChanged
{
    private int _stock;

    public string Name { get; init; } = string.Empty;

    public int Stock
    {
        get => _stock;
        set
        {
            if (_stock == value)
            {
                return;
            }

            _stock = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stock)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

`Stock` raises a change notification.
A stock count already rendered in a cell therefore updates correctly.

The view model holds the collection and the default view obtained from it, and the view carries a "stock is one or more" filter.

```csharp
public sealed class InventoryViewModel
{
    public InventoryViewModel()
    {
        Products =
        [
            new() { Name = "Bolt", Stock = 0 },
            new() { Name = "Nut", Stock = 5 },
            new() { Name = "Washer", Stock = 12 },
            new() { Name = "Screw", Stock = 3 },
        ];

        View = CollectionViewSource.GetDefaultView(Products);
        View.Filter = item => ((Product)item).Stock > 0;
    }

    public ObservableCollection<Product> Products { get; }

    public ICollectionView View { get; }
}
```

The initial display is exactly as intended: only Bolt, whose `Stock` is 0, is excluded.
This happens because assigning to the `Filter` property itself rebuilds the view.

Restocking Bolt exposes the problem.

```csharp
viewModel.Products[0].Stock = 8;
```

The stock count rendered in the cell changes to 8, yet Bolt does not appear in the list.
Conversely, a product whose stock drops to 0 does not disappear from the list.

<figure class="article-figure">
  <img src="/images/articles/wpf-collectionviewsource-filter-not-refreshing/collectionview-filter-refresh.png" alt="Three ListBox controls carrying the same filter. On the left Bolt is still missing after its stock was set to 8; in the middle Refresh has been called; on the right live filtering is enabled. Both of the latter show Bolt." width="602" height="201" loading="lazy">
  <figcaption>Three views carrying a Stock &gt; 0 filter, immediately after the stock of Bolt was changed from 0 to 8. Left: nothing else done. Middle: Refresh called. Right: IsLiveFiltering enabled. Produced on .NET 10 / Windows 11.</figcaption>
</figure>

A search box produces the same symptom.
When the predicate reads a view model field, rewriting that field changes nothing on screen.

```csharp
private string _keyword = string.Empty;

// The predicate reads _keyword.
View.Filter = item => ((Product)item).Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase);

// Changing the keyword leaves the list untouched.
_keyword = "Nut";
```

Neither the collection nor any item has changed here, which makes this variant even harder to recognize than the inventory case.

---

## Cause / Background

When the source collection implements `INotifyCollectionChanged`, as `ObservableCollection<T>` does, the view subscribes to it and receives notifications for added and removed items.
Only **the item named by the notification** is evaluated.
Counting predicate invocations while adding one item to a view holding 1,000 items yields exactly one call, and a removal yields zero.
Items the view already knows about are never re-examined.

A `PropertyChanged` notification from an item, by contrast, is not a re-evaluation trigger by default.
Writing to `Stock` produces zero predicate invocations, and the view raises no `CollectionChanged` event at all.
Bindings subscribe to `PropertyChanged` individually, so the rendered cell updates, but **the membership of the view stays frozen at the decision made earlier**.
That asymmetry is precisely what produces the symptom: the value changes while the row neither enters nor leaves the list.

The same reasoning extends to sorting.
Changing a sort key property on a view that carries `SortDescriptions` does not move the row.

When the predicate reads state outside the items, such as a field holding a search keyword, the situation is simpler still: neither the source collection nor any item has changed.
No path exists through which the view could learn that the condition moved.

What obscures the problem is that the filter is correct right after it is assigned.
As the official documentation states, assigning `Filter`, `SortDescriptions`, or `GroupDescriptions` triggers a refresh on its own.
Only that first evaluation is implicit, which makes the view look as though it keeps up automatically.

---

## Solution

Supply the re-evaluation trigger explicitly.
There are two strategies.

- **Re-evaluate every item** — call `ICollectionView.Refresh`. The view applies the filter to all items and raises a single `Reset`. Use this when the filter depends on state outside the items.
- **Re-evaluate only the items that changed** — enable `ICollectionViewLiveShaping.IsLiveFiltering` and register the property names the predicate reads in `LiveFilteringProperties`. The view subscribes to change notifications for those properties, re-tests only the affected item, and raises `Add` or `Remove` only when that item enters or leaves the view. Use this when the filter depends solely on item properties.

`ListCollectionView`, which is the concrete type behind the default view of an `ObservableCollection<T>` or a `List<T>`, implements `ICollectionViewLiveShaping` and returns `true` from `CanChangeLiveFiltering`.
The two strategies are not exclusive.
Combining them — live filtering for item properties, `Refresh` only when the search keyword changes — is the arrangement that proves most practical.

---

## Implementation

When the filter reads external state, call `Refresh` from the code that writes that state.
Implement `INotifyPropertyChanged` on the `InventoryViewModel` shown earlier, then add the `Keyword` property shown below.
Concentrating the re-evaluation in the keyword setter makes the call hard to omit.

```csharp
public string Keyword
{
    get => _keyword;
    set
    {
        if (_keyword == value)
        {
            return;
        }

        _keyword = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Keyword)));

        // The state the predicate reads has changed, so re-evaluate everything.
        View.Refresh();
    }
}
```

`Refresh` applies the predicate again.
The invocation count matches the size of the source collection rather than the size of the filtered view, so a source of 10,000 items costs 10,000 predicate calls per `Refresh`.
A search box using `UpdateSourceTrigger=PropertyChanged` calls `Refresh` on every keystroke, and the cost of the predicate then turns directly into input lag.

To track item properties, treat the view as `ICollectionViewLiveShaping` and register the properties the predicate reads.

```csharp
ICollectionView view = CollectionViewSource.GetDefaultView(Products);
view.Filter = item => ((Product)item).Stock > 0;

var liveShaping = (ICollectionViewLiveShaping)view;
liveShaping.IsLiveFiltering = true;
liveShaping.LiveFilteringProperties.Add(nameof(Product.Stock));
```

What gets registered is **the property the predicate reads**, not the property that is displayed.
Live filtering does nothing while `LiveFilteringProperties` is empty.
Assigning `null` to `IsLiveFiltering` throws `ArgumentNullException`, so assign `false` to turn it off.

The cast above throws `InvalidCastException` when the source does not implement `IList`.
Where the source type is only known at run time, replace that cast with `if (view is ICollectionViewLiveShaping liveShaping && liveShaping.CanChangeLiveFiltering)` and assign only when the test succeeds.
Testing `CanChangeLiveFiltering` as well is necessary because views over a `DataView` or a `BindingList<T>` implement `ICollectionViewLiveShaping` yet cannot switch live filtering on.
A type test alone lets those through, and setting `IsLiveFiltering` then throws `InvalidOperationException`, as noted below.

The XAML form uses `IsLiveFilteringRequested` and `LiveFilteringProperties` on `CollectionViewSource`.
This is also the form used when each screen requires an independent view.

```xml
<Window.Resources>
  <CollectionViewSource x:Key="InStockProducts"
                        Source="{Binding Products}"
                        Filter="OnProductFilter"
                        IsLiveFilteringRequested="True">
    <CollectionViewSource.LiveFilteringProperties>
      <sys:String>Stock</sys:String>
    </CollectionViewSource.LiveFilteringProperties>
  </CollectionViewSource>
</Window.Resources>

<ListBox ItemsSource="{Binding Source={StaticResource InStockProducts}}"
         DisplayMemberPath="Name" />
```

Declare the `sys` prefix as `xmlns:sys="clr-namespace:System;assembly=mscorlib"`; `assembly=System.Runtime` also resolves on both .NET and .NET Framework.
Because the resource is specified as the `Source` of a `Binding`, what reaches `ItemsSource` is not the `CollectionViewSource` itself but the `CollectionViewSource.View` it produces — the filtered view.
Writing `ItemsSource="{StaticResource InStockProducts}"` assigns the `CollectionViewSource` object instead, which raises `XamlParseException` when the XAML is loaded, wrapping an `ArgumentException` that reports the value as invalid for `ItemsSource`.

`Filter` here is an event that sets `FilterEventArgs.Accepted`, which is a separate entry point from the `ICollectionView.Filter` property.

```csharp
private void OnProductFilter(object sender, FilterEventArgs e)
{
    e.Accepted = ((Product)e.Item).Stock > 0;
}
```

`e.Accepted` defaults to `true`, so only items for which it is set to `false` are excluded from the view.
The handler is invoked over the same range as an `ICollectionView.Filter` predicate: every item in the source when the view is rebuilt (on assignment or on `Refresh`), but only the affected item when an item is added or re-tested by live filtering.
`e.Item` is typed as `object`, so an unconditional cast like the one above throws `InvalidCastException` on a collection holding items of mixed types.

When filtering, sorting, and grouping are reconfigured together, wrap the changes in `DeferRefresh`.
The rebuild that would otherwise run once per assignment collapses into a single pass when the scope exits.

```csharp
using (View.DeferRefresh())
{
    View.Filter = item => ((Product)item).Stock > 0;
    View.SortDescriptions.Add(
        new SortDescription(nameof(Product.Stock), ListSortDirection.Descending));
}
```

Performing those two assignments without `DeferRefresh` raises `Reset` twice; wrapping them raises it once.
Reading the contents or the current position of the view inside the scope throws `InvalidOperationException`, so restrict the body of the `using` to assignments.

---

## Notes

- **Live filtering is not applied synchronously.** Enumerating the view in the same method right after writing a property returns the previous contents. The update runs on a `Dispatcher` callback, so a unit test must pump the message queue, for example with a `DispatcherFrame`, before asserting.
- **Changes to unregistered properties are ignored.** A configuration whose predicate reads `IsActive` while only `Stock` is registered in `LiveFilteringProperties` will not react to `IsActive`. Revisit the registrations whenever the filter condition changes.
- **Live filtering requires items to implement `INotifyPropertyChanged`.** Without change notifications, no re-evaluation trigger reaches the view.
- **The default view is shared by every control bound to that collection.** Handing the same `ObservableCollection<T>` to two `ListBox` controls and then setting a filter on `CollectionViewSource.GetDefaultView` narrows both. Assigning `ItemsControl.Items.Filter` writes to the same default view, so it cannot narrow one screen alone. Create separate `CollectionViewSource` instances to keep views independent.
- **`Refresh` raises `Reset`, so an `ItemsControl` rebuilds its item containers.** Containers for items that remain in the view are regenerated as well. Live filtering raises `Add` and `Remove` only for items that enter or leave the view, so containers for the surviving items are reused. The difference becomes noticeable when item containers carry an expensive template.
- **"`Refresh` drops the selection" is not accurate.** As long as the selected item keeps passing the filter, `SelectedItem`, `SelectedItems`, and `CurrentItem` all survive a `Refresh`. This was confirmed on a `ListBox` with `SelectionMode` set to `Single` and, separately, to `Extended`. Selection is lost when the selected item stops passing the filter and leaves the view, and live filtering behaves the same way in that case. The cause differs from [selection loss under UI virtualization](/articles/wpf-listbox-virtualization-selecteditems/).
- **Views over a `DataView` or a `BindingList<T>` cannot use `Filter`.** Their default view is a `BindingListCollectionView` whose `CanFilter` is `false` in both cases, and assigning `Filter` throws `NotSupportedException`. `CanChangeLiveFiltering` is `false` as well, so setting `IsLiveFiltering` throws `InvalidOperationException`.
- **`CustomFilter` works only on a collection that implements `IBindingListView`.** The fallback for `Filter` is to pass a string expression to `CustomFilter`. `DataView` implements `IBindingListView`, so its `CanCustomFilter` is `true`; `BindingList<T>` does not, so its `CanCustomFilter` is `false` and assigning `CustomFilter` throws `NotSupportedException` as well. Test `CanCustomFilter` before assigning.
- **Sources that do not implement `IList` cannot use live filtering.** The default view over a plain `IEnumerable`, such as the result of a LINQ query, is an internal class derived from `CollectionView`; it supports `Filter` but does not implement `ICollectionViewLiveShaping`. The runtime type is not public, so it cannot be named in a cast, but a type test against `ICollectionViewLiveShaping` is still valid and simply returns `false`.
- **Sorting needs its own settings.** Enabling live filtering does not keep the sort order current. Configure `IsLiveSorting` and `LiveSortingProperties` separately. Column-header sorting in a `DataGrid` runs on the same `ICollectionView`, so when live filtering is combined with [column sorting](/articles/wpf-datagrid-sorting/), keep in mind that both settings act on the same view.

---

## Alternatives / Comparison

| Approach | Re-evaluation trigger | Predicate invocations | Notification | Constraints |
|---|---|---|---|---|
| `Refresh` | An explicit call | Every item in the source (10,000 calls for 10,000 items) | One `Reset`; all item containers regenerated | A missed call leaves stale contents on screen |
| `IsLiveFiltering` + `LiveFilteringProperties` | Change notifications for registered properties | Only the items that changed (10 calls for 10 changes) | `Add` / `Remove` only for items entering or leaving; surviving containers reused | Does not track conditions outside the items; applied asynchronously |
| A hand-built filtered collection | Whenever the rebuild is invoked | Determined by the rebuild implementation | `Reset` from `Clear` and re-adding | Selection is lost for every item, including those that remain; sorting and grouping must be reimplemented |

Building a filtered `ObservableCollection<T>` by hand escapes the constraints of `ICollectionView` but handles selection worst of the three.
An implementation that clears and re-adds discards the selection even for items that keep passing the filter.
There is little reason to choose it where an `ICollectionView` is available.

---

## Summary

An `ICollectionView` holds a filter but does not re-evaluate it when an item property changes.
When the display looks stale, what to examine is not the condition in the predicate, but whether a re-evaluation trigger was ever supplied.

- **When the filter reads only item properties:**
Enable `IsLiveFiltering` and register every property the predicate reads in `LiveFilteringProperties`.
Re-evaluation stays limited to the items that changed and item containers are reused, which makes this the default choice.
- **When the filter reads state outside the items, such as a search keyword:**
Call `Refresh` from the code that writes that state.
Live filtering cannot track it.
- **When the filter depends on both:**
Combine them.
Leave property changes to live filtering and call `Refresh` only when an external condition moves.
- **When filtering, sorting, and grouping change together:**
Wrap the assignments in `DeferRefresh` to collapse the rebuild into one.
Do not read the view inside the scope.
- **When the source is a `DataView`:**
Neither `Filter` nor `IsLiveFiltering` can be assigned.
Pass a string expression to `BindingListCollectionView.CustomFilter`.
A `BindingList<T>` cannot use that fallback either, so move to an `ObservableCollection<T>` when filtering is required.
- **When the same collection must be narrowed differently on several screens:**
Do not use the default view, which is shared.
Create a `CollectionViewSource` instance per screen.

---

<!-- Related articles -->
- [How to Implement DataGrid Sorting in WPF](/articles/wpf-datagrid-sorting/)
- [How to Reset DataGrid Sorting in WPF](/articles/wpf-datagrid-sort-reset/)
- [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/)
- [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/)
