---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/dockpanel.html
title: "DockPanel"
badge: "Layout"
lead: "DockPanel places each child along one of its edges, in order, and by default gives the last child the space that is left."
description: "WPF DockPanel measured on .NET 10: where each Dock puts a child, what LastChildFill does to the last child's Dock, and how child order changes the layout."
---

## Overview

In a **DockPanel**, each child sets the attached `DockPanel.Dock`; a child without it is docked `Left`. Children take their edge in the order they are written, from the space the earlier children left. In a panel 300 × 100 with a top, a bottom, a left, and a content child, the left child was 44.08 high, between the top and the bottom. Written first, the same left child took the full 100 of the height.

`LastChildFill` is `True` by default. The last child then fills the rest, and its own `Dock` is ignored: a last child with `Dock="Top"` still filled the remaining 257.66 × 100 beside the first child.

The demo app has sections for `LastChildFill`, `Dock`, `Background`, and `ZIndex`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![The DockPanel page of the demo app, with the control list on the left and the first section, LastChildFill](/images/wpf-standard-control-demo/dockpanel.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `LastChildFill` | `bool` | Whether the last child fills the remaining space. The demo app binds it to a check box that starts unchecked, so the demo starts at `False`, not at the default `True`. With its two labels in a panel 300 wide, `False` left the second label at its own width, 42.34. With `True`, the second label stretched to 257.66, filling the rest. |
| `DockPanel.Dock (attached)` | `Left / Top / Right / Bottom` | The edge a child is docked to. The demo app shows one label in a panel 100 high with `LastChildFill="False"`. In a panel 300 × 100, `Left` and `Right` gave the label its own width and the full height, at x=0 and x=257.66. `Top` and `Bottom` gave it the full width and its own height, 27.96, at y=0 and y=72.04. |
| `Background (Panel)` | `Brush` | The fill of the panel, and whether its empty space can be clicked. With `LastChildFill="False"` and one child on the left, a hit test in the empty area found nothing with the default `null`, and found the panel with a color. |
| `ZIndex (Panel attached)` | `int` | Which child is on top where children overlap. In the demo app, the second label has a left margin of -30 and overlaps the first. With `ZIndex` 1 and 2, the second label was on top at the overlap; with 3 and 2, the first. |

## XAML Example

The following XAML is the `Dock` section of the demo app (`DockPanelUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="DockComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=Dock}"
            SelectedValuePath="Value"
            SelectedIndex="0" />

  <DockPanel x:Name="DockUsageDockPanel" Height="100" LastChildFill="False">
    <Label Background="LightBlue" BorderBrush="Black" BorderThickness="1"
           Content="Item1"
           DockPanel.Dock="{Binding SelectedValue, ElementName=DockComboBox}" />
  </DockPanel>
</StackPanel>
```

## Common Use Cases

- **Window layouts:** a menu at the top, a status bar at the bottom, a navigation pane on the left, and the content filling the rest.
- **Rows with a stretching field:** a label docked left, a button docked right, and a TextBox as the last child.
- **Panels with a header:** a title docked at the top and the content below it.

## Tips and Best Practices

- **Write the children in the order they should take space.** A side pane written before the top and bottom bars runs the full height.
- **Leave `Dock` off the last child when `LastChildFill` is `True`.** It is ignored.
- **Set `LastChildFill="False"` to dock every child.** The remaining space then stays empty.
- **Give the panel a `Background` if the empty space should receive clicks.**

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`DockPanelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/DockPanelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with labels like the demo app's (border 1) in a panel 300 × 100. The panels were laid out with `Measure` and `Arrange`. Sizes are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/dockpanel/dockpanel-behavior.svg" alt="Table of DockPanel results: LastChildFill is True by default and an undocked child goes Left, the demo's second label stretches only with LastChildFill True, a last child's Dock Top is ignored when it fills, the demo's single label takes full height at Left and Right and full width at Top and Bottom, a left child written first runs the full height, the empty area is hit only with a Background, and ZIndex orders the demo's overlapping labels" width="1210" height="500" loading="lazy">
  <figcaption>Docking, <code>LastChildFill</code>, the order of children, background, and <code>ZIndex</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [Grid](/apps/wpf-standard-control-demo/grid.html) — rows and columns with star sizing, for layouts that DockPanel's order cannot express.
- [StackPanel](/apps/wpf-standard-control-demo/stackpanel.html) — children in one line, without filling the rest.
- [Menu](/apps/wpf-standard-control-demo/menu.html) — often docked at the top of a window.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View DockPanel source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/DockPanelUsage){: target="_blank" rel="noopener noreferrer"}
