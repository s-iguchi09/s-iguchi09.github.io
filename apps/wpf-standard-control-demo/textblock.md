---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/textblock.html
title: "TextBlock"
badge: "Display"
lead: "TextBlock displays read-only text, either a plain string or formatted runs. It is the lightest way to put text on the screen."
description: "WPF TextBlock measured on .NET 10: Text versus Inlines, how each TextWrapping treats a long word, when trimming works, and why the demo overlaps its lines."
---

## Text and Inlines do not mix as one might expect

**TextBlock** derives from `FrameworkElement`, not from `Control`, so it has no template. It does not take the focus (`Focusable` was `False`), and its text cannot be selected; the article linked below shows how to display selectable read-only text.

Text can be given in two ways: `Text` as one string, or `Inlines` such as `Run`, `Bold`, and `Hyperlink` for formatted sentences. A TextBlock whose `Inlines` (a `Run` and a `Bold`) were added before its first layout returned an empty `Text`, before and after layout, so do not rely on `Text` of a TextBlock built that way. Setting `Text` on it afterwards threw no exception; it replaced the formatted runs with a single one.

To combine binding and formatting, bind the `Text` of a `Run` instead: a bound `Run` after a bold "Name: " showed the bound text and followed the source when it changed.

## Long words, limited widths, and padding

`TextWrapping` defaults to `NoWrap`. With "Say Supercalifragilisticexpialidocious now" in a width of 100, whose long word alone is wider than 100, `NoWrap` gave one line cut off at 100. `Wrap` gave 4 lines, breaking the long word itself. `WrapWithOverflow` gave 3 lines and kept the word whole, so it ran past 100 and was cut off there. Use `Wrap` when nothing may run past the edge.

`TextTrimming`, whose default is `None`, ends text that does not fit with an ellipsis, as for a name in a column of fixed width. It only works when the width is limited. With `CharacterEllipsis`, a long text was 100 wide in a Grid 100 wide. In a horizontal StackPanel, which gives unlimited width, it took its full width of 220.43, so nothing was trimmed.

`Padding` defaults to 0. With `Padding="10"`, the TextBlock grew by 20 in each direction, from 61.57 × 15.96 to 81.57 × 35.96.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/textblock/textblock-behavior.svg" alt="Table of TextBlock results: it derives from FrameworkElement, is not focusable, and has no padding by default, Text is empty for inlines added before the first layout and setting it replaces them without an exception, a long word gives one clipped line with NoWrap, 4 lines with Wrap, and 3 clipped lines with WrapWithOverflow, trimming limits the width to 100 in a Grid but not in a StackPanel, and Padding 10 adds 20 in each direction" width="1022" height="290" loading="lazy">
  <figcaption>Text and inlines, wrapping, trimming, and padding. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Why the demo's lines overlap: LineHeight and LineStackingStrategy

`LineHeight` sets the height of each line, and `LineStackingStrategy` how it is applied. The demo app starts at `LineHeight` 5 with `BlockLineHeight`, the first value of its combo box. With its four lines, that gave a height of 20 instead of 63.84, so the lines overlap. `MaxHeight` does not let a line be lower than its text: with 5, the height stayed 63.84. With 30, both gave 120. Use `LineStackingStrategy="MaxHeight"` or a `LineHeight` larger than the text to avoid overlapping lines.

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/textblock/textblock-lineheight.svg" alt="Table of the height of the demo's four lines: 63.84 without LineHeight, 20 with LineHeight 5 and BlockLineHeight but 63.84 with MaxHeight, and 120 with LineHeight 30 for both strategies" width="406" height="170" loading="lazy">
  <figcaption>Height of the demo's four lines by <code>LineHeight</code> and <code>LineStackingStrategy</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The TextBlock page of the demo app, with the control list on the left and the first section, Text](/images/wpf-standard-control-demo/textblock.png){: .screenshot-img}

The TextBlock page of the demo app has sections for `Text`, `TextWrapping`, `TextTrimming`, `TextAlignment`, `Padding`, the font properties, `Background`, `Foreground`, and `LineHeight` with `LineStackingStrategy`. The `Text` section binds the text to a text box, and the `TextAlignment` section shows the four values `Left`, `Right`, `Center`, and `Justify` side by side with wrapping on. The "Show Code" link under each section displays its XAML. The following XAML is the `LineHeight` section (`TextBlockUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <TextBox x:Name="LineHeightTextTextBox" AcceptsReturn="True"
           Text="TEXTBLOCK&#10;TEXTBLOCK&#10;TEXTBLOCK&#10;TEXTBLOCK" />
  <TextBox x:Name="LineHeightTextBox" Text="5" />
  <ComboBox x:Name="LineStackingStrategyComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=LineStackingStrategy}"
            SelectedValuePath="Value" SelectedIndex="0" />

  <TextBlock x:Name="LineHeightResultTextBlock"
             LineHeight="{Binding Text, ElementName=LineHeightTextBox}"
             LineStackingStrategy="{Binding SelectedValue, ElementName=LineStackingStrategyComboBox}"
             Text="{Binding Text, ElementName=LineHeightTextTextBox}" />
</StackPanel>
```

## Related controls and articles

- [Label](/apps/wpf-standard-control-demo/label.html) — a caption with an access key that moves the focus.
- [TextBox](/apps/wpf-standard-control-demo/textbox.html) — text the user can edit, or select when read-only.
- [How to Display Selectable, Copyable Read-Only Text in WPF](/articles/wpf-selectable-readonly-text-display/) — what to use when the text must be selectable.
- [Why WPF Slows Down with Many Labels and When to Switch to TextBlock](/articles/wpf-label-vs-textblock-performance/) — the cost of Labels compared with TextBlocks.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TextBlockDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TextBlockDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The TextBlocks were laid out with `Measure` and `Arrange`. Line counts are the height divided by the height of one line, and "clipped" means WPF's layout clip cut the TextBlock at the width it was given. Sizes depend on the font and display scaling; they are the values on the measuring machine.

[View TextBlock source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TextBlockUsage){: target="_blank" rel="noopener noreferrer"}
