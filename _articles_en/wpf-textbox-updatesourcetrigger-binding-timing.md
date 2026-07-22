---
layout: article-en
title: "Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF"
date: 2026-07-21
category: WPF
excerpt: "TextBox.Text defaults to LostFocus, so typed input may never reach the ViewModel. This covers the three UpdateSourceTrigger values, their timing, and pitfalls."
---

## Overview

In WPF two-way binding, text typed into a `TextBox` may not reach the bound ViewModel property immediately.
A command or another control keeps showing the old value even after typing, and the cause is often mistaken for a broken `PropertyChanged` implementation.
The behavior centers on `Binding.UpdateSourceTrigger`, and the confusion stems from `TextBox.Text` having a default that differs from other controls.
Working from the design reason behind that default, this article walks through how the three values `LostFocus`, `PropertyChanged`, and `Explicit` change the timing of the source update, down to practical pitfalls such as IME composition, validation timing, and buttons that do not take focus.

---

## Prerequisites / Environment

- Framework / Language: .NET 8 / C# 12 (`UpdateSourceTrigger` is available in WPF since .NET Framework 3.0)
- Target control / feature: two-way binding on `TextBox.Text` (`Mode=TwoWay` / `OneWayToSource`)
- Architecture: MVVM (a ViewModel property bound to a `TextBox` in the View)
- Assumed knowledge: change notification via `INotifyPropertyChanged` and basic data binding

`UpdateSourceTrigger` is meaningful only on `TwoWay` or `OneWayToSource` bindings.
It determines the *timing* at which a value is written back from the target (`TextBox.Text`) to the source (the ViewModel); it does not affect the source-to-target display update.

---

## Problem

Bind a `TextBox` to a ViewModel property, and add a "Save" button or another `TextBlock` that consumes that value.
When the user types into the `TextBox` and, while the caret is still in the field, commits through an interaction that does not move focus away from the field (such as the `Focusable="False"` button below), the ViewModel property still holds the **old value from before the edit**.

```xml
<!-- Default binding; UpdateSourceTrigger is not specified -->
<TextBox Text="{Binding UserName, Mode=TwoWay}" />
<Button Content="Save" Command="{Binding SaveCommand}" Focusable="False" />
```

Clicking a button with `Focusable="False"` above does not move focus away from the `TextBox`, so `SaveCommand` runs without the typed text reaching `UserName`.
The symptom appears as "the value does not arrive even though `INotifyPropertyChanged` is implemented correctly".

---

## Cause / Background

The cause is that the default `UpdateSourceTrigger` of `TextBox.Text` is `LostFocus`.
The default of `UpdateSourceTrigger` is `Default`, which means "the default update timing defined for the target dependency property".
For most dependency properties (such as `CheckBox.IsChecked`) that default is `PropertyChanged`, but `TextBox.Text` alone defaults to `LostFocus`.

This is a deliberate design choice.
Updating the source on every keystroke runs change notification, validation, and related processing per character, which harms performance.
It also denies the user the usual opportunity to fix input (backspace) before committing.
For that reason, WPF chose `LostFocus` as the default, updating the source once the `TextBox` loses focus.

The default value of a dependency property can be confirmed in code.
Inspect `DefaultUpdateSourceTrigger` on the metadata obtained via `DependencyProperty.GetMetadata`.

```csharp
// Retrieve the default UpdateSourceTrigger of TextBox.Text
var metadata = (FrameworkPropertyMetadata)TextBox.TextProperty.GetMetadata(typeof(TextBox));
UpdateSourceTrigger def = metadata.DefaultUpdateSourceTrigger; // => LostFocus
```

The result being `LostFocus` is the basis for the problem above.
No source update occurs unless focus moves away.

---

## Solution

To control the timing, specify `UpdateSourceTrigger` explicitly on the binding.
Three values are available, each with a different update trigger.

- `PropertyChanged` — updates the source immediately whenever `TextBox.Text` changes (per keystroke).
- `LostFocus` — updates the source when the `TextBox` loses focus (the default for `TextBox.Text`).
- `Explicit` — updates the source only when the app explicitly calls `UpdateSource()`.

For the problem case (delivering the value even when a button runs mid-edit), specifying `PropertyChanged` reflects each keystroke.
For a form that should commit only when a submit button is pressed, `Explicit` is appropriate.

---

## Implementation

When immediate reflection of input is required, specify `UpdateSourceTrigger=PropertyChanged`.
This suits UIs where per-keystroke reflection is natural, such as a search box or chat input.

```xml
<!-- Reflect into UserName on each keystroke -->
<TextBox Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}" />
```

With this setting, `UserName` updates on each keystroke even while the `TextBox` keeps focus.

