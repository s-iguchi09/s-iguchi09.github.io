---
layout: article-en
title: "Why WPF Style Triggers and DataTriggers Do Not Apply — Dependency Property Value Precedence"
date: 2026-08-03
category: WPF
excerpt: "A style trigger that never applies is usually outranked by a local value in XAML. Covers value precedence and the Setter, SetCurrentValue and ClearValue fixes."
image: /images/articles/wpf-style-trigger-not-working-local-value/style-trigger-local-value.png
---

## Overview

A `Trigger` or `DataTrigger` declared in `Style.Triggers` sometimes has no visible effect even though its condition is met.
The common assumption is a broken binding or a type mismatch in the trigger condition, but a frequent cause is that the trigger fires correctly and its value is simply outranked by a higher-precedence input.
This article explains the cause in terms of dependency property value precedence, shows how to repair markup that carries a local value, and gives criteria for choosing among the available fixes.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (the precedence rules are identical on WPF for .NET Framework 4.x)
- Language: C#
- Target feature: `Trigger` / `DataTrigger` / `MultiTrigger` declared in `Style.Triggers`
- Default theme: Aero2 (the Fluent theme available from .NET 9 differs from what is described below in both the standard control colors and the structure of the default templates)
- Architecture: applicable to both MVVM and code-behind

---

## Problem

Consider a `Style` with a `DataTrigger` that changes the background of a frame according to a validation state.

```xml
<Window.Resources>
    <Style x:Key="StatusBox" TargetType="Border">
        <Style.Triggers>
            <DataTrigger Binding="{Binding HasError}" Value="True">
                <Setter Property="Background" Value="#FFD4D4" />
            </DataTrigger>
        </Style.Triggers>
    </Style>
</Window.Resources>

<Border Style="{StaticResource StatusBox}" Background="White">
    <TextBlock Text="HasError = True" />
</Border>
```

When `HasError` becomes `true`, the background of this `Border` stays `White`.
The binding resolves correctly and no binding error appears in the Output window (for reading those messages, see [Reading WPF Binding Errors and Diagnosing Them with the Output Window](/articles/wpf-binding-error-debugging-output-window/)).
The same result occurs with a property trigger such as `Trigger Property="IsMouseOver"`, so the trigger type is not the cause.

---

## Cause / Background

A WPF dependency property can receive values from several inputs: local values, styles, templates, and inheritance.
Which one becomes the effective value is decided by **dependency property value precedence**, and a higher-precedence input silences every lower one.

The order is as follows, highest precedence first.

| Rank | Source of the value | Example |
| --- | --- | --- |
| 1 | Property system coercion | `CoerceValueCallback` |
| 2 | Active animations, or animations with a `Hold` behavior | `Storyboard` |
| 3 | **Local value** | A XAML attribute or property element, `SetValue`, or a `Binding` / `StaticResource` / `DynamicResource` written on the element |
| 4 | `TemplatedParent` template property values | Elements created by a `ControlTemplate` or `DataTemplate` |
| 5 | Implicit styles | Applies to the `Style` property only |
| 6 | **Style triggers** | `Style.Triggers` |
| 7 | Template triggers | `ControlTemplate.Triggers` / `DataTemplate.Triggers` |
| 8 | Style setter values | A `Setter` directly under `Style` |
| 9 | Default (theme) styles | Theme style triggers, then theme style setters |
| 10 | Inheritance | Inheritable properties such as `FontSize` |
| 11 | Default value from dependency property metadata | The default value in `PropertyMetadata` |

The problem lies entirely in the gap between rank 3 and rank 6.
A value written as a XAML attribute, such as `Background="White"`, is a local value at rank 3 and therefore outranks a style trigger at rank 6.
The trigger condition is evaluated and its `Setter` is applied to that lower rank, but the effective value remains the local value, so nothing changes on screen.

The part that is easy to miss is that **a `Binding` or a `DynamicResource` written directly on the element also counts as a local value**.
Writing `Background="{Binding NormalBrush}"` only defers evaluation of the value; its precedence is still rank 3, and a style trigger cannot win against it.

