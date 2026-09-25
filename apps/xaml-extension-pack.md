---
layout: app-page
permalink: /apps/xaml-extension-pack.html
title: "Xaml.ExtensionPack | WPF MVVM Extension Library"
heading: "Xaml.ExtensionPack"
lead: "A lightweight XAML extension library for WPF that adds no additional third-party runtime dependencies. Provides essential MVVM foundations as a single small library."
description: "A lightweight WPF XAML extension library with no third-party runtime dependencies, providing BindableBase, RelayCommand and AsyncRelayCommand with examples."
---

## Overview

Xaml.ExtensionPack is a small library designed to eliminate boilerplate in WPF/XAML application development. Its four building blocks are the things almost every MVVM application ends up writing by hand — a change-notification base class, a synchronous command, an async command, and a safe fire-and-forget helper — alongside the small interfaces those commands are exposed through.

The package brings **no third-party dependencies into your project**. Adding it does not pull a dependency tree along and does not force an application architecture on you — you can adopt a single type from it and ignore the rest.

The library itself takes one build-time reference, `Microsoft.NETFramework.ReferenceAssemblies`, which supplies the reference assemblies needed to compile the `net472` target. It is declared with `PrivateAssets="all"`, so it stays inside this project and is never propagated to consumers.

## Installation

The project is set up to build a NuGet package, but it is **not published to NuGet.org yet**. If NuGet.org is the only source configured, `dotnet add package` has nothing to resolve against; a local or private feed that holds the package works normally. With no such feed available, clone the repository and reference the project directly:

```shell
git clone https://github.com/s-iguchi09/Xaml.ExtensionPack.git
```

```shell
dotnet add YourApp.csproj reference path/to/Xaml.ExtensionPack/src/Xaml.ExtensionPack/Xaml.ExtensionPack.csproj
```

Alternatively, pack it locally and consume it from a local feed. Build the package into a directory, register that directory as a NuGet source, then add the package by exact version. The project file does not set a version, so `dotnet pack` produces `1.0.0`:

```shell
dotnet pack src/Xaml.ExtensionPack/Xaml.ExtensionPack.csproj -c Release -o ./local-feed
```

```shell
dotnet nuget add source ./local-feed --name xaml-extensionpack-local
```

```shell
dotnet add YourApp.csproj package Xaml.ExtensionPack --version 1.0.0 --source ./local-feed
```

