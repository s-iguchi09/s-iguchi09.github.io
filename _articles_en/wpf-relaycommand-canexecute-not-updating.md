---
layout: article-en
title: "Fixing a RelayCommand Whose CanExecute Does Not Update the Button State in WPF"
date: 2026-07-23
category: WPF
excerpt: "A custom RelayCommand's button stays stuck when CanExecuteChanged is never raised. This compares delegating to RequerySuggested with raising it manually."
---

## Overview

In WPF MVVM, a button is bound to an `ICommand` on the view model, and its enabled state follows the result of `CanExecute`.
A common defect is that changing the condition `CanExecute` depends on, such as whether an input field is filled, does not update the button.
This article explains that the cause is a missing `ICommand.CanExecuteChanged` notification, and it organizes two approaches with their trade-offs: delegating to `CommandManager.RequerySuggested`, and raising `CanExecuteChanged` manually.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (the same applies to .NET Framework 4.5 and later)
- Language: C# / XAML (samples assume nullable reference types are enabled; on C# 8 or earlier, drop the nullable annotations)
- Target feature: a custom `RelayCommand` implementing `System.Windows.Input.ICommand`, bound through `Button.Command`
- Architecture: MVVM (command logic lives in the view model)
- Namespaces: `System`, `System.Windows.Input`

---

## Problem

Consider binding a view model command to `Button.Command` and driving the enabled state from `CanExecute`.
The following code intends to enable a save button only when a name has been entered.

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter) => _execute();

    // Never raised, so the button stays at its first evaluation
    public event EventHandler? CanExecuteChanged;
}
```

`CanExecute` is evaluated once at startup, but entering a value into `Name` afterward leaves the save button disabled.
Because `CanExecuteChanged` is never raised, the button has no trigger to re-evaluate `CanExecute`.

---

## Cause / Background

A command source, such as a `Button`, subscribes to `ICommand.CanExecuteChanged` and re-queries `CanExecute` only when that event is raised, updating its own enabled state accordingly.
The official documentation states that a command source typically subscribes to `CanExecuteChanged`, calls `CanExecute` when it is raised, and disables itself if the command cannot execute.
Therefore, no matter how the return value of `CanExecute` changes, the button never reflects it unless `CanExecuteChanged` is raised.

The reason the built-in `RoutedCommand` rarely exposes this problem is that its `CanExecuteChanged` is delegated to `CommandManager.RequerySuggested`.
When the `CommandManager` detects conditions that might change a command's ability to execute, such as a change in keyboard focus, it raises `RequerySuggested` and prompts every bound command to re-evaluate.
A custom `RelayCommand` does not ride on this mechanism, so it is responsible for raising `CanExecuteChanged` itself.
Note also that the `CommandManager` only detects UI interactions such as focus changes; it does not detect UI-independent condition changes, such as a view model property being updated.

---

## Solution

There are two ways to raise `CanExecuteChanged`.

- **Delegate to `CommandManager.RequerySuggested`** — forward the `CanExecuteChanged` subscription to `CommandManager.RequerySuggested`. This rides on the re-evaluation triggered by UI interactions with minimal code. For UI-independent conditions, call `CommandManager.InvalidateRequerySuggested()` to force a re-evaluation.
- **Raise `CanExecuteChanged` manually** — keep a dedicated event and raise it explicitly when the condition changes. Re-evaluation is limited to the command in question, and the trigger is fully under control.

The former rides on WPF's re-evaluation cycle; the latter re-evaluates only when explicitly told to.

---

## Implementation

### Delegate to CommandManager.RequerySuggested

The `add` / `remove` of `CanExecuteChanged` is forwarded to `CommandManager.RequerySuggested`.
The `CommandManager` then prompts a re-evaluation on each UI interaction, such as a focus change, and the button follows.

```csharp
public event EventHandler? CanExecuteChanged
{
    add    => CommandManager.RequerySuggested += value;
    remove => CommandManager.RequerySuggested -= value;
}
```

For condition changes without a UI interaction, such as a timer or the completion of an asynchronous operation, force a re-evaluation explicitly.
`InvalidateRequerySuggested` re-evaluates every command subscribed to `RequerySuggested` (the built-in `RoutedCommand` and delegating `RelayCommand`) at once.

```csharp
// Called when a condition changes without a UI interaction
CommandManager.InvalidateRequerySuggested();
```

This call does not evaluate immediately; it raises `RequerySuggested` to prompt each command source to re-query.
It therefore carries the cost of re-evaluating every subscribed command, as noted below.

### Raise CanExecuteChanged manually

`CanExecuteChanged` is kept as a dedicated event, with a method that raises it when re-evaluation is needed.

```csharp
public event EventHandler? CanExecuteChanged;

