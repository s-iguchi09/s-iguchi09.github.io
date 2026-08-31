---
layout: article-en
title: "Fixing a RelayCommand Whose CanExecute Does Not Update the Button State in WPF"
date: 2026-07-23
category: WPF
excerpt: "A custom RelayCommand's button stays stuck when CanExecuteChanged is never raised. This compares delegating to RequerySuggested with raising it manually."
image: /images/articles/wpf-relaycommand-canexecute-not-updating/relaycommand-canexecute-button-state.png
---

## Overview

In WPF MVVM, a button is bound to an `ICommand` on the view model, and its enabled state follows the result of `CanExecute`.
A common defect is that changing the condition `CanExecute` depends on, such as whether an input field is filled, does not update the button.
This article explains that the cause is a missing `ICommand.CanExecuteChanged` notification, and it organizes two approaches with their trade-offs: delegating to `CommandManager.RequerySuggested`, and raising `CanExecuteChanged` manually.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (the same applies to .NET Framework 4.5 and later)
- Language: C# / XAML (samples assume nullable reference types are enabled; on C# 7 or earlier, drop the nullable annotations)
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

`CanExecute` is evaluated when the command is first bound. It would normally be re-evaluated afterward in response to `CanExecuteChanged`, but because this implementation never raises it, entering a value into `Name` leaves the save button disabled.
The button has no trigger to re-evaluate `CanExecute`.

<figure class="article-figure">
  <img src="/images/articles/wpf-relaycommand-canexecute-not-updating/relaycommand-canexecute-button-state.png" alt="Two pairs of an input box and a button holding the same text. With an implementation that never raises CanExecuteChanged the button stays disabled, while delegating to CommandManager.RequerySuggested enables it." width="382" height="179" loading="lazy">
  <figcaption>Both rows use the same condition (executable when <code>Name</code> is not empty) and contain the same text. The upper implementation never raises <code>CanExecuteChanged</code>, so the button stays disabled after typing. The lower one delegates to <code>CommandManager.RequerySuggested</code>, so the requery runs and the button becomes enabled.</figcaption>
</figure>

---

## Cause / Background

A command source, such as a `Button`, subscribes to `ICommand.CanExecuteChanged` and re-queries `CanExecute` only when that event is raised, updating its own enabled state accordingly.
The official documentation states that a command source typically subscribes to `CanExecuteChanged`, calls `CanExecute` when it is raised, and disables itself if the command cannot execute.
Therefore, no matter how the return value of `CanExecute` changes, the button never reflects it unless `CanExecuteChanged` is raised.
This evaluation only applies when an `ICommand` is actually assigned to `Command`.
If the binding fails to resolve and `Command` stays `null`, there is nothing to evaluate, and the button remains enabled while doing nothing (see [Binding to the Parent DataContext from Inside a WPF DataTemplate](/articles/wpf-datatemplate-parent-datacontext-binding/)).

The reason the built-in `RoutedCommand` rarely exposes this problem is that its `CanExecuteChanged` is delegated to `CommandManager.RequerySuggested`.
When the `CommandManager` detects conditions that might change a command's ability to execute, such as a change in keyboard focus, it raises `RequerySuggested` and prompts every bound command to re-evaluate.
A custom `RelayCommand` does not ride on this mechanism, so it is responsible for raising `CanExecuteChanged` itself.
Note also that the `CommandManager` only detects UI interactions such as focus changes; it does not detect UI-independent condition changes, such as a view model property being updated.

---

Which way of raising the event reaches which implementation can be confirmed by displaying the button and reading `IsEnabled`.
The figure below shows the button displayed while `CanExecute` returns `false`, the condition then changed so it returns `true`, and the result of calling nothing, `CommandManager.InvalidateRequerySuggested()`, or the command's own `RaiseCanExecuteChanged()`.

<figure class="article-figure">
  <img src="/images/articles/wpf-relaycommand-canexecute-not-updating/relaycommand-requery.svg" alt="A table of Button.IsEnabled per implementation and per way of raising the event. Calling nothing leaves both implementations at False. InvalidateRequerySuggested reaches only the implementation that delegates to RequerySuggested. RaiseCanExecuteChanged reaches only the implementation with its own event. A button with no Command is True throughout." width="548" height="290" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11, reading <code>Button.IsEnabled</code> before and after <code>CanExecute</code> switches from <code>false</code> to <code>true</code>. <code>before</code> is taken just prior to changing the condition; <code>after</code> is taken once the condition changed and the listed call was made.</figcaption>
</figure>

**With nothing called, `IsEnabled` stays `False` on both implementations.** A changed return value from `CanExecute` alone does not reach the button.

Beyond that, **the way the event is raised must match the implementation.**
`InvalidateRequerySuggested()` reaches only an implementation that forwards `CanExecuteChanged` to `CommandManager.RequerySuggested`; it never reaches one that holds its own event.
`RaiseCanExecuteChanged()` is the reverse, reaching only the implementation with its own event.
The choice of approach therefore determines what has to be called when a condition changes.

The last row is a control: a button whose `Command` is unset has nothing to evaluate, so it stays enabled throughout.

---

## Two Ways to Raise the Notification

There are two ways to raise `CanExecuteChanged`.

