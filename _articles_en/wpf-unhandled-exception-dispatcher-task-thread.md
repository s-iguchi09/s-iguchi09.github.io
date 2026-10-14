---
layout: article-en
title: "Exceptions WPF's DispatcherUnhandledException Never Sees — Where Thread, Task and async void Exceptions Go"
seo_title: "Exceptions WPF's DispatcherUnhandledException Never Sees"
date: 2026-10-14
category: WPF
excerpt: "A new Thread exception ends a WPF app despite Handled = true, and an unawaited Task's exception reaches nothing at first. The first layer to catch it decides."
image: /images/articles/wpf-unhandled-exception-dispatcher-task-thread/unhandled-exception-routes.svg
---

## Overview

A common safety net for unhandled exceptions in a WPF application is to subscribe to `DispatcherUnhandledException` in `App.xaml.cs`, write a log entry, and set `e.Handled = true`.
Even with that handler in place, depending on how the exception occurs, the application can still terminate without warning, or nothing may reach the log.

The cause is not how the handler is written.
**Where an exception ends up is decided by the layer that first catches it.**
`DispatcherUnhandledException` only receives exceptions caught by the `Dispatcher` of the main UI thread.
Exceptions that no layer catches before they reach the top of their thread (from `new Thread`, or from an `async void` method started on the thread pool) reach `AppDomain.CurrentDomain.UnhandledException` and end the process.
Exceptions caught by a `Task` that nobody awaits, or by a `System.Timers.Timer`, stop inside it and reach none of the handlers, except that `UnobservedTaskException` may be raised after a GC.
When the `Task` is awaited, the exception is rethrown at the caller's `await`.

This article sets up 16 throwing routes and conditions, and measures the thread that throws, which of the three handlers is called, and whether the process keeps running in each case.
It then lays out where to subscribe and how to write the code so that every route is actually caught.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (measured on .NET 10 only)
- Language: C# (uses `async` / `await`)
- Features covered: `Application.DispatcherUnhandledException`, `Dispatcher.UnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`
- Architecture: the same for MVVM and code-behind (the destination of an exception depends on the layer that catches it, not on how the UI is built)
- Verified on: .NET 10 / Windows 11
- Measurement: a probe application that subscribes to all three handlers in `Startup` was started as a separate process for each route, and each route was run once. The kind of thread that threw and every handler call were written to a file, and the process was marked as "keeps running" if the UI thread was still alive two seconds after the route started. `Click` was raised asynchronously through `ButtonAutomationPeer`'s `Invoke`, so the `Click` handler runs inside the processing of the UI thread's `Dispatcher`. The measurement is implemented as a scene in `tools/screenshot-capture`; the table is re-measured on every run, and the run fails if any result differs from what the article states

.NET Framework was not tested.
In particular, the handling of exceptions from unawaited `Task` objects there depends on the [`ThrowUnobservedTaskExceptions`](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/throwunobservedtaskexceptions-element) element in the configuration file, so the results here should not be applied to it as-is.

---

## Symptom

