---
layout: article-en
title: "Why Task.Result Freezes a WPF App and When ConfigureAwait(false) Does Not Help"
seo_title: "Task.Result Freezes WPF: Where ConfigureAwait(false) Fails"
date: 2026-10-19
category: WPF
excerpt: "Blocking on .Result on the WPF UI thread freezes the window when the code after an await must resume there. ConfigureAwait(false) and Task.Run can fail too."
image: /images/articles/wpf-task-result-deadlock-synchronizationcontext/task-result-deadlock-matrix.svg
---

## Overview

Inside a synchronous method such as a button's Click handler, writing `LoadAsync().Result` to get the result of an asynchronous method makes a WPF window stop responding the moment the button is clicked, if that method returns to the UI thread after an `await`.
No exception is thrown, no timeout fires, and the window never comes back.

The `await` tries to send the rest of the method back to the UI thread it started on, and that UI thread is sitting inside `.Result` waiting for the rest of the method.
Each waits for the other, so neither makes progress.

For cases where blocking cannot be avoided, Microsoft Learn lists two mitigations: adding `ConfigureAwait(false)` to every `await` in the asynchronous method, and offloading the call to the thread pool with `Task.Run`.
Both work under the right conditions.
Code that adds `ConfigureAwait(false)` only to an `await` on an already-completed `Task` or only to the outer `await`, or that calls into the UI thread synchronously from inside `Task.Run`, froze in the same way.
Even when they work, the UI thread stops while it waits.

The sections below wait for the same work from a Click handler in twelve different ways and tabulate where each one stops and where it gets through.
The table is then used to work out which `await`s actually need `ConfigureAwait(false)`.

---

## Prerequisites and Environment

- Framework: .NET 6 or later / WPF (measured on .NET 10 only)
- Language: C# (uses `async` / `await`)
- Features covered: `Task.Result`, `Task.Wait`, `Task.GetAwaiter().GetResult()`, `Task.ConfigureAwait`, `Task.Run`, `Dispatcher.Invoke`, `DispatcherSynchronizationContext`
- Architecture: measured with a code-behind Click handler. Calling from an MVVM `ICommand` also waits on the UI thread, but it was not measured
- Verified on: .NET 10 / Windows 11
- Method: for each way of waiting, a separate WPF test application was started, a window was shown, and Click was raised through `ButtonAutomationPeer.Invoke`. The Click handler first queues two items of work on the UI thread (`Dispatcher.BeginInvoke`, at `Normal` priority and at `Render`, the priority used for rendering) and then waits for the asynchronous method. A watchdog thread treats the handler as "stays blocked" if it has not returned after 2.5 seconds, and records the state of the `Task.Delay` and of the queued work at that moment. A frozen process is terminated from outside. The measurement is implemented as a scene in `tools/screenshot-capture`; the table is re-measured on every run, and the run fails if any result differs from the expected one

.NET Framework and application models other than WPF were not tested.

---

## Problem

The following Click handler never returns once the button is pressed.
In the test, `LoadAsync` waits on `Task.Delay(300)` in place of I/O.
In a real application, an `HttpClient` or database call sits in that position.

```csharp
private static async Task<int> LoadAsync()
{
    await Task.Delay(300);
    return 42;
}

private void OnLoadClick(object sender, RoutedEventArgs e)
{
    int value = LoadAsync().Result; // never returns from here
    StatusText.Text = value.ToString();
}
```

Work that should finish in 300 milliseconds has not finished after 2.5 seconds.
Inspecting the state at that point shows that the `Task.Delay` has already completed.
What is stuck is not the awaited work but the `return 42;` after it.

Replacing `.Result` with `.GetAwaiter().GetResult()` or `.Wait()` does not change the result.
All three block the UI thread inside the Click handler until the `Task` completes.

---

## The Commonly Recommended Fixes

Microsoft Learn's [Synchronous wrappers for asynchronous methods](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/synchronous-wrappers-for-asynchronous-methods) first recommends making the code async all the way up, and lists two mitigations for when that is not possible:

- When the asynchronous method can be modified, add `ConfigureAwait(false)` to every `await` in it
- When it cannot, offload the call to the thread pool with `Task.Run`

