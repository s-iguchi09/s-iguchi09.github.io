---
layout: article-en
title: "Why WPF Label Hides Underscores and How to Fix It"
date: 2026-06-09
category: WPF
excerpt: "When a string containing an underscore (_) is set on a WPF Label, the character disappears from the screen. This article explains the cause and three ways to work around it."
---

## Overview

When a string containing `_` (underscore) is set as the `Content` of a WPF `Label` control, the underscore is not rendered and disappears from the display.  
This behavior is by design in WPF. This article explains the cause and the representative workarounds.  

---

## Prerequisites / Environment

- Framework / Language: WPF / C# / XAML
- Target control: `Label`
- Architecture: Applies to both MVVM and code-behind approaches
- Prior knowledge: WPF basics, XAML fundamentals

---

## Problem

When a string containing an underscore (for example, `my_variable`) is set on a `Label`'s `Content`, the screen displays `myvariable` with the underscore missing.  
When the string contains `_F`, the letter `F` is rendered with an underline instead of showing `_F` as-is.  
The same issue occurs with dynamically bound data: if the bound string contains an underscore, it is silently dropped from the display.  

---

## Cause / Background

`Label` internally uses a control called `AccessText` to render its text.  
`AccessText` interprets underscores as markers for access keys — the shortcut feature that moves focus to a control when the corresponding Alt key combination is pressed.  

The specific rendering behavior is as follows:

| Input string | Rendered output | Interpretation                         |
| ------------ | --------------- | -------------------------------------- |
| `_File`      | **F**ile        | `F` is registered as an access key     |
| `my_var`     | my**v**ar       | `v` is registered as an access key     |
| `name_`      | name            | Trailing underscore is silently dropped |

As a result, any underscore in the bound data causes unintended display corruption.  

---

## Solution

There are three main workarounds. Choose the one that fits the situation.  

- To escape individual underscores, write `__` (two underscores) in place of each `_` (Workaround 1).
- If the access-key feature is not needed and the goal is simply to display text, replace `Label` with `TextBlock` (Workaround 2).
- To keep `Label` while correctly displaying dynamically bound strings that contain underscores, override `ContentTemplate` with a `TextBlock` (Workaround 3).

---

## Implementation

### Workaround 1: Escape underscores by doubling them

Writing `__` renders a single underscore on screen.  
This approach is suitable when the string is set statically in XAML.  

```xml
<Label Content="my__variable" />
```

- **Advantage:** A single change in XAML is all that is required.
- **Disadvantage:** When the bound data comes from a ViewModel and contains underscores, a replacement step must be performed in code before binding.

---

### Workaround 2: Replace Label with TextBlock

If the access-key feature and the `Target` property for focus navigation are not needed, replacing `Label` with `TextBlock` is the best practice.  
`TextBlock` does not use `AccessText`, so underscores are displayed as-is.  

```xml
<TextBlock Text="my_variable" />
```

Dynamic binding works the same way without any special handling.  

```xml
<TextBlock Text="{Binding VariableName}" />
```

- **Advantage:** No escaping is required. `TextBlock` is also more lightweight than `Label`, making it the appropriate choice for display-only use.
- **Disadvantage:** The `Label`'s `Target` property for focus control cannot be used.

---

### Workaround 3: Customize ContentTemplate to use TextBlock

To keep `Label` while correctly rendering bound strings that contain underscores, set a `ContentTemplate` that uses `TextBlock` for the content.  
This causes `Label` to render its `Content` through `TextBlock` instead of `AccessText`.  

```xml
<Label Content="{Binding VariableName}">
    <Label.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding}" />
        </DataTemplate>
    </Label.ContentTemplate>
</Label>
```

For application-wide consistency, the template can be extracted into a shared style.  

```xml
<Style x:Key="PlainLabel" TargetType="Label">
    <Setter Property="ContentTemplate">
        <Setter.Value>
            <DataTemplate>
                <TextBlock Text="{Binding}" />
            </DataTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

- **Advantage:** Dynamically bound data containing underscores is displayed correctly. Defining this as a shared style makes it easy to apply across multiple locations.
- **Disadvantage:** The XAML becomes more verbose.

---

## Notes

- When using the `Target` property to move focus via access keys, the access-key mechanism must remain active. In that case, Workaround 2 and Workaround 3 are not suitable; use Workaround 1 (escaping) instead.
- When applying Workaround 3, the `ContentTemplate` is applied regardless of the `Content` type. Confirm that the bound data is of type `string` before adopting this approach.
- Replacing `_` with `__` in the ViewModel is also possible, but it introduces a View-layer concern into the ViewModel and should generally be avoided.

---

## Alternatives / Comparison

| Approach                            | Advantage                                        | Disadvantage                                           | Best suited for                                 |
| ----------------------------------- | ------------------------------------------------ | ------------------------------------------------------ | ----------------------------------------------- |
| Workaround 1: Escape with `__`      | Simple one-line XAML change                      | Requires replacement logic in code for dynamic data    | Fixing individual static strings in XAML        |
| Workaround 2: Switch to `TextBlock` | Simplest and most lightweight                    | `Label`'s `Target` feature is unavailable              | Display-only text where access keys are unused  |
| Workaround 3: Override `ContentTemplate` | Handles dynamic bound data correctly        | More verbose XAML                                      | Keeping `Label` with dynamic string binding     |

---

## Summary

Underscores disappear in WPF `Label` because the internal `AccessText` control interprets them as access-key markers.  

- For display-only text, switching to `TextBlock` (Workaround 2) is the simplest and most appropriate solution.
- When `Label` must be used with static strings, escaping with `__` (Workaround 1) is the quickest fix.
- When `Label` must be used with dynamically bound data that contains underscores, override `ContentTemplate` to render via `TextBlock` (Workaround 3).

Choosing the right approach based on the use case ensures that underscores are always displayed correctly.  

---

<!-- Related articles -->
<!-- - [WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue) -->
<!-- - [How to Display Selectable, Read-Only Text in WPF](/articles/wpf-selectable-readonly-text-display) -->
