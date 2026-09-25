---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/slider.html
title: "Slider"
badge: "Inputs"
lead: "Slider lets the user pick a value in a range by dragging a thumb along a track, by clicking the track, or with the keyboard. Tick marks, snapping to them, a highlighted sub-range, and a tooltip with the current value are built in."
description: "WPF Slider measured on .NET 10: range coercion, snapping, keys and the tooltip, a Value binding that updates while dragging, and ticks drawn at both ends."
---

## Overview

**Slider** derives from `RangeBase`, as do `ProgressBar` and `ScrollBar`. `Minimum`, `Maximum`, `Value`, `SmallChange`, and `LargeChange` come from `RangeBase`. A new Slider runs from 0 to 10, and `Value` is a `double`. `Value` binds two-way by default, and its default `UpdateSourceTrigger` is `PropertyChanged`. A bound source property therefore receives every change while the thumb is being dragged, not only when the drag ends or the Slider loses focus.

`Value` always stays inside the range. Setting 150 on a 0–100 Slider gave 100. When `Maximum` was later raised to 200, `Value` went back to 150, because the value that was set is remembered. A `Maximum` below `Minimum` does not raise an error: it is treated as equal to `Minimum`.

Snapping applies only to what the user does. With `IsSnapToTickEnabled="True"`, dragging the thumb, pressing the arrow keys, and clicking the track all snapped the value to a tick. A `Value` set from code was not snapped. In the demo app, each property below has its own section, and the "Show Code" link under each section displays its XAML.

## Screen Preview

