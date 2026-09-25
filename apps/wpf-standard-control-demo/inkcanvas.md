---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/inkcanvas.html
title: "InkCanvas"
badge: "Graphics"
lead: "InkCanvas is a surface to draw on with the mouse or a pen. What is drawn is kept as strokes that can be erased, selected, and saved."
description: "WPF InkCanvas measured on .NET 10 with a real mouse: drawing, erasing by stroke and by point, gestures that drop strokes, SelectAll, and pen settings."
---

## Overview

An **InkCanvas** collects what is drawn in `Strokes`, a `StrokeCollection`. A drag with the real mouse added one stroke. The `Background` property's own default is `null`, but the default style sets it to the system window color (`SystemColors.WindowBrush`), white on the measuring machine, so the InkCanvas is not transparent. Even with `Background` set to `null` a drag still drew a stroke: the InkCanvas takes input over its whole area.

The demo app starts with the pen color `AntiqueWhite` and the background `AliceBlue`, the second item of `Colors` and the first of `Brushes` in its combo boxes. The contrast ratio between these two colors is 1.09 : 1, so pick a darker pen color before drawing. The demo also starts `EditingModeInverted` at `InkAndGesture`, the fourth value of the enumeration, rather than the default.

The demo app has one section with `EditingMode`, `EditingModeInverted`, `ActiveEditingMode`, the pen color and width, `Background`, and Copy, Cut, Paste, Select All, and Clear All buttons. The "Show Code" link under the section displays its XAML.

## Screen Preview

![inkcanvas demo screen](/images/wpf-standard-control-demo/inkcanvas.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `EditingMode` | `None / Ink / GestureOnly / InkAndGesture / Select / EraseByPoint / EraseByStroke` | What the mouse does; the default is `Ink`. A left-to-right drag drew a stroke with `Ink` and nothing with `None`. With `GestureOnly` and `InkAndGesture`, the same drag was recognized as the gesture `Right`, and no stroke remained in either mode. Recognizing gestures needs a recognizer on the machine; `IsGestureRecognizerAvailable` was `True` on the measuring machine. Across a line, `EraseByStroke` removed the whole line and `EraseByPoint` cut it in two. With `Select`, a click on the line selected it. |
| `ActiveEditingMode` | `InkCanvasEditingMode (ReadOnly)` | The mode in use; the demo app shows it. During a mouse drag it was `Ink`, the same as `EditingMode`. |
| `EditingModeInverted` | `InkCanvasEditingMode` | The mode for the inverted end of a pen; the default is `EraseByStroke`. The mouse does not use it: while dragging with the default, `ActiveEditingMode` stayed `Ink`. |
| `DefaultDrawingAttributes` | `DrawingAttributes` | The pen for new strokes: by default black, about 2 × 2 (exactly 2.0031496062992127 in both directions), with an ellipse tip, and not a highlighter. Each stroke gets its own copy. After the pen's `Color` and `Width` were changed in place, as the demo app does, the first stroke stayed black and about 2 wide, and the next one was red and 10 wide. |
| `Strokes` | `StrokeCollection` | The strokes drawn. Two strokes saved in ISF (Ink Serialized Format) with `Save` took 66 bytes and loaded back as two strokes. The demo app's Clear All button calls `Strokes.Clear()`. |
| `Background` | `Brush` | The fill behind the strokes; the default style sets it to the system window color, white on the measuring machine. A `null` background still takes input (see above). |

The demo app's buttons send `ApplicationCommands` to the InkCanvas. `SelectAll` depended on the mode and the strokes: in the `Ink` mode it could not execute even with a stroke, and in the `Select` mode it could not with no strokes, could with one, and selected it. After that, `Copy` could execute. `Undo` could not execute: the InkCanvas does not handle it.

## XAML Example

The following XAML is part of the demo app's section (`InkCanvasUsageControl.xaml`), with the styles, the pen settings, the other buttons, and the surrounding GroupBoxes left out and the namespace declarations added. The InkCanvas is given a height of 200 in place of the demo app's layout. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="EditingModeComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=InkCanvasEditingMode}"
            SelectedValuePath="Value"
            SelectedIndex="1" />
  <ComboBox x:Name="EditingModeInvertedComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=InkCanvasEditingMode}"
            SelectedValuePath="Value"
            SelectedIndex="3" />
  <ComboBox x:Name="BackgroundBrushComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:StaticBindingSource TargetType=Brushes}"
            SelectedValuePath="Value"
            SelectedIndex="0" />

  <Button Command="ApplicationCommands.SelectAll"
          CommandTarget="{Binding ElementName=DemoInkCanvas}"
          Content="Select All" />

  <InkCanvas x:Name="DemoInkCanvas"
             Height="200"
             Background="{Binding SelectedValue, ElementName=BackgroundBrushComboBox}"
             EditingMode="{Binding SelectedValue, ElementName=EditingModeComboBox}"
             EditingModeInverted="{Binding SelectedValue, ElementName=EditingModeInvertedComboBox}">
    <InkCanvas.DefaultDrawingAttributes>
      <DrawingAttributes Color="Black" />
    </InkCanvas.DefaultDrawingAttributes>
  </InkCanvas>
</StackPanel>
```

## Common Use Cases

- **Signatures:** an area to sign in, saved as ISF.
- **Annotations:** an InkCanvas without a background over a picture, to mark it up.
- **Sketches:** a drawing pad with a pen, an eraser, and a selection mode.

## Tips and Best Practices

- **Check the pen color against the background.** The demo app's starting colors have a contrast ratio of only 1.09 : 1.
- **Switch to `Select` before running `SelectAll`.** In the `Ink` mode the command was disabled.
- **Keep strokes that are not meant as gestures.** A straight drag was taken as a gesture and not kept as ink. In the `InkAndGesture` mode, setting `Cancel` to `True` in the `Gesture` handler kept it as a stroke, and so did limiting the recognized gestures with `SetEnabledGestures` to a circle only.
- **Change a stroke's own `DrawingAttributes` to recolor it.** Changing the default pen affects only later strokes.
- **Keep your own history for undo.** `ApplicationCommands.Undo` is not handled.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`InkCanvasDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/InkCanvasDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, on an InkCanvas 300 × 200. Strokes were drawn, erased, and selected by dragging and clicking with the real mouse; a pen was not used. Copy, Cut, and Paste were not run, so that the measuring machine's clipboard was left alone; only whether they could execute was read. The demo app's starting values were found the same way its markup extensions list them.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/inkcanvas/inkcanvas-behavior.svg" alt="Table of InkCanvas results: defaults are Ink, EraseByStroke and the system window color (white) from the default style with a black pen about 2 wide, the demo starts with Ink, InkAndGesture, AntiqueWhite on AliceBlue at a contrast of 1.09 to 1, a real drag draws one stroke even with a null background, each stroke keeps a copy of the pen, None and the gesture modes keep no stroke, the gesture modes recognizing Right, EraseByStroke removes the line and EraseByPoint splits it, a click selects it, SelectAll cannot execute in Ink mode and needs a stroke in Select mode, Undo cannot execute, and two strokes saved as ISF take 66 bytes" width="1242" height="710" loading="lazy">
  <figcaption>Defaults, the demo's start, drawing, modes, and commands. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [Canvas](/apps/wpf-standard-control-demo/canvas.html) — a panel that places elements at coordinates, without ink.
- [Image](/apps/wpf-standard-control-demo/image.html) — a picture that an InkCanvas can be laid over for annotations.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under the section displays the XAML of that section.

[View InkCanvas source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/InkCanvasUsage){: target="_blank" rel="noopener noreferrer"}
