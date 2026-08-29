---
layout: article-en
title: "Why WPF Label Hides Underscores and How to Fix It"
date: 2026-06-09
category: WPF
excerpt: "When a string containing an underscore (_) is set on a WPF Label, the character disappears from the screen. This article explains the underlying ContentPresenter.RecognizesAccessKey behavior, identifies which controls are affected, and covers four workarounds with measured results."
image: /images/articles/wpf-label-underscore-issue/label-underscore-rendering.png
---

## Overview

When a string containing `_` (underscore) is set as the `Content` of a WPF `Label` control, the underscore is not rendered and disappears from the display.
This behavior is by design in WPF. This article explains the cause and the representative workarounds.

The issue is commonly treated as specific to `Label`, but the same result occurs on several other controls including `Button` and `CheckBox`.
This article establishes the affected range of controls through measurement, then presents four workarounds and the criteria for choosing among them.

---

## Prerequisites / Environment

- Framework / Language: .NET 10 / C# / WPF / XAML
- Target controls: standard controls whose default template sets `ContentPresenter.RecognizesAccessKey` to `True` — `Label`, `Button`, `CheckBox`, `RadioButton`, `ToggleButton`, and the `Header` of `GroupBox`, `Expander`, `TabItem`, and `MenuItem`
- Architecture: Applies to both MVVM and code-behind approaches
- Verification environment: Windows 11, default theme (Aero2), display scaling 100%
- Prior knowledge: WPF basics, XAML fundamentals

The figures and measured values in this article were captured by actually running an application in the environment above.
**Other versions and themes were not verified.** Because the behavior depends on `RecognizesAccessKey` in the default templates, results can differ where the theme or `ControlTemplate` has been replaced.

---

## Problem

When a string containing an underscore (for example, `my_variable`) is set on a `Label`'s `Content`, the screen displays `myvariable` with the underscore missing.
When the string contains `_F`, the letter `F` is rendered with an underline instead of showing `_F` as-is.
The same issue occurs with dynamically bound data: if the bound string contains an underscore, it is silently dropped from the display.

File paths, identifiers, database column names, and snake_case keys are all common cases where underscores reach the screen.
Feeding such values directly into a `Label` often goes unnoticed during development and surfaces only once real data is displayed.

---

## Cause / Background

`Label` internally uses a control called `AccessText` to render its text.
`AccessText` interprets underscores as markers for access keys — the shortcut feature that moves focus to a control when the corresponding Alt key combination is pressed.

The specific rendering behavior is as follows:

| Input string | Rendered output | Interpretation                                          |
| ------------ | --------------- | ------------------------------------------------------- |
| `_File`      | **F**ile        | `F` is registered as the access key                     |
| `my_var`     | my**v**ar       | `v` is registered as the access key                     |
| `name_`      | name_           | No character follows, so the underscore remains visible |

The image below shows those three cases rendered by an actual application.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-underscore-issue/label-underscore-rendering.png" alt="A WPF window showing Label controls set to _File, my_var, and name_. _File renders as File and my_var renders as myvar, with the underscore removed. Only name_ keeps its underscore." width="461" height="166" loading="lazy">
  <figcaption>Result of assigning each string to a <code>Label</code> on .NET 10 / Windows 11. The left column is the XAML markup and the right column is the actual rendering. <code>_File</code> and <code>my_var</code> lose their underscore, while <code>name_</code> is not treated as an access key because no character follows it.</figcaption>
</figure>

As a result, display corruption occurs simply because the data contains an underscore.
Because the underscore is only consumed when a character follows it, the outcome depends on where the underscore sits in the string.

When a string contains multiple underscores, only the first one is registered as an access key.
Given `a_b_c`, the access key becomes `b` and the second underscore is rendered as-is.

### What Decides Whether AccessText Is Used

Whether `AccessText` is used is determined not by the `Label` type itself, but by the **`RecognizesAccessKey` property of the `ContentPresenter` placed in the default `ControlTemplate`**.
The `ContentPresenter` produces an `AccessText` only when `RecognizesAccessKey` is `True` **and the string contains an underscore**.
For a string without one, a plain `TextBlock` is used even though `RecognizesAccessKey` is `True`.

