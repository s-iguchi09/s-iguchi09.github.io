---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/toolbar.html
title: "ToolBar"
badge: "Menu"
lead: "ToolBar lays out commands in a strip and moves items that do not fit into an overflow menu. Several toolbars can be arranged in a ToolBarTray."
description: "WPF ToolBar measured on .NET 10: overflow with and without a ToolBarTray, OverflowMode, bands, locking, orientation, and the styles given to items in a toolbar."
---

## Overflow works without a ToolBarTray

**ToolBar** derives from `HeaderedItemsControl`. Items that do not fit are moved to an overflow popup opened by the button at the right end. This does not require a `ToolBarTray`: in a ToolBar on its own, 100 wide, four of five buttons were moved to the overflow.

The attached `ToolBar.OverflowMode` decides where an item goes when space runs out; the default is `AsNeeded`. With five buttons and the second one as the target, the target at width 100 went to the overflow with `AsNeeded`, stayed on the bar with `Never`, and was always in the overflow with `Always`, even at width 400. `Never` is not a way to keep everything visible: with all five buttons set to `Never` in a toolbar 100 wide, nothing overflowed and the buttons took 198.15, so most of them were cut off. Keep `Never` for a few items.

`HasOverflowItems` tells whether the toolbar has any item in its overflow, and the attached `ToolBar.IsOverflowItem` tells it for a single item; both are read-only. `IsOverflowOpen` is whether the overflow popup is open. With nothing in the overflow (width 400), the overflow button was still visible but disabled. Setting `IsOverflowOpen = true` still opened the popup, which had no items to show.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/toolbar/toolbar-overflow-matrix.svg" alt="Table of where the demo app's target button goes in a ToolBar outside a ToolBarTray: at width 100 it is in the overflow with AsNeeded and Always and on the bar with Never, at widths 200 and 400 it is on the bar except with Always" width="882" height="170" loading="lazy">
  <figcaption>The target button of the demo app's overflow section, by toolbar width and <code>OverflowMode</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## The styles items get inside a toolbar

Items placed in a ToolBar receive the toolbar's own styles: a `Button`, a `ToggleButton`, a `ComboBox`, and a `Separator` each had the style stored under `ToolBar.ButtonStyleKey`, `ToggleButtonStyleKey`, `ComboBoxStyleKey`, and `SeparatorStyleKey`. A button in a ToolBar therefore does not use the application's implicit `Button` style; style items through the toolbar's style keys. A Separator in a horizontal toolbar became a vertical line, 1 wide.

The toolbar's own `Background` can be changed directly: set to `LightYellow`, it replaced the default `#FFEEF5FD` in the template's border.

## What a ToolBarTray adds: bands, dragging, and locking

`Band` is the row of a ToolBarTray the toolbar is in, and `BandIndex` its order in that row. With bands 0, 0, 1, 1 and indexes 0, 1, 0, 1, the toolbars were placed at (0, 0), (147.79, 0), (0, 30.96), and (147.79, 30.96). Dragging a toolbar by its thumb changes them: the toolbar at band 1, index 0, dragged 250 to the right and 30 upward, moved to band 0, index 0. Save `Band` and `BandIndex` to restore a user's layout.

`IsLocked` on the tray is inherited by its toolbars. With `True`, each toolbar's thumb was collapsed, so the toolbars cannot be dragged; set it to fix the layout.

The tray's `Orientation` decides the toolbars' orientation. `ToolBar.Orientation` is read-only: in a vertical tray it was `Vertical`, outside a tray `Horizontal`, and setting it threw `InvalidOperationException`. The two cannot be out of step.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/toolbar/toolbar-tray.svg" alt="Table of ToolBar styles and ToolBarTray results: items get the ToolBar style keys, the Background set on a ToolBar reaches its template, bands place toolbars in rows, dragging the thumb changes Band, IsLocked collapses the thumb, and ToolBar.Orientation follows the tray and cannot be set" width="1093" height="380" loading="lazy">
  <figcaption>Item styles, background, bands, locking, and orientation. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The ToolBar page of the demo app, with the control list on the left and the first section, HasOverflowItems (ToolBar) / IsOverflowItem (ToolBar) / IsOverflowOpen (ToolBar) / OverflowMode (ToolBar)](/images/wpf-standard-control-demo/toolbar.png){: .screenshot-img}

The ToolBar page of the demo app has a section for each of the overflow properties (`OverflowMode`, `HasOverflowItems` and `IsOverflowItem`, `IsOverflowOpen`), `Band` and `BandIndex`, and the ToolBarTray's `Background`, `IsLocked`, and `Orientation`. The overflow section uses a ToolBar on its own, 100 to 400 wide, with five buttons and the second one as the target; it shows `HasOverflowItems` and `IsOverflowItem` for the toolbar and the target button, and binds `IsOverflowOpen` to a check box. The band section places four toolbars in a tray. The "Show Code" link under each section displays its XAML. The following XAML is the overflow section (`ToolBarUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and some alignment attributes left out and the namespace declarations added. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <CheckBox x:Name="IsOverflowOpenCheckBox" Content="IsOverflowOpen" />
  <ComboBox x:Name="OverflowModeComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=OverflowMode}"
            SelectedValuePath="Value" SelectedIndex="0" />
  <Slider x:Name="WidthSlider" Minimum="100" Maximum="400" Value="200"
          LargeChange="50" TickFrequency="50" TickPlacement="BottomRight"
          AutoToolTipPlacement="BottomRight" />

  <ToolBar x:Name="OverflowToolBar" HorizontalAlignment="Left"
           Width="{Binding Value, ElementName=WidthSlider}"
           IsOverflowOpen="{Binding IsChecked, ElementName=IsOverflowOpenCheckBox}">
    <Button Content="Item 1" />
    <Button x:Name="OverflowTargetButton" Content="Target Item"
            ToolBar.OverflowMode="{Binding SelectedValue, ElementName=OverflowModeComboBox}" />
    <Button Content="Item 2" />
    <Button Content="Item 3" />
    <Button Content="Item 4" />
  </ToolBar>

  <TextBlock Text="{Binding HasOverflowItems, ElementName=OverflowToolBar}" />
  <TextBlock Text="{Binding (ToolBar.IsOverflowItem), ElementName=OverflowTargetButton}" />
</StackPanel>
```

## Related controls and articles

- [Menu](/apps/wpf-standard-control-demo/menu.html) — commands in a hierarchy of menu items.
- [Button](/apps/wpf-standard-control-demo/button.html) — the most common toolbar item.
- [ToggleButton](/apps/wpf-standard-control-demo/togglebutton.html) — for on/off commands such as bold.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ToolBarDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToolBarDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Dragging a toolbar was reproduced by raising the same `DragStarted`, `DragDelta`, and `DragCompleted` events on its thumb that a mouse drag raises. Sizes and positions are the values on the measuring machine.

[View ToolBar source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToolBarUsage){: target="_blank" rel="noopener noreferrer"}
