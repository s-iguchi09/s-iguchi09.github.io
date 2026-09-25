---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/popup.html
title: "Popup"
badge: "Overlays"
lead: "Popup shows its Child in a separate window that floats above the application, positioned relative to another element. ComboBox and Menu use one for their drop-downs."
description: "WPF Popup measured on .NET 10: where each Placement puts the child, what happens at the screen edge, the real StaysOpen default, and AllowsTransparency."
---

## Overview

**Popup** derives from `FrameworkElement`. When it is open, its `Child` is not drawn in the window that holds the Popup. It had its own window handle, and its parent was an internal decorator under the popup's own root. Because it is a window of its own, the child can extend beyond the application window, and even over the taskbar. It also does not follow the application window: when the window was moved by (40, 40) with the popup open, the child did not move.

The defaults are `IsOpen` `False`, `StaysOpen` `True`, `AllowsTransparency` `False`, `Placement` `Bottom`, and `PopupAnimation` `None`. `IsOpen` binds two-way by default, so a check box bound to it is cleared when the popup closes by itself.

The default templates of ComboBox and of a top-level MenuItem contain a Popup named `PART_Popup`.

The demo app has four sections: `IsOpen`, `StaysOpen`, and `AllowsTransparency`; the placement and offsets; `PopupAnimation`; and `Child` with `PlacementRectangle`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![popup demo screen](/images/wpf-standard-control-demo/popup.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `IsOpen` | `bool` | Whether the popup is shown. The demo app binds it to a check box. |
| `StaysOpen` | `bool` | Whether the popup stays open when the user clicks elsewhere. The default is `True`. With `False`, a real mouse click in an empty part of the window closed the popup, and the check box bound to `IsOpen` was cleared. With `True`, the same click left it open, so something else has to close it. |
| `AllowsTransparency` | `bool` | Whether the popup's window can be transparent. With `True`, the popup's window had the layered style (`WS_EX_LAYERED`); with `False`, it did not. It also decides whether `PopupAnimation` works (see below). |
| `Placement` | `PlacementMode` | Where the child goes relative to the target. With a target 150 × 30 and a child 120 × 40, the child's top-left was at (0, 30) from the target's for `Bottom`, (0, -40) for `Top`, (150, 0) for `Right`, (-120, 0) for `Left`, and (15, -5), the centers aligned, for `Center`. Near a screen edge, WPF repositions a popup that would not fit, and how depends on the `Placement` value and on which edge it reaches. In the measurement, with the target 10 above the bottom of the screen, `Bottom` put the child above the target. With the target 10 above the top of the taskbar, the child still went below, over the taskbar. `Absolute` without a `PlacementRectangle` put the child at (0, 0) on the screen. |
| `HorizontalOffset / VerticalOffset` | `double` | A shift added after placement. Positive values moved the child right and down for every placement measured, `Top` and `Left` included. With (20, 10), `Top` gave (20, -30), 10 closer to the target. |
| `PopupAnimation` | `None / Fade / Slide / Scroll` | The animation when the popup opens. It needs `AllowsTransparency="True"`. With `Fade` and `True`, the opacity of the popup's root was still partway shortly after opening (0.48 about 100 ms after opening in the measured run; the value varies from run to run) and 1 about half a second later. With `False`, it was 1 from the start, so there was no fade. The demo app sets `AllowsTransparency="True"` on this popup. |
| `Child / PlacementRectangle` | `UIElement / Rect` | The content, and a rectangle used in place of the target's bounds. The demo app's popup for this section has no `PlacementTarget`. Without one, the element that holds the Popup is used: a Popup in a Grid 200 × 60 with `Bottom` put its child at (0, 60) from the Grid. With the demo's `PlacementRectangle` (0, 0, 100, 50), it was at (0, 50), below the rectangle. |

## XAML Example

The following XAML is the first section of the demo app (`PopupUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsOpenCheckBox" Content="IsOpen" />
  <CheckBox x:Name="StaysOpenCheckBox" Content="StaysOpen" IsChecked="True" />
  <CheckBox x:Name="AllowsTransparencyCheckBox" Content="AllowsTransparency" IsChecked="True" />

  <Grid>
    <Button x:Name="BasicPlacementTarget" Content="Placement Target" />
    <Popup x:Name="BasicPopup"
           AllowsTransparency="{Binding IsChecked, ElementName=AllowsTransparencyCheckBox}"
           IsOpen="{Binding IsChecked, ElementName=IsOpenCheckBox}"
           Placement="Bottom"
           PlacementTarget="{Binding ElementName=BasicPlacementTarget}"
           StaysOpen="{Binding IsChecked, ElementName=StaysOpenCheckBox}">
      <Border Background="White" BorderBrush="Gray" BorderThickness="1">
        <TextBlock Text="Basic Popup Content" />
      </Border>
    </Popup>
  </Grid>
</StackPanel>
```

## Common Use Cases

- **Drop-down panels:** a ToggleButton that opens a panel of options below itself.
- **Details on demand:** extra information next to an element, closed when the user clicks elsewhere.
- **Custom drop-down controls:** a popup in a control template, as ComboBox and Menu do.

## Tips and Best Practices

- **Set `StaysOpen="False"` for popups that should close on an outside click.** The default is `True`.
- **Close the popup when the window moves.** It does not follow the window.
- **Set `AllowsTransparency="True"` together with `PopupAnimation`.** Without it the animation does not run.
- **Set `PlacementTarget` explicitly.** Without it, the popup is placed relative to whatever element holds it.
- **Do not count on a fixed side.** Near a screen edge, WPF can move the popup: with `Bottom` near the bottom of the screen, it went above the target.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`PopupDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/PopupDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Positions are the difference between the screen positions of the child and of the target, in device-independent pixels. The click outside the popup was made with the real mouse, in an empty part of the measuring window.

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/popup/popup-placement.svg" alt="Table of Popup positions for a target 150 by 30 and a child 120 by 40: Bottom (0, 30), Top (0, -40), Right (150, 0), Left (-120, 0), Center (15, -5), positive offsets move right and down, near the bottom of the work area the child goes over the taskbar, near the bottom of the screen it moves above the target, Absolute without a rectangle is at (0, 0), and without a PlacementTarget the Grid that holds the Popup is used" width="818" height="380" loading="lazy">
  <figcaption>Where the child appears for each placement. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/popup/popup-behavior.svg" alt="Table of Popup behavior: it derives from FrameworkElement with StaysOpen True by default, the child is in a separate window, ComboBox and MenuItem templates contain PART_Popup, StaysOpen False closes on an outside click and clears the bound check box, StaysOpen True does not, the child does not move with the window, and only AllowsTransparency True makes a layered window and lets Fade animate" width="1085" height="320" loading="lazy">
  <figcaption>Defaults, the separate window, <code>StaysOpen</code>, and transparency. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [ToolTip](/apps/wpf-standard-control-demo/tooltip.html) — shown automatically on hover.
- [ComboBox](/apps/wpf-standard-control-demo/combobox.html) — its drop-down list is a Popup.
- [Menu](/apps/wpf-standard-control-demo/menu.html) — its top-level items open their submenus in a Popup.
- [ToggleButton](/apps/wpf-standard-control-demo/togglebutton.html) — a common way to open and close a Popup.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View Popup source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/PopupUsage){: target="_blank" rel="noopener noreferrer"}
