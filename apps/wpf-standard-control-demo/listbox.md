---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/listbox.html
title: "ListBox"
badge: "List"
lead: "ListBox shows a scrollable list from which the user selects one item or, with <code>SelectionMode</code>, several."
description: "WPF ListBox measured on .NET 10: selection modes, SelectedItem and SelectedValue, virtualization, matching by Equals, and why selecting in code does not scroll."
---

## What a click selects in each SelectionMode

**ListBox** derives from `Selector`, and `ListView` derives from ListBox. Items come from `Items` or `ItemsSource`, and each item is shown in a `ListBoxItem` container.

`SelectionMode` decides how clicks select items; the default is `Single`. Clicking Item 1, Item 2, and Item 1 without modifier keys gave these selections. With `Single`: Item 1, then Item 2, then Item 1. With `Multiple`: Item 1, then Items 1 and 2, then Item 2, because each click toggles an item. With `Extended`: the same as `Single`, because a plain click replaces the selection (selecting several items in `Extended`, for example files for a batch operation, takes Shift or Ctrl, which was not reproduced). `SelectAll()` selected all four items in `Multiple` and `Extended`, and threw `NotSupportedException` in `Single`, so check `SelectionMode` before calling it.

## Selecting from code: matched by Equals

Setting `SelectedIndex = 2` selected the third item, updated `SelectedItem`, and raised `SelectionChanged` once. An out-of-range index (10 with four items) raised no exception and left the selection unchanged; -1 cleared it.

`SelectedItem` is matched with `Equals`, not by reference. A new instance of a class that does not override `Equals` selected nothing. A new instance of a record with the same values selected the matching item. `SelectedItem` then held the new instance, not the one in the list. When the ViewModel restores a selection from saved data, give the item class a meaningful `Equals`, or select through `SelectedValue` and an ID.

`SelectedValuePath` names the property of the selected item that `SelectedValue` returns, for example an `Id`. The order does not matter: `SelectedValue = 2` set before `ItemsSource` selected the item with `Id` 2 once the items arrived.

`DisplayMemberPath` is the property of each item to show as text. With `DisplayMemberPath="Name"`, each item was shown by a `TextBlock` reading "Desktop". A path that does not exist gave an empty `TextBlock`, with no exception. It cannot be combined with `ItemTemplate`: setting both threw `InvalidOperationException`.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listbox/listbox-values.svg" alt="Table of ListBox selection values: an out-of-range SelectedIndex is ignored, -1 clears the selection, a new instance of a class without Equals selects nothing while an equal record selects the item, SelectedValue set before ItemsSource is applied later, a missing DisplayMemberPath shows empty text, and DisplayMemberPath with ItemTemplate throws InvalidOperationException" width="1077" height="350" loading="lazy">
  <figcaption>SelectedIndex, SelectedItem, SelectedValue, and DisplayMemberPath. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Virtualization: what exists, and what scrolls

The default style arranges the items in a `VirtualizingStackPanel`, so only the visible containers are created. With 1,000 items in a list 100 high, six `ListBoxItem`s existed.

That is why a selection made from code can be out of view. Setting `SelectedIndex = 500` did not scroll the list, and the container for item 500 was not created; `ScrollIntoView` scrolled to it. Call `ScrollIntoView` after selecting from code.

It also affects multi-selection. `IsSelected` on a `ListBoxItem` binds two-way by default, but in a list created from `ItemsSource`, the containers are created only while visible, so a binding in `ItemContainerStyle` covers only those items. [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/) measures how such a selection gets out of sync when the list scrolls. Handle `SelectionChanged` and update the ViewModel from `SelectedItems`, or follow the article.

The scroll bars are set with the attached properties `ScrollViewer.HorizontalScrollBarVisibility` and `ScrollViewer.VerticalScrollBarVisibility`; the default style sets both to `Auto`. With 1,000 items, `Auto` showed the vertical bar. `Disabled` hid it but did not stop scrolling: the scrollable range was the same (996 items), and 20 presses of the down arrow scrolled the list as far as with `Auto`.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listbox/listbox-scrolling.svg" alt="Table of ListBox scrolling with 1,000 items: six containers are created, selecting item 500 from code does not scroll until ScrollIntoView is called, and a Disabled vertical scroll bar hides the bar but keyboard navigation still scrolls" width="1022" height="230" loading="lazy">
  <figcaption>Virtualization and scrolling with 1,000 items in a list 100 high. Offsets are in items. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The ListBox page of the demo app, with the control list on the left and the first section, SelectionMode](/images/wpf-standard-control-demo/listbox.png){: .screenshot-img}

The ListBox page of the demo app has a section for each of `SelectionMode`, `DisplayMemberPath`, `SelectedIndex` and `SelectedItem`, `SelectedValuePath` and `SelectedValue`, `IsSelected`, and the scroll bars. The `SelectionMode` section shows `SelectedItems.Count` under the list. The `DisplayMemberPath` and `SelectedValuePath` sections hold a list of brushes and take the path from a text box: `Name` shows the brush names and any other text leaves the items blank, and as the value path, `Value` returns the brush and `Name` its name. The `IsSelected` section binds two `ListBoxItem`s to check boxes, so checking a box selects the item and clicking the item changes the box. The scroll bar section has a long first item so you can also try the horizontal bar. The "Show Code" link under each section displays its XAML. The following XAML is the `IsSelected(ListBoxItem)` section (`ListBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsSelectedItem1CheckBox" Content="Select Item 1" />
  <CheckBox x:Name="IsSelectedItem2CheckBox" Content="Select Item 2" />

  <ListBox x:Name="IsSelectedListBox" Height="100" SelectionMode="Extended">
    <ListBoxItem Content="Item 1 (Bound to CheckBox)"
                 IsSelected="{Binding IsChecked, ElementName=IsSelectedItem1CheckBox}" />
    <ListBoxItem Content="Item 2 (Bound to CheckBox)"
                 IsSelected="{Binding IsChecked, ElementName=IsSelectedItem2CheckBox}" />
    <ListBoxItem Content="Item 3 (Independent)" />
  </ListBox>

  <TextBlock Text="{Binding SelectedItems.Count, ElementName=IsSelectedListBox}" />
</StackPanel>
```

## Related controls and articles

- [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/) — multi-selection with virtualization.
- [ListView](/apps/wpf-standard-control-demo/listview.html) — derives from ListBox and adds views such as `GridView` columns.
- [ComboBox](/apps/wpf-standard-control-demo/combobox.html) — a `Selector` whose list opens as a drop-down.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ListBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ListBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Clicks were reproduced by raising the left-button event on the `ListBoxItem`, which is how a plain click reaches the ListBox. Clicks with Shift or Ctrl were not reproduced, because the ListBox reads the actual keyboard state.

[View ListBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ListBoxUsage){: target="_blank" rel="noopener noreferrer"}
