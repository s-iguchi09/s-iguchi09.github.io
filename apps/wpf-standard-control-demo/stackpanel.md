---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/stackpanel.html
title: "StackPanel"
badge: "Layout"
lead: "StackPanel places its children one after another in a single row or column. It is the simplest panel, and the one whose sizing most often surprises."
description: "WPF StackPanel measured on .NET 10: the unlimited size it gives children, what happens when they do not fit, why empty areas ignore clicks, and ZIndex limits."
---

## Unlimited space in the stacking direction

A **StackPanel** gives each child unlimited space in the stacking direction. In a vertical panel 200 × 100, a child was measured with 200 × Infinity; in a horizontal one, with Infinity × 100. In the other direction the child is stretched: a child that wanted 40 × 20 was arranged 200 wide in the vertical panel and 100 high in the horizontal one.

## Why a list in a vertical StackPanel stops scrolling

Because of the unlimited space, a list in a vertical StackPanel does not get a limited height. A ListBox of 1000 items in an area 200 high was 200 high, could scroll by 991, and created 10 items. Put inside a vertical StackPanel in the same area, it grew to 19964 high, could not scroll, and created all 1000 items. The article linked below shows the same for a ScrollViewer. Give a list that should scroll a limited height instead, for example a Grid row.

A StackPanel itself does not virtualize either. With 1000 children in a StackPanel inside a ScrollViewer 200 high, all 1000 were measured, while a ListBox with a limited height created only the items it showed. Use a ListBox rather than a StackPanel for long lists.

## Children never wrap, and the overflow is cut off

The default `Orientation` is `Vertical`, and children never wrap. With the demo app's three labels in a horizontal StackPanel 80 wide, the third label started at 94.69, past the panel's edge. The part beyond the edge is cut off: `ClipToBounds` was `False`, but WPF's layout clip limited the panel to its 80, and a hit test on the third label beyond that edge found nothing. Use WrapPanel to wrap. There is also no `Spacing` property; space children with their `Margin`.

## The empty area and ZIndex

The demo app shows an empty StackPanel 30 high. With the default `Background` of `null`, a hit test in its middle found nothing, so clicks there go to what is behind it. With `Transparent` or a color, the same hit test found the panel.

Where children overlap, `Panel.ZIndex` decides which one is drawn on top. In the demo app, the second label overlaps the first by 15. With `ZIndex` 1 and 2, the second label was on top at the overlap; with 3 and 2, the first. It only orders children of the same panel: a child of an inner panel with `ZIndex="100"` stayed under a sibling of that inner panel whose `ZIndex` was 0.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/stackpanel/stackpanel-behavior.svg" alt="Table of StackPanel results: Vertical by default with no Spacing property, children measured with infinite size in the stacking direction and stretched across it, overflowing labels cut off at the panel's edge by the layout clip, all 1000 children measured in a ScrollViewer, a 1000-item ListBox that stops scrolling and creates every item inside a StackPanel, an empty area hit only with a Background, the demo's overlapping labels ordered by ZIndex, and ZIndex not reaching outside the child's own panel" width="1077" height="500" loading="lazy">
  <figcaption>Sizes given to children, overflow, background, and <code>ZIndex</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The StackPanel page of the demo app, with the control list on the left and the first section, Orientation](/images/wpf-standard-control-demo/stackpanel.png){: .screenshot-img}

The StackPanel page of the demo app has three sections: `Orientation`, `Background`, and `ZIndex`. The "Show Code" link under each section displays its XAML. The following XAML is the `ZIndex` section (`StackPanelUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="ZIndexItem1Text" Text="1" />
  <TextBox x:Name="ZIndexItem2Text" Text="2" />

  <StackPanel x:Name="ZIndexStackPanel">
    <Label VerticalAlignment="Top"
           Panel.ZIndex="{Binding Text, ElementName=ZIndexItem1Text}"
           Background="LightBlue" BorderBrush="Black" BorderThickness="1"
           Content="Item1" />
    <Label Margin="0,-15,0,0" VerticalAlignment="Top"
           Panel.ZIndex="{Binding Text, ElementName=ZIndexItem2Text}"
           Background="SkyBlue" BorderBrush="Black" BorderThickness="1"
           Content="Item2" />
  </StackPanel>
</StackPanel>
```

## Related controls and articles

- [WrapPanel](/apps/wpf-standard-control-demo/wrappanel.html) — stacks children and wraps them to the next line.
- [DockPanel](/apps/wpf-standard-control-demo/dockpanel.html) — places children along the edges, with the last one filling the rest.
- [Grid](/apps/wpf-standard-control-demo/grid.html) — rows and columns with limited sizes.
- [Why a WPF ScrollViewer Does Not Scroll and How to Fix It](/articles/wpf-scrollviewer-not-scrolling/) — what the unlimited height of a StackPanel does to a ScrollViewer.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`StackPanelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/StackPanelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The panels were laid out with `Measure` and `Arrange`, and the element on top was found by a hit test at the given point. Sizes are the values on the measuring machine.

[View StackPanel source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/StackPanelUsage){: target="_blank" rel="noopener noreferrer"}
