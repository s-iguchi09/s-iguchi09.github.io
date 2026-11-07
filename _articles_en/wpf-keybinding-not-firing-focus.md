---
layout: article-en
title: "WPF KeyBinding Not Firing: Find Where Keyboard Focus Is"
date: 2026-11-07
category: WPF
excerpt: "A KeyBinding runs only if it sits on the path from the focused element up to the Window. Real key presses separate three focus-related causes and their fixes."
image: /images/articles/wpf-keybinding-not-firing-focus/keybinding-focus-position.svg
---

## Overview

A `KeyBinding` for `Ctrl+S` can work in one part of a window and do nothing in another.
In that situation, where keyboard focus is may deserve suspicion before the `Command` binding does.

A `KeyBinding` runs only when it sits on the path from the element that has keyboard focus up to the `Window`.
This article sorts the failures into three cases: focus is nowhere or in another window, focus is outside the element that holds the `KeyBinding`, and the focused control uses the key itself.
Each case is checked with real key presses.
The article also shows how to tell what is wrong when focus is on the path and the binding still does not run, and that adding `Focusable="True"` to a `UserControl` does not move focus to it when a `TextBlock` inside it is clicked.

---

## Prerequisites / Environment

- Framework: WPF on .NET Framework 4.0 or later and .NET Core 3.0 or later (the versions in which the `Command` of a `KeyBinding` can be bound and `Keyboard.ClearFocus` exists)
- Verified on: .NET 10.0.13 / Windows 11 (10.0.26200)
- Architecture: MVVM (the `Command` of each `KeyBinding` is bound to an `ICommand` on the view model)
- Measurement: the test window was brought to the foreground and keys were pressed through OS input (`SendInput`). Mouse clicks also went through OS input.

---

## Problem

In the following window, `Ctrl+S`, `Ctrl+Z`, and `Enter` are assigned on the `Window`, and `Ctrl+D` on the editing panel, a `UserControl`.

```xml
<Window ...>
  <Window.InputBindings>
    <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveCommand}" />
    <KeyBinding Key="Z" Modifiers="Control" Command="{Binding UndoCommand}" />
    <KeyBinding Key="Enter" Command="{Binding SubmitCommand}" />
  </Window.InputBindings>
  <StackPanel Margin="8" Width="240">
    <TextBox x:Name="titleBox" Text="{Binding Title}" />
    <Button x:Name="okButton" Content="OK" Command="{Binding OkCommand}" Margin="0,4" />
    <UserControl x:Name="panel" Background="Transparent">
      <UserControl.InputBindings>
        <KeyBinding Key="D" Modifiers="Control" Command="{Binding DeleteCommand}" />
      </UserControl.InputBindings>
      <StackPanel>
        <TextBlock x:Name="panelLabel" Text="Panel" Padding="0,8" />
        <TextBox x:Name="panelBox" />
      </StackPanel>
    </UserControl>
  </StackPanel>
</Window>
```

The `DataContext` is a single view model set on the `Window`, and each command only records that it ran.
Every command returns `true` from `CanExecute`, so `CanExecute` plays no part in the results below.

The window shows these symptoms.

- `Ctrl+D` does nothing while typing in `titleBox` at the top
- `Ctrl+Z` is not called while typing in `titleBox`
- When `titleBox` accepts multiple lines (`AcceptsReturn="True"`), `Enter` only inserts a line break

---

## What the Symptom Narrows Down

Keyboard events are raised on the element that has keyboard focus.
Bubbling events such as `KeyDown` travel from there up to the parents, while tunneling events such as `PreviewKeyDown` travel the other way, from the root down (Microsoft Learn, [Input Overview](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/input-overview)).
In the measurements in Table 1, a `KeyBinding` also ran only when it was on this path.
The focus-related causes therefore narrow down to three.

