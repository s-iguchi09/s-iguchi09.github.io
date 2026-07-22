---
layout: article-en
title: "Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls"
date: 2026-07-22
category: WPF
excerpt: "Calling GetBindingExpression().UpdateSource() from the View has pitfalls: null returns, bulk updates, wrong direction, and MVVM design. This covers each, grounded in official docs."
---

## Overview

To defer committing input until a moment such as a "Submit" button in WPF, set `UpdateSourceTrigger=Explicit` and call `BindingExpression.UpdateSource()` from the View to write the value back to the source.
The call itself is a single line, but in practice there are pitfalls specific to the calling side: `GetBindingExpression` returns `null` and throws a `NullReferenceException`, or several input fields fail to commit together and some keep a stale value.
Centered on calling `UpdateSource()` from the View, this article covers the conditions under which it returns `null`, updating multiple bindings at once, the directional difference from `UpdateTarget()`, and a design that preserves MVVM, based on documented behavior.
It does not cover choosing among the three `UpdateSourceTrigger` values that decide the *timing* of the update.

---

## Prerequisites / Environment

- Framework / Language: .NET 8 / C# 12 (`UpdateSource` is available in WPF since .NET Framework 3.0)
- Target control / feature: two-way binding on `TextBox.Text` (`Mode=TwoWay` / `OneWayToSource`)
- Architecture: MVVM (a ViewModel property bound to a `TextBox` in the View)
- Assumed knowledge: change notification via `INotifyPropertyChanged` and the basics of `UpdateSourceTrigger`

`UpdateSource()` writes the current value of the target (`TextBox.Text`) back to the source (the ViewModel).
For a binding with `UpdateSourceTrigger=Explicit`, the source is never updated unless this method is called.

---

## Problem

When writing back an `Explicit` binding from the View, the following code may not work as intended.

```csharp
// Called on the submit button click, but may throw or do nothing
BindingExpression be = amountBox.GetBindingExpression(TextBox.TextProperty);
be.UpdateSource();
```

Three failures are typical.
`GetBindingExpression` returns `null` and the second line throws a `NullReferenceException`; `UpdateSource()` is called but the source is not updated; or, among several `TextBox` controls in a form, only the one explicitly called reflects while the rest stay stale.

---

## Cause / Background

The first cause is that `GetBindingExpression` returns a `BindingExpression` **only when the dependency property has an active single `Binding`**, and returns `null` otherwise.
The official reference states that checking the return value for `null` is the technique for determining whether a property has an active binding.
It returns `null` when a literal is assigned such as `Text="fixed string"`, when `Text` holds a `MultiBinding` (use `GetMultiBindingExpression` in that case), or when the target `TextBox` sits inside another control's template and its name cannot be referenced directly.

Second, `UpdateSource()` does nothing unless the binding's `Mode` is `TwoWay` or `OneWayToSource`.
Calling it on a `OneWay` or `OneTime` binding is silently ignored without an exception.
Calling it while the binding is detached from its target throws an `InvalidOperationException`.

Third, `UpdateSource()` writes back only the single `BindingExpression` it was called on.
Committing an entire form requires calling it on each target `TextBox` individually, or using a bulk-update mechanism described below.

---

## Solution

First, always null-check the retrieved `BindingExpression` and, when it is `null`, isolate the cause: not bound, a `MultiBinding`, or inside a template.
Write back a single element safely with the null-conditional operator `?.`.
For a form that commits multiple elements together, either walk the visual tree and call each `TextBox`, or update in bulk with a `BindingGroup`.
To keep the View decoupled, avoid writing directly in code-behind and make the call reusable as an attached property (behavior).

---

## Implementation

The basic form for writing back a single `TextBox` is as follows.
Receive the result of `GetBindingExpression` with `?.` so that nothing happens when it is `null` (not bound, and so on).

```csharp
// amountBox is a TextBox bound with UpdateSourceTrigger=Explicit
BindingExpression be = amountBox.GetBindingExpression(TextBox.TextProperty);
be?.UpdateSource();
```

The `?.` avoids a `NullReferenceException` even when no binding exists.
To treat `null` as an error, branch explicitly to log it or return early.

To write back several `TextBox` controls in a form together, walk the visual tree recursively and call `UpdateSource()` on each `TextBox.Text` binding.

```csharp
// Write back every TextBox.Text binding under the given element
static void UpdateAllTextSources(DependencyObject root)
{
    int count = VisualTreeHelper.GetChildrenCount(root);
    for (int i = 0; i < count; i++)
    {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        if (child is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        UpdateAllTextSources(child);
    }
}
```

Run this walk after the visual tree is built (from `Loaded` onward).
Note that not-yet-realized elements inside a virtualized list are excluded from the walk.

To write back multiple bindings at once with validation, use a `BindingGroup`.
Setting a `BindingGroup` on a parent makes the descendant bindings join the group, and by default they do not update the source until `UpdateSources()` is called.

```xml
<StackPanel x:Name="formPanel">
    <StackPanel.BindingGroup>
        <BindingGroup />
    </StackPanel.BindingGroup>
    <TextBox Text="{Binding Street}" />
    <TextBox Text="{Binding City}" />
</StackPanel>
```

