---
layout: article-en
title: "WPF Window.Closing: Why e.Cancel Has No Effect After await, and How to Close Again Safely"
seo_title: "WPF Window.Closing: Why e.Cancel Has No Effect After await"
date: 2026-11-11
category: WPF
image: /images/articles/wpf-window-closing-async-cancel/closing-cancel-after-await.svg
excerpt: "e.Cancel set after an await in Window.Closing has no effect: WPF reads it as soon as the handler returns. Measures a cancel-first fix and its pitfalls."
---

## Overview

A common way to protect unsaved changes is to `await` a save in `Closing` and set `e.Cancel = true` when the save fails.
This does not work when the save takes time.
The window closes before `Cancel` is set, the process exits if it was the last window, and the rest of the save never runs.
When the check returns `false` without waiting, the same code does stop the window (a synchronous confirmation dialog decided before any `await` is one such case), which makes the bug easy to miss in a quick manual test.

This article locates the point in the WPF source where `Cancel` is read after `Closing`, and derives an implementation from it: stop first, then close again after the check.
A test app measures the four pitfalls of closing again (`Closing` repeating, the exception from `Close` inside `Closing`, a second × click during the check, and `ShowDialog` returning `false`), as well as `Application.Shutdown` and related paths, where the check has no effect at all.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF
- Verified on: .NET 10 (10.0.13) / Windows 11
- Architecture: MVVM (the check lives in the view model; the `Closing` handling is packaged as an attached property)
- Library used for comparison: CommunityToolkit.Mvvm 8.4.0 (only in the "Symptom" section)

Each condition was measured by starting a test app in a separate process.
The test sends the window `WM_CLOSE`, the same message the × button produces (some conditions instead call `Application.Shutdown`, close another window, press the exit button through UI Automation, or set `DialogResult`), and has the app log `Closing`, `Closed`, the start and completion of the check, and unhandled exceptions in the order they occur.
.NET Framework and closing by OS sign-out or shutdown were not measured.

---

## Symptom

The common pattern passes `Closing` to a view-model command and sets `Cancel` after awaiting the check.
The command is CommunityToolkit.Mvvm's `AsyncRelayCommand<T>`.

```csharp
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed class EditorViewModel
{
    public IAsyncRelayCommand<CancelEventArgs> ClosingCommand { get; }

    public EditorViewModel()
    {
        ClosingCommand = new AsyncRelayCommand<CancelEventArgs>(async e =>
        {
            if (!await CanCloseAsync())
            {
                e!.Cancel = true; // stop only when the window must stay open
            }
        });
    }

    // Saves or asks the user; returns true when the window may close.
    public Task<bool> CanCloseAsync() => /* omitted */;
}
```

The view connects them with an attached property (`ClosingEvent.Command`) that receives `Closing` and passes the `CancelEventArgs` to the command's `Execute`.

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Sample;

public static class ClosingEvent
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command", typeof(ICommand), typeof(ClosingEvent),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(Window window) => (ICommand?)window.GetValue(CommandProperty);

    public static void SetCommand(Window window, ICommand? value) => window.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)d;
        window.Closing -= OnClosing;
        if (e.NewValue is not null)
        {
            window.Closing += OnClosing;
        }
    }

    private static void OnClosing(object? sender, CancelEventArgs e) => GetCommand((Window)sender!)?.Execute(e);
}
```

`OnClosing` only calls `Execute` and returns; it does not wait for the command to finish.
In XAML, the command is bound to this attached property.

```xml
<Window x:Class="Sample.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:Sample"
        Title="Editor" Width="320" Height="200"
        local:ClosingEvent.Command="{Binding ClosingCommand}">
  <TextBox />
