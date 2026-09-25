---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/wrappanel.html
title: "WrapPanel"
badge: "Layout"
lead: "WrapPanel places its children one after another and moves to a new row (or column) when the current one is full."
description: "WPF WrapPanel measured on .NET 10: where the demo's items wrap, row height, ItemWidth and ItemHeight, and why it can stop wrapping in a ScrollViewer."
---

## Overview

By default a **WrapPanel** is `Horizontal`, and `ItemWidth` and `ItemHeight` are `NaN`, so each child keeps its own size. The demo app's five labels, 150 wide, went into two rows of 3 and 2. A row is as high as its tallest child. A child 20 high next to one 40 high was stretched to 40 when its `VerticalAlignment` was `Stretch`, and stayed 20 with `Top`.

WrapPanel needs a limited width to wrap. In a ScrollViewer whose horizontal scrolling is disabled, the default, the five labels went into 3 rows of 2, 2, and 1: the vertical scroll bar took part of the 150. With `HorizontalScrollBarVisibility="Auto"`, the panel got unlimited width and put all five in one row.

It does not virtualize. A ListBox 300 × 200 with a WrapPanel as its `ItemsPanel` created all 1000 of its 1000 items.

The demo app has sections for `Orientation`, `ItemHeight`, `ItemWidth`, `Background`, and `ZIndex`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![The WrapPanel page of the demo app, with the control list on the left and the first section, Orientation](/images/wpf-standard-control-demo/wrappanel.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Orientation` | `Horizontal / Vertical` | The direction in which children are placed before wrapping. With `Vertical` and a height of 60, the demo app's five labels, each about 32 high, went into five columns of one. |
| `ItemHeight` | `double` | A fixed height for every child's slot; the demo app starts at 100. With 100, the labels were stretched to 96 (100 minus their margin), and the second row started at 100, with its first label at 102 because of the label's margin of 2. |
| `ItemWidth` | `double` | A fixed width for every child's slot; the demo app starts at 100. With 100, a label was stretched to 96. A child 150 wide was given 100 and cut off there: its layout clip was 100 wide, and a hit test 20 past it found nothing. |
| `Background (Panel)` | `Brush` | The fill of the panel, and whether the gaps between children can be clicked. With the default `null`, a hit test in the gap between two labels found nothing; with `Transparent`, it found the panel. |
| `ZIndex (Panel attached)` | `int` | Which child is on top where children overlap. With two labels overlapping by 15, `ZIndex` 1 and 2 put the second on top, and 3 and 2 the first. |

## XAML Example

The following XAML is the `ItemWidth` section of the demo app (`WrapPanelUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and the input behavior of the text box left out and the namespace declarations added:

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

## Common Use Cases

- **Tags and chips:** a short list of labels that wraps to the width of the window.
- **Small galleries:** tiles of the same size with `ItemWidth` and `ItemHeight`.
- **Button groups:** buttons that move to a second row when the window is narrow.

## Tips and Best Practices

- **Disable horizontal scrolling around a horizontal WrapPanel.** With horizontal scrolling allowed, it never wraps.
- **Keep lists in a WrapPanel short.** It creates every item, even as the panel of a ListBox.
- **Choose `ItemWidth` and `ItemHeight` larger than the largest child.** Larger children are cut off, not shrunk.
- **Set `Background="Transparent"` to make the gaps clickable.**

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`WrapPanelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/WrapPanelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with labels like the demo app's (margin 2, border 1). The panels were laid out with `Measure` and `Arrange`. Sizes are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/wrappanel/wrappanel-behavior.svg" alt="Table of WrapPanel results: Horizontal with NaN item sizes by default, the demo's five labels in two rows of 3 and 2 at 150 wide and five columns at 60 high, a short child stretched to the row height only with Stretch, ItemWidth and ItemHeight 100 stretching labels to 96 and cutting off a 150-wide child at 100, three rows in a ScrollViewer and one row when horizontal scrolling is allowed, gaps hit only with a Background, ZIndex ordering overlapping labels, and all 1000 items created in a ListBox" width="1038" height="530" loading="lazy">
  <figcaption>Wrapping, line sizes, <code>ItemWidth</code> and <code>ItemHeight</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [StackPanel](/apps/wpf-standard-control-demo/stackpanel.html) — places children in one line without wrapping.
- [UniformGrid](/apps/wpf-standard-control-demo/uniformgrid.html) — cells of the same size in a fixed number of rows and columns.
- [ScrollViewer](/apps/wpf-standard-control-demo/scrollviewer.html) — decides whether the panel inside it gets a limited width.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View WrapPanel source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/WrapPanelUsage){: target="_blank" rel="noopener noreferrer"}