From code-behind, a single call to `UpdateSources()` is enough.
This method runs each binding's `ValidationRule` and, if all succeed, writes back to the sources and returns `true`.

```csharp
// Validate all participating bindings and write back only on success
bool committed = formPanel.BindingGroup.UpdateSources();
```

`UpdateSources()` writes nothing and returns `false` if even one validation fails.
It does not end the `IEditableObject` edit transaction, so use `CommitEdit()` to commit fully.

To push the display back from source to target, call `UpdateTarget()` instead of `UpdateSource()`.
The two run in opposite directions, and confusing them causes bugs such as "saved but the field does not change" or "wrote to the source when the intent was to discard input".

```csharp
// Force source-to-target (the inverse of UpdateSource; used to discard input and re-display)
amountBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
```

To avoid code-behind under MVVM, confine the `UpdateSource()` call to an attached property.
The following makes an "update on Enter" behavior reusable as an attached behavior.

```csharp
public static class TextBoxBehavior
{
    public static readonly DependencyProperty UpdateSourceOnEnterProperty =
        DependencyProperty.RegisterAttached(
            "UpdateSourceOnEnter",
            typeof(bool),
            typeof(TextBoxBehavior),
            new PropertyMetadata(false, OnChanged));

    public static bool GetUpdateSourceOnEnter(DependencyObject obj) =>
        (bool)obj.GetValue(UpdateSourceOnEnterProperty);

    public static void SetUpdateSourceOnEnter(DependencyObject obj, bool value) =>
        obj.SetValue(UpdateSourceOnEnterProperty, value);

    static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.KeyDown -= OnKeyDown;
        if ((bool)e.NewValue)
        {
            textBox.KeyDown += OnKeyDown;
        }
    }

    static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }
}
```

In XAML, only the attached property is set, keeping View-specific logic out of code-behind.

```xml
<TextBox Text="{Binding Amount, UpdateSourceTrigger=Explicit}"
         local:TextBoxBehavior.UpdateSourceOnEnter="True" />
```

When wiring events, use a named handler and `-=` before `+=` as above to avoid double subscription.
Subscribing with a lambda cannot be unsubscribed, so the subscriptions accumulate.

---

## Notes

- **Do not swallow `null`**: `?.` prevents the exception, but a `null` on an element that should be bound indicates a configuration mistake (wrong retrieval method for a `MultiBinding`, an element inside a template, or a name-resolution failure). Log the `null` branch while debugging.
- **`Mode` constraint**: `UpdateSource()` is silently ignored outside `TwoWay` / `OneWayToSource`. When nothing reflects, check `Mode` first.
- **Detached bindings**: calling it after the binding is detached (for example, the element left the tree) throws an `InvalidOperationException`.
- **Scope of bulk update**: the `VisualTreeHelper` walk targets only realized elements, so items not yet generated by virtualization are not written back. Watch for unrealized regions such as inactive `TabControl` tabs.
- **`BindingGroup` is tied to validation**: `UpdateSources()` runs the `ValidationRule` objects and returns `false` without writing back on failure. Used as a plain bulk write, a validation failure can look like no response.
- **Direction of `UpdateSource` vs `UpdateTarget`**: the former is target-to-source, the latter source-to-target. Choose by intent (commit or discard).

---

## Alternatives / Comparison

Choose the way to write back from the View according to scope and design policy.

| Approach | Scope | Pros | Cons | Best suited for |
|---|---|---|---|---|
| `GetBindingExpression().UpdateSource()` | Single element | Clear and easy to control | Must be called per element | Committing one specific field |
| `VisualTreeHelper` walk (bulk) | All descendant `TextBox` | Writes back many at once | Excludes unrealized elements; walk cost | Committing a realized group of inputs |
| `BindingGroup.UpdateSources()` | Bindings in the group | Bulk commit tied to validation | Requires validation design; return value handling | Forms validating and saving multiple fields |
| Via an attached behavior | The element it is set on | Keeps the View decoupled; reusable | More implementation code | MVVM where code-behind is avoided |

Calling `UpdateSource()` directly is the clearest choice for committing a single field.
A `BindingGroup` suits committing a whole form with validation, and an attached behavior keeps MVVM separation intact.

---

## Summary

The basic form for writing a `TextBox` binding back from the View is `GetBindingExpression(TextBox.TextProperty)?.UpdateSource()`.
Because `GetBindingExpression` returns `null` when there is no binding, guard it with `?.`, and suspect a `MultiBinding`, a template, or name resolution when `null` appears on an element that should be bound.
Keep in mind that `UpdateSource()` works only on `TwoWay` / `OneWayToSource` and throws once detached.
Call it directly for a single commit, use `BindingGroup.UpdateSources()` to commit multiple fields with validation, and move to an attached behavior when View separation is the priority.
To push the direction back, use `UpdateTarget()` rather than `UpdateSource()` to avoid bugs from confusing the two.

The separate question of *when* the update fires (choosing among `LostFocus` / `PropertyChanged` / `Explicit`) is covered in [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/).

---

<!-- Related articles -->
<!-- - [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/) -->