| Symptom | Cause to suspect |
|---|---|
| Fails everywhere in the window for a while | Focus is nowhere, or it is in another window |
| Fails only while typing in one part of the window, and the `KeyBinding` sits on a `UserControl` or panel | Focus is outside the element that holds the `KeyBinding` |
| Fails only while typing in one control, and that control uses keys itself (`TextBox` and the like) | The focused control uses the key first |

A `KeyBinding` written with the `Key` attribute ran without a modifier key on the test machine (Table 4 under Caveats).

---

## Diagnosis Procedure

The starting point is `Keyboard.FocusedElement` just before the key is pressed.
The checks go in this order.

1. If `Keyboard.FocusedElement` is `null`, focus is nowhere.
2. If it is not `null`, pass it to `Window.GetWindow` as a `DependencyObject`. If the result is not the window that holds the `KeyBinding`, focus is in another window.
3. If it is in the same window, read `IsKeyboardFocusWithin` on the element that holds the `KeyBinding`. If it is `false`, focus is outside that element.
4. If it is `true`, the `KeyBinding` is on the path. If it still does not run, either the `Command` binding is not resolved, or another element on the path handles the key first. The former shows as a `null` `Command` on that `KeyBinding` (the element of `InputBindings`), as in the row in Table 1 where the other window has no `DataContext`. The latter can be the focused control itself, a `KeyBinding` for the same key on an element further in, or a handler on any element on the path (including the outer `Window`) that sets `Handled` in `PreviewKeyDown` (Table 2). The case where `CanExecute` returns `false` was not measured in this article.

The "isolation step that applies" column in Tables 1 and 2 is these four steps written as code and evaluated just before the key press.
No command ran in any row judged 1 to 3.
In every row judged 4 where nothing ran, apart from the owned window without a `DataContext`, focus was in a `TextBox`.

Focus moves while the user works toward pressing the key, so recording every move makes the position at the key press readable afterward.
For the duration of the check, the following debug code can go temporarily into the static constructor of `App` (`static App()`).
It needs `using System.Windows;` and `using System.Windows.Input;`.

```csharp
static App()
{
    // Write the element that got focus. The event also travels to the parents, so write only for that element itself.
    EventManager.RegisterClassHandler(
        typeof(UIElement),
        Keyboard.GotKeyboardFocusEvent,
        new KeyboardFocusChangedEventHandler((sender, e) =>
        {
            if (sender == e.OriginalSource)
            {
                System.Diagnostics.Debug.WriteLine($"focus: {e.NewFocus}");
            }
        }),
        handledEventsToo: true);

    // When focus goes nowhere, no element gets it, so write from the element that lost it.
    EventManager.RegisterClassHandler(
        typeof(UIElement),
        Keyboard.LostKeyboardFocusEvent,
        new KeyboardFocusChangedEventHandler((sender, e) =>
        {
            if (sender == e.OriginalSource && e.NewFocus is null)
            {
                System.Diagnostics.Debug.WriteLine("focus: null");
            }
        }),
        handledEventsToo: true);
}
```

As long as focus is on a `UIElement`, the last line written is the focus at the time of the key press.
After `Keyboard.ClearFocus()`, the last line was `focus: null`, and after a following `Keyboard.Focus(window)` it was `focus: System.Windows.Window`.
Moving focus to an empty `TextBox` produced a line starting with `focus: System.Windows.Controls.TextBox`.
The measurement used a plain `Window`, hence `System.Windows.Window`; in an application, the derived class name such as `MainWindow` appears instead.
Only `UIElement` elements are written.
When focus moves to a `ContentElement` such as a `Hyperlink`, no line is written, so the last line matches the focus only while focus is on a `UIElement`.
A handler registered with `RegisterClassHandler` cannot be removed and applies to the whole application, so the code should be deleted once the check is done.

---

## Fixes by Cause

### Focus Is Nowhere or in Another Window

After `Keyboard.ClearFocus()`, `Keyboard.FocusedElement` was `null` and the `Window`'s `Ctrl+S` did not run.
The `Window`'s `PreviewKeyDown` was not raised either.
The key event never reached the `Window`.
While typing in a `TextBox` in another window whose Owner is the main window, the main `Window`'s `PreviewKeyDown` was not raised and `Ctrl+S` did not run.

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-focus-position.en.md %}

