---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/textbox.html
title: "TextBox"
badge: "Inputs"
lead: "TextBox is the editable text input of WPF, for single-line fields and, with <code>AcceptsReturn</code>, multiline text."
description: "WPF TextBox measured on .NET 10: MaxLength and CharacterCasing affect only typing, Wrap vs WrapWithOverflow, and why MinLines in XAML does not apply at first."
---

## When the text reaches the bound source

**TextBox** derives from `TextBoxBase`, which derives from `Control`. Its `Text` property binds two-way by default, and its default `UpdateSourceTrigger` is `LostFocus`: a bound source receives the text when the TextBox loses focus, not on every keystroke. Typing "abc" one character at a time wrote the source 3 times with `UpdateSourceTrigger=PropertyChanged`, and not at all with the default until the focus left the TextBox. Use `PropertyChanged` for a search box whose filter follows each keystroke. [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/) measures the difference between the triggers.

## What applies only to typing

Several properties act only on what the user types. Treat them as input aids, not rules, and validate values that come from code or from a ViewModel separately.

- **`MaxLength`:** the default 0 means no limit, and a negative value throws `ArgumentException`. With `MaxLength="5"`, typing "ABCDEFGH" left "ABCDE". Text set from code or through a binding kept all eight letters.
- **`CharacterCasing`:** the default is `Normal`. With `Upper`, typing "hello" gave "HELLO", but `Text = "hello"` from code stayed "hello".
- **`AcceptsReturn`:** with the default `False`, Enter did not change the text; with `True`, it inserted a line break (`\r\n`).
- **`IsReadOnly`:** typed text was ignored, but `SelectAll()` still selected the text; with the text selected, Copy could execute while Cut and Paste could not. This suits logs or error details the user may need to copy. The default template has no trigger on `IsReadOnly`, and the background and border stayed the same colors as an editable TextBox. Add your own style trigger if users need to see the difference. [How to Display Selectable, Copyable Read-Only Text in WPF](/articles/wpf-selectable-readonly-text-display/) compares this with other ways to show copyable text.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/textbox/textbox-input.svg" alt="Table comparing typed input with text set from code or a binding: MaxLength 5 cuts typed text to ABCDE but not text from code or a binding, CharacterCasing Upper converts typed text but not text from code, IsReadOnly ignores typed text but allows selection, Enter inserts a line break only with AcceptsReturn, PropertyChanged writes the source on every typed character while the default waits for the focus to leave, and a read-only TextBox ignores typing while Copy can still execute" width="975" height="440" loading="lazy">
  <figcaption>Typed input compared with text set from code or a binding. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Wrapping, alignment, and decorations

`TextWrapping` defaults to `NoWrap`. With two 26-letter words in a TextBox 150 wide, `NoWrap` kept one line 385 wide. `Wrap` produced 4 lines and broke inside the words, with 20 letters on the first line. `WrapWithOverflow` broke only between words: it produced 2 lines, and each word ran past the edge (190.86 wide in a 144-wide viewport). Choose `WrapWithOverflow` for text with long tokens that must not be split, such as paths or URLs, and `Wrap` when everything must stay inside the width. TextBox has no `TextTrimming` property; that is a `TextBlock` feature.

`TextAlignment` defaults to `Left`. With "123" in a TextBox 200 wide, the first character was at x = 3 with `Left`, 90.3 with `Center`, and 177.59 with `Right`.

`TextDecorations` can combine several lines: `TextDecorations="Underline, Strikethrough"` was converted into a collection of two decorations. `SelectionOpacity` defaults to 0.4.

## Height: MinLines, MaxLines, and scroll bars

A multiline TextBox shows no scroll bar by default, because `VerticalScrollBarVisibility` and `HorizontalScrollBarVisibility` default to `Hidden`. With ten lines in a TextBox 60 high, the content was 159.6 high, but no bar appeared until `VerticalScrollBarVisibility="Auto"`. In a `StackPanel`, which does not limit height, the same TextBox simply grew to 161.6. For notes and comments, combine `AcceptsReturn="True"`, `TextWrapping="Wrap"`, and `VerticalScrollBarVisibility="Auto"`.

`MaxLines` is the most lines the TextBox grows to; the default is `Int32.MaxValue`. With `AcceptsReturn`, `VerticalScrollBarVisibility="Auto"`, and ten lines of text, `MaxLines` 2, 4, and 6 gave heights of 33.92, 65.84, and 97.77, with a vertical scroll bar in each.

`MinLines` is the fewest lines the TextBox is tall; the default is 1. On .NET 10 it did not take effect when it was set before the TextBox was shown, as in XAML. An empty TextBox with `MinLines="4"` was one line tall (17.96), the same as with 1, 2, or 6. It took effect once the text changed after the TextBox was displayed: the height became 65.84, four lines. Setting `MinLines` after the TextBox was displayed applied it at once, and so did setting it in a `Loaded` handler when XAML did not set it. With `MinLines="4"` also in XAML, setting 4 again in `Loaded` did not change the value, and the TextBox stayed one line tall. If it matters on first display, set it after loading and not in XAML.

`ScrollToEnd()` on the UI thread scrolled to the bottom (`VerticalOffset` 101.6, equal to the scrollable height). Called from a worker thread it threw `InvalidOperationException`; call it through the `Dispatcher` when text is appended from another thread.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/textbox/textbox-layout.svg" alt="Table of TextBox layout results: line counts and overflow for NoWrap, Wrap, and WrapWithOverflow, heights for MinLines and MaxLines including MinLines having no effect until the text changes after display, scroll bars for a 60-high multiline TextBox, text alignment positions, combined text decorations, and ScrollToEnd from a worker thread throwing InvalidOperationException" width="1116" height="920" loading="lazy">
  <figcaption>Wrapping, line limits, scroll bars, alignment, and <code>ScrollToEnd</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The TextBox page of the demo app, with the control list on the left and the first section, Text](/images/wpf-standard-control-demo/textbox.png){: .screenshot-img}

The TextBox page of the demo app has a section for each of `Text`, `TextWrapping`, `TextDecorations`, `TextAlignment`, `MaxLength`, `MaxLines`, `MinLines`, and `CharacterCasing`. In several sections, one source text box feeds the others through bindings, so you can compare the settings with the same text. Because of that, the `MaxLength="5"` and `MaxLength="10"` boxes show text past their limit uncut, and the `MinLines` boxes start one line tall and grow when you type in the source box. The "Show Code" link under each section displays its XAML. The following XAML is the `Text` section (`TextBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. The three text boxes write to the same `TextBlock` at different times: the first two when you leave the box, and the third on every keystroke:

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

## Related controls and articles

- [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/) — when each trigger updates the source.
- [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/) — updating the source explicitly.
- [How to Display Selectable, Copyable Read-Only Text in WPF](/articles/wpf-selectable-readonly-text-display/) — read-only text the user can select.
- [PasswordBox](/apps/wpf-standard-control-demo/passwordbox.html) — masked input for passwords.
- [TextBlock](/apps/wpf-standard-control-demo/textblock.html) — displays text without editing.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TextBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TextBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Typing was reproduced one character at a time with `TextCompositionManager`, which delivers text through the same `TextInput` path as the keyboard, and the Enter key by sending a key event to a displayed window. Heights, widths, and positions on this page are the values on the measuring machine; they depend on the font and display scaling.

[View TextBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TextBoxUsage){: target="_blank" rel="noopener noreferrer"}
