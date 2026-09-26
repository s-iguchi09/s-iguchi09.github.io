---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/grid.html
title: "Grid"
badge: "Layout"
lead: "Grid arranges its children in rows and columns. Each row and column is sized by a fixed value, by <code>Auto</code> (the size of its content), or by a star (<code>*</code>) share of the space that remains."
description: "WPF Grid measured on .NET 10: Auto, fixed and star sizes, spans, SharedSizeGroup, ZIndex, where out-of-range indexes go, and when star columns lose their ratio."
---

## How the three sizing modes share the width

A **Grid** defines its rows with `RowDefinition` and its columns with `ColumnDefinition`, and each child picks a cell with the attached properties `Grid.Row` and `Grid.Column` (zero-based). The three sizing modes work together: in a Grid 440 wide with the columns `Auto` \| `80` \| `1*` \| `2*` and 60-wide content in each column, the columns measured 60 / 80 / 100 / 200. The star columns split the remaining 300 in a 1:2 ratio. Widening the same Grid to 740 changed only the star columns, to 200 / 400.

`MinWidth` and `MaxWidth` limit `Auto` columns as well as fixed ones: `MaxWidth="30"` on an `Auto` column with 60-wide content kept the column at 30. When the limits conflict, `MinWidth` wins. The demo app starts with `Width="25" MinWidth="30" MaxWidth="100"`, and the column is 30 wide; `MinWidth="100" MaxWidth="50"` gave 100. Rows follow the same rules. With the demo app's starting values (`Height="20" MinHeight="10" MaxHeight="100"`) the row is 20 high, `Height="5" MinHeight="10"` gave 10, and an `Auto` row with `MaxHeight="15"` and 40-high content was 15 high.

Mixing `Auto` and star definitions can make Grid measure a child twice. In a 2 × 2 Grid whose columns are `Auto`, `*` and rows are `*`, `Auto`, the child in the `Auto`\-column/star-row cell was measured twice on the first layout; with only fixed, only `Auto`, or only star definitions, every child was measured once. This matters for children whose measurement is expensive.

## Where a child ends up

The default of `Grid.Row` and `Grid.Column` is 0, so children that omit them all land in the first cell and overlap. Out-of-range values do not raise an error:

- An index beyond the defined columns or rows places the child in the last one. In a 2 × 2 Grid, `Grid.Column="5" Grid.Row="5"` put the child in the bottom-right cell. The demo app shows this, because its 2 × 2 Grid lets you choose column 2, and the text then stays in the right-hand column.
- A span longer than the remaining columns or rows is cut short. In a three-column Grid, `Grid.Column="1" Grid.ColumnSpan="5"` covered columns 1 and 2; `RowSpan="5"` in a two-row Grid covered both rows.
- A Grid without any definitions is one cell. Every child fills it, and a child with `Grid.Column="1"` was still placed in that single cell.

Only invalid numbers are rejected: a negative index or a span of 0 throws `ArgumentException`. Because everything else is clamped silently, check the children that referred to a `ColumnDefinition` after removing it; they move into the last column without any warning.

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/grid/grid-placement.svg" alt="Table of where children are placed in a 300 by 200 Grid: children without Grid.Row and Grid.Column overlap in the first cell, out-of-range indices and spans are clamped to the last column and row, negative values and a span of 0 throw ArgumentException, and a Grid without definitions places every child in one cell" width="810" height="380" loading="lazy">
  <figcaption>Where children end up in a 300 &times; 200 Grid (x, y, width, height). Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## When star columns keep their ratio

Star columns keep their ratio only when the Grid has a finite width to divide. They do not collapse to zero inside a `StackPanel` or `ScrollViewer`, but they can lose the ratio:

- In a horizontal `StackPanel`, `1*` \| `2*` columns holding 60-wide and 30-wide content became 60 / 30, their content widths.
- A vertical `StackPanel` passes its width down, and the ratio held (133.33 / 266.67).
- In a `ScrollViewer` narrower than the content, the result depends on horizontal scrolling. With it enabled, the columns took their content widths and a scroll bar appeared. With it disabled, the columns kept the 1:2 ratio (20 / 40), which cut off the 60-wide content.

Rows behave the same way vertically. Putting `Auto` rows inside a `ScrollViewer` is not a problem: three 100-high `Auto` rows in a 120-high ScrollViewer made the Grid 300 high, and the vertical scroll bar appeared.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/grid/grid-unbounded.svg" alt="Table of the widths of 1-star and 2-star columns holding 60-wide and 30-wide content in different containers: content widths 60 and 30 in a horizontal StackPanel, a 1 to 2 ratio in a vertical StackPanel and in a wide ScrollViewer, and in a 60-wide ScrollViewer either content widths with a scroll bar or a 20 to 40 ratio without one" width="964" height="320" loading="lazy">
  <figcaption>Star columns and rows by container. None of them collapse to zero. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Aligning columns across separate Grids

