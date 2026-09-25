---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/label.html
title: "Label"
badge: "Display"
lead: "Label shows a caption, usually for an input field, and can move the keyboard focus to that field with an access key."
description: "WPF Label measured on .NET 10: where its access key moves the focus, with or without Target and into a ToolBar, and what Target does not give screen readers."
---

## Overview

**Label** derives from `ContentControl`. It does not take the focus itself: `Focusable` and `IsTabStop` were both `False`, and <kbd>Tab</kbd> from a TextBox before a Label went straight to the TextBox after it. Its default `Padding` is 5 on every side, and its content is aligned `Left` and `Top`.

A string content is shown with access keys recognized: the character after an underscore becomes the access key, and the underscore itself is hidden. The article linked below covers this, and how to show an underscore. A string with a line break is shown on two lines: the Label was 25.96 high with one line and 41.92 with two.

The demo app has sections for `Target`, the content, and the `Control` properties `Padding`, the font properties, `Background`, `Foreground`, the border, and the content alignment. The "Show Code" link under each section displays its XAML.

## Screen Preview

![label demo screen](/images/wpf-standard-control-demo/label.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Target` | `UIElement` | The element that receives the focus when the Label's access key is pressed. In the demo app's two pairs, `_Name(Press Alt+N)` and `_Age(Press Alt+A)`, the access keys `N` and `A` moved the focus to the Name and Age text boxes. Without `Target`, the access key of `_Plain` left the focus where it was. A different focus scope is not a problem: a Label whose `Target` was a TextBox inside a ToolBar moved the focus into it. `Target` did not name the field for screen readers, although the documentation of the `Label` class says that setting the target makes UI Automation use the label's text as the name of the target. Read through the UI Automation client API, which screen readers use, the Name TextBox had an empty name and no `LabeledBy`. This matches an open report, [dotnet/wpf#9185](https://github.com/dotnet/wpf/issues/9185){: target="_blank" rel="noopener noreferrer"}. Setting `AutomationProperties.LabeledBy` by hand did work: the Age TextBox was then named "Age(Press Alt+A)". |
| `Content (ContentControl)` | `object` | The caption. The demo app binds it to a multi-line text box, so a line break in the text gives a Label of two lines. |
| `Padding (Control)` | `Thickness` | The space around the content; the default is 5 on every side. |
| `FontWeight / FontStyle / FontStretch / FontSize / FontFamily (Control)` | font values | The font of the caption. The demo app has a section for each. |
| `Background / Foreground / BorderBrush / BorderThickness (Control)` | `Brush / Thickness` | The colors and the border of the Label. The demo app has a section for each. |
| `HorizontalContentAlignment / VerticalContentAlignment (Control)` | `HorizontalAlignment / VerticalAlignment` | Where the content sits inside the Label; the defaults are `Left` and `Top`. |

## XAML Example

The following XAML is the `Target` section of the demo app (`LabelUsageControl.xaml`), with the styles and the surrounding GroupBox left out and the namespace declarations added:

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

## Common Use Cases

- **Form captions:** a caption next to each field, with an access key that jumps to it.
- **Captions for other controls:** a ComboBox or a DatePicker as the `Target`. The access key moved the focus to a ComboBox itself, and to the text box inside an editable ComboBox or a DatePicker.
- **Captions with content:** an icon and text together, since the content can be any element.

## Tips and Best Practices

- **Set `Target` on every caption with an access key.** Without it, the key does nothing.
- **Name the field for screen readers separately.** Contrary to the documentation, `Target` did not give the TextBox a name. Setting `AutomationProperties.LabeledBy` to the Label on the field gave it the label's text as its name.
- **Use a TextBlock for text that needs no access key.** The article linked below measures what many Labels cost.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`LabelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/LabelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Access keys were sent through `AccessKeyManager.ProcessKey`, which handles a key pressed with <kbd>Alt</kbd>, and <kbd>Tab</kbd> through WPF's input manager, the same path as keys typed on a keyboard. UI Automation names were read through the UI Automation client API, which screen readers also use, from a thread other than the window's UI thread in the same process. Sizes are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/label/label-behavior.svg" alt="Table of Label results: it derives from ContentControl, is not focusable or a tab stop, has padding 5 and Left and Top content alignment, Tab skips it, the demo's access keys N and A move the focus to the Name and Age text boxes, a Label without Target moves nothing, a Target inside a ToolBar receives the focus, the target TextBox gets no UI Automation name or LabeledBy while AutomationProperties.LabeledBy set by hand names it, a line break makes the Label two lines high, and a Target that is a ComboBox receives the focus itself while an editable ComboBox or a DatePicker passes it to the text box inside" width="1108" height="380" loading="lazy">
  <figcaption>Defaults, access keys, <code>Target</code>, and UI Automation. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [TextBlock](/apps/wpf-standard-control-demo/textblock.html) — lighter text without access keys or a template.
- [TextBox](/apps/wpf-standard-control-demo/textbox.html) — the most common `Target` of a Label.
- [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/) — access keys in a Label's text, and showing an underscore.
- [Why WPF Slows Down with Many Labels and When to Switch to TextBlock](/articles/wpf-label-vs-textblock-performance/) — the cost of many Labels compared with TextBlocks.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View Label source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/LabelUsage){: target="_blank" rel="noopener noreferrer"}