Walking the visual tree makes the difference explicit.

| `Content` | Visual tree that gets built |
| --- | --- |
| `Status Running` | `Label` → `Border` → `ContentPresenter` → `TextBlock` |
| `Status _Running` | `Label` → `Border` → `ContentPresenter` → `AccessText` → `TextBlock` |

An `AccessText` layer is inserted only when the content contains an underscore.
The accurate statement is therefore not that `Label` swallows underscores, but that a `ContentPresenter` with `RecognizesAccessKey="True"` does.

### Which Controls Are Affected

`Label` is not the only control whose default template sets `RecognizesAccessKey` to `True`.
The table below records whether an `AccessText` appears in the visual tree when the same string `my_var` is assigned to each control.

| Control | Underscore handling | Affected property |
| --- | --- | --- |
| `Label` | Removed | `Content` |
| `Button` | Removed | `Content` |
| `CheckBox` | Removed | `Content` |
| `RadioButton` | Removed | `Content` |
| `ToggleButton` | Removed | `Content` |
| `GroupBox` | Removed | `Header` only |
| `Expander` | Removed | `Header` only |
| `TabItem` | Removed | `Header` only |
| `MenuItem` | Removed | `Header` only |
| `TreeViewItem` | Preserved | — |
| `ListBoxItem` | Preserved | — |
| `ComboBoxItem` | Preserved | — |
| `StatusBarItem` | Preserved | — |
| `TextBlock` | Preserved | — |

On these header-bearing controls, the only `ContentPresenter` with `RecognizesAccessKey="True"` is the one that renders the `Header`.
How the main `Content` is rendered differs by control.

| Control | `ContentPresenter` instances in its own template |
| --- | --- |
| `GroupBox`, `Expander` | Two: one for `Header` (`True`) and one for `Content` (`False`) |
| `MenuItem` | Two: one for `Header` (`True`) and one for `Icon` (`False`). `MenuItem` has no `Content` property |
| `TabItem` | One: for `Header` (`True`). The selected `Content` is rendered by `PART_SelectedContentHost` on the parent `TabControl` |

Consequently, within the same control, only strings placed in the header lose their underscores.

The rendering result for the principal controls, all given the same string, is shown below.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-underscore-issue/label-underscore-affected-controls.png" alt="A WPF window where the string my_var is assigned to a Label, Button, CheckBox, GroupBox header, ListBoxItem, and TextBlock. The first four render myvar while ListBoxItem and TextBlock render my_var." width="504" height="309" loading="lazy">
  <figcaption>Result of assigning the same string <code>my_var</code> to each control on .NET 10 / Windows 11. <code>Label</code>, <code>Button</code>, <code>CheckBox</code>, and the <code>GroupBox</code> header lose the underscore, while <code>ListBoxItem</code> and <code>TextBlock</code> preserve it. The difference comes from <code>ContentPresenter.RecognizesAccessKey</code> in the default templates.</figcaption>
</figure>

`ListBoxItem` and `ComboBoxItem` are unaffected because assigning access keys to entries in a list serves no purpose.
Conversely, placing a `Label` or `Button` inside an `ItemTemplate` reintroduces the problem at that point.

---

## Solution

There are four workarounds. The appropriate one depends on the use case.

- To simply escape the underscore, write it twice as `__` (Workaround 1).
- When access keys are unnecessary and the text is display-only, switch to `TextBlock` (Workaround 2).
- To keep `Label` while rendering dynamically bound data correctly, use a `TextBlock` in `ContentTemplate` (Workaround 3).
- To disable access-key interpretation in the default string display itself, set `RecognizesAccessKey="False"` in a `ControlTemplate` (Workaround 4). This replaces the default template, so the `Border` and padding must be rebuilt by hand.

Workaround 1 is the only approach that fixes the display while retaining access-key functionality. Workarounds 2 through 4 all give up access keys in exchange for handling strings verbatim.