</Window>
```

Rows 1 and 2 of the next table show the result of clicking × when the check returns `false` (keep the window open).
Rows 3 to 5 use the implementation described later (`ClosingGuard`); row 5 is the same as row 3 except that `DataContext` is not set.

{% include tables/articles/wpf-window-closing-async-cancel/closing-cancel-after-await.en.md %}

.NET 10.0.13 / Windows 11. `WM_CLOSE` was sent 200 ms after the window was shown, and the window state was read 1.5 s after the window was shown. Instead of saving or showing a dialog, the check either awaits `Task.Delay(300)` before returning its result or returns without waiting (completes synchronously). The checks in rows 3 and 4 (ClosingGuard) take 300 ms. Row 5 does not set `DataContext`, so the check is never called. "checks started / completed" counts the log lines written at the start of the check method and just before it returns or throws. One run per condition.
{: .table-caption}

**In row 1, where the check takes 300 ms, `Closed` was raised before the check completed, and the window closed.**
Because it was the last window, the process exited too, and the completion of the check was never logged.
With a real save, the app would exit in the middle of it.
**In row 2, where the check completes immediately, the same code kept the window open.**

---

## What Happens Internally

### Cancel is read right after the Closing handler returns

On `WM_CLOSE`, `WmClose` in [WPF's Window.cs](https://github.com/dotnet/wpf/blob/release/10.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs) runs the following steps (only the essentials: comments, the guard clause at the top, and exception handling are replaced with `// ...`, and the `else` branch is rewritten as an early return).

```csharp
private bool WmClose()
{
    // ...
    _isClosing = true;

    CancelEventArgs e = new CancelEventArgs(false);
    OnClosing(e);                      // calls the Closing handlers (exception handling omitted)

    if (ShouldCloseWindow(e.Cancel))   // reads Cancel right after they return
    {
        CloseWindowFromWmClose();
        return false;
    }

    _isClosing = false;
    _dialogResult = null;              // a cancelled close resets DialogResult to null
    return true;
}
```

`Cancel` is read exactly once, immediately after `OnClosing` returns.
Nothing reads `Cancel` after that point.

An `async` method returns to its caller the first time it awaits a `Task` that has not completed.
`AsyncRelayCommand<T>.Execute` returns without waiting for the `Task` that `ExecuteAsync` returned (`ICommand.Execute` returns `void`, so it cannot hand the `Task` back to its caller).
So at the `await` of the check, `Execute` returns, the attached property's handler returns, and `OnClosing` returns.
`Cancel` is still `false` at that moment, and the window closes.
The `Cancel = true` line runs only when the check finishes and the continuation resumes on the UI thread, which is after `Cancel` has been read.

In row 2, the check completes synchronously, so the method does not return at the `await`; the continuation runs at once and `Cancel = true` is written inside `OnClosing`.
**Whether it works depends on whether an incomplete `await` comes before `Cancel` is written, not on whether the handler is marked `async`.**

### Close cannot be called while Closing is running

`WmClose` sets `_isClosing` to `true` before calling `OnClosing`, and resets it to `false` before returning when the close is cancelled.
`Close` calls `InternalClose`, which first calls `VerifyNotClosing`; it throws `InvalidOperationException` when `_isClosing` is `true`.
The [Closing reference](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.closing) also states that calling `Close` while a window is closing throws `InvalidOperationException`.

### During Shutdown, Cancel is not consulted

`ShouldCloseWindow` looks not only at `Cancel` but also at whether the application is shutting down.

```csharp
private bool ShouldCloseWindow(bool cancelled)
{
    return ((!cancelled) || (_appShuttingDown) || (_ignoreCancel));
}
```