It is tempting to assume that the following handler logs every exception anywhere in the application and keeps it from crashing.

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log("UI", args.Exception);
            args.Handled = true;
        };
    }
}
```

`Log` is the logging method defined in the implementation example later in this article.
This handler only works when the exception escapes work run by the UI thread's `Dispatcher`, such as a button's `Click` handler.
When the same exception is thrown inside a `new Thread`, the process terminates without the handler being called.
When it is thrown inside work passed to `Task.Run` and the `Task` is not awaited, the handler is not called and the application keeps running as if nothing happened.
In both cases, this handler writes nothing to the log.

---

## What Happens Internally

With all three handlers subscribed, only the throwing route was varied.
The results are shown below.

{% include tables/articles/wpf-unhandled-exception-dispatcher-task-thread/unhandled-exception-routes.en.md %}

Recorded on .NET 10 / Windows 11 by starting the probe application, with `ShutdownMode` set to `OnExplicitShutdown`, as a separate process for each route and running each route once. The `DispatcherUnhandledException` handler sets `e.Handled = true` except in the "Handled not set" row. The `UnobservedTaskException` handler calls `e.SetObserved()`. A row starting with "+" adds a condition to the row above it. The "(no GC)" rows and the "+ GC.Collect()" row suppress the GC with `GC.TryStartNoGCRegion` for the two seconds after the route starts. The "+ GC.Collect()" row then ends the suppression and calls `GC.Collect()`, `GC.WaitForPendingFinalizers()` and `GC.Collect()` in that order. "keeps running" means the UI thread was still alive two seconds after the route started and the probe then exited on its own. For routes that keep running, handler calls are judged from what was recorded up to the two-second check. Parentheses in the `DispatcherUnhandledException` column show what received the exception instead.
{: .table-caption}

The rows fall into four groups according to the mechanism that first catches the exception.

### Exceptions caught by the main Dispatcher

Exceptions from a `Click` handler and from `DispatcherTimer.Tick` reach `DispatcherUnhandledException`.
Both are work run by the `Dispatcher` of the main UI thread.
The `Dispatcher` catches exceptions that escape the work it runs, and `Application` reports them as `DispatcherUnhandledException`.
Setting `e.Handled = true` keeps the process running.

Without `e.Handled`, the exception leaves the `Dispatcher`.
In that case `AppDomain.UnhandledException` was raised with `IsTerminating = true` after `DispatcherUnhandledException`, and the process ended.
`AppDomain.UnhandledException` cannot stop the termination.
It is the last chance to write a log entry, not a way to recover.

### Exceptions the main Dispatcher never catches

An exception on a thread created with `new Thread` does not reach `DispatcherUnhandledException`.
That thread has no `Dispatcher`, so nothing catches the exception before it reaches the top of the thread.
It becomes an unhandled exception for the runtime, `AppDomain.UnhandledException` is raised with `IsTerminating = true`, and the process ends.

A second UI thread gives the same result when nothing is subscribed on it.
That thread runs its own `Dispatcher`, but `Application.DispatcherUnhandledException` only reports exceptions from the `Dispatcher` of the thread that created the `Application`.
Subscribing to `Dispatcher.UnhandledException` on that thread and setting `Handled` kept the process running.

### Exceptions that stop inside a Task or a Timer

An exception from work passed to `Task.Run` is caught by the `Task`, which moves itself to the `Faulted` state.
If nobody awaits that `Task`, the exception travels no further.
As the "(no GC)" row shows, none of the three handlers is called, and the process keeps running.

This holds even when the throwing thread is the UI thread.
In the row where a `Click` handler calls an `async Task` method and discards the returned `Task`, the exception was thrown on the UI thread after `await`, yet it did not reach `DispatcherUnhandledException`.
The exception was caught by the method's returned `Task`, not by the `Dispatcher`.

`TaskScheduler.UnobservedTaskException` is raised when a faulted `Task` whose exception was never observed is reclaimed by garbage collection.
In the "+ GC.Collect()" row, the handler was called after the GC was run explicitly.
When the GC runs depends on memory conditions, so there is no guarantee that this handler fires soon after the exception.

An exception thrown in a synchronous `System.Timers.Timer.Elapsed` handler also reached none of the handlers.
The official documentation states that `System.Timers.Timer` catches and suppresses exceptions thrown by `Elapsed` handlers ([Timer Class](https://learn.microsoft.com/en-us/dotnet/api/system.timers.timer)).
The same page notes that this does not apply to asynchronous handlers that use `await`.
As the table shows, an `async` `Elapsed` handler ended the process.
The next section explains why.

### Exceptions from async void

An exception that escapes an `async void` method is rethrown on the `SynchronizationContext` that was current **when the method started**.
This is because an `async void` method has no `Task` to store the exception in ([Async/Await - Best Practices in Asynchronous Programming](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)).

In an `async void` `Click` handler started on the UI thread, the `SynchronizationContext` points to the `Dispatcher`.
As the table shows, both the exception from an awaited `Task.Run` and the exception thrown after `ConfigureAwait(false)` reached `DispatcherUnhandledException`.
The "throwing thread" column shows that both were thrown on a thread-pool thread.
The destination was decided by the `SynchronizationContext` at the start of the `async void` method, not by the thread that threw.

An `async void` method started on a thread-pool thread, on the other hand, has no `SynchronizationContext`.
The exception was rethrown on a thread-pool thread and ended the process, just like `new Thread`.
When `SynchronizingObject` is not set, `System.Timers.Timer.Elapsed` is raised on the thread pool, so an `async` lambda registered there takes this route.

---

## Consequences of These Routes

The following points follow from this mechanism.

- **`DispatcherUnhandledException` alone cannot catch exceptions that bypass the `Dispatcher`.** Only `AppDomain.UnhandledException` receives exceptions from `new Thread` or from an `async void` method started on the thread pool, and that handler cannot prevent the process from ending. Code on those threads has to use `try` / `catch` inside the work itself.
- **Treat exceptions from a `Task` that was not awaited as not reaching the log in time.** Even when the throwing thread is the UI thread, once a `Task` catches the exception it does not reach `DispatcherUnhandledException`. `UnobservedTaskException` is not raised until a GC runs.
- **An unbroken chain of `await` from a UI event handler (`async void`) routes exceptions to `DispatcherUnhandledException`.** When the event handler awaits `Task.Run` or an `async Task` method, and that method in turn awaits every inner `Task`, the exception climbs the chain and escapes the event handler. From there it goes to the `SynchronizationContext` that was current when the event handler started, which is the main `Dispatcher`. This is the "async void Click, await async Task" row in the table. Discarding a returned `Task` anywhere along the chain stops the exception at that `Task`.
- **The same `async` handler ends up somewhere different depending on the `SynchronizationContext` of the thread that raises the event.** An `async` handler on `Click` reaches `DispatcherUnhandledException`, while an `async` lambda on `System.Timers.Timer.Elapsed` ends the process. A synchronous `Elapsed` handler has its exceptions suppressed, but making it `async` ends the process instead, so the behavior flips when the handler is rewritten.

---

## Implementation Example

The three handlers are subscribed according to the exceptions each one can receive and the role it plays.
`DispatcherUnhandledException` logs UI-thread exceptions and lets the application continue, `AppDomain.UnhandledException` records the final entry before termination, and `UnobservedTaskException` detects `Task` objects that were never awaited.

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Exceptions caught by the main Dispatcher. Setting Handled keeps the application running.
        DispatcherUnhandledException += (_, args) =>
        {
            Log("UI", args.Exception);
            args.Handled = true;
        };

        // Exceptions that bypass the Dispatcher, and UI-thread exceptions left unhandled above.
        // Termination can no longer be prevented here.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(args.IsTerminating ? "Fatal" : "NonFatal", args.ExceptionObject as Exception);

        // Exceptions from faulted Tasks that were never awaited. Raised after a GC, so it lags behind the failure.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log("Unobserved", args.Exception);
            args.SetObserved();
        };
    }

    private static void Log(string kind, Exception? exception)
    {
        // A real application writes to a file or a logging framework.
        System.Diagnostics.Trace.WriteLine($"[{kind}] {exception}");
    }
}
```