public void RaiseCanExecuteChanged()
    => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
```

In the view model, `RaiseCanExecuteChanged` is called right after updating a property that `CanExecute` depends on.
The following raises a re-evaluation whenever the save button's condition, whether `Name` is entered, changes.

```csharp
private string _name = string.Empty;
public string Name
{
    get => _name;
    set
    {
        if (_name == value) return;
        _name = value;
        // The input state changed, so re-evaluate the save command
        SaveCommand.RaiseCanExecuteChanged();
    }
}
```

With this approach, only `SaveCommand` is re-evaluated, and the timing is explicit.
The `RelayCommand` in `CommunityToolkit.Mvvm` uses this approach, exposing an equivalent trigger through the `NotifyCanExecuteChanged()` method and the `[NotifyCanExecuteChangedFor]` attribute.

---

## Notes

- **`RequerySuggested` holds handlers by weak reference:** `CommandManager.RequerySuggested` keeps registered handlers as weak references. In the delegating approach the handler registered with `RequerySuggested` is created and held by the command source (such as a `Button`) itself, so it is not collected while that source stays alive in the visual tree, and this is usually fine; but a custom implementation that registers a handler directly with `RequerySuggested` must keep a separate strong reference, or the handler is collected and re-evaluation stops.
- **Call `InvalidateRequerySuggested` on the UI thread:** this API prompts the `CommandManager` to re-evaluate and assumes it is called on the UI thread. When state changes on a background thread, marshal to the UI thread with the `Dispatcher` before calling it.
- **Raise manual events on the UI thread as well:** `RaiseCanExecuteChanged` synchronously invokes the button-side handler, which updates a UI element. Raising it from another thread touches UI elements off the UI thread, so marshal to the UI thread with the `Dispatcher`.
- **Delegation re-evaluates every subscribed command:** `InvalidateRequerySuggested` re-queries `CanExecute` on every command subscribed to `RequerySuggested`. Heavy work inside `CanExecute` makes frequent re-evaluation harm responsiveness, so keep `CanExecute` lightweight.
- **Do not leave `CanExecuteChanged` declared but unraised:** the opening example, which declares `CanExecuteChanged` without ever raising it, compiles cleanly yet is a classic reason the state stays frozen.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best suited for |
|---|---|---|---|
| Delegate to `RequerySuggested` | Minimal code; follows UI interactions automatically | Re-evaluates all subscribed commands; opaque trigger; weak-reference caveat | Executability tied mainly to UI interaction (focus, selection) |
| Raise `CanExecuteChanged` manually | Re-evaluates only the target command; explicit trigger | Requires an explicit raise per condition change | Executability determined by view model properties |
| Call `InvalidateRequerySuggested` on demand | Re-evaluates at any moment while delegating | Cost of re-evaluating all subscribed commands; easy to forget | Reflecting UI-independent changes under the delegating approach |

---

## Summary

The button fails to update because of a missing `CanExecuteChanged` notification, not because of the `CanExecute` result itself.
The selection criteria are as follows.

- **When executability is tied to UI interactions such as focus or selection:** delegate to `CommandManager.RequerySuggested`. It needs the least code and follows UI interactions automatically.
- **When executability is determined by view model properties:** raise `CanExecuteChanged` manually. Only the target command is re-evaluated, exactly when the condition changes. Using a framework such as `CommunityToolkit.Mvvm` amounts to this approach.
- **When reflecting an asynchronous completion under the delegating approach:** call `CommandManager.InvalidateRequerySuggested()` at that moment.

Raising every notification on the UI thread and keeping `CanExecute` lightweight are the prerequisites for not harming responsiveness.

---

<!-- Related articles -->
- [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/)
