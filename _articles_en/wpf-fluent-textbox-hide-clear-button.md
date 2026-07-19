---
layout: article-en
title: "Hiding the Clear Button on a Fluent-Themed WPF TextBox"
date: 2026-07-19
category: WPF
excerpt: "How to hide the clear button a Fluent-themed WPF TextBox shows on focus, without changing input behavior, on .NET 10 and .NET 9."
---

## Overview

A WPF `TextBox` with the Fluent theme applied shows a clear button (×) at the right edge of the text when it receives focus.
This default behavior helps with input, but it is unnecessary for search or filter fields that already provide their own clear action, where two buttons with the same role appear side by side.
This article explains how to hide only that clear button without effectively changing the template's input behavior, targeting `.NET 10` as the primary version.
There are two approaches.
One hides the button element (a named part) inside the template directly, and the other uses the hide trigger of the `AcceptsReturn` property.
Because the part name differs on `.NET 9`, that difference and the corresponding code are also documented.

---

## Prerequisites / Environment

- Framework / Language: `.NET 10` as the primary target (with `.NET 9` differences noted) / C# 13
- Target control: WPF `TextBox` (with the Fluent theme applied)
- Theme: `PresentationFramework.Fluent` (via `ThemeMode` or a merge of `Fluent.xaml`)
- Architecture: applicable to both MVVM and code-behind

---

## Problem

A Fluent-themed `TextBox` automatically shows the clear button defined in its template when keyboard focus enters it.
This element does not exist in the standard theme (Aero2), so it appears unexpectedly after switching themes.
On a screen that already provides a "×" button or a command to clear the input value, a button with the same function is duplicated and layout consistency is lost.

---

## Cause / Background

This clear button is defined as a named part in the control template of the Fluent-themed `TextBox`.
The part name differs by version: it is `DeleteButton` on `.NET 10` and `ClearButton` on `.NET 9` (the button element was renamed in the update from `.NET 9` to `.NET 10`).
In the `.NET 10` template, the button's default `Visibility` is `Collapsed`, and a trigger shows it only when `IsKeyboardFocusWithin` is `true`.
Therefore, when focus is not within the control, it stays hidden by its default value.
The template also defines triggers that hide it when `Text` is empty or `IsReadOnly` is set, as well as when `AcceptsReturn=True` or when `TextWrapping` is `Wrap` or `WrapWithOverflow`.
The `.NET 9` template (whose part is named `ClearButton`) has none of the `AcceptsReturn` or `TextWrapping` triggers, and hides the button through an `IsKeyboardFocusWithin` `false` trigger when focus leaves.
Because of this, no public property is provided to hide only the clear button, so the options are to manipulate the part directly or to satisfy one of the hide conditions above.

---

## Solution

As noted above, there are two families of approaches.

- **Approach 1: hide the named part directly.**
  Set `Collapsed` as a local value on the `Visibility` of the part (`DeleteButton` on `.NET 10`, `ClearButton` on `.NET 9`).
  WPF dependency properties have a defined value precedence, and a local value wins over style and template triggers.
  Therefore, even when focus enters and a trigger attempts to set `Visible`, the local `Collapsed` value wins and the element stays hidden.
  The advantage is direct control of the display: it hides reliably regardless of trigger conditions or property values.
  The trade-off is a dependency on the internal part name of the template.

- **Approach 2: use the hide trigger of the `AcceptsReturn` property.**
  Set `AcceptsReturn=True` to satisfy the template's hide trigger (added in `.NET 10`) and remove the clear button.
  The advantage is that it depends on a public property, so it is unaffected by future changes to the template's part name.
  The trade-off is that it turns a single-line `TextBox` into multi-line input, and it does not work on `.NET 9` because that trigger does not exist there.

Both approaches obtain the part with `Template.FindName`.
Wrapping the operations in an attached property allows each to be applied declaratively by adding a single attribute in XAML.

---

## Implementation

### Approach 1: Hide the Named Part (works on both `.NET 10` and `.NET 9`)

