---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/wrappanel.html
title: "WrapPanel"
badge: "Layout"
lead: "WrapPanel places its children one after another and moves to a new row (or column) when the current one is full."
description: "WPF WrapPanel measured on .NET 10: where the demo's items wrap, row height, ItemWidth and ItemHeight, and why it can stop wrapping in a ScrollViewer."
---

## Where the items wrap, and how high a row is

By default a **WrapPanel** is `Horizontal`, and `ItemWidth` and `ItemHeight` are `NaN`, so each child keeps its own size. The demo app's five labels, 150 wide, went into two rows of 3 and 2. With `Orientation="Vertical"` and a height of 60, the same five labels, each about 32 high, went into five columns of one.

A row is as high as its tallest child. A child 20 high next to one 40 high was stretched to 40 when its `VerticalAlignment` was `Stretch`, and stayed 20 with `Top`.

## Fixed slots with ItemWidth and ItemHeight

`ItemWidth` and `ItemHeight` give every child a slot of the same size; the demo app starts both at 100. With `ItemHeight` 100, the labels were stretched to 96 (100 minus their margin), and the second row started at 100, with its first label at 102 because of the label's margin of 2. With `ItemWidth` 100, a label was stretched to 96.

A child larger than its slot is not shrunk but cut off. A child 150 wide was given 100: its layout clip was 100 wide, and a hit test 20 past it found nothing. Choose `ItemWidth` and `ItemHeight` larger than the largest child.

## Why it stops wrapping in a ScrollViewer

WrapPanel needs a limited width to wrap. In a ScrollViewer whose horizontal scrolling is disabled, the default, the five labels went into 3 rows of 2, 2, and 1: the vertical scroll bar took part of the 150. With `HorizontalScrollBarVisibility="Auto"`, the panel got unlimited width and put all five in one row. Keep horizontal scrolling disabled around a horizontal WrapPanel.

It does not virtualize either. A ListBox 300 × 200 with a WrapPanel as its `ItemsPanel` created all 1000 of its 1000 items, so keep lists in a WrapPanel short.

## The gaps and ZIndex

With the default `Background` of `null`, a hit test in the gap between two labels found nothing; with `Transparent`, it found the panel. Set `Transparent` to make the gaps clickable. Where children overlap, `Panel.ZIndex` decides the order: with two labels overlapping by 15, `ZIndex` 1 and 2 put the second on top, and 3 and 2 the first.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/wrappanel/wrappanel-behavior.svg" alt="Table of WrapPanel results: Horizontal with NaN item sizes by default, the demo's five labels in two rows of 3 and 2 at 150 wide and five columns at 60 high, a short child stretched to the row height only with Stretch, ItemWidth and ItemHeight 100 stretching labels to 96 and cutting off a 150-wide child at 100, three rows in a ScrollViewer and one row when horizontal scrolling is allowed, gaps hit only with a Background, ZIndex ordering overlapping labels, and all 1000 items created in a ListBox" width="1038" height="530" loading="lazy">
  <figcaption>Wrapping, line sizes, <code>ItemWidth</code> and <code>ItemHeight</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The WrapPanel page of the demo app, with the control list on the left and the first section, Orientation](/images/wpf-standard-control-demo/wrappanel.png){: .screenshot-img}

The WrapPanel page of the demo app has sections for `Orientation`, `ItemHeight`, `ItemWidth`, `Background`, and `ZIndex`. The "Show Code" link under each section displays its XAML. The following XAML is the `ItemWidth` section (`WrapPanelUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and the input behavior of the text box left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="ItemWidthText" Text="100" />

  <WrapPanel x:Name="ItemWidthWrapPanel" ItemWidth="{Binding Text, ElementName=ItemWidthText}">
    <Label Margin="2" BorderBrush="Black" BorderThickness="1" Content="Item1" />
    <Label Margin="2" BorderBrush="Black" BorderThickness="1" Content="Item2" />
    <Label Margin="2" BorderBrush="Black" BorderThickness="1" Content="Item3" />
  </WrapPanel>
</StackPanel>
```

## Related controls and articles

- [StackPanel](/apps/wpf-standard-control-demo/stackpanel.html) — places children in one line without wrapping.
- [UniformGrid](/apps/wpf-standard-control-demo/uniformgrid.html) — cells of the same size in a fixed number of rows and columns.
- [ScrollViewer](/apps/wpf-standard-control-demo/scrollviewer.html) — decides whether the panel inside it gets a limited width.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`WrapPanelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/WrapPanelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with labels like the demo app's (margin 2, border 1). The panels were laid out with `Measure` and `Arrange`. Sizes are the values on the measuring machine.

[View WrapPanel source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/WrapPanelUsage){: target="_blank" rel="noopener noreferrer"}