---

## Implementation

### Workaround 1: Escape the Underscore by Doubling It

Writing `__` renders a single underscore on screen.
This suits cases where the string is set statically in XAML.

```xml
<Label Content="my__variable" />
```

- **Advantage:** Requires a single edit in XAML. Access-key functionality remains available.
- **Disadvantage:** When the data is dynamically bound, replacement logic is required on the ViewModel side.

For dynamic data handled by replacement, write it as follows.

```csharp
// Escape underscores for display purposes. The original value is left untouched.
public string DisplayName => Name.Replace("_", "__");
```

This replacement **must be applied exactly once**.
Applying it again to an already-escaped string turns `a_b` into `a____b`, which renders incorrectly as `a__b`.
Computing the value in a property getter each time makes double application easy to avoid.

---

### Workaround 2: Switch to the TextBlock Control

When access-key functionality and focus control via the `Target` property are unnecessary, switching `Label` to `TextBlock` is the simplest fix.
`TextBlock` does not use `AccessText`, so underscores render as-is.

```xml
<TextBlock Text="my_variable" />
```

Binding works the same way.

```xml
<TextBlock Text="{Binding VariableName}" />
```

- **Advantage:** Underscores no longer require attention. `TextBlock` is lighter than `Label` and is the appropriate choice for display-only text.
- **Disadvantage:** Focus control through `Label`'s `Target` property is lost. The default `Padding` also disappears, so line spacing and alignment need rechecking.

---

### Workaround 3: Use a TextBlock in ContentTemplate

To keep `Label` while correctly rendering dynamically bound data that contains underscores, specify a `TextBlock` in `ContentTemplate`.
This makes `Label` render its `Content` through a `TextBlock` rather than an `AccessText`.

```xml
<Label Content="{Binding VariableName}">
    <Label.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding}" />
        </DataTemplate>
    </Label.ContentTemplate>
</Label>
```

To apply this consistently across an application, define the template as a style.

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

- **Advantage:** Dynamically bound data containing underscores renders correctly. Sharing the template as a style makes it easy to apply in many places.
- **Disadvantage:** The XAML grows. `ContentTemplate` also applies when `Content` is an object rather than a string, so its scope must be constrained.

---

### Workaround 4: Set RecognizesAccessKey to False in a ControlTemplate

This addresses the cause directly.
Replace the `ControlTemplate` and set `RecognizesAccessKey` to `False` on the `ContentPresenter`.

```xml
<Label Content="{Binding VariableName}">
    <Label.Template>
        <ControlTemplate TargetType="Label">
            <ContentPresenter RecognizesAccessKey="False" />
        </ControlTemplate>
    </Label.Template>
</Label>
```

The `ContentPresenter` no longer selects an `AccessText` for the default string display, so underscores render as-is.

This setting only affects the default path where the `ContentPresenter` picks a display element from a string.
An explicit `ContentTemplate` takes precedence, and an `AccessText` placed directly in `Content` is rendered as that element.
In neither case does the value of `RecognizesAccessKey` change the result.

- **Advantage:** Strips access-key interpretation from the default string display without requiring a `ContentTemplate`. As long as the bound data is a string, it applies uniformly regardless of the value.
- **Disadvantage:** Replacing the `ControlTemplate` means reimplementing what the default template provides, such as the `Border` and the disabled-state appearance (the foreground color when `IsEnabled="False"`). Placing only a `ContentPresenter`, as in the example above, discards them.

When the default appearance must be preserved, Workaround 3 has a smaller blast radius than replacing the entire `ControlTemplate`.

Applying each of the four workarounds produces the same rendered result.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-underscore-issue/label-underscore-workarounds.png" alt="A WPF window showing four workarounds: an escaped Label, a TextBlock, a Label with a replaced ContentTemplate, and a Label with RecognizesAccessKey set to False. All four render my_variable." width="619" height="202" loading="lazy">
  <figcaption>The four workarounds executed in the same application. Escaping with <code>__</code>, switching to <code>TextBlock</code>, replacing <code>ContentTemplate</code>, and setting <code>RecognizesAccessKey="False"</code> all display <code>my_variable</code> without loss.</figcaption>