The following defines an attached property `HideClearButton` that collapses the clear-button part once `True` is set.
Because the part name differs by version, it probes both `DeleteButton` (`.NET 10`) and `ClearButton` (`.NET 9`) and falls back accordingly.
Because the template may not be applied at the moment the property changes, the code waits for `Loaded` before processing if the control is not yet loaded.
The `Loaded` subscription uses a weak reference through `WeakEventManager` so that the handler does not extend the lifetime of the `TextBox`.

```csharp
public static partial class TextBoxHelper
{
    // "DeleteButton" on .NET 10, "ClearButton" on .NET 9. Fall back depending on the runtime.
    private static readonly string[] ClearButtonPartNames = ["DeleteButton", "ClearButton"];

    public static bool GetHideClearButton(DependencyObject obj) =>
        (bool)obj.GetValue(HideClearButtonProperty);

    public static void SetHideClearButton(DependencyObject obj, bool value) =>
        obj.SetValue(HideClearButtonProperty, value);

    public static readonly DependencyProperty HideClearButtonProperty =
        DependencyProperty.RegisterAttached(
            "HideClearButton",
            typeof(bool),
            typeof(TextBoxHelper),
            new FrameworkPropertyMetadata(false, OnHideClearButtonChanged));

    private static void OnHideClearButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox || !(bool)e.NewValue)
        {
            return;
        }

        if (textBox.IsLoaded)
        {
            HideClearButtonPart(textBox);
        }
        else
        {
            // Remove first to prevent duplicate registration, then add.
            WeakEventManager<FrameworkElement, RoutedEventArgs>.RemoveHandler(textBox, nameof(FrameworkElement.Loaded), OnLoaded);
            WeakEventManager<FrameworkElement, RoutedEventArgs>.AddHandler(textBox, nameof(FrameworkElement.Loaded), OnLoaded);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            WeakEventManager<FrameworkElement, RoutedEventArgs>.RemoveHandler(textBox, nameof(FrameworkElement.Loaded), OnLoaded);
            HideClearButtonPart(textBox);
        }
    }

    private static void HideClearButtonPart(TextBox textBox)
    {
        textBox.ApplyTemplate();

        foreach (string partName in ClearButtonPartNames)
        {
            if (textBox.Template?.FindName(partName, textBox) is UIElement clearButton)
            {
                clearButton.Visibility = Visibility.Collapsed;
            }
        }
    }
}
```

`ApplyTemplate` forces the template to be applied before the part is obtained.
Because `Visibility` is set as a local value, the element stays hidden even when triggers are re-evaluated on focus changes.
Since `AcceptsReturn` is not modified, the behavior of the Enter key and pasting is not affected at all.

On the XAML side, the attached property is added to the target `TextBox`.

```xml
<TextBox helper:TextBoxHelper.HideClearButton="True"
         Text="{Binding Keyword, UpdateSourceTrigger=PropertyChanged}" />
```

`helper` is the XML namespace prefix mapped to the namespace of the class that defines the attached property.

### Approach 2: Use the `AcceptsReturn` Hide Trigger (`.NET 10` or later)

As an approach that does not depend on the part name, set `AcceptsReturn=True` to satisfy the template's hide trigger.
Since that alone turns a single-line `TextBox` into multi-line input, line breaks from the Enter key are suppressed and newlines on paste are stripped to preserve single-line behavior.
The following is an implementation of an attached property `SingleLineHideClear` that enables all of this together.

