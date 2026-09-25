---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/canvas.html
title: "Canvas"
badge: "Graphics"
lead: "Canvas places each child at the coordinates given by the attached properties Canvas.Left, Top, Right, and Bottom, at the child's own size."
description: "WPF Canvas measured on .NET 10: which of Left and Right wins, where Right and Bottom measure from, why a Canvas is 0 wide yet shows its children, and ZIndex."
---

## Overview

A **Canvas** measures its children, but with unlimited space: a child was measured with Infinity × Infinity. Each child therefore keeps its own size. In a Canvas 300 wide, a TextBlock was 60.23 wide, and a Border with `HorizontalAlignment="Stretch"` was only as wide as its text, 37.29. A child with no position was placed at (0, 0), and a negative `Canvas.Left` such as -30 put it partly left of the Canvas.

The Canvas itself asks for no space. With a rectangle at (20, 20) inside it, a Canvas in a horizontal StackPanel had a `DesiredSize` of 0 × 0. Its children are still shown, because `ClipToBounds` is `False` by default. In a Canvas 200 wide, a rectangle from 150 to 250 was hit at x=230, outside the Canvas. With `ClipToBounds="True"`, which the demo app sets, nothing was hit there.

The demo app has one section with two rectangles 100 × 100: rectangle A, whose `Top`, `Left`, `Right`, `Bottom`, and `ZIndex` are bound to text boxes, and rectangle B at (50, 50) with its own `ZIndex`. The "Show Code" link under the section displays its XAML.

## Screen Preview

![The Canvas page of the demo app, with the control list on the left and the first section, Top / Left / Right / Bottom / ZIndex(Panel)](/images/wpf-standard-control-demo/canvas.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Canvas.Left / Canvas.Top` | `double` | The distance from the left and top edges. With the demo app's initial `Top` 20 and `Left` 20, rectangle A was at (20, 20), 100 × 100. |
| `Canvas.Right / Canvas.Bottom` | `double` | The distance from the right and bottom edges. They lose to `Left` and `Top`: with all four set to 20, the rectangle stayed at (20, 20) and 100 × 100, neither moved nor stretched. They measure from the Canvas's actual size, so the Canvas needs no `Width`. With only `Right` 20 and `Bottom` 20, in a Canvas stretched to 300 × 200, the rectangle was at (180, 80). In a Canvas of size 0, the same rectangle was at (-120, -120). The demo app binds them with `FallbackValue` NaN, so an empty text box leaves them unset (NaN), and "30" sets 30. |
| `ZIndex (Panel attached)` | `int` | Which child is on top where children overlap. The demo app starts with A at 0 and B at 1; at (85, 85), where they overlap, B was on top. With A at 2, A was on top. With both at 0, B, the later child, was on top. |

## XAML Example

The following XAML is the result area of the demo app (`CanvasUsageControl.xaml`), with the text boxes the rectangles are bound to, and the namespace declarations added. The `TargetNullValue` and `FallbackValue` of the bindings, the styles, the input behavior of the text boxes, and the GroupBoxes are left out, and the surrounding layout is replaced by a `DockPanel` 300 high:

```xml
<DockPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           Height="300">
  <UniformGrid DockPanel.Dock="Top" Columns="6">
    <TextBox x:Name="TopTextBox" Text="20" />
    <TextBox x:Name="LeftTextBox" Text="20" />
    <TextBox x:Name="RightTextBox" Text="" />
    <TextBox x:Name="BottomTextBox" Text="" />
    <TextBox x:Name="ZIndexTextBox" Text="0" />
    <TextBox x:Name="ZIndexTextBoxRed" Text="1" />
  </UniformGrid>

  <Canvas Background="#F0F0F0" ClipToBounds="True">
    <Rectangle x:Name="TargetRect"
               Canvas.Left="{Binding Text, ElementName=LeftTextBox}"
               Canvas.Top="{Binding Text, ElementName=TopTextBox}"
               Canvas.Right="{Binding Text, ElementName=RightTextBox}"
               Canvas.Bottom="{Binding Text, ElementName=BottomTextBox}"
               Width="100" Height="100"
               Panel.ZIndex="{Binding Text, ElementName=ZIndexTextBox}"
               Fill="SkyBlue" Stroke="DodgerBlue" StrokeThickness="2" />
    <Rectangle Canvas.Left="50" Canvas.Top="50"
               Width="100" Height="100"
               Panel.ZIndex="{Binding Text, ElementName=ZIndexTextBoxRed}"
               Fill="LightCoral" Stroke="IndianRed" StrokeThickness="2" />
  </Canvas>
</DockPanel>
```

## Common Use Cases

- **Diagrams:** shapes and connectors at computed coordinates.
- **Drawing surfaces:** shapes the user places or drags.
- **Overlays:** a badge kept at a fixed distance from a corner with `Right` and `Bottom`.

## Tips and Best Practices

- **Give a Canvas a size from its parent or its own `Width` and `Height`.** It asks for 0 × 0, so an auto-sized parent gives it no space.
- **Set `ClipToBounds="True"` to keep children inside.** Otherwise they are drawn, and can be clicked, outside the Canvas.
- **Set either `Left` or `Right`, not both.** `Left` wins, and the child is not stretched.
- **Give children an explicit size if they should fill something.** Alignment does not stretch a child in a Canvas.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`CanvasDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/CanvasDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with rectangles 100 × 100 like the demo app's. The Canvas was laid out with `Measure` and `Arrange`, and the element on top was found by a hit test at the given point. Sizes are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/canvas/canvas-behavior.svg" alt="Table of Canvas results: children measured with infinite size and not stretched, a Canvas with children asking for 0 by 0 with ClipToBounds False, Left and Top winning over Right and Bottom without stretching, Right and Bottom measured from the Canvas's actual size including size 0, an unplaced child at (0, 0), a child outside the Canvas hit only without clipping, ZIndex ordering the demo's rectangles with the later child on top when equal, and an empty bound text giving NaN" width="1179" height="470" loading="lazy">
  <figcaption>Sizes, positions, clipping, and <code>ZIndex</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [Grid](/apps/wpf-standard-control-demo/grid.html) — places children in rows and columns that follow the size of the window.
- [Viewbox](/apps/wpf-standard-control-demo/viewbox.html) — scales its content, such as a Canvas of fixed size, to fit.
- [InkCanvas](/apps/wpf-standard-control-demo/inkcanvas.html) — a surface for drawing with the mouse or a pen.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under the section displays its XAML.

[View Canvas source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/CanvasUsage){: target="_blank" rel="noopener noreferrer"}