![slider demo screen](/images/wpf-standard-control-demo/slider.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Minimum / Maximum / Value (RangeBase)` | `double` | The range and the current value; the defaults are 0, 10, and 0. `Value` is kept inside the range, and `Maximum` is kept at or above `Minimum`, without exceptions. Setting `Minimum="50"` and then `Maximum="10"` gave an effective `Maximum` of 50. Because of this, the order in which you change the two does not matter: moving the range from 0–10 to 100–200 ended in the same state whichever was set first. The correction is not written back to a source: a source value of 150, bound two-way to a 0–100 Slider, stayed 150 while the Slider showed 100. |
| `Orientation` | `Horizontal / Vertical` | The direction of the track; the default is `Horizontal`. At `Value = Maximum`, the thumb was at the right end of a horizontal Slider and at the top of a vertical one. A vertical Slider therefore already puts large values at the top. |
| `SmallChange / LargeChange` | `double` | The step for the arrow keys and the step for Page Up / Page Down and for clicks on the track. The defaults are 0.1 and 1. With 1 and 10 on a Slider at 50, the right and up arrows gave 51, the left arrow 49, Page Up 60, Page Down 40, and a real mouse click on the track right of the thumb 60. That is with `IsMoveToPointEnabled` at its default, `False`. With `True`, the same click moved the thumb to the point clicked instead, and the value became 86.84. Home and End jumped to `Minimum` and `Maximum`. The demo app starts with 1 and 10 on a 0–10 Slider, so one Page Up or one click on the track already moves the thumb across the whole range. |
| `IsDirectionReversed` | `bool` | Reverses the track. At `Value = Maximum`, the thumb moved to the left end of a horizontal Slider and to the bottom of a vertical one. The keys follow the track: the right arrow decreased the value (50 to 49). The tick marks are reversed as well: with `Ticks="1,2"` on 0–10, the marks moved to the right end of the track. Only labels you draw yourself next to the Slider have to be reordered by hand. |
| `TickPlacement` | `None / TopLeft / BottomRight / Both` | Where tick marks are drawn; the default is `None`. Tick marks take space: a horizontal Slider was 18 high with `None`, 24 with `BottomRight`, and 30 with `Both`. Drawing ticks does not snap to them: with only `TickPlacement="BottomRight"`, a drag to 23.4 left the value at 23.4. |
| `TickFrequency` | `double` | The spacing of the tick marks, and of the snap positions when snapping is on; the default is 1. Tick marks are always drawn at `Minimum` and `Maximum`, even when the frequency does not divide the range. With `TickFrequency="30"` on 0–100, marks were drawn at 0, 30, 60, 90, and 100. `Maximum` was also a snap position: a drag to 94 snapped to 90, and a drag to 97 snapped to 100. |
| `IsSnapToTickEnabled` | `bool` | Makes user input snap to the nearest tick; the default is `False`. A drag to 23.4 gave 20 with `TickFrequency="10"` and 23 with `TickFrequency="1"`. The value snaps while the thumb is being dragged, not only when it is released. It also changes the keys and the track: with `TickFrequency="10"`, the right arrow moved 50 to 60 instead of 51, and a click on the track with `LargeChange="7"` moved 50 to 60 instead of 57. A value of 23.4 set from code stayed 23.4. |
| `Ticks` | `DoubleCollection` | Tick positions listed explicitly; when set, they replace `TickFrequency`. The demo app uses `1,3,5,7,9` on a 0–10 Slider, and the marks are drawn at 0, 1, 3, 5, 7, 9, and 10: `Minimum` and `Maximum` are added. With snapping on, they are also snap positions: a drag to 5.8 gave 5, and a drag to 0.2 gave 0. |
| `SelectionStart / SelectionEnd / IsSelectionRangeEnabled` | `double / double / bool` | Highlights a part of the track. With the demo app's values (2 to 8 on 0–10), the highlight was hidden while `IsSelectionRangeEnabled` was `False`, its default. With `True`, it covered the track from 2 to 8. The selection does not limit `Value`: a value of 9.5 could still be set. |
| `AutoToolTipPlacement` | `None / TopLeft / BottomRight` | Shows a tooltip with the current value while the thumb is dragged; the default `None` shows none. The text is a plain number. Slider has no property for its format, as the only related properties are `AutoToolTipPlacement` and `AutoToolTipPrecision`. The number follows the current culture: 1234.56 with one decimal was shown as `1,234.6` with en-US and as `1.234,6` with de-DE. For units such as "%", show the value in a separate `TextBlock` with a `StringFormat`. |
| `AutoToolTipPrecision` | `int` | The number of decimal places in the tooltip; the default is 0. The number is rounded, not truncated: with 0, a value of 33.6 was shown as `34` and 33.4 as `33`. With 2, 33.456 was shown as `33.46`. Only the tooltip is rounded; `Value` keeps its full precision. |
| `Delay / Interval` | `int (ms)` | The repeat timing when the mouse button is held down on the track: the wait before repeating and the time between repeats. The defaults come from the Windows keyboard settings: on the measuring machine, where `SystemParameters.KeyboardDelay` was 1 and `KeyboardSpeed` 31, they were 500 and 33, and the `Delay` default matched (`KeyboardDelay` + 1) × 250. The Slider passes them to the two `RepeatButton`s of its track: with the demo app's 1000 and 50, both buttons had `Delay="1000"` and `Interval="50"`. A click released before `Delay` ran out moved the value once, by `LargeChange`. |

## XAML Example

The following XAML is the `Minimum` / `Maximum` / `Value` section of the demo app (`SliderUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `behaviors` is the prefix for the demo app's own attached behaviors:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:behaviors="clr-namespace:WPFStandardControlDemoApp.Common.Behaviors">
  <TextBox x:Name="MinTextBox"
           behaviors:TextBoxDoubleInputBehavior.IsEnabled="True"
           Text="0" />
  <TextBox x:Name="MaxTextBox"
           behaviors:TextBoxDoubleInputBehavior.IsEnabled="True"
           Text="100" />
  <TextBox x:Name="ValueTextBox"
           behaviors:TextBoxDoubleInputBehavior.IsEnabled="True"
           Text="{Binding Value, ElementName=MinMaxSlider, UpdateSourceTrigger=PropertyChanged}" />

  <Slider x:Name="MinMaxSlider"
          Maximum="{Binding Text, ElementName=MaxTextBox}"
          Minimum="{Binding Text, ElementName=MinTextBox}" />
</StackPanel>
```

The `UpdateSourceTrigger=PropertyChanged` here belongs to the binding on `TextBox.Text`, whose default is `LostFocus`. It makes typing in the text box move the Slider at once. A binding on `Slider.Value` needs no such setting, because its default is already `PropertyChanged`.

## Common Use Cases

- **Volume and balance:** a 0–100 Slider with `SmallChange="1"` and `LargeChange="10"`.
- **Zoom:** a Slider bound to the scale of an image or document.
- **Opacity:** a 0–1 Slider bound to `Opacity` for a live preview, without snapping.
- **Discrete choices:** sizes or levels listed in `Ticks` with `IsSnapToTickEnabled="True"`.

## Tips and Best Practices

- **No `UpdateSourceTrigger` is needed on `Value`.** With default binding settings, the source received the value in the middle of a drag, before the mouse button was released. If the source's setter does heavy work, that work runs on every drag step.
- **Keep the source inside the range yourself.** Slider limits its own `Value` but does not write the corrected value back to a two-way source. The source then holds a value the Slider does not show.
- **Choose `SmallChange` and `LargeChange` for the range.** The defaults (0.1 and 1) suit the default 0–10 range, not a 0–100 one. With snapping on, the arrow keys move by ticks instead.
- **Remember that ticks at the ends are always there.** `Minimum` and `Maximum` get tick marks and are snap positions even when they are not in `Ticks` or on the `TickFrequency` grid.
- **Use a separate readout for formatted values.** The automatic tooltip shows a plain number rounded to `AutoToolTipPrecision`.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`SliderDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/SliderDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Thumb drags were reproduced by raising the same `DragStarted` and `DragDelta` events that the thumb raises for a mouse drag, and keys by sending key events to a displayed window. Clicks on the track were made with the real mouse over the measuring window, at 85% of the track's length, with the button released after about 0.1 s.

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/slider/slider-snap.svg" alt="Table of Slider values after dragging the thumb: 23.4 without snapping or with only TickPlacement, 20 with TickFrequency 10 and 23 with TickFrequency 1, 90 or 100 with TickFrequency 30, 5 and 0 with Ticks 1,3,5,7,9, a value set from code that is not snapped, and a bound source updated during the drag" width="700" height="380" loading="lazy">
  <figcaption>Values after dragging the thumb, with and without snapping. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/slider/slider-ticks-tooltip.svg" alt="Table of tick marks, selection range, and automatic tooltip: tick marks are always drawn at Minimum and Maximum and are mirrored by IsDirectionReversed, tick placement changes the height, the selection range is shown only when enabled, and the tooltip rounds the value to AutoToolTipPrecision" width="1046" height="590" loading="lazy">
  <figcaption>Tick marks (as values, left to right), heights, the selection range, and the automatic tooltip text. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [ProgressBar](/apps/wpf-standard-control-demo/progressbar.html) — also derives from `RangeBase`; it displays a value in a range.
- [RepeatButton](/apps/wpf-standard-control-demo/repeatbutton.html) — the button type that the Slider's track uses for clicks and repeats.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View Slider source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/SliderUsage){: target="_blank" rel="noopener noreferrer"}