Both the source and the version are worth naming explicitly. This package ID is unclaimed on NuGet.org, so an unqualified lookup could resolve to whatever someone else publishes under that name later, and leaving the version open lets a higher one win once that happens. For an ongoing setup, pin the ID to the local feed with [Package Source Mapping](https://learn.microsoft.com/nuget/consume-packages/package-source-mapping){: target="_blank" rel="noopener noreferrer"} in `nuget.config`. Once that section exists, every package in the project — transitive ones included — has to match a pattern, which is what the `*` entry covers. The keys under `packageSourceMapping` refer back to the sources declared in `packageSources`. The `<clear />` entries drop inherited configuration. NuGet walks up the directory tree and merges collection elements from every `nuget.config` it finds, plus the user- and machine-level ones, so wherever such a file exists an inherited source or an inherited mapping for the same ID would otherwise still be in play. They are not required when nothing is inherited, but keeping them makes the result the same on every machine:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="xaml-extensionpack-local" value="./local-feed" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="xaml-extensionpack-local">
      <package pattern="Xaml.ExtensionPack" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Two limits come with it. Package Source Mapping arrived in NuGet 6.0, so it needs Visual Studio 2022, .NET SDK 6.0.100, or nuget.exe 6.0.0 or later; older tooling ignores the section silently, which means every environment that restores the project — CI included — has to be on a compatible version for the pin to hold. And source mapping governs only where restore downloads from: it does not stop `dotnet add package` from querying other sources for metadata, which is the other reason to pass `--source`.

All public types live in the `Xaml.ExtensionPack` namespace, so a single using directive is enough:

```csharp
using Xaml.ExtensionPack;
```

## Background: Cutting Boilerplate Without Heavyweight Dependencies

WPF applications built with the MVVM pattern tend to accumulate repetitive infrastructure code: `INotifyPropertyChanged` implementations, `ICommand` wrappers, and async-safe fire-and-forget helpers. While community toolkits exist, they often pull in large dependency trees or enforce architectural conventions beyond what a project needs.

There is also a practical constraint that narrows the field: line-of-business WPF applications frequently still target .NET Framework 4.7.2. An explicit `net472` target is not what decides this — .NET Framework 4.6.1 and later consume `netstandard2.0` assets, so a toolkit that ships one remains usable — and 4.7.2 is the version Microsoft recommends for that combination, the earlier ones having known issues with it. What rules a library out is shipping no `net472`\-compatible asset at all, which is what happens once a package targets modern .NET exclusively.

That is the general rule for `netstandard2.0` libraries, and it is worth separating from what this one offers. Xaml.ExtensionPack ships no `netstandard2.0` asset: its .NET Framework support is the `net472` target specifically. A project on 4.6.1 through 4.7.1 will not pick that asset up, so 4.7.2 is the floor here rather than a recommendation.

Xaml.ExtensionPack multi-targets `net10.0`, `net8.0`, and `net472` from a single package, so its API surface is the same on all three. Across a migration from .NET Framework to modern .NET, the view model code that depends on this library does not have to change; code that touches other APIs is a separate question.

## What Is Included

| Type | Kind | Purpose |
| --- | --- | --- |
| `BindableBase` | abstract class | Implements `INotifyPropertyChanged`. Provides `SetProperty` (with an optional post-change callback) and both string-based and lambda-based `OnPropertyChanged`. |
| `RelayCommand` | class | Delegate-based command taking an `Action` and an optional `Func<bool>` for `CanExecute`. |
| `RelayCommand<T>` | class | Typed variant that receives `CommandParameter` as `T`. Handles a null parameter for both reference and value types. |
| `AsyncRelayCommand` | class | Async command wrapping a `Func<Task>`. Exposes `IsExecuting` and blocks re-entry while the task is running. |
| `AsyncRelayCommand<T>` | class | Typed variant of the above, wrapping a `Func<T, Task>`. |
| `IRaiseCanExecuteChanged` | interface | Extends `ICommand` with `RaiseCanExecuteChanged()`, so a view model can re-evaluate command availability without depending on a concrete command type. |
| `IAsyncCommand` | interface | Contract for the async commands, exposing `ExecuteAsync` and `IsExecuting` alongside the `ICommand` members. |
| `TaskExtensions.FireAndForget` | extension method | Awaits a `Task` in a discarded context with an optional exception handler, avoiding unobserved task exceptions. |

## Usage

### BindableBase — property change notification

Derive a view model from `BindableBase` and route setters through `SetProperty`. It compares the current value with `EqualityComparer<T>.Default` and raises `PropertyChanged` only when the value actually changes, so redundant notifications do not reach the binding system.

```csharp
public class MainViewModel : BindableBase
{
    private string _userName = string.Empty;

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }
}
```

The overload taking an `Action` runs a callback only when the value changed — useful for refreshing a dependent command or a derived property:

```csharp
private int _quantity;

public int Quantity
{
    get => _quantity;
    set => SetProperty(ref _quantity, value, () => SubmitCommand.RaiseCanExecuteChanged());
}
```

When you need to notify for a property other than the one being set, the lambda overload keeps the reference refactoring-safe — renaming the property updates the notification automatically, which a string literal would not:

```csharp
set
{
    SetProperty(ref _firstName, value);
    OnPropertyChanged(() => FullName);
}
```

### RelayCommand — binding a method to a button

`RelayCommand` takes the execution delegate and an optional predicate. WPF disables the bound control automatically whenever `CanExecute` returns `false`.

```csharp
public class MainViewModel : BindableBase
{
    private string _userName = string.Empty;

    public RelayCommand SubmitCommand { get; }

    public MainViewModel()
    {
        SubmitCommand = new RelayCommand(Submit, () => !string.IsNullOrEmpty(UserName));
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value, () => SubmitCommand.RaiseCanExecuteChanged());
    }

    private void Submit()
    {
        // ...
    }
}
```

```xml
<Button Content="Submit" Command="{Binding SubmitCommand}" />
```

Note that `CanExecute` is not re-evaluated on its own. Call `RaiseCanExecuteChanged()` when the state it depends on changes — the `SetProperty` callback overload shown above is the usual place to do it.

### RelayCommand&lt;T> — receiving a command parameter

```csharp
public RelayCommand<Order> DeleteCommand { get; }

// in the constructor
DeleteCommand = new RelayCommand<Order>(
    order => Orders.Remove(order!),
    order => order is not null);
```

```xml
<Button Content="Delete"
        Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
        CommandParameter="{Binding}" />
```

### AsyncRelayCommand — preventing double execution

The common failure mode for an async command is the user clicking the button twice before the first operation finishes. `AsyncRelayCommand` sets `IsExecuting` for the duration of the task and returns `false` from `CanExecute` while it is set, so the button disables itself until the work completes — for the button's enabled state, nothing has to be tracked in the view model.

The guard covers invocations arriving one after another on the UI thread, which is what a command bound to a button produces. It is not a concurrency primitive: `ExecuteAsync` checks `CanExecute` and then sets `IsExecuting` as two separate steps, so two threads calling it at the same moment can both pass the check. Driving the command from several threads at once needs synchronization of your own.

One behavior to know before wiring a command to a button: `ICommand.Execute` — the path WPF takes on a click — runs `ExecuteAsync(parameter).FireAndForget()` with no handler argument. As described below, `FireAndForget` without a handler swallows the exception, so a failure inside the async operation disappears silently on that path. Catch inside the delegate you hand to the command, or call `ExecuteAsync` yourself and await it somewhere you can handle the failure:

```csharp
LoadCommand = new AsyncRelayCommand(async () =>
{
    try
    {
        Items = await _repository.GetItemsAsync();
    }
    catch (Exception ex)
    {
        _logger.Error(ex);
    }
});
```

```csharp
public AsyncRelayCommand LoadCommand { get; }

// in the constructor
LoadCommand = new AsyncRelayCommand(LoadAsync);

private async Task LoadAsync()
{
    Items = await _repository.GetItemsAsync();
}
```

For a progress indicator, do **not** bind to `LoadCommand.IsExecuting` directly. When it changes it raises `CanExecuteChanged`, not `PropertyChanged` — the command classes do not implement `INotifyPropertyChanged`. A binding to that property is evaluated once and never updated, so the indicator would never appear or disappear. Expose the state as a notifying property on the view model instead:

```csharp
private bool _isLoading;

public bool IsLoading
{
    get => _isLoading;
    private set => SetProperty(ref _isLoading, value);
}

private async Task LoadAsync()
{
    IsLoading = true;
    try
    {
        Items = await _repository.GetItemsAsync();
    }
    finally
    {
        IsLoading = false;
    }
}
```

```xml
<ProgressBar IsIndeterminate="True"
             Visibility="{Binding IsLoading,
                          Converter={StaticResource BooleanToVisibilityConverter}}" />
```

`StaticResource` resolves its key while the XAML is being loaded, so the converter has to be defined under that same key beforehand — in `Window.Resources`, in `App.xaml`, or in a merged `ResourceDictionary`. If no such resource exists, loading the XAML throws rather than silently leaving the binding unset:

```xml
<Window.Resources>
    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
</Window.Resources>
```

`IsExecuting` still does its job for the button itself: `CanExecute` consults it, and `CanExecuteChanged` is exactly what the command system listens to, so the bound control disables and re-enables on its own.

### FireAndForget — starting a task without awaiting it

Calling a `Task`\-returning async method without awaiting it leaves any exception captured inside that `Task`, which is left in a faulted state. Nothing surfaces it where it happened: awaiting the task or calling `Wait()` would rethrow it to the caller, and reading `Exception` would hand back the `AggregateException` without throwing, but a call site that simply discards the result does none of the three. An `async void` method is a separate case — it has no task to hold the exception, which is raised on the calling context instead.

The exception is not discarded outright. When the internal object holding it is finalized — the `Task` itself has no finalizer — the runtime raises `TaskScheduler.UnobservedTaskException`. That is a diagnostic channel rather than error handling: it fires whenever that object's finalizer happens to run, which can be long after the failure and in an unrelated part of the program. On modern .NET, and on .NET Framework since 4.5, the process then keeps running whether or not a handler observed the exception. .NET Framework can restore the older terminate-on-unobserved behavior through the application configuration file:

```xml
<configuration>
  <runtime>
    <ThrowUnobservedTaskExceptions enabled="true"/>
  </runtime>
</configuration>
```

Subscribing to the event is worth doing for telemetry, but it is an optional subscription and it arrives late: a failure that only reaches it has already passed the point where anything specific to the operation could respond.

`FireAndForget` awaits the task internally and passes the caught exception to the handler you supply, so the failure is observed where it occurs. A task that completes successfully does nothing further:

```csharp
_notifier.SendAsync(message).FireAndForget(ex => _logger.Error(ex));
```

Two things about the helper are worth knowing before relying on it. The handler is optional, and leaving it out swallows the exception entirely instead of reporting it, so pass one unless discarding failures is the intent. And the handler runs inside the `catch` block of an `async void` method, which means an exception thrown by the handler itself is not caught — it reaches the calling context unhandled. Keep the handler to logging, or something equally unlikely to throw.

## Design Notes

- **Commands expose `RaiseCanExecuteChanged` through an interface.** Depending on `IRaiseCanExecuteChanged` rather than the concrete `RelayCommand` type means a command property can be swapped between the sync and async implementations without touching the code that refreshes it.
- **Null command parameters are handled explicitly.** `RelayCommand<T>` distinguishes value types from reference types when the incoming `CommandParameter` is null, instead of failing the type check and silently doing nothing.
- **Async re-entry is guarded through `CanExecute`.** `CanExecute` returns `false` while the command is running, so the UI reflects the busy state on its own rather than the command accepting the invocation and discarding it. Setting `IsExecuting` raises `CanExecuteChanged` so bound controls refresh, and `ExecuteAsync` re-checks the same condition on entry and returns immediately when it no longer holds, which covers callers that invoke it directly instead of through the binding.
- **No source generators, no analyzers, no custom attributes.** The types are ordinary classes — the only attribute in play is `[CallerMemberName]` on the `SetProperty` and `OnPropertyChanged` parameters — so behavior is visible at the call site and debugging steps into real code rather than generated partials.

## Technology Stack

- **Language:** C# (latest)
- **Target Frameworks:** .NET 10, .NET 8, .NET Framework 4.7.2
- **Dependencies:** none at runtime; one build-time reference for the net472 target, not propagated to consumers
- **License:** MIT

## Repository

Source code, the sample application, and issue tracking are available on GitHub:

[View GitHub Repository — Xaml.ExtensionPack](https://github.com/s-iguchi09/Xaml.ExtensionPack){: .github-link target="_blank" rel="noopener noreferrer"}
