---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/scrollviewer.html
title: "ScrollViewer"
badge: "Layout"
lead: "ScrollViewer shows part of content that is larger than itself and lets the user scroll through the rest. ListBox, TextBox, TreeView, and DataGrid each contain one."
description: "WPF ScrollViewer measured on .NET 10: the four scroll bar settings with content that fits or overflows, CanContentScroll and virtualization, deferred scrolling."
---

## Overview

By default a **ScrollViewer** has `VerticalScrollBarVisibility` `Visible` and `HorizontalScrollBarVisibility` `Disabled`. The controls that contain one set their own values. The ScrollViewer inside a ListBox, a TreeView, and a DataGrid had `Auto` horizontally, and the one inside a TextBox had `Hidden`.

To change the ScrollViewer inside a control, set the attached property on the control itself. On a ListBox, `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` reached the ScrollViewer inside it. Putting the ListBox in another ScrollViewer set to `Disabled` did not: the inner one stayed `Auto`.

The size properties are known only after layout. `ExtentHeight` and `ViewportHeight` were 0 before the first layout and 251.64 and 100 after it. Why a ScrollViewer sometimes does not scroll at all, for example inside a StackPanel, is covered in the article linked below.

The demo app has sections for `CanContentScroll`, `IsDeferredScrollingEnabled`, both scroll bar settings, and the read-only size and offset properties in each direction. Each uses a ScrollViewer 100 high holding labels with a border. The "Show Code" link under each section displays its XAML.

## Screen Preview