`Application.Shutdown` closes each window with `InternalClose(true, true)` (`DoShutdown` in [Application.cs](https://github.com/dotnet/wpf/blob/release/10.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Application.cs)).
This sets both `_appShuttingDown` and `_ignoreCancel` to `true`, and the window closes even when `Cancel` is set.
With `ShutdownMode` set to `OnMainWindowClose`, the remaining windows are closed by the same `Shutdown` processing after the main window has closed.
The main window's own `Closing` goes through the usual `WmClose`, so a `Cancel` set there does take effect (derived from the source; not measured).
The reference likewise says that `Closing` is raised for each window when `Shutdown` is called, but cancellation is ignored.

---

## Consequences of the Path

### Set Cancel first, then close again after the check

Since the result cannot be known before `Cancel` is read, the only option is to set `Cancel = true` before the first `await` and stop the close for now.
When the check returns "may close", `Close` is called again.
Closing again has the three pitfalls shown in the next table (the fourth is covered in the next subsection).

{% include tables/articles/wpf-window-closing-async-cancel/closing-close-again.en.md %}

Measured as in the previous table. Except in row 7, the check returns `true` (may close). In the "× twice" rows, a second `WM_CLOSE` was sent 100 ms after the first (during the check), and the app's `ShutdownMode` was set to `OnExplicitShutdown` so that the process stayed alive after the window closed and the completion of the second check could be read. In row 7, the first check throws `IOException` after 300 ms and the second check returns `true`; the second `WM_CLOSE` was sent 600 ms after the first. In rows 8 and 9, `DataContext` was replaced with another `EditorViewModel` 100 ms after `WM_CLOSE` (during the check). Row 9 is a simplified `ClosingGuard` without the check for a replaced `DataContext` (it keeps its state in static fields and does not handle `DialogResult`, but behaves the same on this modeless path). In row 10, `DataContext` was set to `null` 100 ms after `WM_CLOSE`, and a second `WM_CLOSE` was sent 50 ms after that. Row 11 uses a check that completes synchronously; after closing again was scheduled, `DataContext` was replaced and `Close()` was called ahead of it (with `DispatcherPriority.Send`). In row 12, a handler registered after `ClosingGuard` cancels only the second `Closing` (closing again), and a second `WM_CLOSE` was sent 700 ms after the first. One run per condition.
{: .table-caption}

**Row 1: the `Close` called again also raises `Closing`.**
The same handler sets `Cancel` again and starts a new check, so the window does not close even though the check returns `true`; `Closing` and the check kept repeating until 1.5 s after the window was shown (the count depends on how long the check takes and on timer precision).
The handler has to remember that the check finished with "may close" and do nothing on the second `Closing`.

**Rows 2 and 3: calling `Close` directly runs it inside `Closing` only when the check completes synchronously.**
In row 2, where the check takes 300 ms, `Close` is called after `Closing` has finished, and the window closes.
In row 3, where the check completes immediately, the continuation of the `await` runs inside `OnClosing`, `Close` is called while `_isClosing` is `true`, and `InvalidOperationException` is thrown.
An exception that escapes an `async void` handler is rethrown on the `Dispatcher`, so it reached `DispatcherUnhandledException` (marked `Handled` in the measurement), and the window stayed open.
Because the exception occurs only when there is nothing to save, this one is also easy to miss in manual testing.
Deferring `Close` with `Dispatcher.InvokeAsync`, as in row 4, makes it run after `Closing` has finished, and the window closes even when the check completes synchronously.

**Row 5: a second × click during the check starts the check twice.**
The second `Closing` must still set `Cancel` (otherwise `Cancel` is still `false` when `OnClosing` returns, and the window closes along the path described in "What Happens Internally").
But starting the check again runs the save twice, and the second check keeps running after the first one has closed the window.
Remembering that a check is in progress and only setting `Cancel` before returning keeps it to a single check, as in row 6.

Row 7 is the `ClosingGuard` described later, with a check that throws.
The exception reached `DispatcherUnhandledException` (marked `Handled` in the measurement), and the window stayed open.
Clicking × again started the check over, and the window closed after the second check returned `true` (`Closed` was raised only after the second check completed).

Row 8 replaces `DataContext` with the view model of another document during the check.
In row 9, which does not check whether it was replaced, the window closed after the old check returned `true`, and the check of the new document never started.
`ClosingGuard` confirms after the check that `DataContext` is still the view model it asked, so in row 8 the window stayed open even though the old check returned `true`.
The × the user clicked is dropped in that case; the new view model's check starts when × is clicked again.
In row 10, setting `DataContext` to `null` during the check and then clicking × again also stopped the second `Closing`, and the window stayed open.

Another `Close` can also arrive between the end of the check and closing again.
Row 11 replaces `DataContext` and calls `Close()` after closing again was scheduled but before it ran; that `Close` was stopped, and the scheduled close-again did not close the window either.
Row 12 is the case where another handler cancels closing again.
The window stayed open, and clicking × again re-ran the check and closed it.

### ShowDialog loses DialogResult

When `WmClose` cancels a close, it resets `DialogResult` to `null` (see the excerpt in "What Happens Internally").
So for a window opened with `ShowDialog`, even if the OK button sets `DialogResult = true`, stopping the close for the check and closing again with `Close` makes `ShowDialog` return `false`.
A dialog that closes while the value is `null` returns `false` (`DoDialogHide` in `Window.cs` replaces `null` with `false`).

{% include tables/articles/wpf-window-closing-async-cancel/closing-dialog-result.en.md %}

.NET 10.0.13 / Windows 11. `DialogResult` was set to `true` 200 ms after `ShowDialog` was called (no `WM_CLOSE` was sent). The check returns `true` after 300 ms. To log the return value of `ShowDialog`, the app's `ShutdownMode` was set to `OnExplicitShutdown`. Row 2 is the implementation that closes again with `Close()`, the same as row 2 of the table in "Set Cancel first, then close again after the check". One run per condition.
{: .table-caption}

Setting the `DialogResult` from before the cancel back when closing again returns `true`, as in row 3.
On a modal window, setting `DialogResult` closes the window.

### A window closed by Shutdown or by its Owner cannot be stopped

When `Application.Shutdown` is called, even a window with the `ClosingGuard` described later cannot be stopped.
Another path through `Shutdown`, and closing the `Owner`, which does not go through `Shutdown`, were measured as well.
An exit button should therefore use the same `Close` path as ×, not `Shutdown`.

{% include tables/articles/wpf-window-closing-async-cancel/closing-shutdown.en.md %}

Measured as in the first table, without sending `WM_CLOSE`. Every check takes 300 ms. Row 1 calls `Application.Current.Shutdown()`. Row 2 sets the app's `ShutdownMode` to `OnMainWindowClose`, makes a separate window without `ClosingGuard` the `MainWindow`, and closes it. Row 3 makes a separate window without `ClosingGuard` the `Owner` and closes it. Rows 4 and 5 press an exit button bound to `Command="Close"` (`ApplicationCommands.Close`) through UI Automation's `Invoke`. Except in row 2, `ShutdownMode` is the default, `OnLastWindowClose`. One run per condition.
{: .table-caption}

**In row 1, with `Shutdown`, the window closed and the process exited even though `ClosingGuard` set `Cancel`, and the check never completed.**
Row 2, closing the main window under `OnMainWindowClose`, gave the same result.
**In row 3, closing the `Owner`, `Closing` was not raised at all, and the window closed without a check.**
The [Closing reference](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.closing) also states that `Closing` is not raised on an owned window when its owner is closed.
Win32's [DestroyWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-destroywindow) destroys owned windows before the owner by sending them `WM_DESTROY` (that it does not send `WM_CLOSE` was confirmed by the next measurement).
The measurement also showed that `WM_CLOSE` never reached the owned window's window procedure (recorded with `HwndSource.AddHook`).
So the window does not go through `WmClose`, and `Closing` is not raised.
In rows 4 and 5, the exit button reaches the window's `Close` through `ApplicationCommands.Close`, goes through the same check as ×, and the window stays open when the check returns `false`.
With `ShutdownMode` set to `OnLastWindowClose`, the application ends when the last window closes.
The relation between `ShutdownMode` and process exit is covered in [Diagnosing a WPF Process That Stays Alive After the Window Closes — ShutdownMode and Foreground Threads](/articles/wpf-application-not-exiting-shutdownmode-threads/).

---

## Implementation Example

The check lives in the view model, and the close-again procedure is packaged as an attached property.
The interface the view model implements has a single method that returns whether the window may close.

```csharp
namespace Sample;

public interface IClosingGuard
{
    // Returns true when the window may close. Save or ask the user here.
    Task<bool> CanCloseAsync();
}
```

A window with the attached property `ClosingGuard.IsEnabled` set to `True` asks the `IClosingGuard` in its `DataContext` on every `Closing`.
The per-window state (checking, approved) is kept in a private attached property, which, together with the close-again procedure, avoids each of the four pitfalls from the previous section.

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Sample;

public static class ClosingGuard
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(ClosingGuard),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(Window window) => (bool)window.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Window window, bool value) => window.SetValue(IsEnabledProperty, value);

    // Per-window state: a check is in progress, or it finished and the window may close.
    private enum State { Idle, Checking, Approved }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State", typeof(State), typeof(ClosingGuard),
            new PropertyMetadata(State.Idle));

    private static readonly CommandBinding CloseBinding =
        new(ApplicationCommands.Close, (sender, _) => ((Window)sender).Close());

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)d;
        window.Closing -= OnClosing;
        window.CommandBindings.Remove(CloseBinding);

        if ((bool)e.NewValue)
        {
            window.Closing += OnClosing;
            // Route Command="Close" (an exit button, for example) through the same Close path as ×.
            window.CommandBindings.Add(CloseBinding);
        }
    }

    private static async void OnClosing(object? sender, CancelEventArgs e)
    {
        var window = (Window)sender!;
        var state = (State)window.GetValue(StateProperty);

        if (state == State.Approved)
        {
            return; // the second Close after an approved check closes as usual
        }

        if (state == State.Checking)
        {
            e.Cancel = true; // stop × clicked again during the check, whatever DataContext is
            return;
        }

        if (window.DataContext is not IClosingGuard guard)
        {
            return; // nothing to ask, so close as usual
        }

        e.Cancel = true; // stop before the first await

        // Cancelling makes WPF reset DialogResult to null, so keep it for closing again.
        bool? dialogResult = window.DialogResult;
        window.SetValue(StateProperty, State.Checking);
        bool canClose = false;
        try
        {
            // If DataContext was replaced by another view model during the check, do not close on the old result.
            canClose = await guard.CanCloseAsync() && ReferenceEquals(window.DataContext, guard);
        }
        finally
        {
            if (!canClose)
            {
                window.SetValue(StateProperty, State.Idle);
            }
        }

        if (!canClose)
        {
            return;
        }

        // Calling Close while Closing is running throws, so close again after Closing has finished.
        // Stay in Checking until closing again, so other Close calls in between are also stopped.
        _ = window.Dispatcher.InvokeAsync(() =>
        {
            if (!ReferenceEquals(window.DataContext, guard))
            {
                window.SetValue(StateProperty, State.Idle); // do not close if it was replaced before closing again either
                return;
            }

            window.SetValue(StateProperty, State.Approved);
            try
            {
                if (dialogResult is bool result)
                {
                    window.DialogResult = result; // restore the ShowDialog result (setting it closes the window)
                }
                else
                {
                    window.Close();
                }
            }
            finally
            {
                // If the window remains (another handler cancelled the close, for example), go back to the state before the check.
                if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
                {
                    window.SetValue(StateProperty, State.Idle);
                }
            }
        });
    }
}
```

`State.Approved` prevents the repetition in row 1 of the table in "Set Cancel first, then close again after the check", `Dispatcher.InvokeAsync` prevents the exception in row 3, and `State.Checking` prevents the double check in row 5.
Keeping `dialogResult` and setting it back prevents row 2 of the table in "ShowDialog loses DialogResult".
On a modeless window, `DialogResult` stays `null`, so the window is closed again with `Close`.
The check after the check, that `DataContext` is still the view model that was asked, handles rows 8 and 9 of the table in "Set Cancel first, then close again after the check".
After the check, the state stays `State.Checking` until closing again, and becomes `State.Approved` only inside `InvokeAsync`, right after `DataContext` is checked once more.
This also stops any other `Close` that arrives between the check and closing again (row 11 of the same table).
If the window is still there after closing again (another handler cancelled the close, for example), the state goes back to `State.Idle`, so the next × runs the check again (row 12 of the same table).
A × clicked again during the check is stopped before `DataContext` is examined, so that the window does not close in the middle of a save even if `DataContext` has been replaced with something that is not an `IClosingGuard` (row 10 of the same table).
When `DataContext` is not an `IClosingGuard`, the window closes without a check (row 5 of the first table).

When the check throws, `finally` resets the state to `Idle`, and the exception then escapes the `async void` method.
If `DispatcherUnhandledException` marks it `Handled`, the close is still cancelled, so, as row 7 of the table in "Set Cancel first, then close again after the check" shows, clicking × again starts the check over.
Otherwise WPF's default behavior treats it as an unhandled exception and ends the application (according to the [DispatcherUnhandledException reference](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception); not measured in this article).
When a failed save is expected, catching the exception inside `CanCloseAsync` and returning `false` is the better choice.
Where exceptions from `async void` end up is measured in [Exceptions WPF's DispatcherUnhandledException Never Sees — Where Thread, Task and async void Exceptions Go](/articles/wpf-unhandled-exception-dispatcher-task-thread/).

The view sets the attached property to `True` and binds the exit button to the `Close` command.

```xml
<Window x:Class="Sample.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:Sample"
        Title="Editor" Width="320" Height="200"
        local:ClosingGuard.IsEnabled="True">
  <DockPanel>
    <Button DockPanel.Dock="Bottom" Content="Exit" Command="Close" />
    <TextBox />
  </DockPanel>
