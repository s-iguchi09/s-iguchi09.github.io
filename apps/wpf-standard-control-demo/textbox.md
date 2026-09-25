---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/textbox.html
title: "TextBox"
badge: "Inputs"
lead: "TextBox is the editable text input of WPF, for single-line fields and, with <code>AcceptsReturn</code>, multiline text."
description: "WPF TextBox control reference: overview, properties, XAML examples, and use cases. Part of the WPF Standard Control Demo App running on .NET 10."
---

## Overview

**TextBox** derives from `TextBoxBase`, which derives from `Control`. Its `Text` property binds two-way by default, and its default `UpdateSourceTrigger` is `LostFocus`: a bound source receives the text when the TextBox loses focus, not on every keystroke. [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/) measures the difference between the triggers.

Several properties act only on what the user types. `MaxLength` stopped typed input at the limit, but text set from code or through a binding was not cut. `CharacterCasing="Upper"` turned typed "hello" into "HELLO", but text set from code stayed "hello". Validate values that come from code or from a ViewModel separately.

The demo app has a section for each property below. In several sections, one source text box feeds the others through bindings, so you can compare the settings with the same text. The "Show Code" link under each section displays its XAML.

## Screen Preview

![textbox demo screen](/images/wpf-standard-control-demo/textbox.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `Text` | `string` | The content of the TextBox. It binds two-way by default, and the default `UpdateSourceTrigger` is `LostFocus`. The demo app binds three text boxes to one `TextBlock` with `UpdateSourceTrigger` set to `Default`, `LostFocus`, and `PropertyChanged`. The first two update the `TextBlock` when you leave the box, and the third on every keystroke. |
| `TextWrapping` | `NoWrap / Wrap / WrapWithOverflow` | How long lines are broken; the default is `NoWrap`. The demo app uses two 26-letter words. In a TextBox 150 wide, `NoWrap` kept one line 385 wide. `Wrap` produced 4 lines and broke inside the words, with 20 letters on the first line. `WrapWithOverflow` broke only between words: it produced 2 lines, and each word ran past the edge (190.86 wide in a 144-wide viewport). TextBox has no `TextTrimming` property; that is a `TextBlock` feature. |
| `TextDecorations` | `None / Underline / Strikethrough / OverLine / Baseline` | Lines drawn with the text. The demo app shows each value in its own TextBox. Several can be combined: `TextDecorations="Underline, Strikethrough"` was converted into a collection of two decorations. |
| `TextAlignment` | `Left / Right / Center / Justify` | The horizontal alignment of the text; the default is `Left`. With "123" in a TextBox 200 wide, the first character was at x = 3 with `Left`, 90.3 with `Center`, and 177.59 with `Right`. The demo app compares the four values with wrapped text. |
| `MaxLength` | `int` (0 = unlimited) | The maximum number of characters the user can type; the default 0 means no limit, and a negative value throws `ArgumentException`. With `MaxLength="5"`, typing "ABCDEFGH" left "ABCDE". The limit does not apply to text from code or a binding: both kept all eight letters. The demo app feeds its `MaxLength="5"` and `MaxLength="10"` boxes through bindings, so text typed in the source box appears in them uncut. |
| `MaxLines` | `int` | The most lines the TextBox grows to; the default is `Int32.MaxValue`. With the demo app's settings (`AcceptsReturn`, `VerticalScrollBarVisibility="Auto"`) and ten lines of text, `MaxLines` 2, 4, and 6 gave heights of 33.92, 65.84, and 97.77, with a vertical scroll bar in each. |
| `MinLines` | `int` | The fewest lines the TextBox is tall; the default is 1. On .NET 10 it did not take effect when it was set before the TextBox was shown, as in XAML. With the demo app's markup, an empty TextBox with `MinLines="4"` was one line tall (17.96), the same as with 1, 2, or 6. It took effect once the text changed after the TextBox was displayed: the height became 65.84, four lines. Setting `MinLines` after the TextBox was displayed also worked at once. In the demo app, the `MinLines` boxes therefore start one line tall and grow when you type in the source box. |
| `CharacterCasing` | `Normal / Upper / Lower` | Converts typed letters to upper or lower case; the default is `Normal`. With `Upper`, typing "hello" gave "HELLO", but `Text = "hello"` from code stayed "hello". In the demo app, type into each of the three boxes to compare. |

## Other TextBox Behavior

These are not in the demo app's TextBox screen, but they come up in almost every multiline or read-only TextBox. They were measured in the same way.

- **`AcceptsReturn`:** with the default `False`, Enter did not change the text; with `True`, it inserted a line break (`\r\n`). A multiline TextBox shows no scroll bar by default, because `VerticalScrollBarVisibility` and `HorizontalScrollBarVisibility` default to `Hidden`. With ten lines in a TextBox 60 high, the content was 159.6 high but no bar appeared until `VerticalScrollBarVisibility="Auto"`. In a `StackPanel`, which does not limit height, the same TextBox simply grew to 161.6.
- **`IsReadOnly`:** typed text was ignored, but `SelectAll()` still selected the text, so it can be copied. The default template has no trigger on `IsReadOnly`, and the background and border stayed the same colors as an editable TextBox. Add your own style trigger if users need to see the difference. [How to Display Selectable, Copyable Read-Only Text in WPF](/articles/wpf-selectable-readonly-text-display/) compares this with other ways to show copyable text.
- **`ScrollToEnd()`:** on the UI thread it scrolled to the bottom (`VerticalOffset` 101.6, equal to the scrollable height). Called from a worker thread it threw `InvalidOperationException`; call it through the `Dispatcher` when text is appended from another thread.
- **`SelectionOpacity`:** the default is 0.4.

## XAML Example

The following XAML is the `Text` section of the demo app (`TextBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. The three text boxes write to the same `TextBlock` at different times:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="UpdateSourceTriggerDefaultTextBox"
           Text="{Binding Text, ElementName=UpdateSourceTrigger, UpdateSourceTrigger=Default}" />
  <TextBox x:Name="UpdateSourceTriggerLostFocusTextBox"
           Text="{Binding Text, ElementName=UpdateSourceTrigger, UpdateSourceTrigger=LostFocus}" />
  <TextBox x:Name="UpdateSourceTriggerPropertyChangedTextBox"
           Text="{Binding Text, ElementName=UpdateSourceTrigger, UpdateSourceTrigger=PropertyChanged}" />

  <TextBlock x:Name="UpdateSourceTrigger" />
</StackPanel>
```

## Common Use Cases

- **Form fields:** names, addresses, and codes, with `MaxLength` to guide the user and validation in the ViewModel.
- **Search boxes:** `UpdateSourceTrigger=PropertyChanged` so the filter follows each keystroke.
- **Notes and comments:** `AcceptsReturn="True"`, `TextWrapping="Wrap"`, and `VerticalScrollBarVisibility="Auto"`.
- **Copyable output:** `IsReadOnly="True"` for logs or error details the user may need to copy.

## Tips and Best Practices

- **Treat `MaxLength` and `CharacterCasing` as input aids, not rules.** Neither affected text set from code or through a binding.
- **Give multiline text boxes a scroll bar.** With the default `Hidden`, no bar appears even when the content is taller than the TextBox.
- **Set `MinLines` after loading, and not in XAML, if it matters on first display.** Set in XAML, it did not apply until the text changed. Setting it after the TextBox was displayed applied it at once, and so did setting it in a `Loaded` handler when XAML did not set it. With `MinLines="4"` also in XAML, setting 4 again in `Loaded` did not change the value, and the TextBox stayed one line tall.
- **Choose `WrapWithOverflow` for text with long tokens that must not be split,** such as paths or URLs, and `Wrap` when everything must stay inside the width.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TextBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TextBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Typing was reproduced one character at a time with `TextCompositionManager`, which delivers text through the same `TextInput` path as the keyboard, and the Enter key by sending a key event to a displayed window. Heights, widths, and positions on this page are the values on the measuring machine; they depend on the font and display scaling.

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/textbox/textbox-input.svg" alt="Table comparing typed input with text set from code or a binding: MaxLength 5 cuts typed text to ABCDE but not text from code or a binding, CharacterCasing Upper converts typed text but not text from code, IsReadOnly ignores typed text but allows selection, and Enter inserts a line break only with AcceptsReturn" width="575" height="350" loading="lazy">
  <figcaption>Typed input compared with text set from code or a binding. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/textbox/textbox-layout.svg" alt="Table of TextBox layout results: line counts and overflow for NoWrap, Wrap, and WrapWithOverflow, heights for MinLines and MaxLines including MinLines having no effect until the text changes after display, scroll bars for a 60-high multiline TextBox, text alignment positions, combined text decorations, and ScrollToEnd from a worker thread throwing InvalidOperationException" width="1116" height="920" loading="lazy">
  <figcaption>Wrapping, line limits, scroll bars, alignment, and <code>ScrollToEnd</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/) — when each trigger updates the source.
- [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/) — updating the source explicitly.
- [How to Display Selectable, Copyable Read-Only Text in WPF](/articles/wpf-selectable-readonly-text-display/) — read-only text the user can select.
- [PasswordBox](/apps/wpf-standard-control-demo/passwordbox.html) — masked input for passwords.
- [TextBlock](/apps/wpf-standard-control-demo/textblock.html) — displays text without editing.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View TextBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TextBoxUsage){: target="_blank" rel="noopener noreferrer"}
