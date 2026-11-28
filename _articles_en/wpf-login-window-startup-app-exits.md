---
layout: article-en
title: "WPF ShutdownMode and MainWindow: Why the App Exits When the Login Window Closes"
seo_title: "WPF ShutdownMode: App Exits When the Login Window Closes"
date: 2026-11-28
category: WPF
image: /images/articles/wpf-login-window-startup-app-exits/login-startup-exit-matrix.svg
excerpt: "A login window in OnStartup can end a WPF app before its main window appears. This article measures why OnMainWindowClose fails and OnExplicitShutdown works."
---

## Overview

A common way to show a login window before the main window in WPF is to remove `StartupUri` from `App.xaml`, call `ShowDialog` on the login window in `OnStartup`, and then call `Show` on the main window.
Written that way, the app exits right after the user signs in, and the main window never appears.
No exception is thrown and `Show()` returns normally, so the code itself gives no hint of the cause.

The cause depends on `ShutdownMode`.
With the default `OnLastWindowClose`, the login window is the application's last window during the login.
Switching to `OnMainWindowClose` does not help, because the login window, created first, becomes `MainWindow`.
In both cases, shutdown has already begun by the time the login window closes and `ShowDialog` returns.

This article measures these two causes and the conditions under which the fixes derived from the `ShutdownMode` documentation fail.
Finally, it implements, in MVVM, a startup sequence that sets `ShutdownMode` to `OnExplicitShutdown` only while the login window is open.

---

## Prerequisites and Environment

- Framework: WPF on .NET 5 or later is assumed. The behavior was measured only in the environment below; .NET 5–9 and .NET Framework 4.8 were not measured
- Language: the code uses nullable reference type annotations (`object?`), so it requires C# 8 or later
- Architecture: MVVM. The view model base class `ObservableObject` (with a `SetProperty` that returns `true` when the value changes) and `RelayCommand` are the minimal hand-written implementations shown below, with no external library. The views' code-behind contains only `InitializeComponent`; the code that builds the windows lives in `App.OnStartup`
- Verified on: .NET 10 / Windows 11. Each configuration was launched as a separate process, and the test app recorded when `Application.Exit` was raised and whether the process ended
- Scenario: the user enters a user name in the login window, signs in, and the main window shows the signed-in user

---

## Problem

The login view model does not reference its window; it only reports that it wants to close, together with the result.
This is the same shape as the child window in [the article on showing dialogs from MVVM](/articles/wpf-mvvm-dialog-service/).

The view model base class `ObservableObject` has a `SetProperty` that raises `PropertyChanged` and returns `true` only when the value changes.
The buttons use a minimal `RelayCommand` that only receives the action to execute.
The buttons in this article are always enabled, so `CanExecute` always returns `true`.

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LoginSample
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
```

`CanExecuteChanged` is never raised, so its accessors only accept subscriptions and do nothing.
For a command with an enabling condition, use the implementation in [the CanExecute article](/articles/wpf-relaycommand-canexecute-not-updating/).

The login and main view models are as follows.
The main view model only turns the signed-in user name into a display string.

```csharp
using System;
using System.Windows.Input;

namespace LoginSample
{
    public interface ICloseRequest
    {
        event EventHandler<bool>? CloseRequested;
    }

    public sealed class LoginViewModel : ObservableObject, ICloseRequest
    {
        private string _userName = "";

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
            CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        }

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public ICommand LoginCommand { get; }

        public ICommand CancelCommand { get; }

        public event EventHandler<bool>? CloseRequested;
    }

    public sealed class MainViewModel
    {
        public MainViewModel(string userName) => Greeting = $"Signed in as {userName}";

        public string Greeting { get; }
    }
}
```

Authentication itself is outside the scope of this article, so `LoginCommand` simply reports `true`.
A real application raises `CloseRequested` with `true` only when authentication succeeds.

`StartupUri` is removed from `App.xaml`, and `OnStartup` shows the login window.
`App`, which created the window, handles `CloseRequested` and sets `DialogResult`.

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var loginViewModel = new LoginViewModel();
    var login = new LoginWindow { DataContext = loginViewModel };

    void OnCloseRequested(object? sender, bool result) => login.DialogResult = result;
    loginViewModel.CloseRequested += OnCloseRequested;
    bool? result = login.ShowDialog();
    loginViewModel.CloseRequested -= OnCloseRequested;

    if (result != true)
    {
        Shutdown();
        return;
    }

    var main = new MainWindow { DataContext = new MainViewModel(loginViewModel.UserName) };
    main.Show();
}
```