</Window>
```

The code-behind needs nothing beyond `InitializeComponent`.
The view model is set to `DataContext` by the code that opens the window.
If it is forgotten, `ClosingGuard` closes the window without a check (row 5 of the first table).

```csharp
using System.Windows;

namespace Sample;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Do not use StartupUri in App.xaml; open the window with its view model.
        new EditorWindow { DataContext = new EditorViewModel() }.Show();
    }
}
```

The test app likewise sets the view model to `DataContext` of the window created from XAML before calling `Show`.
The view model implements `IClosingGuard` and saves or asks the user in `CanCloseAsync`.

```csharp
public sealed class EditorViewModel : IClosingGuard
{
    public bool IsDirty { get; private set; }

    public async Task<bool> CanCloseAsync()
    {
        if (!IsDirty)
        {
            return true; // returns without waiting (completes synchronously)
        }

        return await SaveAsync(); // close only when the save succeeded
    }

    private Task<bool> SaveAsync() => /* omitted */;
}
```

The measurements used `ClosingGuard` and `IClosingGuard` exactly as above, replacing only `CanCloseAsync` with one that awaits `Task.Delay(300)`, returns without waiting, or throws `IOException` on the first call.
The "ClosingGuard" rows of each table and all rows of the `Shutdown` table are the result.

---

## Notes

- **Keep saving and confirmation inside `CanCloseAsync`.** If a handler registered after `ClosingGuard` cancels closing again, `ClosingGuard` goes back to the state before the check (row 12 of the table in "Set Cancel first, then close again after the check"). Setups where another handler starts a save of its own were not measured.
- **When application code calls `Shutdown`, the result of the check has no effect.** `ClosingGuard` still starts a check on the `Closing` raised by `Shutdown`, but, as row 1 of the `Shutdown` table shows, the check is cut off before it completes. Route exit operations through the window's `Close` instead of `Shutdown`, and let `ShutdownMode` set to `OnLastWindowClose` end the application. Under `OnMainWindowClose`, the checks of the remaining windows have no effect once the main window has closed (row 2 of the same table).
- **Closing the `Owner` closes owned windows without a check.** This is because `Closing` is not raised (row 3 of the `Shutdown` table). When an owned window can hold unsaved changes, they need to be checked together in the owner's check (this setup was not measured).
- **OS sign-out and shutdown were not measured.** The [reference](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.closing) states that `Closing` is not raised in that case and recommends handling `Application.SessionEnding`.

---

## Summary

Setting `Cancel` after awaiting a check in a `Closing` handler only works when the check completes synchronously.
WPF reads `Cancel` once, right after the `Closing` handlers return, and an `async` handler returns at its first incomplete `await`.

To stop a window with an asynchronous check, follow this order.

1. Set `Cancel = true` before the first `await`.
2. After the check, if the window may close, call `Close` again through `Dispatcher.InvokeAsync` (to avoid the exception inside `Closing`).
3. Remember "approved" and "checking" per window: let the second `Closing` through, and stop × during a check and until closing again. If closing again is cancelled, go back to the state before the check. If `DataContext` was replaced during the check, do not close on the old result.
4. For a window opened with `ShowDialog`, keep `DialogResult` before cancelling and set it back when closing again.

Packaged as an attached property, this leaves the view model with nothing but `CanCloseAsync` to implement.
However, `Application.Shutdown` ignores `Cancel` on every window, `OnMainWindowClose` ignores it on the windows remaining after the main window, and closing the `Owner` does not raise `Closing` on owned windows at all.
Route exit operations through the window's `Close` rather than `Shutdown`, and end the application with `OnLastWindowClose`.

---

## Related Articles

- [Diagnosing a WPF Process That Stays Alive After the Window Closes — ShutdownMode and Foreground Threads](/articles/wpf-application-not-exiting-shutdownmode-threads/)
- [Exceptions WPF's DispatcherUnhandledException Never Sees — Where Thread, Task and async void Exceptions Go](/articles/wpf-unhandled-exception-dispatcher-task-thread/)
- [Where Exceptions from CommunityToolkit.Mvvm AsyncRelayCommand Go in WPF, and What FlowExceptionsToTaskScheduler Changes](/articles/wpf-asyncrelaycommand-exception-handling/)