```csharp
public static partial class TextBoxHelper
{
    public static bool GetSingleLineHideClear(DependencyObject obj) =>
        (bool)obj.GetValue(SingleLineHideClearProperty);

    public static void SetSingleLineHideClear(DependencyObject obj, bool value) =>
        obj.SetValue(SingleLineHideClearProperty, value);

    public static readonly DependencyProperty SingleLineHideClearProperty =
        DependencyProperty.RegisterAttached(
            "SingleLineHideClear",
            typeof(bool),
            typeof(TextBoxHelper),
            new FrameworkPropertyMetadata(false, OnSingleLineHideClearChanged));

    private static void OnSingleLineHideClearChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            // AcceptsReturn=True satisfies the .NET 10 hide trigger.
            textBox.AcceptsReturn = true;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
        else
        {
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Suppress line breaks from the Enter key to keep a single-line appearance.
        if (e.Key is Key.Enter)
        {
            e.Handled = true;
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            return;
        }

        string text = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText);
        if (text.Contains('\n') || text.Contains('\r'))
        {
            // Replace newlines in the pasted text with spaces before pasting.
            string singleLine = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            DataObject data = new();
            data.SetData(DataFormats.UnicodeText, singleLine);
            e.DataObject = data;
        }
    }
}
```

The `AcceptsReturn=True` hide trigger is declared after the focus-driven show trigger, and when both apply the later-declared trigger wins, so the clear button stays hidden even while focused.
Suppressing Enter and stripping newlines on paste keeps both the appearance and the input single-line.
This trigger was added in `.NET 10`, so note that it does not hide the button on `.NET 9`.

On the XAML side, `SingleLineHideClear` is added to the target `TextBox`.

```xml
<TextBox helper:TextBoxHelper.SingleLineHideClear="True"
         Text="{Binding Keyword, UpdateSourceTrigger=PropertyChanged}" />
```

As in Approach 1, the `helper` prefix maps to the namespace of the class that defines the attached property.

---

## Notes

- Approach 1 depends on the internal part name of the template. The name actually changed from `ClearButton` on `.NET 9` to `DeleteButton` on `.NET 10`, and if the structure changes again in a future update, the part will not be found and the clear button reappears (no exception is thrown; it is a graceful degradation where only the hiding stops working).
- The Approach 2 hide trigger was added in `.NET 10`, so setting `AcceptsReturn=True` on `.NET 9` does not remove the clear button. Use Approach 1 when `.NET 9` is a target.
- Approach 2 makes the control multi-line internally through `AcceptsReturn=True`. Suppressing Enter and stripping newlines on paste preserves a single line, but IME behavior and multi-line paste handling should be validated further according to application requirements.
- A `TextBox` whose control template has been fully replaced may not contain a clear-button part. In that case, remove the button element itself within the replaced template.
- The implementations above only hide the button when `True` is set and do not include logic to restore it dynamically. If toggling on and off at runtime is required, add a branch that restores `Visibility` and the handler registrations.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best suited for |
|---|---|---|---|
| Collapse the named part (Approach 1) | Direct and reliable control of the display, easy to support both `.NET 9` and `.NET 10`, no side effects on input | Depends on the internal part name (which has been renamed across versions) | The common case of hiding it reliably while staying single-line |
| Use the `AcceptsReturn` trigger (Approach 2) | Depends on a public property, resilient to part-name changes | Requires counteracting the multi-line side effect, does not work on `.NET 9` | Avoiding part-name dependence on `.NET 10` or later |
| Fully replace the control template | Full control over the structure | Verbose and high maintenance cost | Heavily customizing the theme |

---

## Summary

To remove the clear button on a Fluent-themed `TextBox`, setting `Visibility=Collapsed` as a local value on the target part (`DeleteButton` on `.NET 10`, `ClearButton` on `.NET 9`) is reliable while preserving input behavior, and it is straightforward to support both versions with Approach 1.
When resilience to part-name changes matters, choose Approach 2 with the `AcceptsReturn` hide trigger, but this is limited to `.NET 10` or later and requires counteracting the multi-line side effect.
For most cases such as single-line search and filter fields, make Approach 1 the default, consider Approach 2 only for `.NET 10`-or-later designs that need to avoid part-name dependence, and choose full template replacement when redesigning the entire theme.

---

## Related Articles

- [Applying Fluent Design in WPF Without Extra Libraries](/articles/wpf-fluent-design-with-systemcolors/)
