---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/viewbox.html
title: "Viewbox"
badge: "Layout"
lead: "Viewbox lays out its single child at the child's own size and then scales it to fit the space the Viewbox has."
description: "WPF Viewbox measured on .NET 10: the scale for each Stretch and StretchDirection in a wide and a small area, what UniformToFill cuts off, and the demo start."
---

## The child is laid out at its own size, then scaled

**Viewbox** derives from `Decorator`, so it has one `Child`. It measures the child with unlimited space, Infinity × Infinity, so the child takes its natural size: the demo app's label was 72.34 × 57.96. The Viewbox then scales that size. The child's own `ActualWidth` and `ActualHeight` stay at the natural size; only the drawing is scaled. To read the displayed size, use `TransformToAncestor`, not `ActualWidth`.

The defaults are `Stretch="Uniform"` and `StretchDirection="Both"`. The demo app does not start there: its combo boxes start at the first value of each enumeration, `None` and `UpOnly`, so the label is not scaled until a `Stretch` other than `None` is chosen.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/viewbox/viewbox-behavior.svg" alt="Table of Viewbox results: it derives from Decorator with Uniform and Both as defaults while the demo starts at None and UpOnly, the child is measured with infinite size and the label is 72.34 by 57.96, UniformToFill cuts off what does not fit at the Viewbox's layout height, and a Viewbox in a horizontal StackPanel scales by the height only" width="1108" height="230" loading="lazy">
  <figcaption>Type, defaults, measuring, and clipping. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## The scale for each Stretch

In an area 300 × 100, the demo app's label was scaled by 1.73 in both directions with `Uniform` (fitting the height), by 4.15 in both with `UniformToFill` (filling the width), and by 4.15 across and 1.73 down with `Fill`, which distorts it. `None` left it at 1.

With `UniformToFill`, the part that does not fit is cut off. In a Viewbox 300 × 100, the label became 300 × 240.35 from the top of the Viewbox, and a hit test 5 below the Viewbox's layout height of 100 found nothing: the Viewbox's layout clip was 300 × 100. Do not use `UniformToFill` for content that must be seen whole.

## Enlarge, reduce, or both: StretchDirection

`StretchDirection` decides whether the child may be enlarged, reduced, or both. In the area 300 × 100, where the label is enlarged, `DownOnly` kept every `Stretch` at 1. In an area 40 × 20, where it is reduced, `UpOnly` kept every `Stretch` at 1, and the label was larger than the area. `Both` gave the same scale as the direction that applies. Use `DownOnly` to shrink but never enlarge, for example to keep text from growing in a large window.

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/viewbox/viewbox-matrix.svg" alt="Table of the demo label's scale for each Stretch and StretchDirection in areas 300 by 100 and 40 by 20: None always 1, Fill 4.15 by 1.73 or 0.55 by 0.35, Uniform 1.73 or 0.35, UniformToFill 4.15 or 0.55, with UpOnly keeping 1 in the small area and DownOnly keeping 1 in the large area" width="572" height="320" loading="lazy">
  <figcaption>Scale (x, y) by <code>Stretch</code>, area, and <code>StretchDirection</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## It needs a limited size to scale to

In a horizontal StackPanel 100 high, which gives unlimited width, a `Uniform` Viewbox scaled the label by 1.73, to fit the height alone. Give the Viewbox a limited size in both directions if it should fit both.

## Trying it in the demo app

![The Viewbox page of the demo app, with the control list on the left and the first section, Stretch / StretchDirection](/images/wpf-standard-control-demo/viewbox.png){: .screenshot-img}

The Viewbox page of the demo app has one section, with `Stretch` and `StretchDirection`. The "Show Code" link under the section displays its XAML. The following XAML is that section (`ViewboxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. In the demo app, the Viewbox fills the result area of the window; the measurements on this page gave the Viewbox fixed sizes (300 × 100 and 40 × 20) instead. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="StretchComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=Stretch}"
            SelectedValuePath="Value" SelectedIndex="0" />
  <ComboBox x:Name="StretchDirectionComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=StretchDirection}"
            SelectedValuePath="Value" SelectedIndex="0" />

  <Viewbox x:Name="StretchViewBox"
           Stretch="{Binding SelectedValue, ElementName=StretchComboBox}"
           StretchDirection="{Binding SelectedValue, ElementName=StretchDirectionComboBox}">
    <Label Padding="20" BorderBrush="Black" BorderThickness="1" Content="Item1" />
  </Viewbox>
</StackPanel>
```

## Related controls and articles

- [Image](/apps/wpf-standard-control-demo/image.html) — has its own `Stretch` and `StretchDirection` for pictures.
- [Canvas](/apps/wpf-standard-control-demo/canvas.html) — a fixed-size drawing that a Viewbox can scale.
- [Grid](/apps/wpf-standard-control-demo/grid.html) — resizes its content by giving it more room, not by scaling it.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ViewboxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ViewboxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with a label like the demo app's (padding 20, border 1). The scale is the size of the label as seen from the Viewbox divided by the label's own size. Sizes are the values on the measuring machine.

[View Viewbox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ViewboxUsage){: target="_blank" rel="noopener noreferrer"}
