---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/label.html
title: "Label"
badge: "Display"
lead: "Label shows a caption, usually for an input field, and can move the keyboard focus to that field with an access key."
description: "WPF Label measured on .NET 10: where its access key moves the focus, with or without Target and into a ToolBar, and what Target does not give screen readers."
---

## Where the access key moves the focus

**Label** derives from `ContentControl`. By default it does not take the focus itself: `Focusable` and `IsTabStop` were both `False`, and <kbd>Tab</kbd> from a TextBox before a Label went straight to the TextBox after it. What it does is pass the focus on: `Target` is the element that receives the focus when the Label's access key is pressed.

With `_Name(Press Alt+N)` and `_Age(Press Alt+A)`, processing the access keys `N` and `A` with `AccessKeyManager.ProcessKey` moved the focus to the Name and Age text boxes (a physical key press was not measured). Without `Target`, the access key of `_Plain` left the focus where it was, so set `Target` on every caption with an access key. A different focus scope is not a problem: a Label whose `Target` was a TextBox inside a ToolBar moved the focus into it. Other controls work as targets too: the access key moved the focus to a ComboBox itself, and to the text box inside an editable ComboBox or a DatePicker.

## Target does not name the field for screen readers

The documentation of the `Label` class says that setting the target makes UI Automation use the label's text as the name of the target. On .NET 10 it did not. Read through the UI Automation client API, which screen readers use, the Name TextBox had an empty name and no `LabeledBy`. This matches an open report, [dotnet/wpf#9185](https://github.com/dotnet/wpf/issues/9185){: target="_blank" rel="noopener noreferrer"}.

Name the field separately. Setting `AutomationProperties.LabeledBy` to the Label on the field did work: the Age TextBox was then named "Age(Press Alt+A)".

{% include tables/wpf-standard-control-demo/verification/label/label-behavior.en.md %}

Defaults, access keys, <code>Target</code>, and UI Automation. Measured on .NET 10 / Windows 11.
{: .table-caption}

## The caption itself

A string content is shown with access keys recognized: the character after an underscore becomes the access key, and the underscore itself is hidden. The article linked below covers this, and how to show an underscore. A string with a line break is shown on two lines: the Label was 25.96 high with one line and 41.92 with two. The content can also be any element, such as an icon and text together.

`Padding` defaults to 5 on every side, and `HorizontalContentAlignment` and `VerticalContentAlignment` default to `Left` and `Top`. For text that needs no access key, use a TextBlock; the article linked below measures what many Labels cost.

## Trying it in the demo app

![The Label page of the demo app, with the control list on the left and the first section, Target](/images/wpf-standard-control-demo/label.png){: .screenshot-img}

The Label page of the demo app has sections for `Target`, the content, and the `Control` properties `Padding`, the font properties, `Background`, `Foreground`, the border, and the content alignment. The `Target` section has the two pairs `_Name(Press Alt+N)` and `_Age(Press Alt+A)`, and the content section binds the caption to a multi-line text box, so a line break in the text gives a Label of two lines. The "Show Code" link under each section displays its XAML. The following XAML is the `Target` section (`LabelUsageControl.xaml`), with the styles and the surrounding GroupBox left out and the namespace declarations added:

```xml
<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="auto" />
    <ColumnDefinition Width="*" />
  </Grid.ColumnDefinitions>
  <Grid.RowDefinitions>
    <RowDefinition />
    <RowDefinition />
  </Grid.RowDefinitions>
  <Label x:Name="TargetNameLabel" Grid.Row="0" Grid.Column="0"
         Content="_Name(Press Alt+N)"
         Target="{Binding ElementName=TargetNameTextBox}" />
  <TextBox x:Name="TargetNameTextBox" Grid.Row="0" Grid.Column="1" />
  <Label x:Name="TargetAgeLabel" Grid.Row="1" Grid.Column="0"
         Content="_Age(Press Alt+A)"
         Target="{Binding ElementName=TargetAgeTextBox}" />
  <TextBox x:Name="TargetAgeTextBox" Grid.Row="1" Grid.Column="1" />
</Grid>
```

## Related controls and articles

- [TextBlock](/apps/wpf-standard-control-demo/textblock.html) — lighter text without access keys or a template.
- [TextBox](/apps/wpf-standard-control-demo/textbox.html) — the most common `Target` of a Label.
- [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/) — access keys in a Label's text, and showing an underscore.
- [Why WPF Slows Down with Many Labels and When to Switch to TextBlock](/articles/wpf-label-vs-textblock-performance/) — the cost of many Labels compared with TextBlocks.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`LabelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/LabelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Access keys were sent through `AccessKeyManager.ProcessKey`, which processes the given access key as if its key-down had been passed to the access key manager (an API call, not a physical <kbd>Alt</kbd>+key press), and <kbd>Tab</kbd> through WPF's input manager, the same path as keys typed on a keyboard. UI Automation names were read through the UI Automation client API, which screen readers also use, from a thread other than the window's UI thread in the same process. Sizes are the values on the measuring machine.

[View Label source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/LabelUsage){: target="_blank" rel="noopener noreferrer"}