![The ScrollViewer page of the demo app, with the control list on the left and the first section, CanContentScroll](/images/wpf-standard-control-demo/scrollviewer.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `VerticalScrollBarVisibility / HorizontalScrollBarVisibility` | `Disabled / Auto / Hidden / Visible` | Whether the bar is shown and whether the content can scroll. Like the demo app, a viewer 200 wide and 100 high was given 2 labels, which fit, and 9, which do not. `Disabled` turns scrolling off. The content was given only the viewer's height (`ExtentHeight` 100 for 9 labels), so the rest is cut off, and scrolling to 50 left the offset at 0. `Auto` showed the bar only for 9 labels. `Hidden` never showed it, but scrolling to 50 worked. `Visible` showed it even for 2 labels. The bar takes width: `ViewportWidth` was 183 with the bar and 200 without. These values were measured for the vertical bar. `HorizontalScrollBarVisibility` accepts the same four values; the horizontal direction was not measured. |
| `CanContentScroll` | `bool` | Whether the content scrolls by its own units. With the demo app's StackPanel of 9 labels, `False` gave `ExtentHeight` 251.64 and `ViewportHeight` 100 in pixels, and one `LineDown` moved 16. `True` gave 9 and 3, counted in items, and one `LineDown` moved one item. It also decides virtualization in a list. A ListBox with 1000 items and 200 high created 10 `ListBoxItem`s with `True` and all 1000 with `False`. |
| `IsDeferredScrollingEnabled` | `bool` | Whether the content waits until the thumb is released. With `False`, dragging the thumb 30 down moved `VerticalOffset` to 114.38 during the drag. With `True`, it stayed at 0 during the drag and moved to 114.38 on release. |
| `ComputedVerticalScrollBarVisibility / ExtentHeight / ViewportHeight / ScrollableHeight / VerticalOffset / ContentVerticalOffset` | `ReadOnly` | Whether the bar is actually shown, the size of the content and of the visible part, and the position. `ComputedVerticalScrollBarVisibility` was `Visible` only for `Auto` with 9 labels and for `Visible`; in every other case it was `Collapsed`. The demo app shows these values for 2 and 9 labels. |
| `ComputedHorizontalScrollBarVisibility / ExtentWidth / ViewportWidth / ScrollableWidth / HorizontalOffset / ContentHorizontalOffset` | `ReadOnly` | The same values for the horizontal direction. The demo app shows them for labels placed side by side. |

## XAML Example

The following XAML is the `CanContentScroll` section of the demo app (`ScrollViewerUsageControl.xaml`), with the styles, and the surrounding GroupBoxes left out and the namespace declarations added. The styles set the ScrollViewer's `Height` to 100 and give each label a black border 1 wide.

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="CanContentScrollCheckBox" Content="CanContentScroll" />

  <ScrollViewer x:Name="CanContentScrollScrollViewer"
                CanContentScroll="{Binding IsChecked, ElementName=CanContentScrollCheckBox}"
                Height="100">
    <StackPanel Orientation="Vertical">
      <Label Content="Item1" />
      <Label Content="Item2" />
      <Label Content="Item3" />
      <Label Content="Item4" />
      <Label Content="Item5" />
      <Label Content="Item6" />
      <Label Content="Item7" />
      <Label Content="Item8" />
      <Label Content="Item9" />
    </StackPanel>
  </ScrollViewer>
</StackPanel>
```

## Common Use Cases

- **Long forms:** a StackPanel of fields in a ScrollViewer, so a small window can still reach every field.
- **Large images:** an image shown at its own size, with both bars set to `Auto`.
- **Controlling a list's scrolling:** the attached properties on a ListBox, TreeView, or DataGrid.

## Tips and Best Practices

- **Prefer `Auto` to `Visible`.** `Visible` takes the bar's width even when there is nothing to scroll.
- **Use `Hidden`, not `Disabled`, to hide the bar but keep scrolling.** `Disabled` cuts the content off.
- **Keep `CanContentScroll` `True` in long lists.** `False` creates a container for every item.
- **Set scrolling of a list with attached properties on the list.** A ScrollViewer around it does not change the one inside.
- **Read the sizes after layout.** They are 0 until the first layout.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ScrollViewerDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ScrollViewerDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The content was the demo app's labels with a border. Dragging the thumb was reproduced by raising the same `DragStarted`, `DragDelta`, and `DragCompleted` events on it that a mouse drag raises. Sizes are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/scrollviewer/scrollviewer-visibility.svg" alt="Table of the four VerticalScrollBarVisibility values with 2 and 9 labels in a ScrollViewer 200 by 100: Disabled shows no bar, gives the content a height of 100, and does not scroll; Auto shows the bar only for 9 labels; Hidden shows no bar but scrolls to 50; Visible always shows the bar, and the bar reduces the viewport width from 200 to 183" width="946" height="320" loading="lazy">
  <figcaption>Each <code>VerticalScrollBarVisibility</code> value with content that fits and content that overflows. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/scrollviewer/scrollviewer-scrolling.svg" alt="Table of ScrollViewer scrolling: the defaults are Visible and Disabled, CanContentScroll False scrolls 16 pixels and True one item, a ListBox of 1000 items creates 10 containers with CanContentScroll True and 1000 with False, deferred scrolling keeps the offset at 0 until the thumb is released, ListBox, TreeView, and DataGrid have Auto and TextBox Hidden inside, an attached property reaches the ListBox's ScrollViewer and an outer ScrollViewer does not, and the sizes are 0 before layout" width="936" height="380" loading="lazy">
  <figcaption><code>CanContentScroll</code>, deferred scrolling, and the ScrollViewers inside controls. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [ListBox](/apps/wpf-standard-control-demo/listbox.html) — contains a ScrollViewer that the attached properties change.
- [StackPanel](/apps/wpf-standard-control-demo/stackpanel.html) — a common child of a ScrollViewer, which scrolls by items when `CanContentScroll` is `True`.
- [Viewbox](/apps/wpf-standard-control-demo/viewbox.html) — scales content to fit instead of scrolling it.
- [Why a WPF ScrollViewer Does Not Scroll and How to Fix It](/articles/wpf-scrollviewer-not-scrolling/) — the layouts in which a ScrollViewer gets no scroll bar.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View ScrollViewer source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ScrollViewerUsage){: target="_blank" rel="noopener noreferrer"}
