---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/repeatbutton.html
title: "RepeatButton"
badge: "Inputs"
lead: "RepeatButton keeps clicking while it is held down. WPF uses it for the arrows and the track of a ScrollBar and for the track of a Slider."
description: "WPF RepeatButton measured on .NET 10 with a real mouse: when clicks repeat, what Delay and Interval default to, and what an invalid bound value turns into."
---

## When the clicks repeat

**RepeatButton** derives from `ButtonBase`. Its `ClickMode` defaults to `Press`, so the first click comes when the mouse button goes down. Holding it down for about a second with `Delay="300"` and `Interval="100"` gave the first click right away, the second about `Delay` later, and the rest about every `Interval` (110 ms on the measuring machine), so do not count on `Interval` being exact. Releasing the button did not add a click. A bound `Command` ran once for every click; clamp the value in the command, or a spinner runs past its range.

The repetition stops when the pointer leaves the button: with the mouse button still held, there was no click while the pointer was outside, and `IsPressed` was `False`. Clicks resumed when the pointer came back. Holding <kbd>Space</kbd> also repeats. A single key-down event, with no key repeat from Windows, gave 8 clicks in a second. UI Automation's `Invoke` gave one click.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/repeatbutton/repeatbutton-repeat.svg" alt="Table of RepeatButton timing: the defaults were Delay 500 and Interval 33 with keyboard delay 1 and speed 31, ClickMode defaults to Press, holding for about a second with Delay 300 and Interval 100 gave 8 clicks with the first at once, the second about 320 ms later and the rest about 110 ms apart, no click on release, one command execution per click, no clicks while the pointer was outside, and 8 clicks for a held Space key" width="998" height="350" loading="lazy">
  <figcaption>Defaults, and clicks while the mouse button or <kbd>Space</kbd> is held. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Delay and Interval come from the user's keyboard settings

The defaults of `Delay` and `Interval` are not fixed numbers. On the measuring machine, where the Windows keyboard repeat delay setting is 1, `Delay` was 500; with the keyboard repeat rate setting at 31, `Interval` was 33. Set both explicitly if the speed matters.

A negative `Delay` set from code threw `ArgumentException`; 0 was accepted. `Interval` must be greater than 0: setting 0 from code threw `ArgumentException`.

## An invalid bound value resets to the default

The demo app binds `Delay` and `Interval` to text boxes. A value that cannot be used does not keep the previous one. After `"200"`, entering `"-1"`, an empty string, or `"abc"` each set `Delay` to the default, 500. Entering `"0"` after `"50"` set `Interval` to the default, 33. Validate the values before they reach the button.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/repeatbutton/repeatbutton-values.svg" alt="Table of RepeatButton values: Delay -1 and Interval 0 throw ArgumentException from code, invalid text bound from a TextBox resets Delay to 500 and Interval to 33, the ScrollBar template has RepeatButtons for LineUp, PageUp, PageDown, and LineDown, and the Slider template has DecreaseLarge and IncreaseLarge" width="983" height="290" loading="lazy">
  <figcaption>Invalid values, and the RepeatButtons in WPF's own templates. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Where WPF itself uses RepeatButton

A vertical `ScrollBar` has four RepeatButtons, with the `LineUp`, `PageUp`, `PageDown`, and `LineDown` commands; a horizontal one has `LineLeft`, `PageLeft`, `PageRight`, and `LineRight`. These are the two arrows and the track on each side of the thumb. A `Slider` has two, `DecreaseLarge` and `IncreaseLarge`, on each side of its thumb; it has no arrow buttons.

## Trying it in the demo app

![The RepeatButton page of the demo app, with the control list on the left and the first section, Delay / Interval](/images/wpf-standard-control-demo/repeatbutton.png){: .screenshot-img}

The RepeatButton page of the demo app binds `Delay` and `Interval` to two text boxes and shows the time of the last click. The "Show Code" link under the section displays its XAML. The following XAML is that section (`RepeatButtonUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and the input behavior of the text boxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="DelayTextBox" Text="100" />
  <TextBox x:Name="IntervalTextBox" Text="100" />

  <RepeatButton x:Name="RepeatButtonResult"
                Command="{Binding ClickCommand}"
                Content="Hold down"
                Delay="{Binding Text, ElementName=DelayTextBox}"
                Interval="{Binding Text, ElementName=IntervalTextBox}" />

  <TextBlock x:Name="ClickDateTimeTextBlock" Text="{Binding ClickDateTimeText}" />
</StackPanel>
```

## Related controls and articles

- [Button](/apps/wpf-standard-control-demo/button.html) — the ButtonBase that clicks once, when the mouse button is released by default.
- [Slider](/apps/wpf-standard-control-demo/slider.html) — its track on each side of the thumb is a RepeatButton.
- [ScrollViewer](/apps/wpf-standard-control-demo/scrollviewer.html) — its scroll bars use RepeatButtons for the arrows and the track.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`RepeatButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/RepeatButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Holding the button was done with the real mouse over the measuring window. The keys were sent through WPF's input manager, the same path as keys typed on a keyboard. Times and defaults are the values on the measuring machine.

[View RepeatButton source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/RepeatButtonUsage){: target="_blank" rel="noopener noreferrer"}
