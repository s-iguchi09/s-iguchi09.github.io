---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/gridsplitter.html
title: "GridSplitter"
badge: "Resizer"
lead: "GridSplitter is placed in a Grid and lets the user resize the rows or columns next to it, by dragging it or with the arrow keys."
description: "WPF GridSplitter measured on .NET 10: what each ResizeBehavior resizes, why the default Right alignment grows its own column, preview, and the arrow keys."
---

## The splitter rewrites the column definitions

**GridSplitter** derives from `Thumb` (and `Thumb` from `Control`). The drag events `DragStarted`, `DragDelta`, and `DragCompleted`, and the read-only `IsDragging` property, all come from `Thumb`. The splitter does not move by itself: while it is dragged, it rewrites the `Width` of the `ColumnDefinition`s (or the `Height` of the `RowDefinition`s) next to it. After a drag, a pair of star columns keeps star values (for example `227.5*` and `167.5*`), a fixed column gets a new pixel value, and an `Auto` column is converted to a fixed width.

## Which pair of columns changes

`ResizeBehavior` decides which pair of columns (or rows) changes. `PreviousAndNext` moves the boundary between the neighbors of the splitter's column, `CurrentAndNext` resizes the splitter's own column and the next one, and `PreviousAndCurrent` resizes the previous column and its own. `BasedOnAlignment` (the default) chooses from `HorizontalAlignment` when the splitter resizes columns: `Left` behaved like `PreviousAndCurrent`, `Right` like `CurrentAndNext`, and `Center` or `Stretch` like `PreviousAndNext`. When it resizes rows, the documentation says it chooses from `VerticalAlignment` instead; that case was not measured.

The default `HorizontalAlignment` of GridSplitter is `Right`, not `Stretch`, so by default a splitter resizes its own column and the next one. That is what you want when the splitter shares a column with content and sits on its right edge: a right-aligned splitter in column 0 resized columns 0 and 1. It is not what you want when the splitter has a column of its own: dragging it 30 to the right widened its own 5-wide `Auto` column to 35 and left a gap beside the splitter. In a column of its own, set `HorizontalAlignment="Stretch"` (or `Center`) or `ResizeBehavior="PreviousAndNext"`.

In the demo app the splitter sits in its own `Auto` column. There, only `PreviousAndNext` resized the two panes in all nine combinations of star, `Auto`, and fixed neighbors. `CurrentAndNext` widened the splitter's own column. `PreviousAndCurrent` widened the previous column, except when that column was a star column; then it did nothing.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/gridsplitter/gridsplitter-resize-behavior.svg" alt="Table of column widths after dragging a GridSplitter 30 to the right in its own Auto column of a 400-wide Grid, for nine combinations of star, Auto, and 200-wide neighboring columns and the four ResizeBehavior values: PreviousAndNext moves the boundary between the neighbors, BasedOnAlignment and CurrentAndNext widen the splitter's own column to 35, and PreviousAndCurrent does nothing when the previous column is a star column" width="977" height="350" loading="lazy">
  <figcaption>Widths (left / splitter / right) after a 30-unit drag, for each <code>ResizeBehavior</code>. Contents are 60 wide. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Columns or rows

`ResizeDirection` decides whether the splitter resizes columns or rows. The default `Auto` decides from the alignment. A splitter that is not horizontally stretched resizes columns. When both alignments are `Stretch`, the shape decides: a splitter taller than it is wide resized columns, and one wider than it is tall resized rows.

The demo app's section has a vertical splitter spanning all rows and a horizontal splitter spanning all columns. With `Auto`, each resized in its own direction. With `Columns`, the horizontal splitter did nothing, and with `Rows` the vertical one did nothing. The horizontal splitter spans every column, so it sits in column 0 and has no column before it to resize; likewise the vertical splitter spans every row from row 0 and has no row before it.

A misplaced splitter fails silently. A splitter outside a Grid, or one with no column before it under `PreviousAndNext`, raised no exception and changed nothing. If dragging has no effect, check the splitter's column and its `ResizeBehavior`.

## Live resizing or a preview

With the default `ShowsPreview="False"`, the columns change on every drag step: over ten drag steps, the content of the left column was measured again ten times. With `True`, only a preview moves while dragging. The columns kept their widths and their content was not measured at all until the mouse button was released, and then the change was applied in one step. Use it when the panes are expensive to lay out.

