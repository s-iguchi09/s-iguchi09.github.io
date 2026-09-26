---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/uniformgrid.html
title: "UniformGrid"
badge: "Layout"
lead: "UniformGrid places its children in cells of the same size, filling rows from left to right. Only the number of rows or columns is set; there are no row or column definitions."
description: "WPF UniformGrid measured on .NET 10: how many rows and columns it makes, where extra children go, when FirstColumn is reset to 0, and how large each cell is."
---

## How many rows and columns it makes

**UniformGrid** is in the `System.Windows.Controls.Primitives` namespace. `Rows`, `Columns`, and `FirstColumn` are 0 by default. For `Rows` and `Columns`, 0 means "work it out": with neither set, UniformGrid made a square arrangement: for the demo app's five labels it was 3 rows of 3 columns (cells 100 × 66.67 in a 300 × 200 grid), and the labels filled the first 2 rows, leaving the third row empty. Setting one of them fixes that dimension and adds the other as needed. In a grid 300 × 200, the five labels went into 5 rows of cells 300 × 40 with `Columns="1"`, and 3 rows of cells 150 × 66.67 with `Columns="2"`. With `Rows="1"` they went into one row of 5 columns, and with `Rows="2"` into 2 rows of 3 columns.

A `Collapsed` child takes no cell: with the second of five labels collapsed and `Columns="2"`, the third label moved into the second cell, and 2 rows remained. Collapse a child to remove it from the grid.

## What happens to children that do not fit

Set only one of `Rows` and `Columns`. When both are set and the children do not fit, the extra children are not dropped but placed outside the grid. With `Rows="2"`, `Columns="2"`, and five labels, the fifth was placed at (0, 200), below the four cells and outside the grid's 200.

## When FirstColumn is reset to 0

`FirstColumn` is the number of empty cells before the first child, which is how a calendar starts the first day of the month on its weekday. The demo app starts at 1 with `Columns="3"` and nine labels: the first label was in the second cell, (100, 0), the third label started the second row, and the grid had 4 rows.

Keep `FirstColumn` below `Columns`. With `FirstColumn` 3 or 4, not less than `Columns`, the grid reset it to 0: after layout the property read 0, the first label was at (0, 0), and the grid had 3 rows. The reset also removes a binding. With `FirstColumn` bound to a text box as in the demo app, typing 4 set it to 0 and removed the binding, and typing 1 afterwards left it at 0.

## How large each cell is

Every cell has the same size. Stretched to 300 wide with three columns, the cells were 100 wide even with a child 150 wide in one of them. When the grid is sized to its content, the widest child sets the size of all cells: the same grid in a horizontal StackPanel had cells 150 wide. One large child therefore makes every cell as large.

## Empty cells and ZIndex

With two columns and one label, a hit test in the empty cell found nothing with the default `Background` of `null`, and found the grid with `Transparent`. Where children overlap, `Panel.ZIndex` decides the order. In the demo app, the second label has a top margin of -15 and overlaps the first. With `ZIndex` 1 and 2, the second was on top; with 3 and 2, the first.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/uniformgrid/uniformgrid-behavior.svg" alt="Table of UniformGrid results: in the Primitives namespace with Rows, Columns, and FirstColumn 0, the demo's five labels in 5 by 1, 3 by 2, 1 by 5, and 2 by 3 layouts and, with neither set, a 3 by 3 layout whose third row is empty, a fifth child placed below a 2 by 2 grid, FirstColumn 1 starting in the second cell and FirstColumn 3 or 4 reset to 0 with 3 columns, removing a binding on it, a collapsed child taking no cell, cells 100 wide when stretched and 150 when sized to a 150-wide child, empty cells hit only with a Background, and ZIndex ordering the demo's overlapping labels" width="1187" height="590" loading="lazy">
  <figcaption>Rows, columns, <code>FirstColumn</code>, and cell size. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The UniformGrid page of the demo app, with the control list on the left and the first section, Columns](/images/wpf-standard-control-demo/uniformgrid.png){: .screenshot-img}

The UniformGrid page of the demo app has sections for `Columns`, `Rows`, `FirstColumn`, `Background`, and `ZIndex`. The "Show Code" link under each section displays its XAML. The following XAML is the `FirstColumn` section (`UniformGridUsageControl.xaml`), with the styles, the surrounding GroupBoxes, the input behavior of the text boxes, and six of the nine labels left out and the namespace declarations added:

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

## Related controls and articles

- [Grid](/apps/wpf-standard-control-demo/grid.html) — rows and columns of different sizes, with spanning.
- [WrapPanel](/apps/wpf-standard-control-demo/wrappanel.html) — wraps children by the available width instead of a fixed count.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`UniformGridDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/UniformGridDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with labels like the demo app's (border 1). The grids were laid out with `Measure` and `Arrange`, and rows and columns were counted from the children's positions. Sizes are the values on the measuring machine.

[View UniformGrid source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/UniformGridUsage){: target="_blank" rel="noopener noreferrer"}
