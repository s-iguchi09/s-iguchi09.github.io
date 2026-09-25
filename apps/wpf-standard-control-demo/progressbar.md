---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/progressbar.html
title: "ProgressBar"
badge: "Display"
lead: "ProgressBar shows how far an operation has gone, as a bar filled in proportion to Value. When the amount of work is unknown, the indeterminate mode shows a moving bar instead."
description: "WPF ProgressBar measured on .NET 10: the fill length, an empty or reversed range, when the indeterminate animation runs, and updates from other threads."
---

## Overview

**ProgressBar** derives from `RangeBase`, the same base class as Slider and ScrollBar. By default `Minimum` is 0, `Maximum` is 100, and `Value` is 0. The fill (the template's `PART_Indicator`) is proportional to (`Value` - `Minimum`) / (`Maximum` - `Minimum`). In a bar 200 wide, `Value` 30 of 0 to 100 gave a fill 60 wide, and `Value` 70 of 20 to 120 gave 100.

A ProgressBar created on the UI thread cannot be updated from another thread: setting `Value` from a worker thread threw `InvalidOperationException`. A `Progress<double>` created on the UI thread solves this. When a worker called `Report(40)`, the callback ran on the UI thread and set `Value` to 40.

To UI Automation, the bar is a read-only range. The `RangeValue` pattern reported the value 30 with `IsReadOnly` `True`. In the indeterminate mode, the pattern was not supported, so assistive technology gets no value.

The demo app has sections for the range, `IsIndeterminate`, and `Orientation`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![The ProgressBar page of the demo app, with the control list on the left and the first section, IsIndeterminate](/images/wpf-standard-control-demo/progressbar.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Minimum / Maximum / Value (RangeBase)` | `double` | The range and the current position; the demo app binds all three to text boxes. An empty range does not break the bar. With `Minimum` and `Maximum` both 50, the bar was drawn full, with no exception. Setting `Maximum` below `Minimum` is corrected: with `Minimum` 50, `Maximum = 10` became 50, and the bar was again full. A `Value` above `Maximum` is shown as `Maximum`, but the value that was set is kept. `Value = 150` with `Maximum` 100 read as 100, and raising `Maximum` to 200 brought it back to 150. |
| `IsIndeterminate` | `bool` | Shows that work is going on without showing how much. With `True`, the fill covered the whole bar whatever `Value` was, and the template went to its `Indeterminate` visual state. Its animation ran while the bar was shown. It stopped when the bar was `Collapsed`, and there was none in the normal mode. The demo app switches it with a check box, on a bar whose `Value` is 50. |
| `Orientation` | `Horizontal / Vertical` | The direction of the bar. With `Vertical`, the fill grows from the bottom: in a bar 20 wide and 200 high, `Value` 75 gave a fill from y=50 to the bottom, 150 high. The demo app binds the bar's `Width` to the width of the surrounding Grid, so the vertical bar there is as wide as the whole area. |

## XAML Example

The following XAML is the range section of the demo app (`ProgressBarUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and the input behavior of the text boxes left out and the namespace declarations added:

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

## Common Use Cases

- **File operations:** copying or downloading, with `Maximum` set to the number of files or bytes.
- **Loading of unknown length:** the indeterminate mode while waiting for a server, switched to a normal bar once the total is known.
- **Levels:** a vertical bar for a level that goes up and down, such as a volume meter.

## Tips and Best Practices

- **Report progress with `Progress<T>`.** Create it on the UI thread; its callback then runs there and can set the bar.
- **Set `Maximum` to the number of items.** Adding 1 to `Value` per item then fills the bar without computing percentages.
- **Collapse the bar when nothing is running.** The indeterminate animation runs as long as the bar is shown.
- **Show the numbers in text as well.** In the indeterminate mode, UI Automation has no value to report.
- **Do not rely on an empty range to show an empty bar.** `Minimum` = `Maximum` draws a full bar.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ProgressBarDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ProgressBarDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The fill was read from the bounds of the template's `PART_Indicator`. Whether the indeterminate animation was running was judged by reading the template's elements twice, 300 ms apart, and checking whether anything had changed.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/progressbar/progressbar-range.svg" alt="Table of ProgressBar ranges: it derives from RangeBase with defaults 0, 100, and 0, the fill is 60 for 30 of 0 to 100 and 100 for 70 of 20 to 120 in a bar 200 wide, an empty range and a Maximum below Minimum draw a full bar, a Value of 150 reads 100 and returns to 150 when Maximum becomes 200, and a vertical bar fills from the bottom" width="1022" height="320" loading="lazy">
  <figcaption>The range, the fill, and the orientation. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/progressbar/progressbar-modes.svg" alt="Table of ProgressBar modes: the indeterminate mode fills the whole bar and enters the Indeterminate state, its animation runs while shown and stops when collapsed, setting Value from a worker thread throws InvalidOperationException, Progress of double calls back on the UI thread, and UI Automation reports a read-only range that is not supported in the indeterminate mode" width="1179" height="230" loading="lazy">
  <figcaption>The indeterminate mode, threads, and UI Automation. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [Slider](/apps/wpf-standard-control-demo/slider.html) — a RangeBase the user can drag.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View ProgressBar source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ProgressBarUsage){: target="_blank" rel="noopener noreferrer"}
