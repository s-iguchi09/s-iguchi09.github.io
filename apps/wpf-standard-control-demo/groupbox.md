---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/groupbox.html
title: "GroupBox"
badge: "Display"
lead: "GroupBox draws a border with a header around related controls, such as the fields of one section of a form."
description: "WPF GroupBox measured on .NET 10: when HeaderStringFormat applies, multi-line headers, Padding, what inherits into the content, and the header access key."
---

## Overview

**GroupBox** derives from `HeaderedContentControl`. By default its border is 1 wide in `#FFD5DFE5`, and its `Padding` is 0. To UI Automation it is a `Group` whose name is the header: a GroupBox with the header "Personal information" was reported under that name.

The header takes part in keyboard access. A string header is shown with access keys recognized, so `Header="_Name"` made `N` an access key. Pressing it moved the focus into the group, to the first TextBox of the content, not to the header.

The demo app has sections for the header, `HeaderStringFormat`, the content, and the `Control` properties `Padding`, the font properties, `Background`, `Foreground`, and the border. The "Show Code" link under each section displays its XAML.

## Screen Preview

![groupbox demo screen](/images/wpf-standard-control-demo/groupbox.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Header / HasHeader (HeaderedContentControl)` | `object / bool (ReadOnly)` | The header, and whether there is one. The demo app turns an empty text box into `null`. With `null`, `HasHeader` was `False` and the header area was 0 high. A string with a line break is shown on two lines: the header was 15.96 high for one line and 31.92 for two, so no TextBlock is needed for that. |
| `HeaderStringFormat (HeaderedContentControl)` | `string` | A format for the header. With the demo app's `"Header={0}."`, the header `HEADER` was shown as `Header=HEADER.`. It applies only to headers that are not elements: with a TextBlock as the header, the text stayed `HEADER`. |
| `Content (ContentControl)` | `object` | The single child inside the border. To put several controls in a group, use a panel as the content. |
| `Padding (Control)` | `Thickness` | The space between the border and the content. It works in the default template: with `Padding="20"`, the content moved down by 20. |
| `FontWeight / FontStyle / FontStretch / FontSize / FontFamily (Control)` | font values | The font of the header and the content. `FontSize="20"` set on the GroupBox made both the header's text and a TextBlock in the content 20. |
| `Background / Foreground (Control)` | `Brush` | `Foreground` reaches the text of both the header and the content. `Background` fills the area inside the border, starting at the middle of the header: in a GroupBox whose header was 1 to 27.6 from the top, the painted area started at 13.8. The content lies inside that area, so the background shows behind it. |
| `BorderBrush / BorderThickness (Control)` | `Brush / Thickness` | The color and width of the border. The defaults are `#FFD5DFE5` and 1. |

## XAML Example

The following XAML is the `HeaderStringFormat` section of the demo app (`GroupBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="HeaderForHeaderStringFormatTextBox" Text="HEADER" />

  <GroupBox x:Name="HeaderStringFormatGroupBox"
            Header="{Binding Text, ElementName=HeaderForHeaderStringFormatTextBox}"
            HeaderStringFormat="Header={0}." />
</StackPanel>
```

## Common Use Cases

- **Sections of a form:** personal information, address, and payment in separate groups.
- **Settings dialogs:** related options grouped under one heading.
- **Headers with data:** a header such as "Items (12)" built with `HeaderStringFormat`.

## Tips and Best Practices

- **Give the header an access key.** `Header="_Name"` lets the user jump into the group with the keyboard.
- **Write meaningful headers.** The header becomes the group's name for screen readers.
- **Use `HeaderStringFormat` only with non-element headers.** It is ignored when the header is an element.
- **Expect the GroupBox's `Background` to start at the middle of the header.** The top half of the header stays outside the painted area.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`GroupBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/GroupBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The header was read from the text element in the template's header presenter. The access key was sent through `AccessKeyManager.ProcessKey`, which handles a key pressed with <kbd>Alt</kbd>. Positions are measured from the GroupBox's top-left corner, and sizes are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/groupbox/groupbox-behavior.svg" alt="Table of GroupBox results: it derives from HeaderedContentControl with a 1-wide #FFD5DFE5 border and no padding, a null header takes no height, HeaderStringFormat applies to a string header but not to a TextBlock, a header with a line break is two lines high, Padding 20 moves the content down by 20, FontSize and Foreground reach the header and the content, Background starts at the middle of the header and covers the content, UI Automation reports a Group named after the header, and the header's access key moves the focus to the first TextBox inside" width="1077" height="470" loading="lazy">
  <figcaption>The header, padding, inherited properties, and the access key. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [Expander](/apps/wpf-standard-control-demo/expander.html) — a header and content that the user can fold away.
- [Label](/apps/wpf-standard-control-demo/label.html) — also shows its text with access keys recognized.
- [Grid](/apps/wpf-standard-control-demo/grid.html) — a common content of a GroupBox, to lay out labels and fields.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View GroupBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/GroupBoxUsage){: target="_blank" rel="noopener noreferrer"}
