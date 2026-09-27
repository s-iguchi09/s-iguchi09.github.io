---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/tooltip.html
title: "ToolTip"
badge: "Overlays"
lead: "A ToolTip appears after the pointer rests on an element, or when the element gets the keyboard focus. Its timing and position are set through the ToolTipService attached properties."
description: "WPF ToolTip measured on .NET 10 with a real mouse and keyboard: timing defaults, placement, keyboard focus, disabled elements, and when a tooltip closes."
---

## When a tooltip opens, and when it closes

Setting `ToolTip="text"` on an element is enough; WPF wraps the string in a **ToolTip** when it opens. It does so each time: opening the same string tooltip twice gave two different ToolTip instances.

The timing is set through the `ToolTipService` attached properties, and the defaults are not the numbers often quoted. `InitialShowDelay`, the wait before the tooltip opens, was 1000 ms. `BetweenShowDelay`, the time after one tooltip closes during which the next opens without the initial delay, was 100 ms. `ShowDuration` was `Int32.MaxValue`, so a tooltip does not close by itself while the pointer stays. With `ShowDuration="1000"`, it was closed 2.5 seconds after it opened, with the pointer still on the button.

With a real mouse, a tooltip with `InitialShowDelay` 500 opened after about 600 to 700 ms across runs (700 ms in the run in the table below) and one with 2000 after about 2050 ms.

## Keyboard focus and disabled elements

Keyboard users get tooltips too. When a real <kbd>Tab</kbd> key moved the focus to a button, with the mouse pointer resting on another button, its tooltip opened with `ShowsToolTipOnKeyboardFocus` unset (the default, `null`) or `True`, and not with `False`. Leave it on.

A disabled element shows no tooltip unless `ShowOnDisabled` is `True`: over a disabled button, the tooltip did not open, and with `ShowOnDisabled="True"` it opened after about 350 ms (the buttons measured for this had `InitialShowDelay` set to 300, to shorten the wait). Set it when the tooltip should explain why a control is disabled.

## Where it appears

`Placement` defaults to `Mouse`: the tooltip's top-left was 17 below the pointer. With `Bottom`, it was at the bottom-left corner of the button, 30 below its top, wherever the pointer was, so use `Bottom` for a position that does not depend on the pointer. `HorizontalOffset` and `VerticalOffset` shift it: with `HorizontalOffset` 50, the tooltip moved from (0, 30) to (50, 30) from the button's corner.

`PlacementTarget` and `PlacementRectangle` are the element, or a rectangle, that the placement is measured from. The Popup page measures the same properties on a Popup.

The default of the attached `HasDropShadow`, read on a button, was `False`. Whether a shadow is actually drawn was not measured.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tooltip/tooltip-behavior.svg" alt="Table of ToolTip results: defaults of 1000 ms initial delay, Int32.MaxValue duration, 100 ms between delay, Mouse placement, null keyboard setting, ShowOnDisabled and HasDropShadow both False, a tooltip opening after about 700 and 2050 ms for delays of 500 and 2000, a ShowDuration of 1000 closing it, a new ToolTip instance for each opening, a disabled button showing a tooltip only with ShowOnDisabled, Bottom and Mouse positions with a 50 offset, and keyboard focus opening the tooltip except with False" width="1116" height="440" loading="lazy">
  <figcaption>Defaults, timing, reuse, disabled elements, placement, and keyboard focus. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The ToolTip page of the demo app, with the control list on the left and the first section, Placement](/images/wpf-standard-control-demo/tooltip.png){: .screenshot-img}

The ToolTip page of the demo app has sections for the placement, with a button for each of eleven `Placement` values, and for its offsets, set from sliders on a button with `Placement="Bottom"`, and its target and rectangle. It also has sections for the keyboard (`True` and `False`), the drop shadow, switched with a check box, the three delays, whose sliders start at 500 (`InitialShowDelay`), 5000 (`ShowDuration`), and 2000 (`BetweenShowDelay`), and the width, height, colors, border, padding, and content alignment of the tooltip. The "Show Code" link under each section displays its XAML. The following XAML is the `InitialShowDelay` section (`ToolTipUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

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

## Related controls and articles

- [Popup](/apps/wpf-standard-control-demo/popup.html) — a floating window that the application opens and closes itself.
- [TextBlock](/apps/wpf-standard-control-demo/textblock.html) — trimmed text whose full content a tooltip can show.
- [Button](/apps/wpf-standard-control-demo/button.html) — a button with only an icon needs a tooltip and a UI Automation name.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ToolTipDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToolTipDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Tooltips were opened by resting the real mouse on buttons in the measuring window, and <kbd>Tab</kbd> was pressed as real keyboard input. Times were taken from the moment the tool moved the cursor onto the button until it saw the tooltip open, checking about every 10 ms, and are rounded to 50 ms; positions are in device-independent pixels; both are the values on the measuring machine.

[View ToolTip source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToolTipUsage){: target="_blank" rel="noopener noreferrer"}
