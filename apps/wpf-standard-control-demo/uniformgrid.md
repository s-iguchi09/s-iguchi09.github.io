---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/uniformgrid.html
title: "UniformGrid"
badge: "Layout"
lead: "UniformGrid places its children in cells of the same size, filling rows from left to right. Only the number of rows or columns is set; there are no row or column definitions."
description: "WPF UniformGrid measured on .NET 10: how many rows and columns it makes, where extra children go, when FirstColumn is reset to 0, and how large each cell is."
---

## Overview

**UniformGrid** is in the `System.Windows.Controls.Primitives` namespace. `Rows`, `Columns`, and `FirstColumn` are 0 by default. For `Rows` and `Columns`, 0 means "work it out"; for `FirstColumn`, it means no empty cells before the first child. With neither `Rows` nor `Columns` set, the demo app's five labels went into 2 rows of 3 columns. A `Collapsed` child takes no cell: with the second of five labels collapsed and `Columns="2"`, the third label moved into the second cell, and 2 rows remained.

Every cell has the same size. Stretched to 300 wide with three columns, the cells were 100 wide even with a child 150 wide in one of them. When the grid is sized to its content, the widest child sets the size of all cells: the same grid in a horizontal StackPanel had cells 150 wide.

The demo app has sections for `Columns`, `Rows`, `FirstColumn`, `Background`, and `ZIndex`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![uniformgrid demo screen](/images/wpf-standard-control-demo/uniformgrid.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Columns` | `int` | The number of columns; rows are added as needed. The demo app starts at 1. In a grid 300 × 200, its five labels went into 5 rows of cells 300 × 40 with `Columns="1"`, and 3 rows of cells 150 × 66.67 with `Columns="2"`. |
| `Rows` | `int` | The number of rows; columns are added as needed. The demo app starts at 1. The five labels went into one row of 5 columns with `Rows="1"`, and 2 rows of 3 columns with `Rows="2"`. When both are set and the children do not fit, the extra children are not dropped. With `Rows="2"`, `Columns="2"`, and five labels, the fifth was placed at (0, 200), below the four cells and outside the grid's 200. |
| `FirstColumn` | `int` | The number of empty cells before the first child. The demo app starts at 1 with `Columns="3"` and nine labels: the first label was in the second cell, (100, 0), the third label started the second row, and the grid had 4 rows. With `FirstColumn` 3 or 4, not less than `Columns`, the grid reset it to 0: after layout the property read 0, the first label was at (0, 0), and the grid had 3 rows. The reset also removes a binding. With `FirstColumn` bound to a text box as in the demo app, typing 4 set it to 0 and removed the binding, and typing 1 afterwards left it at 0. |
| `Background (Panel)` | `Brush` | The fill of the panel, and whether empty cells can be clicked. With two columns and one label, a hit test in the empty cell found nothing with the default `null`, and found the grid with `Transparent`. |
| `ZIndex (Panel attached)` | `int` | Which child is on top where children overlap. In the demo app, the second label has a top margin of -15 and overlaps the first. With `ZIndex` 1 and 2, the second was on top; with 3 and 2, the first. |

## XAML Example

The following XAML is the `FirstColumn` section of the demo app (`UniformGridUsageControl.xaml`), with the styles, the surrounding GroupBoxes, the input behavior of the text boxes, and six of the nine labels left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="FirstColumnTextBox" Text="1" />
  <TextBox x:Name="ColumnsForFirstColumnTextBox" Text="3" />

  <UniformGrid x:Name="FirstColumnUniformGrid"
               Columns="{Binding Text, ElementName=ColumnsForFirstColumnTextBox}"
               FirstColumn="{Binding Text, ElementName=FirstColumnTextBox}">
    <Label BorderBrush="Black" BorderThickness="1" Content="Item1" />
    <Label BorderBrush="Black" BorderThickness="1" Content="Item2" />
    <Label BorderBrush="Black" BorderThickness="1" Content="Item3" />
  </UniformGrid>
</StackPanel>
```

## Common Use Cases

- **Calendars:** seven columns, with `FirstColumn` set to the weekday of the first day of the month.
- **Button pads:** keys of the same size, such as a numeric keypad.
- **Equal columns:** a row of buttons that share the width equally, with `Rows="1"`.

## Tips and Best Practices

- **Set only one of `Rows` and `Columns`.** With both, extra children are placed outside the grid.
- **Keep `FirstColumn` below `Columns`.** Otherwise it is reset to 0, and a binding on it is removed.
- **Collapse children to remove them from the grid.** A collapsed child gives up its cell.
- **Watch for one large child.** In a grid sized to its content, it makes every cell as large.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`UniformGridDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/UniformGridDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with labels like the demo app's (border 1). The grids were laid out with `Measure` and `Arrange`, and rows and columns were counted from the children's positions. Sizes are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/uniformgrid/uniformgrid-behavior.svg" alt="Table of UniformGrid results: in the Primitives namespace with Rows, Columns, and FirstColumn 0, the demo's five labels in 5 by 1, 3 by 2, 1 by 5, 2 by 3, and 2 by 3 layouts, a fifth child placed below a 2 by 2 grid, FirstColumn 1 starting in the second cell and FirstColumn 3 or 4 reset to 0 with 3 columns, removing a binding on it, a collapsed child taking no cell, cells 100 wide when stretched and 150 when sized to a 150-wide child, empty cells hit only with a Background, and ZIndex ordering the demo's overlapping labels" width="1093" height="590" loading="lazy">
  <figcaption>Rows, columns, <code>FirstColumn</code>, and cell size. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [Grid](/apps/wpf-standard-control-demo/grid.html) — rows and columns of different sizes, with spanning.
- [WrapPanel](/apps/wpf-standard-control-demo/wrappanel.html) — wraps children by the available width instead of a fixed count.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View UniformGrid source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/UniformGridUsage){: target="_blank" rel="noopener noreferrer"}