`SharedSizeGroup` keeps columns (or rows) that share a group name at the same width across separate Grids. This is how form rows built as individual Grids, or items of an `ItemsControl` that each have their own Grid, keep their label columns aligned. Set `Grid.IsSharedSizeScope="True"` on a common ancestor. With 50-wide and 120-wide content, the two `Auto` columns measured 50 / 120 without the scope and 120 / 120 with it.

A star column in a shared group behaves like `Auto`: it also measured 120 / 120 instead of filling the remaining space. In the demo app, the check box switches the scope on and off, and the text boxes change the content of each Grid.

## Overlap, ZIndex, and the empty area

Several children can share one cell; they overlap. Without `Panel.ZIndex`, the child added later is in front; with `ZIndex="1"`, the earlier child came to the front. `ZIndex` only compares children of the same panel: an element with `ZIndex="100"` inside a nested Grid stayed behind a later sibling of that nested Grid, and came to the front only after the nested Grid itself was given a higher `ZIndex`. This is the mechanism behind an overlay such as a loading indicator placed in the same cell as the content.

The `Background` decides whether the empty parts of the Grid receive the mouse. With the default `null`, a hit test on an empty area of the Grid went through to the element behind it. With `Transparent`, the same hit test found the Grid. Set `Transparent` when the whole area should react to clicks or `MouseEnter` without painting a color.

## Grid lines and nesting

`ShowGridLines` draws lines along the row and column boundaries, which helps you check how cells are sized. The lines are drawn by an extra internal visual (`GridLinesRenderer`), and `ShowGridLines` is the only public property of Grid that concerns them. Their color, thickness, and style cannot be set, so use `Border` elements for lines that are part of the design.

Nesting Grids costs little by itself. For 500 TextBlocks, putting each row in its own nested Grid kept the first layout within about 10% of a flat Grid over repeated runs. Wrapping every TextBlock in three extra Grids, 1,500 in all, cost roughly 10–25% more. `Auto` columns instead of `*` made no measurable difference. Choose the structure that is easiest to read.

## Trying it in the demo app

![The Grid page of the demo app, with the control list on the left and the first section, Column / Row](/images/wpf-standard-control-demo/grid.png){: .screenshot-img}

The Grid page of the demo app has a section for each property above, with input controls that change the value while the result is displayed, and a "Show Code" link that shows the XAML of that section. The following XAML is the `ColumnSpan` / `RowSpan` section (`GridUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. The combo boxes are bound to the spans of the first and second text blocks:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ComboBox x:Name="ColumnSpanComboBox" SelectedIndex="0" SelectedValuePath="Content">
    <ComboBoxItem Content="1" />
    <ComboBoxItem Content="2" />
  </ComboBox>
  <ComboBox x:Name="RowSpanComboBox" SelectedIndex="0" SelectedValuePath="Content">
    <ComboBoxItem Content="1" />
    <ComboBoxItem Content="2" />
  </ComboBox>

  <Grid x:Name="ColumnSpanRowSpanResultGrid">
    <Grid.ColumnDefinitions>
      <ColumnDefinition />
      <ColumnDefinition />
      <ColumnDefinition />
    </Grid.ColumnDefinitions>
    <Grid.RowDefinitions>
      <RowDefinition />
      <RowDefinition />
    </Grid.RowDefinitions>
    <TextBlock Grid.Row="0" Grid.Column="0"
               Grid.ColumnSpan="{Binding SelectedValue, ElementName=ColumnSpanComboBox}"
               Background="CadetBlue" Text="1x1,ColumnSpanSetting" />
    <TextBlock Grid.Row="0" Grid.Column="2"
               Grid.RowSpan="{Binding SelectedValue, ElementName=RowSpanComboBox}"
               Background="SteelBlue" Text="1x3,RowSpanSetting" />
    <TextBlock Grid.Row="1" Grid.Column="0" Background="CornflowerBlue" Text="2x1" />
    <TextBlock Grid.Row="1" Grid.Column="1" Background="LightBlue" Text="2x2" />
  </Grid>
</StackPanel>
```

## Related controls and articles

- [GridSplitter](/apps/wpf-standard-control-demo/gridsplitter.html) — lets the user resize Grid rows or columns by dragging.
- [DockPanel](/apps/wpf-standard-control-demo/dockpanel.html) — docks children to the edges; enough for a window frame with few regions.
- [StackPanel](/apps/wpf-standard-control-demo/stackpanel.html) — stacks children in one direction.
- [UniformGrid](/apps/wpf-standard-control-demo/uniformgrid.html) — makes every cell the same size without row or column definitions.
- [Why a WPF ScrollViewer does not scroll](/articles/wpf-scrollviewer-not-scrolling/) — a ScrollViewer inside a vertical StackPanel never gets a finite height, so it cannot scroll; replacing the StackPanel with a Grid fixes it.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`GridDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/GridDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool.

[View Grid source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/GridUsage){: target="_blank" rel="noopener noreferrer"}