When the user signs in with this code, `ShowDialog` returns `true` and execution reaches `main.Show()`.
Even so, the main window does not appear, `Application.Exit` is raised, and the process ends.
`Show()` did not throw, and `main.IsVisible` was `false` immediately after the call (the "OnLastWindowClose (default), Show after login" row of the table below).

---

## Commonly Suggested Fixes

The default `ShutdownMode` is `OnLastWindowClose`, which shuts the application down when the last window closes ([Application.ShutdownMode](https://learn.microsoft.com/dotnet/api/system.windows.application.shutdownmode)).
Two fixes follow from that description.

- **Set `ShutdownMode` to `OnMainWindowClose`.** The idea is that if the app only shuts down when the main window closes, closing the login window will not end it
- **Create the main window first.** The idea is that if a main window instance still exists when the login window closes, the login window is not the "last window"

A third pattern keeps `StartupUri`, shows the login window in `OnStartup`, and leaves creating the main window to `StartupUri`.

The table below runs these configurations, and the one this article recommends, with the same login and main windows.

{% include tables/articles/wpf-login-window-startup-app-exits/login-startup-exit-matrix.en.md %}

Each configuration was launched as a separate process of the test app, changing only the body of `OnStartup`. Measured on .NET 10 / Windows 11. The scene that builds the table was run twice (launching each configuration once per run), and the two tables were identical. The user's action on the login window was replaced, 0.8 seconds after it was shown, by executing the view model's `LoginCommand`, by executing `CancelCommand` in the canceled rows, or by sending `WM_CLOSE` to the window in the "login closed with ×" row. The "ShowDialog() result" column is the `bool?` converted to a string (`True` / `False`). The main window was checked for being open 1.5 seconds after it was shown and then closed. The `MainWindow` columns show the type name of `Application.MainWindow`, read in the login window's `Loaded` ("during login"), right after `ShowDialog` returned ("after login closes"), and right after `Show()` ("after Show()"). "Shutdown right after ShowDialog()" is the value of `Application`'s internal static property `IsShuttingDown`, read through reflection right after `ShowDialog` returned. Whether the main window was created was recorded in its constructor; "never created" in the "Main window" column means the constructor was never called, and "never shown" means the window was created but `Loaded` was never raised. The "no MainWindow assignment, …" and "MainWindow assigned, …" rows are variations of the recommended form, and both "OnExplicitShutdown → OnLastWindowClose" rows assign `MainWindow`. "another Window created" means one `Window` was created before the main window and never shown. In the "Application.Exit triggered by" column, "login window closed" means shutdown had already begun right after `ShowDialog`, regardless of any later `Shutdown()` call; otherwise the trigger is the `Shutdown()` call or the closing of the main window recorded before `Exit`. "—" means the configuration does not call `Show()` or execution stopped before it (in the exception column, that no exception occurred). Exceptions were recorded in `DispatcherUnhandledException` and marked as handled. A process still running after 10 seconds is "keeps running" and was killed. In the `StartupUri` row, `StartupUri` was set to `MainWindow.xaml` in code.
{: .table-caption}

---

## Why They Do Not Work

### With OnLastWindowClose, the login window is the last window

While `OnStartup` is inside the login window's `ShowDialog`, the login window is the only window the application has.
When it closes, the remaining window count drops to zero and the `OnLastWindowClose` condition is met.
In the "OnLastWindowClose (default), Show after login" row, shutdown had already begun right after `ShowDialog` returned (the "Shutdown right after ShowDialog()" column).
The subsequent `Show()` returned without throwing, and `IsVisible` was `false` right after it.

### With OnMainWindowClose, the login window is MainWindow

`Application.MainWindow` is automatically set to the first `Window` the application instantiates ([How to get and set the main application window](https://learn.microsoft.com/dotnet/desktop/wpf/windows/how-to-get-set-main-application-window)).
The first window `OnStartup` creates is the login window, so `MainWindow` is the login window during the login (the "MainWindow during login" column).
`OnMainWindowClose` shuts down when `MainWindow` closes, so in the "OnMainWindowClose from startup" row as well, shutdown had begun right after `ShowDialog` returned.
As with the default configuration, `IsVisible` was `false` right after the following `Show()`.

### The StartupUri main window is created but never shown

In the "StartupUri kept, login in OnStartup" row, shutdown had also begun right after `ShowDialog` returned.
The `StartupUri` main window was created (the "Main window" column says "never shown", not "never created"), but `Loaded` was never raised, and the app shut down.
`ShutdownMode` is still the default, so the cause is the same as in the default configuration.

### Creating the main window first leaves the process running if Shutdown() is forgotten on cancel

Creating the main window first behaved as intended when sign-in succeeded.
Because the already-created main window remains, the login window is not the last window when it closes, and `MainWindow` is the main window, which was created first.

The case that needs care is a canceled login.
In the "main window created before login, canceled, no Shutdown()" row, returning from `OnStartup` after `ShowDialog` returned `false` left the never-shown main window instance behind.
The "last window closes" condition of `OnLastWindowClose` is not met until that window is closed.
`Application.Exit` was never raised, and the process was still running after 10 seconds.
How a hidden window keeps the process alive is diagnosed in [the article on a WPF process that stays alive after the window closes](/articles/wpf-application-not-exiting-shutdownmode-threads/).

Calling `Shutdown()` on cancel makes this configuration end as well (the "main window created before login, canceled → Shutdown()" row).
In this form, however, the main window is created before the login result is known, and the cancel path relies on an explicit `Shutdown()`.
Turning off automatic shutdown for the duration of the login and routing every exit path through `Shutdown()` therefore makes the conditions under which the app ends easier to read from the code.

---

## Conditions That Work

If no automatic shutdown can happen during the login, it does not matter that the login window is the last window or `MainWindow`.
`OnExplicitShutdown` is the mode that shuts down only when `Shutdown()` is called.

- Set `ShutdownMode` to `OnExplicitShutdown` before showing the login window
- On successful sign-in, assign the main window to `MainWindow`, switch `ShutdownMode` to `OnMainWindowClose`, and then `Show` the main window
- On cancel, call `Shutdown()` and return from `OnStartup`

In the "OnExplicitShutdown → OnMainWindowClose (recommended)" row, the main window was still open 1.5 seconds after it was shown, and closing the main window triggered `Application.Exit`.
In the "recommended, canceled → Shutdown()" and "recommended, login closed with × (WM\_CLOSE) → Shutdown()" rows, `ShowDialog` returned `false` in both cases, and `Application.Exit` was raised and the process ended when `Shutdown()` was called.

### Why switch to OnMainWindowClose rather than OnLastWindowClose

Switching to `OnLastWindowClose` after the login also ends the app the same way when the main window is the only window (the "OnExplicitShutdown → OnLastWindowClose" row).
The difference appears when another, never-shown window remains.
In the "OnExplicitShutdown → OnLastWindowClose, another Window created" row, one `Window` was created (and never shown) before the main window, and closing the main window did not raise `Application.Exit`; the process kept running.
Under the same condition, the "MainWindow assigned, another Window created before main" row, which switches to `OnMainWindowClose`, shut down when the main window closed.
`OnMainWindowClose` is the mode that directly expresses the intent to end the app when the main window closes.

### Why assign MainWindow

The "recommended without assigning MainWindow" row produced the same result as the recommended form.
Closing the login window, which was `MainWindow`, reset `MainWindow` to `null` (the "MainWindow after login closes" column), and the main window created next became `MainWindow` automatically.

The assignment is kept anyway, because the automatic setting depends on creation order.
In the "no MainWindow assignment, another Window created before main" row, `MainWindow` became the `Window` created first (the "MainWindow after Show()" column), and closing the main window did not raise `Application.Exit`; the process kept running.
In the "MainWindow assigned, another Window created before main" row, which assigns it, the app shut down when the main window closed.

---

## Implementation Example

`App.OnStartup` looks like this.
The part that opens the login window and receives the result is split into its own method.

```csharp
using System.Windows;

namespace LoginSample
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Do not shut down when a window closes during the login.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!TryLogin(out string userName))
            {
                Shutdown();
                return;
            }

            var main = new MainWindow { DataContext = new MainViewModel(userName) };
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }

        private static bool TryLogin(out string userName)
        {
            var viewModel = new LoginViewModel();
            var login = new LoginWindow { DataContext = viewModel };

            void OnCloseRequested(object? sender, bool result) => login.DialogResult = result;
            viewModel.CloseRequested += OnCloseRequested;
            try
            {
                bool? result = login.ShowDialog();
                userName = viewModel.UserName;
                return result == true;
            }
            finally
            {
                viewModel.CloseRequested -= OnCloseRequested;
            }
        }
    }
}
```

`ShowDialog` returns `bool?`, so it is compared with `== true` to produce a `bool`.
If the login window is closed with the × button, nobody sets `DialogResult`, but `ShowDialog` returns `false` (the "recommended, login closed with × (WM\_CLOSE) → Shutdown()" row), so it follows the same path as a cancel.
The `CloseRequested` subscription is removed in `finally` so that no link remains between the closed login window and its view model.
The `x:Class` in `App.xaml` is `LoginSample.App`, including the C# namespace, so that both declare the same partial class.

`App.xaml` has no `StartupUri`.

```xml
<Application x:Class="LoginSample.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</Application>
```

Without `StartupUri`, `OnStartup` alone decides which window appears at startup.
If `StartupUri` is kept, the main window is created but the app shuts down without showing it, as the "StartupUri kept, login in OnStartup" row shows.

The login window's XAML only binds an input box and two buttons to the view model.

```xml
<Window x:Class="LoginSample.LoginWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Sign in" Width="260" Height="150">
  <StackPanel Margin="12">
    <TextBox Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}" />
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
      <Button Content="Sign in" Command="{Binding LoginCommand}" Width="70" />
      <Button Content="Cancel" Command="{Binding CancelCommand}" Width="70" Margin="8,0,0,0" />
    </StackPanel>
  </StackPanel>
</Window>
```

The login window's code-behind contains only `InitializeComponent`.
Closing goes through the view model's `CloseRequested`, which `App` turns into `DialogResult`.

The main window's XAML only displays the view model's `Greeting`.

```xml
<Window x:Class="LoginSample.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Main" Width="300" Height="160">
  <TextBlock Margin="12" Text="{Binding Greeting}" />
</Window>
```

The main window's code-behind also contains only `InitializeComponent`.
`DataContext` is not set in XAML, because `OnStartup` sets it to a `MainViewModel`.

---

## Notes

- **The return from `Show()` does not reveal the failure.** `Show()` returned without throwing after shutdown had begun. When investigating a main window that never appears, read `IsVisible` right after `Show()`, or set a breakpoint in an `Application.Exit` event handler (or an `OnExit` override)
- **Change `ShutdownMode` before `ShowDialog`.** In the "OnExplicitShutdown set after ShowDialog returns" row, shutdown had already begun, so setting `ShutdownMode` itself threw an `InvalidOperationException`. The exception skipped the rest of `OnStartup`, and the app shut down without ever creating the main window
- **If `OnExplicitShutdown` is never switched, closing the main window does not end the app.** In the "OnExplicitShutdown kept after login" row, closing the main window did not raise `Application.Exit` and the process kept running. Write the line that switches to `OnMainWindowClose` together with the main window's `Show`
- **Showing the login window modelessly with `Show` is not covered.** Unlike `ShowDialog`, it does not pause `OnStartup`, so it requires a different design that shows the main window from the login window's close event

---

## Summary

The app exits right after the login window closes because, with the default `OnLastWindowClose`, the login window is the last window, and with `OnMainWindowClose`, the login window is `MainWindow`.
Either way, shutdown has already begun when the login window closes and `ShowDialog` returns, and the following `Show()` returns without displaying anything.
Creating the main window first leaves the process running if `Shutdown()` is forgotten on cancel.

For an application that shows a login window first, set `ShutdownMode` to `OnExplicitShutdown` only for the duration of the login.
On successful sign-in, assign the main window to `MainWindow` and switch to `OnMainWindowClose`; on cancel, call `Shutdown()`.
With this configuration, the application ends exactly when the code says it should, whether the login succeeds, is canceled, or is closed with ×.

---

## Related Articles

- [Diagnosing a WPF Process That Stays Alive After the Window Closes — ShutdownMode and Foreground Threads](/articles/wpf-application-not-exiting-shutdownmode-threads/)
- [WPF MVVM Dialogs: A Minimal IDialogService and the Owner and Result Pitfalls](/articles/wpf-mvvm-dialog-service/)