</figure>

---

## Notes and Limitations

- When focus movement via the `Target` property is required, the access-key mechanism must stay enabled, so Workarounds 2, 3, and 4 do not apply. Use Workaround 1 (escaping) in that case.
- `ContentTemplate` in Workaround 3 also applies when `Content` is an object rather than a string. Confirm that the bound data type is `string` before applying it.
- Replacing `_` with `__` on the ViewModel side pushes a View-level display rule into the ViewModel. Leave the original property referenced by value objects and business logic unchanged, and expose a separate display-only property.
- The same problem occurs on `Button` and `MenuItem`. Fixing only `Label` leaves the text broken wherever the same string is used as a button caption. Audit every place where data-derived strings are displayed.
- An unintentionally registered access key affects **keyboard operation**, not just rendering. When multiple controls in the same scope share an access key, Alt-key focus movement cycles between them, making interaction unpredictable.
- For strings inside a list, `ListBox` itself is unaffected, but a `Label` placed inside the `ItemTemplate` still drops the underscore. Check the template contents as well.

### Side Effect on Rendering Cost

Inserting an `AccessText` adds one visual layer, which affects rendering cost as well.
Placing 1,000 `Label` controls in a non-virtualized `StackPanel`, layout with underscore-containing strings takes roughly three times as long as without them.
A `Label` with the Workaround 3 `ContentTemplate` applied, by contrast, costs roughly the same as a `Label` without underscores.

On screens that display large amounts of text from underscore-bearing data, Workaround 3 improves both correctness and rendering cost.
The relationship between control choice and rendering cost is covered in detail in [Why WPF Slows Down with Many Labels and When to Switch to TextBlock](/articles/wpf-label-vs-textblock-performance/).

---

## Alternatives / Comparison

| Approach | Advantages | Disadvantages | Best suited for |
| --- | --- | --- | --- |
| Workaround 1: Escape with `__` | One-line XAML edit; access keys retained | Dynamic data needs ViewModel work; risk of double application | Static strings where access keys are also needed |
| Workaround 2: Switch to `TextBlock` | Simplest and most lightweight | Loses `Label`'s `Target` feature and default padding | Display-only text where access keys are unused |
| Workaround 3: Override `ContentTemplate` | Handles dynamic binding; preserves default appearance | More verbose XAML; applies regardless of `Content` type | Keeping `Label` with dynamic string binding |
| Workaround 4: `RecognizesAccessKey="False"` | Disables the cause directly; no `ContentTemplate` needed | Requires reimplementing the `ControlTemplate`; affects only the default string display | Projects that already use a custom template |

---

## Summary

Underscores disappear in WPF `Label` because the `ContentPresenter` in its default template has `RecognizesAccessKey="True"` and therefore renders strings through `AccessText`.
This is not specific to `Label`: `Button`, `CheckBox`, and `RadioButton`, as well as the `Header` of `GroupBox`, `Expander`, `TabItem`, and `MenuItem`, all behave the same way.

- For display-only text, switching to `TextBlock` (Workaround 2) is the simplest and most appropriate solution.
- When `Label` must be used with static strings and access keys must be retained, escape with `__` (Workaround 1).
- When dynamically bound data contains underscores, use a `TextBlock` in `ContentTemplate` (Workaround 3). This preserves the default appearance and avoids the rendering cost added by `AccessText`.
- When a custom `ControlTemplate` already exists, set `RecognizesAccessKey="False"` on its `ContentPresenter` (Workaround 4).

Whether access keys are needed is the deciding factor.
If they are, use Workaround 1. If not, choose among Workarounds 2 through 4 based on how much of `Label`'s appearance must be preserved.

---

<!-- Related articles -->
- [Why WPF Slows Down with Many Labels and When to Switch to TextBlock](/articles/wpf-label-vs-textblock-performance/)
