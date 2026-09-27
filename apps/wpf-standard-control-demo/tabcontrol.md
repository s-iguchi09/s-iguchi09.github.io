---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/tabcontrol.html
title: "TabControl"
badge: "Selectors"
lead: "TabControl shows one page at a time and a strip of tabs to switch between them. Each page is a TabItem."
description: "WPF TabControl measured on .NET 10: where each TabStripPlacement puts the tabs, how ContentStringFormat applies, invalid selections, removed tabs, and keys."
---

## Where the tabs go, and how much room the page keeps

`TabStripPlacement` decides the edge a **TabControl** puts its tabs on; the default is `Top`. In a TabControl 300 × 150, `Top` and `Bottom` put the two tabs side by side, with the content area 294 wide below or above them. `Left` and `Right` stacked the tabs, each about 37 wide and 20 high, with their text still horizontal. The content area narrowed to 255.34.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabcontrol/tabcontrol-placement.svg" alt="Table of tab header and content positions in a TabControl 300 by 150: Top and Bottom place the two headers side by side above or below a content area 294 wide, and Left and Right stack them on one side of a content area 255.34 wide" width="945" height="200" loading="lazy">
  <figcaption>Tab headers and the content area for each <code>TabStripPlacement</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Which tab is selected, and what is ignored

TabControl derives from `Selector`. A new TabControl had `SelectedIndex` -1 until it was shown, and then the first tab was selected. The tabs can be `TabItem` elements in `Items` or come from `ItemsSource`, for example one tab per open document, but not both: setting `ItemsSource` when `Items` already held TabItems, and adding to `Items` when `ItemsSource` was set, both threw `InvalidOperationException`.

An index past the last tab and an item not in the list are ignored rather than rejected. With two tabs, `SelectedIndex = 5` threw nothing and left the index at 0, and setting `SelectedItem` to a TabItem not in the list left the second tab selected. A value below -1 is rejected: `SelectedIndex = -2` threw `ArgumentException`. Check indexes before setting them. With `SelectedIndex = -1`, no page was shown. When the selected tab was removed, the next one was selected: removing the second of three tabs selected the third, at index 1.

The keyboard moves the selection with the focus: with the focus on the first tab's header, the right arrow selected the second tab and focused its header. How the pages' content is created and kept is measured on the TabItem page.

## ContentStringFormat, and a tab's own format

`ContentStringFormat` formats the content of the pages. With `"Format: {0}."`, the page of Tab1 showed "Format: Item1.". A TabItem's own `ContentStringFormat` is used when that tab is selected from the start: Tab2 with `"Own {0}"` showed "Own Item2". Switching to it from Tab1 did not apply it, though: the page showed "Format: Item2.", while `SelectedContentStringFormat` already read `"Own {0}"`. Set one `ContentStringFormat` on the TabControl, or use templates, rather than different formats per TabItem.

`SelectedContent` and `SelectedContentStringFormat` are read-only and give the content and the format of the selected page. With the first tab selected, they were `Item1`, the content before formatting, and `"Format: {0}."`. With no tab selected, `SelectedContent` was `null`.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabcontrol/tabcontrol-behavior.svg" alt="Table of TabControl results: it derives from Selector with Top as the default while the demo starts at Left, the first tab is selected when shown, the demo format shows Format: Item1., a tab's own format applies when selected first but not after switching, an index past the last tab and a foreign SelectedItem are ignored while -2 throws ArgumentException, -1 shows nothing, removing the selected tab selects the next one, mixing Items and ItemsSource throws, and the right arrow selects the next tab" width="1077" height="440" loading="lazy">
  <figcaption>String format, selection, items, and the keyboard. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The TabControl page of the demo app, with the control list on the left and the first section, TabStripPlacement](/images/wpf-standard-control-demo/tabcontrol.png){: .screenshot-img}

The TabControl page of the demo app has sections for `TabStripPlacement`, `ContentStringFormat`, and `SelectedContent` with `SelectedContentStringFormat`. The `TabStripPlacement` combo box starts at `Left`, the first value of the `Dock` enumeration, not at the default `Top`, so set it explicitly if you copy the XAML. The `ContentStringFormat` section uses `"Format: {0}."`, and the last section shows `SelectedContent` and `SelectedContentStringFormat`. The "Show Code" link under each section displays its XAML. The following XAML is the `SelectedContent` section (`TabControlUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TabControl x:Name="SelectedContentTabControl" ContentStringFormat="Format: {0}.">
    <TabItem Content="Item1" Header="Tab1" />
    <TabItem Content="Item2" Header="Tab2" />
  </TabControl>

  <TextBlock x:Name="SelectedContentTextBlock"
             Text="{Binding SelectedContent, ElementName=SelectedContentTabControl}" />
  <TextBlock x:Name="SelectedContentStringFormatTextBlock"
             Text="{Binding SelectedContentStringFormat, ElementName=SelectedContentTabControl}" />
</StackPanel>
```

## Related controls and articles

- [TabItem](/apps/wpf-standard-control-demo/tabitem.html) — one tab and its page, including how page content is created and reused.
- [Expander](/apps/wpf-standard-control-demo/expander.html) — sections that can all be open at once.
- [ListBox](/apps/wpf-standard-control-demo/listbox.html) — another Selector, for a list rather than pages.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TabControlDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TabControlDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with tabs like the demo app's (`Tab1` / `Item1`). The text shown was read from the template's `PART_SelectedContentHost`, and the arrow key was sent through WPF's input manager, the same path as keys typed on a keyboard. Sizes are the values on the measuring machine.

[View TabControl source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TabControlUsage){: target="_blank" rel="noopener noreferrer"}