A `Setter` directly under `Style`, on the other hand, sits at rank 8, below the trigger at rank 6.
Supplying the default through a setter rather than a local value therefore restores the intended relationship.

---

## Solution

Remove the local value from the target element and move the default into a `Setter` inside the `Style`.
The default then comes from rank 8 and the conditional value from rank 6, so the trigger wins whenever its condition holds.

Only the property that the trigger writes to is affected.
Setting unrelated properties such as `Margin` or `Width` as local values on the element causes no interference.

---

## Implementation

The following markup places two `Border` elements one above the other under the same style: one keeps its local value, the other takes its default from the setter.
Both reference the same `StatusBox` style, and the relevant difference is whether `Background` is present as a local value (the `Margin` on the lower one only separates the two vertically and has no bearing on the trigger).

```xml
<Window.Resources>
    <Style x:Key="StatusBox" TargetType="Border">
        <Setter Property="Background" Value="White" />
        <Setter Property="BorderBrush" Value="#9AA4B2" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="18,6" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding HasError}" Value="True">
                <Setter Property="Background" Value="#FFD4D4" />
            </DataTrigger>
        </Style.Triggers>
    </Style>
</Window.Resources>

<StackPanel>
    <!-- The local value remains, so the trigger background is not applied -->
    <Border Style="{StaticResource StatusBox}" Background="White">
        <TextBlock Text="HasError = True" />
    </Border>

    <!-- The default moved into the setter, so the trigger background is applied -->
    <Border Style="{StaticResource StatusBox}" Margin="0,12,0,0">
        <TextBlock Text="HasError = True" />
    </Border>
</StackPanel>
```

The `HasError` used in the trigger condition is a property on the view model assigned to `DataContext`.
It implements `INotifyPropertyChanged` so that runtime changes reach the trigger.

```csharp
public sealed class ValidationViewModel : INotifyPropertyChanged
{
    private bool _hasError;

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (_hasError == value)
            {
                return;
            }

            _hasError = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

Assigning this view model to the `DataContext` of the `Window` propagates changes of `HasError` to the `DataTrigger`.
With a plain property that raises no change notification, the trigger is never re-evaluated when the value changes.

Rendering this markup with `HasError` set to `true` shows the difference directly.

<figure class="article-figure">
  <img src="/images/articles/wpf-style-trigger-not-working-local-value/style-trigger-local-value.png" alt="Two Border elements sharing one style. The upper one, whose Background is set as a local value, stays white, while the lower one takes the pale red from the DataTrigger." width="415" height="139" loading="lazy">
  <figcaption>The result with <code>HasError</code> set to <code>True</code>. The upper border keeps <code>Background</code> as a local value and does not pick up the trigger color; the lower one takes its default from the <code>Setter</code>, so the trigger color is applied. The labels on the left were added to the figure to show how the two <code>Border</code> declarations differ (captured on .NET 10 / Windows 11).</figcaption>
</figure>

Precedence also depends on how a value is assigned from code-behind.
The three statements below all target `Background`, but each stores the value at a different rank.

```csharp
// Becomes a local value, so style triggers no longer affect Background on this element
border.Background = Brushes.White;

// Changes the effective value without writing a local value (a trigger can still take over)
border.SetCurrentValue(Border.BackgroundProperty, Brushes.White);

