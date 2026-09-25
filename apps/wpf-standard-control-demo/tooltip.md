---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/tooltip.html
title: "ToolTip"
badge: "Overlays"
lead: "A ToolTip appears after the pointer rests on an element, or when the element gets the keyboard focus. Its timing and position are set through the ToolTipService attached properties."
description: "WPF ToolTip measured on .NET 10 with a real mouse and keyboard: timing defaults, placement, keyboard focus, disabled elements, and when a tooltip closes."
---

## Overview

Setting `ToolTip="text"` on an element is enough; WPF wraps the string in a **ToolTip** when it opens. It does so each time: opening the same string tooltip twice gave two different ToolTip instances.

The timing defaults are not the numbers often quoted. `InitialShowDelay` was 1000 ms, `BetweenShowDelay` 100 ms, and `ShowDuration` `Int32.MaxValue`, so a tooltip does not close by itself while the pointer stays. With `ShowDuration="1000"`, it was closed 2.5 seconds later. With a real mouse, a tooltip with `InitialShowDelay` 500 opened after about 600 to 700 ms across runs (700 ms in the run in the table below) and one with 2000 after about 2050 ms.

Keyboard users get tooltips too. When a real <kbd>Tab</kbd> key moved the focus to a button, with the mouse pointer resting on another button, its tooltip opened with `ShowsToolTipOnKeyboardFocus` unset (the default, `null`) or `True`, and not with `False`. A disabled element shows no tooltip unless `ShowOnDisabled` is `True`: over a disabled button, the tooltip did not open, and with `ShowOnDisabled="True"` it opened after about 350 ms (the buttons measured for this had `InitialShowDelay` set to 300, to shorten the wait).

The demo app has sections for the placement and its offsets and target, the keyboard, the drop shadow, the three delays, and the size, colors, border, padding, and content alignment of the tooltip. The "Show Code" link under each section displays its XAML.

## Screen Preview

![The ToolTip page of the demo app, with the control list on the left and the first section, Placement](/images/wpf-standard-control-demo/tooltip.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Placement` | `PlacementMode` | Where the tooltip appears; the default is `Mouse`. The demo app has a button for each of eleven values. With `Mouse`, the tooltip's top-left was 17 below the pointer. With `Bottom`, it was at the bottom-left corner of the button, 30 below its top, wherever the pointer was. |
| `HorizontalOffset / VerticalOffset` | `double` | A shift added to the placement. The demo app sets them from sliders on a button with `Placement="Bottom"`. With `HorizontalOffset` 50, the tooltip moved from (0, 30) to (50, 30) from the button's corner. |
| `PlacementTarget / PlacementRectangle` | `UIElement / Rect` | The element, or a rectangle, that the placement is measured from. The demo app has a section for each. The Popup page measures the same properties on a Popup. |
| `ShowsToolTipOnKeyboardFocus` | `bool?` | Whether the tooltip opens when the element gets the keyboard focus. The demo app shows `True` and `False`. With a real <kbd>Tab</kbd>, `True` and the default `null` opened the tooltip, and `False` did not. |
| `HasDropShadow` | `bool` | Whether the tooltip is shown with a shadow. The default of this attached property, read on a button, was `False`. Whether a shadow is actually drawn was not measured. The demo app switches it with a check box. |
| `InitialShowDelay (ToolTipService)` | `int (ms)` | The wait before the tooltip opens. The default was 1000; the demo app's slider starts at 500. With a real mouse, 500 opened the tooltip after about 600 to 700 ms, depending on the run, and 2000 after about 2050 ms. |
| `ShowDuration (ToolTipService)` | `int (ms)` | How long the tooltip stays open while the pointer stays. The default was `Int32.MaxValue`, so it does not close by itself; the demo app's slider starts at 5000. With 1000, the tooltip was closed 2.5 seconds after it opened, with the pointer still on the button. |
| `BetweenShowDelay (ToolTipService)` | `int (ms)` | The time after one tooltip closes during which the next opens without the initial delay. The default was 100; the demo app's slider starts at 2000. |
| `Width / MaxWidth / Height / MaxHeight (FrameworkElement)` | `double` | The size of the tooltip. The demo app has a section for the width and one for the height. |
| `Background / Foreground / BorderBrush / BorderThickness / Padding / HorizontalContentAlignment / VerticalContentAlignment (Control)` | brushes and layout values | The look of the tooltip. The demo app has a section for each. |

## XAML Example

The following XAML is the `InitialShowDelay` section of the demo app (`ToolTipUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Slider x:Name="ValueInitialShowDelaySlider" Maximum="5000" Minimum="0" Value="500" />

  <Button x:Name="InitialShowDelayToolTipButton"
          Content="Show ToolTip on hover."
          ToolTip="Delayed ToolTip"
          ToolTipService.InitialShowDelay="{Binding Value, ElementName=ValueInitialShowDelaySlider}" />
</StackPanel>
```

## Common Use Cases

- **Buttons with only an icon:** the name of the command.
- **Trimmed text:** the full text of a cell cut off with an ellipsis.
- **Fields:** a short hint about the expected input.

## Tips and Best Practices

- **Do not expect tooltips to close by themselves.** The default `ShowDuration` is `Int32.MaxValue`.
- **Set `ShowOnDisabled="True"` to explain why a control is disabled.** Otherwise the tooltip does not open.
- **Leave keyboard tooltips on.** By default, a tooltip also opens when the element is reached with <kbd>Tab</kbd>.
- **Use `Placement="Bottom"` for a position that does not depend on the pointer.**

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ToolTipDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToolTipDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Tooltips were opened by resting the real mouse on buttons in the measuring window, and <kbd>Tab</kbd> was pressed as real keyboard input. Times were taken from the moment the tool moved the cursor onto the button until it saw the tooltip open, checking about every 10 ms, and are rounded to 50 ms; positions are in device-independent pixels; both are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tooltip/tooltip-behavior.svg" alt="Table of ToolTip results: defaults of 1000 ms initial delay, Int32.MaxValue duration, 100 ms between delay, Mouse placement, null keyboard setting, ShowOnDisabled and HasDropShadow both False, a tooltip opening after about 700 and 2050 ms for delays of 500 and 2000, a ShowDuration of 1000 closing it, a new ToolTip instance for each opening, a disabled button showing a tooltip only with ShowOnDisabled, Bottom and Mouse positions with a 50 offset, and keyboard focus opening the tooltip except with False" width="1116" height="440" loading="lazy">
  <figcaption>Defaults, timing, reuse, disabled elements, placement, and keyboard focus. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [Popup](/apps/wpf-standard-control-demo/popup.html) — a floating window that the application opens and closes itself.
- [TextBlock](/apps/wpf-standard-control-demo/textblock.html) — trimmed text whose full content a tooltip can show.
- [Button](/apps/wpf-standard-control-demo/button.html) — a button with only an icon needs a tooltip and a UI Automation name.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View ToolTip source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToolTipUsage){: target="_blank" rel="noopener noreferrer"}