To defer committing input until a user action (a submit button), specify `Explicit`.
First, give the `TextBox` an `x:Name` in XAML and set the binding's `UpdateSourceTrigger` to `Explicit`.

```xml
<!-- Switch to explicit updates -->
<TextBox x:Name="userNameBox" Text="{Binding UserName, UpdateSourceTrigger=Explicit}" />
```

Using that name, obtain the target `BindingExpression` from code-behind and call `UpdateSource()` at the chosen moment to update the source.

```csharp
// Call this on the submit button click, for example
BindingExpression be = userNameBox.GetBindingExpression(TextBox.TextProperty);
be.UpdateSource();
```

With `Explicit`, the source is never updated unless `UpdateSource()` is called.
Because a missed call means the value is never reflected, call it reliably at the start of the submit processing.

---

## Notes

- **Immediate updates during IME composition**: `PropertyChanged` updates the source even during IME composition (unconfirmed text), so intermediate strings flow into the ViewModel before confirmation. To process only after the conversion is committed, use `LostFocus`. Note that `Delay` (below) only reduces update frequency and does not prevent unconfirmed strings from reaching the source.
- **Validation timing**: `ValidationRules` are attached to the `Binding` and run around the source update according to their `ValidationStep` (default `RawProposedValue`), so they track the `UpdateSourceTrigger`. In contrast, `INotifyDataErrorInfo` validates on the ViewModel after the source is updated and reflects results through the `ErrorsChanged` notification, so with asynchronous validation the display timing may not coincide with the update trigger. With `LostFocus`, the source update (and `ValidationRules`) runs after leaving the field; with `PropertyChanged`, it runs per keystroke.
- **Throttling with `Delay`**: excessive updates from `PropertyChanged` can be throttled with `Binding.Delay` (since .NET Framework 4.5), which updates once after a specified number of milliseconds from the last input, e.g. `{Binding UserName, UpdateSourceTrigger=PropertyChanged, Delay=500}`.
- **Interactions that do not move focus**: when the `TextBox` neither loses focus nor has `UpdateSource()` called, no source update occurs under the default `LostFocus`. This covers a `Focusable="False"` button activated by click, a default button (`IsDefault="True"`) activated by Enter, and access keys. Note that `Focusable="False"` only prevents the button from taking focus; it does not prevent activation via `IsDefault` or an access key. Use `PropertyChanged` or `Explicit` for UIs that commit through such paths.
- **Difference from `x:Bind`**: WPF `{Binding}` supports all three values including `Explicit`. UWP/WinUI `{x:Bind}` does not support `Explicit`, so do not conflate the two when reading articles targeting other platforms.

---

## Alternatives / Comparison

Choose the source update timing for `TextBox.Text` according to the nature of the UI.

| Value | Update trigger | Pros | Cons | Best suited for |
|---|---|---|---|---|
| `LostFocus` (default) | When focus is lost | Commits and validates once, after input | Nothing reflects unless focus moves | Ordinary input forms; UIs that commit on focus change |
| `PropertyChanged` | Per keystroke | Input reflects immediately | High update frequency; IME intermediate text flows in | Search boxes, real-time preview, chat input |
| `Explicit` | On `UpdateSource()` call | Full control over commit timing | Nothing reflects if the call is missed | Edit forms that commit in bulk via a submit button |

To keep the immediacy of `PropertyChanged` while reducing update frequency, combining it with `Delay` to update once after a fixed idle time is effective.

---

## Summary

Most cases where `TextBox` input fails to reach the ViewModel stem from `TextBox.Text` defaulting to `LostFocus` for `UpdateSourceTrigger`.
Because the source is not updated unless focus moves or `UpdateSource()` is called, UIs that commit via a `Focusable="False"` button, a default button (`IsDefault="True"`), or an access key run their logic with a stale value.
Choose `PropertyChanged` for search and preview scenarios that need immediate reflection, `Explicit` for edit forms that commit in bulk via a submit button, and the default `LostFocus` for ordinary forms that commit naturally on focus change.
When the update frequency of `PropertyChanged` becomes a problem, throttle it with `Delay`; note that `Delay` does not exclude IME intermediate strings, so use `LostFocus` when processing must wait for the committed text.
Because the update timing governs when `ValidationRules` run (while `INotifyDataErrorInfo` results surface separately via `ErrorsChanged`), select `UpdateSourceTrigger` from both the input experience and the validation design.

For the pitfalls of the `UpdateSource()` call itself when writing an `Explicit` binding back from the View (the conditions under which `GetBindingExpression` returns `null`, updating multiple bindings at once, and the difference from `UpdateTarget()`), see [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/).

---

<!-- Related articles -->
<!-- - [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/) -->