// Removes an existing local value, restoring the setter or trigger value
border.ClearValue(Border.BackgroundProperty);
```

`SetCurrentValue` is a special assignment that does not appear in the precedence list: it changes the current value without overwriting the source of the value.
It suits cases where a temporary value is needed without discarding an existing binding or trigger.
It only avoids creating a local value, however, and does not remove one that is already set.
While a local value remains on the target property the effective value does not change, so it has to be removed with `ClearValue` first.
`ClearValue` removes only the local value, so whichever remaining input ranks highest — a theme style, for instance — becomes the effective value.

---

## Notes

- **`ClearValue` also removes a binding or a `DynamicResource`.**
Calling it on a property that carries only a binding, with no literal local value, discards the binding itself.
To supply a default through a binding, write the `Binding` in the `Value` of a `Setter` instead of on the element.
To express the trigger condition itself through a binding, use the `Binding` property of a `DataTrigger`, which is of type `BindingBase`.
A `Binding` is accepted in that condition binding and in `Setter.Value`, but not in the `Value` of a `Trigger` or `DataTrigger`, which holds the value being compared against.
- **Assigning a local value replaces a binding.**
A plain assignment to a property that holds a binding replaces the deferred value outright.
A later `ClearValue` call does not restore the binding.
- **Triggers in a theme style, and in its `ControlTemplate`, lose to local values as well.**
Setting `Foreground` as a local value on a `Button` suppresses the trigger that greys out the text when the button is disabled.
Depending on how the default theme is implemented, that trigger sits at rank 7 (template triggers) or rank 9 (theme styles), but either way it ranks below a local value at rank 3.
Verify that the standard state feedback of a control is not being broken.
- **If a trigger still has no visible effect after the local value is gone, examine the `ControlTemplate`.**
The default template of a standard control may hard-code appearance such as the mouseover background.
In that case the trigger wins as a property value but never reaches the rendering, and replacing the template is required.
- **A one-way binding or a literal value in an `ItemContainerStyle` setter also loses to a local value on the container.**
When container state is supplied through a style on an `ItemsControl`, assigning the same property from code produces a local value, and style values no longer reach that container afterwards.
A `Mode=TwoWay` binding in the setter is the exception: the binding survives and the assigned value is written back to the source.
The concrete impact on `IsSelected` and `IsExpanded` of `TreeViewItem` is covered in [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/).
- **The same precedence does not apply to the `Style` property itself.**
A `Style` written on the element is an explicit style with local-value precedence (rank 3), while a style applied from a resource whose key matches the element type is an implicit style at rank 5.
When neither is present, the default (theme) style applies at rank 9.
An implicit style is not applied to an element that already has an explicit style.
- **Resource evaluation timing is a separate issue.**
A `StaticResource` that is swapped at runtime and never updates is a matter of evaluation timing rather than precedence (see [Why StaticResource Changes Are Not Reflected in WPF and How to Fix It](/articles/wpf-staticresource-vs-dynamicresource/)).
The two problems should not be conflated.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best suited for |
| --- | --- | --- | --- |
| Move the default into a `Setter` | Stays entirely in XAML and cannot invert the precedence | Harder to give each element a different default | The default choice in most situations |
| Assign with `SetCurrentValue` | Creates no local value, so triggers keep working | Requires code-behind | Applying a temporary value at runtime |
| Remove the local value with `ClearValue` | Leaves the existing XAML untouched | Also removes bindings, and the call timing must be managed | Clearing a local value applied at runtime |
| Replace the `ControlTemplate` | Controls even the appearance that a template hard-codes | Verbose, and does not follow theme updates | A default template that fixes the appearance |
| Start an animation from `EnterActions` | Rank 2, so it overrides a local value | Stopping and rewinding must be managed, and animating the `Color` of an unfrozen shared brush also changes every other element that references it | State changes that need a transition effect |

---

## Summary

A style trigger that appears broken is often not a mistake in the trigger itself; a local value on the target property is a frequent cause.
Local values sit at rank 3, style triggers at rank 6, and style setters at rank 8, and that ordering alone explains the behavior.

Selection criteria are as follows.

- **The default is written in XAML:**
remove the attribute from the element and move it into a `Setter` in the `Style`.
This has the fewest side effects and should be considered first.
- **The value changes at runtime from code-behind:**
use `SetCurrentValue` instead of a plain assignment.
Because it does not overwrite the value source, a trigger that fires later still applies.
It has no effect on a property that already carries a local value, so clear that with `ClearValue` first.
- **A local value is already in place:**
clear it with `ClearValue`, keeping in mind that any binding goes with it.
Supply the default from a `Setter` if one is needed.

Confirming that the target property carries no local value, and keeping every default in a `Setter`, removes this class of problem at design time.
