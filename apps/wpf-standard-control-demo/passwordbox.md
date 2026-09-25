---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/passwordbox.html
title: "PasswordBox"
badge: "Inputs"
lead: "PasswordBox is a text input that shows a mask character for each typed character and does not let the text be copied out."
description: "WPF PasswordBox control reference: overview, properties, XAML examples, and use cases. Part of the WPF Standard Control Demo App running on .NET 10."
---

## Overview

**PasswordBox** derives from `Control`, not from `TextBoxBase`. Its `Password` property is a plain CLR property: there is no `PasswordProperty`, so it cannot be the target of a binding. The value can still be set from code or in XAML. The demo app sets `Password="PASSWORD"` as an attribute, and that value was read back unchanged. `PasswordChanged` was raised once per typed character (three times for "abc"), once for setting `Password` from code, and once for `Clear()`, so a handler sees every change.

Internally, the text is held in a `SecureString` field of a `PasswordTextContainer`. `Password` returns it as an ordinary `string`. `SecurePassword` returned a new, writable `SecureString` on every call, not the same instance. Code that uses it owns that copy, and should dispose of it.

With all text selected, the Copy and Cut commands could not be executed, while Paste could. The demo app has a section for each property below; the "Show Code" link under each section displays its XAML.

## Screen Preview

![passwordbox demo screen](/images/wpf-standard-control-demo/passwordbox.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `CaretBrush` | `Brush` | The brush of the text cursor; the default is `null`. The demo app picks the brush from a list of named brushes, over a PasswordBox that starts with "PASSWORD". |
| `IsInactiveSelectionHighlightEnabled` | `bool` | Decides whether the selection stays highlighted after the PasswordBox loses focus; the highlight itself is drawn on screen and was not measured on this page. The default is `False`. The demo app starts with `True` and places two PasswordBoxes so that focus can be moved between them. |
| `IsSelectionActive` | `bool (ReadOnly)` | Whether the PasswordBox currently has keyboard focus. Despite its name, it does not report whether text is selected. It was `True` as soon as the PasswordBox had focus with nothing selected, still `True` with all text selected, and `False` after focus moved to another control even though the selection was kept. |
| `MaxLength` | `int` (0 = unlimited) | The maximum number of characters the user can type; the default is 0. With `MaxLength="8"`, typing "1234567890" left "12345678". `Password` set from code was not cut: it kept all ten characters. The demo app starts with 8, typed into a text box. |
| `PasswordChar` | `char` | The character shown for each typed character. The property's metadata default is `*`, but the default style sets `●` (U+25CF), so that is what you see. Unlike `Password`, it is a dependency property: the demo app binds it to the first character of a text box that starts with "\*". |
| `SelectionBrush` | `Brush` | The brush of the selection highlight. Its default comes from the property's default value, not from a style; on the measuring machine it was `#FF0078D7`. The demo app picks the brush from a list of named brushes. |
| `SelectionOpacity` | `double` | The opacity of the selection highlight; the default is 0.4. The demo app starts with 0.5, typed into a text box. |

## XAML Example

The following XAML is the `PasswordChar` section of the demo app (`PasswordBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. The binding takes the first character of the text box and falls back to `●` when it is empty:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="PasswordCharTextBox" MaxLength="1" Text="*" />

  <PasswordBox x:Name="PasswordCharPasswordBox"
               PasswordChar="{Binding Text[0], ElementName=PasswordCharTextBox, FallbackValue=●}" />
</StackPanel>
```

## Common Use Cases

- **Sign-in forms:** a user name in a `TextBox` and the password in a PasswordBox.
- **Password change:** two PasswordBoxes compared in their `PasswordChanged` handlers.
- **Secrets in settings:** API keys or connection passwords that should not be shown or copied.

## Tips and Best Practices

- **Pass the value to a ViewModel from `PasswordChanged` or an attached behavior.** `Password` cannot be bound because it is not a dependency property.
- **Treat `MaxLength` as an input aid.** It does not apply to `Password` set from code; check the length where the value is used.
- **Dispose of what `SecurePassword` returns.** Each call creates a new `SecureString`.
- **Do not use `IsSelectionActive` to detect a selection.** It follows keyboard focus.
- **Call `Clear()` when the value is no longer needed.** It emptied `Password` and raised `PasswordChanged`.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`PasswordBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/PasswordBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Typing was reproduced one character at a time with `TextCompositionManager`, which delivers text through the same `TextInput` path as the keyboard. The clipboard was not touched: only whether the Copy, Cut, and Paste commands could execute was read.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-behavior.svg" alt="Table of PasswordBox behavior: MaxLength 8 cuts typed input but not Password set from code, PasswordChanged is raised once per typed character and once each for setting Password and Clear, Copy and Cut cannot execute while Paste can, and IsSelectionActive follows keyboard focus rather than the selection" width="936" height="350" loading="lazy">
  <figcaption>Input, events, clipboard commands, and <code>IsSelectionActive</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-defaults.svg" alt="Table of PasswordBox type and defaults: base class Control, no PasswordProperty but a PasswordCharProperty, PasswordChar metadata default asterisk and default style bullet, internal SecureString storage, and SecurePassword returning a new instance on each call" width="967" height="410" loading="lazy">
  <figcaption>Type, properties, defaults, and how the password is held. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [TextBox](/apps/wpf-standard-control-demo/textbox.html) — plain text input; `MaxLength` behaves the same way there.
- [Label](/apps/wpf-standard-control-demo/label.html) — text that names the field next to it.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View PasswordBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/PasswordBoxUsage){: target="_blank" rel="noopener noreferrer"}
