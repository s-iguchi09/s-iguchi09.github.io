---
layout: article-en
title: "Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF"
date: 2026-07-21
category: WPF
excerpt: "TextBox.Text defaults to LostFocus, so typed input may never reach the ViewModel. This covers the three UpdateSourceTrigger values, their timing, and pitfalls."
image: /images/articles/wpf-textbox-updatesourcetrigger-binding-timing/updatesourcetrigger-lostfocus-vs-propertychanged.png
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
- Verification environment: .NET 10 / Windows 11

`UpdateSourceTrigger` is meaningful only on `TwoWay` or `OneWayToSource` bindings.
It determines the *timing* at which a value is written back from the target (`TextBox.Text`) to the source (the ViewModel); it does not affect the source-to-target display update.

The figures in this article come from reading `DefaultUpdateSourceTrigger` from the metadata of each property in the environment above, and measuring when the value reaches the source.
The following points were confirmed in that environment:

- `TextBox.Text` is the only one that defaults to `LostFocus`; most others default to `PropertyChanged`.
- The default, `PropertyChanged`, and `Explicit` differ in when the value reaches the source.

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

<figure class="article-figure">
  <img src="/images/articles/wpf-textbox-updatesourcetrigger-binding-timing/updatesourcetrigger-lostfocus-vs-propertychanged.png" alt="Two pairs of an input box and the ViewModel value. With the default binding the box shows sato while UserName is still suzuki. With UpdateSourceTrigger=PropertyChanged, UserName is sato as well." width="401" height="175" loading="lazy">
  <figcaption>In both rows <code>UserName</code> starts as <code>suzuki</code> and is then changed to <code>sato</code> while the input keeps focus. The right side displays the same <code>UserName</code> through a <code>OneWay</code> binding. With the default (top) the source is not updated because focus never leaves the box, while <code>PropertyChanged</code> (bottom) updates it immediately.</figcaption>
</figure>

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

Reading the metadata and listing it side by side shows that `TextBox.Text` is the outlier.

<figure class="article-figure">
  <img src="/images/articles/wpf-textbox-updatesourcetrigger-binding-timing/updatesourcetrigger-defaults.svg" alt="A table of DefaultUpdateSourceTrigger per dependency property. Only TextBox.Text is LostFocus; CheckBox.IsChecked, ComboBox.SelectedItem, Slider.Value, and TextBlock.Text are all PropertyChanged." width="634" height="260" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 by reading <code>FrameworkPropertyMetadata.DefaultUpdateSourceTrigger</code> from <code>DependencyProperty.GetMetadata</code>. <code>BindsTwoWayByDefault</code> is shown alongside.</figcaption>
</figure>

**`TextBox.Text` is the only one at `LostFocus`.** The others default to `PropertyChanged` and update the source right after the input or interaction.
That single row of difference is why "the binding is set up but no value arrives" happens mostly on `TextBox`.

When the value actually reaches the source can be measured too, separating the keystroke from the focus change.

<figure class="article-figure">
  <img src="/images/articles/wpf-textbox-updatesourcetrigger-binding-timing/updatesourcetrigger-timing.svg" alt="A table of the source value right after one keystroke and after focus moves away, per UpdateSourceTrigger. Default is empty after input and holds the value after focus leaves. PropertyChanged holds it right after input. Explicit stays empty even after focus leaves and only fills in once UpdateSource is called." width="528" height="170" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 by entering one character into the <code>TextBox</code> and reading the source right afterwards, then again after moving focus to another control. Only the <code>Explicit</code> row calls <code>UpdateSource()</code> at the end.</figcaption>
</figure>

The `Default` row — which for `TextBox.Text` means `LostFocus` — is empty right after the keystroke and fills in only once focus leaves.
`Explicit` stays empty even then, and nothing reaches the source until `UpdateSource()` is called.

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
- **Validation timing**: `ValidationRules` are attached to the `Binding` and run around the source update according to their `ValidationStep` (default `RawProposedValue`), so they track the `UpdateSourceTrigger`. In contrast, `INotifyDataErrorInfo` validates on the ViewModel after the source is updated and reflects results through the `ErrorsChanged` notification, so with asynchronous validation the display timing may not coincide with the update trigger. With `LostFocus`, the source update (and `ValidationRules`) runs after leaving the field; with `PropertyChanged`, it runs per keystroke. A rule with `ValidatesOnTargetUpdated="True"` also runs on target updates, however, so it does not track the `UpdateSourceTrigger` alone. `INotifyDataErrorInfo` results likewise surface when `ErrorsChanged` is raised, which an implementation may do independently of the source update. Because the binding engine re-reads the error state on that notification, `ErrorsChanged` must be raised on the UI thread; when validation completes on background work, marshal both the error-state update and the notification to the UI thread through the `Dispatcher`. Isolating the cause when validation results never reach the screen is covered in [Why WPF Validation Errors Are Not Displayed, and Choosing Between IDataErrorInfo and INotifyDataErrorInfo](/articles/wpf-validation-error-not-displayed/).
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
- [Why a WPF RadioButton Bound to an Enum Shows No Initial Selection — The Role of GroupName](/articles/wpf-radiobutton-enum-binding/)
<!-- - [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/) -->