`ConfigureAwait(false)` tells the `await` not to resume on the original context ([Task.ConfigureAwait(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait)).
The version of `LoadAsync` with it added is named `LoadAllConfigureAwaitAsync`.

```csharp
private static async Task<int> LoadAllConfigureAwaitAsync()
{
    await Task.Delay(300).ConfigureAwait(false);
    return 42;
}
```

Offloading with `Task.Run` calls `LoadAsync` unchanged, as follows.

```csharp
int value = Task.Run(() => LoadAsync()).Result;
```

In both forms, `.Result` returned.
The problem is that real code is rarely as simple as these examples.
There were two ways of writing it that froze even though `ConfigureAwait(false)` appears to be in place, and one that froze even with `Task.Run`.

---

## Why They Do Not Work

The following table shows the result of waiting for an asynchronous method from a Click handler, changing only how it is waited for.
The body of each method is as shown in the code examples in the article.

{% include tables/articles/wpf-task-result-deadlock-synchronizationcontext/task-result-deadlock-matrix.en.md %}

Each way of waiting was run once in a separate process (.NET 10 / Windows 11). The times in the "UI thread" column are from this environment. For rows that return, the time roughly matches the total of the inner `Task.Delay(300)` calls (two calls, 600 milliseconds, for `LoadFirstSuspendConfigureAwaitAsync`), and the `Wait(TimeSpan.FromSeconds(1))` row matches the one-second timeout. Only the `async void` row shows the time until the work queued at the start ran. "UI work queued during the wait" shows when the `Normal`-priority work queued at the start of the Click handler ran. In the rows that stay blocked, the work queued at `Render` priority also stayed Pending.
{: .table-caption}

Inside the Click handler, `SynchronizationContext.Current` was a `DispatcherSynchronizationContext`.
`await` captures this context, and when the awaited `Task` completes, it sends the rest of the method to the UI thread through that context.
The UI thread is blocked inside `.Result`, so what was sent never runs.
That both the `Normal` and the `Render` work queued at the start stay Pending also shows that the UI thread cannot run the queued work.

The `LoadAsync().Wait(TimeSpan.FromSeconds(1))` row shows that the continuation is waiting its turn on the UI thread.
After `Wait` returns `false` at the one-second timeout and the Click handler releases the UI thread, the code after `await` ran on the UI thread.
A timeout only turns the freeze into a failure; the UI thread still stops while it waits.

### ConfigureAwait(false) on an Already-Completed Task

`LoadCachedFirstAsync` first awaits `GetCachedAsync`, which returns `Task.FromResult` when the cache has a value, and then waits for I/O.
`ConfigureAwait(false)` is added only to the first `await`.

```csharp
private static Task<int> GetCachedAsync() => Task.FromResult(42);

private static async Task<int> LoadCachedFirstAsync()
{
    int cached = await GetCachedAsync().ConfigureAwait(false); // an already-completed Task
    await Task.Delay(300);
    return cached;
}
```

The `Task` returned by `GetCachedAsync` has already completed at the `await`.
An `await` on a completed `Task` does not suspend, so `ConfigureAwait(false)` does nothing.
The measurement confirmed that the code right after the first `await` was still on the UI thread.
The second `await`, which does not have it, therefore captures the UI thread's context, and the `LoadCachedFirstAsync().Result` row stays blocked.
`LoadCachedFirstAllConfigureAwaitAsync`, which also adds it to the second `await`, returned.

```csharp
private static async Task<int> LoadCachedFirstAllConfigureAwaitAsync()
{
    int cached = await GetCachedAsync().ConfigureAwait(false); // an already-completed Task
    await Task.Delay(300).ConfigureAwait(false);                // suspends here; the rest runs on the thread pool
    return cached;
}
```

Even though the first `await` does nothing, the rest of the method is not sent back to the original context as long as the second `await`, which actually suspends, has it (it continued on the thread pool in this example).

### ConfigureAwait(false) Only on the Outer await

`LoadOuterAsync` adds it only to the `await` in its own method and leaves the called `LoadAsync` unchanged.

```csharp
private static async Task<int> LoadOuterAsync()
{
    return await LoadAsync().ConfigureAwait(false); // the await inside LoadAsync does not have it
}
```

`ConfigureAwait(false)` affects only the continuation of the `await` it is applied to.
`LoadAsync` is called on the UI thread, and the `await` inside it captures the UI thread's context.
Because the inner continuation waits for the UI thread, the code after the outer `await` was never reached.
Adding it to the calling code does not help if an `await` in the callee lacks it and suspends on the UI thread.

### Dispatcher.Invoke Inside Task.Run

`Task.Run(() => LoadAsync()).Result` returned.
Inside the work passed to `Task.Run`, `SynchronizationContext.Current` was `null`, so the continuation in `LoadAsync` also runs on the thread pool.
However, the call stayed blocked when it wrapped `LoadWithInvokeAsync`, which calls `Dispatcher.Invoke` after the wait to update the window, as `Task.Run(() => LoadWithInvokeAsync()).Result`.

```csharp
private async Task<int> LoadWithInvokeAsync()
{
    await Task.Delay(300);
    Dispatcher.Invoke(() => StatusText.Text = "loaded"); // never returns from here
    return 42;
}
```

The code after `await` ran on the thread pool, but it never returned from `Dispatcher.Invoke`.
`Dispatcher.Invoke` waits synchronously for the delegate to run on the UI thread ([Dispatcher.Invoke](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatcher.invoke)), so it ends up waiting for the UI thread that is blocked in `.Result`.
Offloading with `Task.Run` works only when the wrapped work does not call into the UI thread synchronously.

---

## When They Do Work

Of the rows with `ConfigureAwait(false)`, the three that returned are `LoadAllConfigureAwaitAsync`, `LoadFirstSuspendConfigureAwaitAsync`, and `LoadCachedFirstAllConfigureAwaitAsync`.
`LoadFirstSuspendConfigureAwaitAsync` adds it only to the first `await`, which actually suspends, and not to the second.

```csharp
private static async Task<int> LoadFirstSuspendConfigureAwaitAsync()
{
    await Task.Delay(300).ConfigureAwait(false); // suspends here; the rest runs on the thread pool
    await Task.Delay(300);                        // there is no UI context left to capture
    return 42;
}
```

After the first `await`, `SynchronizationContext.Current` was `null`, so there was no UI context for the second `await` to capture.
What separates the three that returned from the two that stayed blocked is **whether an `await` without `ConfigureAwait(false)` suspends while running on the UI thread**.
In both `LoadCachedFirstAsync` and `LoadOuterAsync`, such an `await` suspended on the UI thread.

Whether this condition holds, however, depends on which `await`s in the callee actually suspend.
That varies with runtime state, such as whether the cache hits, so it is hard to guarantee by reading the code.
The official guidance recommends adding it to every `await` as a performance best practice that also helps prevent deadlocks when callers block.
Adding it everywhere also removes the need to work out which `await` suspends.

Offloading with `Task.Run` returned when the wrapped work contained no call, such as `Dispatcher.Invoke`, that waits synchronously for the UI thread.

Whichever condition lets the call return, the UI thread stops while it waits.
The work queued at the start did not run until after the Click handler returned.
As long as `.Result` is used to wait, the UI thread does not run queued work for the duration of the wait.

The UI thread kept running during the wait only when the Click handler was `async void` and used `await`.
`await` leaves the Click handler without blocking the UI thread, so the work queued at the start ran right away.
The code after `await` resumes on the UI thread, so it can update the window directly.

---

## Implementation Example

Instead of `.Result`, make the event handler `async void` and `await`.
An event handler cannot return a value, which makes it one of the few places where `async void` is appropriate ([Async return types](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-return-types)).

```csharp
private async void OnLoadClick(object sender, RoutedEventArgs e)
{
    LoadButton.IsEnabled = false;
    try
    {
        int value = await LoadAsync();
        StatusText.Text = value.ToString(); // back on the UI thread after await, so it can update directly
    }
    finally
    {
        LoadButton.IsEnabled = true;
    }
}
```

Because the UI thread keeps processing during the `await`, the button may be pressed again.
The button is disabled while the work runs to prevent it from running twice.
An exception that escapes an `async void` handler reaches `Application.DispatcherUnhandledException` ([Exceptions WPF's DispatcherUnhandledException Never Sees — Where Thread, Task and async void Exceptions Go](/articles/wpf-unhandled-exception-dispatcher-task-thread/)).
To show an error for each operation, wrap the `await` in `try` / `catch`.

`.Result` usually appears when a synchronous method sits somewhere in the call chain.
In that case, change the synchronous method to `async Task`, rewrite its callers to use `await` one by one, and carry `async` all the way up to the event handler.

```csharp
// Before: waits inside a synchronous method
public void Refresh()
{
    Items = _repository.LoadAsync().Result;
}

// After: carry async up to the caller
public async Task RefreshAsync()
{
    Items = await _repository.LoadAsync();
}
```

The caller of `RefreshAsync` also waits with `await`.
Reverting to `.Result` at even one point in the chain freezes at that point in the same way.

When writing a library, add `ConfigureAwait(false)` to every `await` in methods whose code after the `await` does not need the original context.
Even a method that never touches the window should first be checked for callbacks or events supplied by the caller that it runs after an `await`, since those may assume the UI thread.
The official guidance likewise excludes code that needs to resume on the captured context.
Adding it reduces the paths that freeze when a caller waits with `.Result`, but it does not keep the caller's UI thread from blocking during the wait.
The fix on the application side is still to wait with `await`.

---

## Caveats

- When an `await` with `ConfigureAwait(false)` suspends, the rest of the method is not sent back to the original context; which thread it continues on depends on how the `Task` completes (the thread pool, for the `Task.Delay` in the measurement). When it is applied to an already-completed `Task`, the `await` does not suspend and the code continues on the UI thread. Do not add it to an `await` that precedes code updating the window.
- Whether a called library uses `ConfigureAwait(false)` cannot be known without reading its source. If only some of its `await`s have it, a branch that returns a completed `Task` lets a later `await` without it capture the UI thread's context. Do not rely on the callee's implementation for `.Result` to return.
- `Task.Run(...).Result` returns, but the UI thread stops while it waits. It freezes as soon as code that updates the window synchronously through `Dispatcher.Invoke` is added to the wrapped work.
- Adding a timeout with `Wait(TimeSpan)` still stops the UI thread during the wait. The asynchronous work does not stop after the timeout, and its continuation runs once the UI thread becomes free.
- `.GetAwaiter().GetResult()` produced the same result as `.Result` as far as freezing is concerned.
- Code that updates the UI through `Dispatcher.Invoke` from a background thread, including collection updates, also stops returning for the same reason if the UI thread is waiting in `.Result`. Updating a collection from a background thread is covered in [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/).

---

## Summary

Calling `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on the UI thread always blocks the UI thread while it waits.
On top of that, if an incomplete `await` has captured the UI thread's context, the continuation keeps waiting for the UI thread to become free and the call never returns.
Even when the call returns, as with `LoadAllConfigureAwaitAsync`, the UI thread still stops for the duration of the wait.
Within the measured cases, the calls with `ConfigureAwait(false)` that returned were those in which no `await` without it suspended on the UI thread.
Adding it only to an `await` on an already-completed `Task` or only to the outer `await` did not help.
Offloading with `Task.Run` also freezes as soon as the work inside calls `Dispatcher.Invoke`.

In a WPF application, make the event handler `async void`, use `await`, and carry `async` up through the callers.
Treat `ConfigureAwait(false)` as something added to every `await` in library code whose continuations do not need the original context.
Reserve `Task.Run(...).Result` for cases where rewriting is not possible, such as implementing an interface that requires a synchronous method, and use it only after confirming that the wrapped work never calls into the UI thread synchronously and that a frozen window is acceptable for the duration of the wait.

---

## Related Articles

- [Exceptions WPF's DispatcherUnhandledException Never Sees — Where Thread, Task and async void Exceptions Go](/articles/wpf-unhandled-exception-dispatcher-task-thread/)
- [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/)
