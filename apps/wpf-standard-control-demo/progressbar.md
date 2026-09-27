---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/progressbar.html
title: "ProgressBar"
badge: "Display"
lead: "ProgressBar shows how far an operation has gone, as a bar filled in proportion to Value. When the amount of work is unknown, the indeterminate mode shows a moving bar instead."
description: "WPF ProgressBar measured on .NET 10: the fill length, an empty or reversed range, when the indeterminate animation runs, and updates from other threads."
---

## How long the fill is, and what an empty range shows

**ProgressBar** derives from `RangeBase`, the same base class as Slider and ScrollBar. By default `Minimum` is 0, `Maximum` is 100, and `Value` is 0. The fill (the template's `PART_Indicator`) is proportional to (`Value` - `Minimum`) / (`Maximum` - `Minimum`). In a bar 200 wide, `Value` 30 of 0 to 100 gave a fill 60 wide, and `Value` 70 of 20 to 120 gave 100. Setting `Maximum` to the number of items, for example files to copy, lets you add 1 to `Value` per item without computing percentages.

An empty range does not break the bar, but it does not show an empty bar either. With `Minimum` and `Maximum` both 50, the bar was drawn full, with no exception. Setting `Maximum` below `Minimum` is corrected: with `Minimum` 50, `Maximum = 10` became 50, and the bar was again full. A `Value` above `Maximum` is shown as `Maximum`, but the value that was set is kept. `Value = 150` with `Maximum` 100 read as 100, and raising `Maximum` to 200 brought it back to 150.

With `Orientation="Vertical"`, the fill grows from the bottom, as for a level meter: in a bar 20 wide and 200 high, `Value` 75 gave a fill from y=50 to the bottom, 150 high.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/progressbar/progressbar-range.svg" alt="Table of ProgressBar ranges: it derives from RangeBase with defaults 0, 100, and 0, the fill is 60 for 30 of 0 to 100 and 100 for 70 of 20 to 120 in a bar 200 wide, an empty range and a Maximum below Minimum draw a full bar, a Value of 150 reads 100 and returns to 150 when Maximum becomes 200, and a vertical bar fills from the bottom" width="1022" height="320" loading="lazy">
  <figcaption>The range, the fill, and the orientation. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## When the indeterminate animation runs

`IsIndeterminate="True"` shows that work is going on without showing how much, for example while waiting for a server before the total is known. The fill covered the whole bar whatever `Value` was, and the template went to its `Indeterminate` visual state. Its animation ran while the bar was shown. It stopped when the bar was `Collapsed`, and there was none in the normal mode. Collapse the bar when nothing is running.

## Updating from another thread, and what UI Automation sees

A ProgressBar created on the UI thread cannot be updated from another thread: setting `Value` from a worker thread threw `InvalidOperationException`. Report progress with a `Progress<double>` created on the UI thread. When a worker called `Report(40)`, the callback ran on the UI thread and set `Value` to 40.

To UI Automation, the bar is a read-only range. The `RangeValue` pattern reported the value 30 with `IsReadOnly` `True`. In the indeterminate mode, the pattern was not supported, so assistive technology gets no value. Show the numbers in text as well.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/progressbar/progressbar-modes.svg" alt="Table of ProgressBar modes: the indeterminate mode fills the whole bar and enters the Indeterminate state, its animation runs while shown and stops when collapsed, setting Value from a worker thread throws InvalidOperationException, Progress of double calls back on the UI thread, and UI Automation reports a read-only range that is not supported in the indeterminate mode" width="1179" height="230" loading="lazy">
  <figcaption>The indeterminate mode, threads, and UI Automation. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The ProgressBar page of the demo app, with the control list on the left and the first section, IsIndeterminate](/images/wpf-standard-control-demo/progressbar.png){: .screenshot-img}

The ProgressBar page of the demo app has sections for the range, `IsIndeterminate`, and `Orientation`. The range section binds `Minimum`, `Maximum`, and `Value` to text boxes; the `IsIndeterminate` section switches the mode with a check box, on a bar whose `Value` is 50; and the `Orientation` section binds the bar's `Width` to the width of the surrounding Grid, so the vertical bar there is as wide as the whole area. The "Show Code" link under each section displays its XAML. The following XAML is the range section (`ProgressBarUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and the input behavior of the text boxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="MinTextBox" Text="0" />
  <TextBox x:Name="MaxTextBox" Text="100" />
  <TextBox x:Name="ValueTextBox" Text="30" />

  <ProgressBar x:Name="BasicProgressBar"
               Height="20"
               Maximum="{Binding Text, ElementName=MaxTextBox}"
               Minimum="{Binding Text, ElementName=MinTextBox}"
               Value="{Binding Text, ElementName=ValueTextBox}" />
</StackPanel>
```

## Related controls and articles

- [Slider](/apps/wpf-standard-control-demo/slider.html) — a RangeBase the user can drag.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ProgressBarDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ProgressBarDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The fill was read from the bounds of the template's `PART_Indicator`. Whether the indeterminate animation was running was judged by reading the template's elements twice, 300 ms apart, and checking whether anything had changed.

[View ProgressBar source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ProgressBarUsage){: target="_blank" rel="noopener noreferrer"}