`PreviewStyle` is the style of that preview. The preview is a `Control` placed in the adorner layer, and this style is applied to it, so a style for it targets `Control`. If you do not set one, the default style of GridSplitter supplies it, and the preview is a `Rectangle` filled with `#80000000` (black at half opacity). The demo app compares this with a custom style whose template is a `YellowGreen` border. In the demo app's own `ShowsPreview` section the splitter keeps the default alignment, so dragging it also widens the splitter's column.

## Drag steps, the keyboard, and cancelling

`DragIncrement` rounds the drag distance to a multiple of its value; the default is 1. With `DragIncrement="20"`, drags of 9, 11, 27, and 31 moved the boundary by 0, 20, 20, and 40. `KeyboardIncrement` is how far one arrow-key press moves the boundary when the splitter has keyboard focus. The default is 10; one press of the right arrow moved the boundary by 10, and by 25 with `KeyboardIncrement="25"`. A value of 0 throws `ArgumentException` for both.

To turn keyboard resizing off, set `Focusable="False"`: the splitter then did not accept focus (`Focus()` returned `False`), so it cannot receive the arrow keys. Esc cancels a drag: pressing Esc before releasing the mouse button put the columns back to their widths before the drag. `IsDragging`, read-only from `Thumb`, became `True` when the left mouse button was pressed on the splitter and `False` again after `CancelDrag()` was called (whether it changes after Esc was not measured); use it in a trigger to highlight the splitter while it is dragged.

## Limits, and keeping the sizes

Set `MinWidth` on the `ColumnDefinition` (or `MinHeight` on the `RowDefinition`) that the splitter resizes. Dragged 1,000 to the left, the left column shrank to 0; with `MinWidth="50"` on its `ColumnDefinition`, it stopped at 50.

To keep the sizes, bind `Width` with `Mode=TwoWay`. With `TwoWay`, the source property received the new width after a drag (`227.5*`) and the binding stayed. Without a mode, the first drag removed the binding and the source kept its old value.

## Trying it in the demo app

![The GridSplitter page of the demo app, with the control list on the left and the first section, DragIncrement](/images/wpf-standard-control-demo/gridsplitter.png){: .screenshot-img}

The GridSplitter page of the demo app has a section for each property on this page. The `ResizeBehavior` section lines up the four behaviors against nine combinations of the neighboring columns (star, `Auto`, and fixed), so you can drag each one and compare, and another section shows `IsDragging` next to a splitter. The "Show Code" link under each section displays its XAML. The following XAML is the `ResizeDirection` section (`GridSplitterUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and the four labels left out and the namespace declarations added. The combo box lists the values of `GridResizeDirection` and is bound to both splitters. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="ResizeDirectionComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=GridResizeDirection}"
            SelectedValuePath="Value"
            SelectedIndex="0" />

  <Grid Height="100">
    <Grid.RowDefinitions>
      <RowDefinition Height="*" />
      <RowDefinition Height="5" />
      <RowDefinition Height="*" />
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*" />
      <ColumnDefinition Width="5" />
      <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <!-- Labels TopLeft / TopRight / BottomLeft / BottomRight in the four corner cells -->
    <GridSplitter x:Name="VerticalSplitter"
                  Grid.RowSpan="3" Grid.Column="1"
                  Width="5" VerticalAlignment="Stretch" Background="Gray"
                  ResizeBehavior="PreviousAndNext"
                  ResizeDirection="{Binding SelectedValue, ElementName=ResizeDirectionComboBox}" />
    <GridSplitter x:Name="HorizontalSplitter"
                  Grid.Row="1" Grid.ColumnSpan="3"
                  Height="5" HorizontalAlignment="Stretch" Background="Gray"
                  ResizeBehavior="PreviousAndNext"
                  ResizeDirection="{Binding SelectedValue, ElementName=ResizeDirectionComboBox}" />
  </Grid>
</StackPanel>
```

## Related controls and articles

- [Grid](/apps/wpf-standard-control-demo/grid.html) — GridSplitter works only inside a Grid; the sizes it changes are the Grid's row and column definitions.
- [DockPanel](/apps/wpf-standard-control-demo/dockpanel.html) — docks panes to the edges; the user cannot resize them.

## Source code and how it was measured

Every behavior on this page, except the row case described as from the documentation, was measured by running it on .NET 10 / Windows 11, using [`GridSplitterDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/GridSplitterDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Drags were reproduced by raising the same `DragStarted`, `DragDelta`, and `DragCompleted` events that `Thumb` raises for a mouse drag.

[View GridSplitter source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/GridSplitterUsage){: target="_blank" rel="noopener noreferrer"}