- **Delegate to `CommandManager.RequerySuggested`** — forward the `CanExecuteChanged` subscription to `CommandManager.RequerySuggested`. This rides on the re-evaluation triggered by UI interactions with minimal code. For UI-independent conditions, call `CommandManager.InvalidateRequerySuggested()` to force a re-evaluation.
- **Raise `CanExecuteChanged` manually** — keep a dedicated event and raise it explicitly when the condition changes. Re-evaluation is limited to the command in question, and the trigger is fully under control.

The former rides on WPF's re-evaluation cycle; the latter re-evaluates only when explicitly told to.

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
`InvalidateRequerySuggested` raises `RequerySuggested`, prompting the connected command sources (buttons subscribing through the built-in `RoutedCommand` or a delegating `RelayCommand`) to re-query `CanExecute`.

```csharp
// Called when a condition changes without a UI interaction
CommandManager.InvalidateRequerySuggested();
```

This call does not evaluate immediately; it raises `RequerySuggested` to prompt the connected command sources to re-query `CanExecute`.
It therefore carries the cost of re-evaluating the command sources connected to `RequerySuggested`, as noted below.

### Raise CanExecuteChanged manually

The `CanExecuteChanged` of the opening `RelayCommand` is changed to a dedicated event, and a method that raises it when re-evaluation is needed is added.

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

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

The view model initializes `SaveCommand` and calls `RaiseCanExecuteChanged` right after updating a property (`Name`) that `CanExecute` depends on.
The following is a compilable, minimal setup that re-evaluates whenever the save button's condition, whether `Name` is entered, changes.

```csharp
public class SaveViewModel
{
    public RelayCommand SaveCommand { get; }

    public SaveViewModel()
    {
        // Executable when Name is not empty
        SaveCommand = new RelayCommand(Save, () => !string.IsNullOrEmpty(Name));
    }

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

    private void Save() { /* Implement the save logic */ }
}
```

With this approach, only `SaveCommand` is re-evaluated, and the timing is explicit.
The `RelayCommand` in `CommunityToolkit.Mvvm` uses this approach, exposing an equivalent trigger through the `NotifyCanExecuteChanged()` method and the `[NotifyCanExecuteChangedFor]` attribute.

---

## How to Choose

Which one applies is settled by where the condition that decides executability lives.

**When executability is tied to UI interaction (focus movement, selection changes), delegate.**
`CommandManager` prompts re-evaluation on those interactions, so the command follows along without raising anything. This needs the least code.

**When executability is determined by a view model property, raise manually.**
Only the target command is re-evaluated, at the moment the property changes, and the trigger is visible to the reader. Frameworks such as `CommunityToolkit.Mvvm` take this approach.

**To reflect a UI-independent change while still delegating, call `CommandManager.InvalidateRequerySuggested()` at that point.**
The completion of asynchronous work is one such trigger. It re-queries every command source connected to `RequerySuggested`, though, so a high frequency costs responsiveness.

---

## Comparing the Approaches

| Approach | Pros | Cons | Best suited for |
|---|---|---|---|
| Delegate to `RequerySuggested` | Minimal code; follows UI interactions automatically | Re-queries the sources connected to `RequerySuggested`; opaque trigger; weak-reference caveat | Executability tied mainly to UI interaction (focus, selection) |
| Raise `CanExecuteChanged` manually | Re-evaluates only the target command; explicit trigger | Requires an explicit raise per condition change | Executability determined by view model properties |
| Call `InvalidateRequerySuggested` on demand | Re-evaluates at any moment while delegating | Cost of re-querying the sources connected to `RequerySuggested`; easy to forget | Reflecting UI-independent changes under the delegating approach |

---

## Notes

- **`RequerySuggested` holds handlers by weak reference:** `CommandManager.RequerySuggested` keeps registered handlers as weak references. In the delegating approach, WPF provides the machinery that keeps the handler alive for as long as the command source (such as a `Button`) is, so this is usually fine. Registering a handler with `RequerySuggested` yourself, on the other hand, means managing its lifetime so that it stays reachable; registered as a local variable or a bare lambda, re-evaluation stops the moment it is collected.
- **Call `InvalidateRequerySuggested` on the UI thread:** the re-evaluation this API prompts is processed on the UI thread by the `CommandManager`, and the target command sources (UI elements) live on the UI thread as well. The call therefore assumes the UI thread; when state changes on a background thread, marshal to the UI thread with the `Dispatcher` before calling it.
- **Raise manual events on the UI thread as well:** `RaiseCanExecuteChanged` synchronously invokes the button-side handler, which updates a UI element. Raising it from another thread touches UI elements off the UI thread, so marshal to the UI thread with the `Dispatcher`.
- **Keep `CanExecute` lightweight:** `InvalidateRequerySuggested` makes the command sources connected to `RequerySuggested` re-query `CanExecute`. Heavy work inside it makes frequent re-evaluation harm responsiveness.
- **Do not leave `CanExecuteChanged` declared but unraised:** the opening example, which declares `CanExecuteChanged` without ever raising it, compiles cleanly yet is a classic reason the state stays frozen.

---

## Summary

The button fails to update because of a missing `CanExecuteChanged` notification, not because of the `CanExecute` result itself.

The deciding question is whether the condition lives in UI interaction or in the view model.
Delegate to `CommandManager.RequerySuggested` for the former; raise manually for the latter.
Raising every notification on the UI thread and keeping `CanExecute` lightweight are the prerequisites for not harming responsiveness.

---

<!-- Related articles -->
- [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/)
