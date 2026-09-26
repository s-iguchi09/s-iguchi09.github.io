---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/passwordbox.html
title: "PasswordBox"
badge: "Inputs"
lead: "PasswordBox is a text input that shows a mask character for each typed character and does not let the text be copied out."
description: "WPF PasswordBox measured on .NET 10: Password cannot be bound, MaxLength limits only typing, IsSelectionActive follows focus, and Copy is blocked."
---

## Why Password cannot be bound, and how to get the value out

**PasswordBox** derives from `Control`, not from `TextBoxBase`. Its `Password` property is a plain CLR property: there is no `PasswordProperty`, so it cannot be the target of a binding. The value can still be set from code or in XAML. The demo app sets `Password="PASSWORD"` as an attribute, and that value was read back unchanged.

To pass the value to a ViewModel, use `PasswordChanged` or an attached behavior. `PasswordChanged` was raised once per typed character (three times for "abc"), once for setting `Password` from code, and once for `Clear()`, so a handler sees every change. `Clear()` emptied `Password`; call it when the value is no longer needed.

Internally, the text is held in a `SecureString` field of a `PasswordTextContainer`. `Password` returns it as an ordinary `string`. `SecurePassword` returned a new, writable `SecureString` on every call, not the same instance. Code that uses it owns that copy, and should dispose of it.

`PasswordChar`, unlike `Password`, is a dependency property. Its metadata default is `*`, but the default style sets `●` (U+25CF), so that is what you see.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-defaults.svg" alt="Table of PasswordBox type and defaults: base class Control, no PasswordProperty but a PasswordCharProperty, PasswordChar metadata default asterisk and default style bullet, internal SecureString storage, and SecurePassword returning a new instance on each call" width="967" height="410" loading="lazy">
  <figcaption>Type, properties, defaults, and how the password is held. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## What MaxLength, Copy, and IsSelectionActive actually do

`MaxLength` limits only typing; the default is 0 (unlimited). With `MaxLength="8"`, typing "1234567890" left "12345678". `Password` set from code was not cut: it kept all ten characters. Treat `MaxLength` as an input aid, and check the length where the value is used.

With all text selected, the Copy and Cut commands could not be executed, while Paste could.

`IsSelectionActive` is read-only and, despite its name, does not report whether text is selected. It was `True` as soon as the PasswordBox had focus with nothing selected, still `True` with all text selected, and `False` after focus moved to another control even though the selection was kept. It follows keyboard focus, so do not use it to detect a selection.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-behavior.svg" alt="Table of PasswordBox behavior: MaxLength 8 cuts typed input but not Password set from code, PasswordChanged is raised once per typed character and once each for setting Password and Clear, Copy and Cut cannot execute while Paste can, and IsSelectionActive follows keyboard focus rather than the selection" width="936" height="350" loading="lazy">
  <figcaption>Input, events, clipboard commands, and <code>IsSelectionActive</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## The caret and the selection highlight

`CaretBrush` defaults to `null`. `SelectionBrush` gets its default from the property's default value, not from a style; on the measuring machine it was `#FF0078D7`. `SelectionOpacity` defaults to 0.4. `IsInactiveSelectionHighlightEnabled` decides whether the selection stays highlighted after the PasswordBox loses focus; its default is `False`. The highlight itself is drawn on screen and was not measured on this page.

## Trying it in the demo app

![The PasswordBox page of the demo app, with the control list on the left and the first section, CaretBrush](/images/wpf-standard-control-demo/passwordbox.png){: .screenshot-img}

The PasswordBox page of the demo app has a section for each of `CaretBrush`, `IsInactiveSelectionHighlightEnabled`, `IsSelectionActive`, `MaxLength`, `PasswordChar`, `SelectionBrush`, and `SelectionOpacity`. The brushes are picked from a list of named brushes, over a PasswordBox that starts with "PASSWORD"; `MaxLength` starts with 8 and `SelectionOpacity` with 0.5, typed into text boxes; the `IsInactiveSelectionHighlightEnabled` section starts with `True` and places two PasswordBoxes so that focus can be moved between them. The "Show Code" link under each section displays its XAML. The following XAML is the `PasswordChar` section (`PasswordBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. The binding takes the first character of the text box and falls back to `●` when it is empty:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="PasswordCharTextBox" MaxLength="1" Text="*" />

  <PasswordBox x:Name="PasswordCharPasswordBox"
               PasswordChar="{Binding Text[0], ElementName=PasswordCharTextBox, FallbackValue=●}" />
</StackPanel>
```

## Related controls and articles

- [TextBox](/apps/wpf-standard-control-demo/textbox.html) — plain text input; `MaxLength` behaves the same way there.
- [Label](/apps/wpf-standard-control-demo/label.html) — text that names the field next to it.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`PasswordBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/PasswordBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Typing was reproduced one character at a time with `TextCompositionManager`, which delivers text through the same `TextInput` path as the keyboard. The clipboard was not touched: only whether the Copy, Cut, and Paste commands could execute was read.

[View PasswordBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/PasswordBoxUsage){: target="_blank" rel="noopener noreferrer"}