`ExceptionObject` on `AppDomain.UnhandledException` is typed as `object`, so it is received with `as` in case something other than an `Exception` arrives.
That is why the parameter of `Log` is nullable.
This handler runs just before the process ends, so it should not do slow work or show UI.

Background work is awaited from a UI event handler.
As the "async void Click, try / catch around await" row shows, the exception is rethrown at the `await`, where `catch` can handle it.
Anything not handled there still reaches `DispatcherUnhandledException`.

```csharp
private async void SaveButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        await Task.Run(() => SaveToFile(_document));
        StatusText.Text = "Saved";
    }
    catch (IOException ex)
    {
        StatusText.Text = $"Could not save: {ex.Message}";
    }
}
```

Even with `ImplicitUsings` enabled, WPF projects do not include `System.IO` in their implicit `using` directives ([Implicit using directives](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview#implicit-using-directives)), so a file that uses `IOException` needs `using System.IO;` at the top.
The `catch` handles only the expected exception and leaves everything else to `DispatcherUnhandledException`.
An exception handled by `catch` is no longer unhandled, so it reaches none of the three handlers.
If it needs to be recorded, record it inside the `catch`.
Without the `await` on `Task.Run(...)`, the exception reaches neither `DispatcherUnhandledException` nor `AppDomain.UnhandledException`.
If it reaches anything at all, it is `UnobservedTaskException` after a GC.

For a thread created with `new Thread` or a `System.Timers.Timer.Elapsed` handler, wrap the body of the work in `try` / `catch`.
The same applies when `Elapsed` is made `async`.
When periodic work may run on the UI thread, use `DispatcherTimer`.
Exceptions from `Tick` reach `DispatcherUnhandledException`.
`Tick` runs on the UI thread, however, so heavy work should be moved off it with `await Task.Run(...)` inside `Tick`.

---

## Caveats

- **`e.Handled = true` does not guarantee that the application state is still valid after the exception.** An exception in the middle of an operation can leave a ViewModel or a file half-updated. Continuing unconditionally after every exception means accepting input in a broken state. Limit which exceptions are allowed to continue, or notify the user and prompt them to save or restart.
- **Do not rely on `UnobservedTaskException` as insurance against a missing `await`.** When a finalizer runs is undefined ([Object.Finalize](https://learn.microsoft.com/en-us/dotnet/api/system.object.finalize)), and .NET 5 and later do not run finalizers at process exit ([Finalizers](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/finalizers)), so it may never be called at all.
- **Do not make ViewModel methods `async void`.** Called from the UI thread, their exceptions reach `DispatcherUnhandledException`, but called from the thread pool, they end the process. Return `Task` instead and `await` it at the call site.
- **When creating a second UI thread, subscribe to `Dispatcher.UnhandledException` on that thread.** `Application.DispatcherUnhandledException` covers only the main `Dispatcher`.
- **.NET 10 also provides `System.Runtime.ExceptionServices.ExceptionHandling.SetUnhandledExceptionHandler` ([API reference](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.exceptionservices.exceptionhandling.setunhandledexceptionhandler)).** This article does not cover it, and its effect on exceptions from other threads in WPF was not tested.

---

## Summary

The destination of an unhandled exception is decided by the layer that first catches it.
`DispatcherUnhandledException` receives only exceptions caught by the main `Dispatcher`.
Exceptions that no layer catches before the top of their thread end the process, and exceptions from unawaited `Task` objects reach no handler until a GC runs.

To keep the application alive and get every unhandled exception into the log, build around routing exceptions to `DispatcherUnhandledException`.
Call background work from UI event handlers through an unbroken chain of `await`, and write periodic work that may run on the UI thread with `DispatcherTimer`.
For `new Thread` and `System.Timers.Timer`, which do not fit that pattern, use `try` / `catch` inside the work.
Subscribe to `AppDomain.UnhandledException` for a final record before termination and to `UnobservedTaskException` for detecting missing `await`s, and use neither as a way to recover from an exception.

---

## Related Articles

- [Diagnosing a WPF Process That Stays Alive After the Window Closes — ShutdownMode and Foreground Threads](/articles/wpf-application-not-exiting-shutdownmode-threads/)
- [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/)