Table 1: Where keyboard focus is and which command ran. Apart from the differences written in "where the KeyBinding is", the XAML is the one in the Problem section. The isolation step was judged against the `KeyBinding` on the `Window` for the Ctrl+S rows (on the owned window for the row that also places it there), and on the `UserControl` for the Ctrl+D rows (on the `Window` for the row that places it there). The key was pressed once through OS input after the listed preparation. Clicks also went through OS mouse input. .NET 10.0.13 / Windows 11.
{: .table-caption}

When focus is `null`, `Keyboard.Focus(window)` moves it to the `Window`.
In Table 1, calling `Keyboard.Focus(window)` after `Keyboard.ClearFocus()` put focus back on the `Window`, and `Ctrl+S` ran.
When the window was only shown and activated, with no focus set anywhere, focus was on the `Window` itself and `Ctrl+S` ran.
Setting `IsEnabled = false` on the focused `Button`, or collapsing the focused `TextBox`, also moved focus to the `Window`, and `Ctrl+S` ran.
Among the operations tried in Table 1, focus became `null` only when `Keyboard.ClearFocus()` was called.
According to the Microsoft Learn page for [Keyboard.ClearFocus](https://learn.microsoft.com/dotnet/api/system.windows.input.keyboard.clearfocus), however, when focus is in a Win32 window hosted through `HwndHost` or similar and a menu closes, WPF itself calls `ClearFocus`, and no WPF element has focus.

For the same shortcut to work in another window, the same `KeyBinding` has to be placed on that window as well.
In the "Window and the owned window" row of Table 1, with `Ctrl+S` also on the other window and the same view model set as its `DataContext`, `SaveCommand` ran while typing in that window's `TextBox`.
In the row where the other window had no `DataContext`, `SaveCommand` did not run even though the step was "4: on the path".
Read just before the key press, the `Command` of the other window's `KeyBinding` was `null` in that row.
Setting Owner does not make the main `Window`'s `DataContext` reach the other window's `{Binding SaveCommand}`.
The other window therefore also needs a view model with the same command as its `DataContext`.

### Focus Is Outside the Element That Holds the KeyBinding

`Ctrl+D` on the `UserControl` ran while typing in `panelBox` inside the `UserControl`, and did not run while typing in `titleBox` outside it.
The Ctrl+D rows for "Keyboard.Focus(panelBox)" and "Keyboard.Focus(titleBox)" in Table 1 show this.
While typing in `titleBox`, the `Window`'s `PreviewKeyDown` was raised, so the key did reach the `Window`.
Even so, the `KeyBinding` on the `UserControl`, which is off the path, did not run.

Shortcuts that must work across the whole window belong in the `Window`'s `InputBindings`.
In the "Window (removed from the UserControl)" row of Table 1, `Ctrl+D` removed from the `UserControl` and placed on the `Window` ran even while typing in `titleBox`.

Adding `Focusable="True"` to the `UserControl` did not fix this.
With focus in `titleBox`, clicking the `TextBlock` inside the `UserControl` left focus in `titleBox`, whether `Focusable` was `False` or `True`.
Focus moved to the `UserControl` only when `Focusable="True"` was set and `Keyboard.Focus(panel)` was called from code.
`Focusable` is the property that decides whether an element can receive focus (Microsoft Learn, [UIElement.Focusable](https://learn.microsoft.com/dotnet/api/system.windows.uielement.focusable)).

### The Focused Control Uses the Key

While typing in `titleBox`, the `Window`'s `Ctrl+Z` did not run and the `TextBox`'s own undo worked instead.
Even with nothing typed yet and nothing to undo, the `Window`'s `Ctrl+Z` did not run.
In the same way, `Enter` inserted a line break in a `TextBox` with `AcceptsReturn="True"`, and on a `Button` it ran the `Button`'s command.
In neither case did the `Window`'s `Enter` run.
Every row was judged "4: on the path", so the placement of the `KeyBinding` is not the problem here.

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-key-consumed.en.md %}

Table 2: Keys the focused control uses itself. Every row keeps the `Window`'s `Ctrl+Z` (`UndoCommand`) and `Enter`; the placement column lists the `KeyBinding`s and handlers added and the attributes changed. The isolation step was judged against the `KeyBinding` on the `Window` (on the `TextBox` for the titleBox.InputBindings row). `"abc"` was typed through the InputManager, and each key was pressed once through OS input. .NET 10.0.13 / Windows 11.
{: .table-caption}

A key can be taken away from a control by placing a `KeyBinding` for that key in the control's own `InputBindings`.
In the "Window and titleBox.InputBindings (TextUndoCommand)" row of Table 2, the `Window`'s `Ctrl+Z` was kept and the `KeyBinding` on the `TextBox` ran a separate command, `TextUndoCommand`.
Only `TextUndoCommand` ran, the `Window`'s `UndoCommand` did not, and the `TextBox` text stayed `"abc"`.
The `KeyBinding` on the `TextBox` itself was handled before both the `TextBox`'s undo and the `Window`'s `KeyBinding`.
Once the key is taken, however, `Ctrl+Z` no longer runs the `TextBox`'s undo.
Taking the key fits when the application replaces the `TextBox` operation with its own, for example undo based on an edit history held by the view model.
Otherwise, assigning the command to a key that the `TextBox` does not use stays closer to what users expect.

---

## Implementation Example

The following window collects the window-wide shortcuts on the `Window` and replaces `Ctrl+Z` with undo based on the view model's edit history.
`Ctrl+Z` overlaps with the `TextBox`'s own behavior, so it goes on the `TextBox`.
The view (XAML) contains only bindings, and the code-behind contains nothing but `InitializeComponent`.

```xml
<Window ...>
  <Window.InputBindings>
    <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveCommand}" />
    <!-- On the UserControl, this would not work while typing outside it -->
    <KeyBinding Key="D" Modifiers="Control" Command="{Binding DeleteCommand}" />
    <KeyBinding Key="Enter" Command="{Binding SubmitCommand}" />
  </Window.InputBindings>
  <StackPanel Margin="8" Width="240">
    <TextBox x:Name="titleBox" Text="{Binding Title}">
      <TextBox.InputBindings>
        <!-- Handled before the TextBox's own undo -->
        <KeyBinding Key="Z" Modifiers="Control" Command="{Binding UndoCommand}" />
      </TextBox.InputBindings>
    </TextBox>
    <Button x:Name="okButton" Content="OK" Command="{Binding OkCommand}" Margin="0,4" />
    <UserControl x:Name="panel">
      <StackPanel>
        <TextBlock Text="Panel" Padding="0,8" />
        <TextBox x:Name="panelBox" />
      </StackPanel>
    </UserControl>
  </StackPanel>
</Window>
```

Table 3 shows the result of pressing each key in this exact XAML.

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-implementation.en.md %}

Table 3: Keys pressed in the implementation example's XAML and the command that ran. `"abc"` was typed through the InputManager, and each key was pressed once through OS input. .NET 10.0.13 / Windows 11.
{: .table-caption}

Because `Ctrl+S` and `Ctrl+D` are on the `Window`, both ran while typing in `panelBox` inside the `UserControl` and in `titleBox` outside it.
For `Ctrl+Z`, the `UndoCommand` of the `KeyBinding` on the `TextBox` ran, and the typed `"abc"` remained.
For `Enter`, `SubmitCommand` ran in `titleBox`, which has no `AcceptsReturn`, and the `Button`'s command ran on the `Button`.
In a `TextBox` with `AcceptsReturn="True"`, the `Window`'s `Enter` does not run either (Table 2).
A window that uses `Enter` to submit has to be designed on the assumption that it does not submit while a control that uses `Enter` has focus.

Receiving the key first in `Window.PreviewKeyDown` and setting `e.Handled = true` also runs the command, with the same result in Table 2 as `TextBox.InputBindings`.
`PreviewKeyDown` travels down from the `Window`, so it is handled before the `TextBox`.
That approach puts logic in the code-behind, so this article solves the problem by where the `InputBindings` are placed.

---

## Caveats

- Tables 1–3 were measured with commands whose `CanExecute` always returns `true`. Behavior when `CanExecute` returns `false` was not measured.
- A `KeyBinding` on a `UserControl` that works only inside the `UserControl` is correct behavior for a shortcut scoped to that component. A key that only means something inside the editing panel can stay on the `UserControl`.
- For a `KeyBinding` without a modifier key, the result depended on how it was written (Table 4). `Key="A"`, `Key="F2"`, and `Key="Enter"` written with the `Key` attribute alone all ran. `Gesture="A"`, on the other hand, threw while loading the XAML, and `new KeyGesture(Key.A)` and `new KeyBinding(command, Key.A, ModifierKeys.None)` in code threw `NotSupportedException`. The Microsoft Learn page for [KeyBinding](https://learn.microsoft.com/dotnet/api/system.windows.input.keybinding) says that, apart from function keys and numeric keypad keys, a gesture needs a modifier key and one without it never invokes its command, but the `Key` attribute form did not behave that way. The same page also says nothing prevents creating an invalid gesture in XAML or code, yet the `Gesture` attribute and the constructors threw at creation time.
- Assigning a letter key without a modifier can conflict with text entry in controls that accept text. That combination was not measured in this article.
- The measurements used OS key input. When tests raise a key event directly on an element with `RaiseEvent`, the route starts at that element (Microsoft Learn, [Routed events overview](https://learn.microsoft.com/dotnet/desktop/wpf/events/routed-events-overview)). How a `KeyBinding` behaves in that case was not measured in this article.

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-no-modifier.en.md %}

Table 4: How a `KeyBinding` without a modifier key was written, and the result. For the XAML rows, the binding was placed on the `Window`, `Keyboard.FocusedElement` was confirmed to be the `Window` itself, and the key was pressed once through OS input. The `Gesture="A"` row is the result of loading the XAML; no key was pressed. The code rows show the result at the point of creating the `KeyGesture` or `KeyBinding`. .NET 10.0.13 / Windows 11.
{: .table-caption}

---

## Summary

A `KeyBinding` runs only when it sits on the path from the element with keyboard focus up to the `Window`.
When one does not run, checking `Keyboard.FocusedElement` first tells whether focus is nowhere, in another window, or outside the element that holds the `KeyBinding`.
If focus is on the path, the next checks are whether the `KeyBinding`'s `Command` is `null` (an unresolved binding) and whether another element on the path handles the key first.

- When focus has become `null`, `Keyboard.Focus(window)` moves it to the `Window`. A shortcut that must also work in another window has to be placed on that window too, with its `DataContext` set.
- Window-wide shortcuts belong in the `Window`'s `InputBindings`. Adding `Focusable="True"` to a `UserControl` did not make a click on a `TextBlock` inside it move focus to it.
- When the application replaces a `TextBox` operation with its own, the `KeyBinding` goes in that `TextBox`'s `InputBindings`. Without such a reason, the command is better assigned to a key the `TextBox` does not use.

---

## Related Articles

- [Why a WPF DataGrid Edit Does Not Reach the ViewModel When Save Is Clicked, and How to Use CommitEdit](/articles/wpf-datagrid-edit-not-committed-on-save/): covers where focus is when saving with a `Ctrl+S` `KeyBinding`
- [Fixing a RelayCommand Whose CanExecute Does Not Update the Button State in WPF](/articles/wpf-relaycommand-canexecute-not-updating/)
- [Binding to a WPF UserControl's Own Dependency Property from Inside the Control](/articles/wpf-usercontrol-dependencyproperty-binding-not-working/)
