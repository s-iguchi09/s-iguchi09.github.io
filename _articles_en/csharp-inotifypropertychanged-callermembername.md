---
layout: article-en
title: "Implementing INotifyPropertyChanged Concisely with CallerMemberName"
date: 2026-07-16
category: C#
excerpt: "Make INotifyPropertyChanged concise with the CallerMemberName attribute, covering nameof differences, SetProperty helper, and dependent-property notification."
---

## Overview

`INotifyPropertyChanged` is the standard interface used in WPF and MVVM to notify the view of changes on a view model.
The implementation only raises the `PropertyChanged` event, but a naive version passes the property name as a string literal, which is verbose and fragile against typos.
This article explains how to make the notification concise with the `CallerMemberName` attribute, comparing it with the traditional approach.
It also covers how to use it alongside the `nameof` operator, a `SetProperty` helper that combines comparison, assignment, and notification into one line, and the limits of dependent-property notification.

---

## Prerequisites / Environment

- Framework / Language: .NET Framework 4.5 or later / .NET Core / .NET 5 or later, C# 5.0 or later
- Target feature: `System.ComponentModel.INotifyPropertyChanged`
- Architecture: MVVM (change notification on the view model)
- Attribute used: `System.Runtime.CompilerServices.CallerMemberName`

The `CallerMemberName` attribute was added in C# 5.0 (.NET Framework 4.5).
On earlier environments, only the string-literal or expression-tree approaches are available.

---

## String Literals and Their Problems

Consider the naive implementation that does not use `CallerMemberName` first.
The following is a base class that raises `PropertyChanged` and a property that consumes it.

```csharp
using System.ComponentModel;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PersonViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged("Name");
        }
    }
}
```

The property name is passed directly as a string in `OnPropertyChanged("Name")`.
This approach cannot verify the name at compile time, so a typo such as `"Nmae"` goes unnoticed until run time.
It also fails silently when the property is renamed, because the string is not updated automatically.

---

## Implementation with the nameof Operator

The `nameof` operator, added in C# 6.0, replaces the string literal with a symbol reference that is checked at compile time.
When the property is renamed, the rename operation also updates the `nameof` target.

```csharp
public string Name
{
    get => _name;
    set
    {
        _name = value;
        OnPropertyChanged(nameof(Name));
    }
}
```

`nameof(Name)` is expanded to the string `"Name"` at compile time, so no run-time reflection occurs.
A typo or a nonexistent name becomes a compile error, and the reference follows renames, which makes it superior to a raw string.
The remaining cost is that `nameof(ownPropertyName)` must still be written explicitly on every setter.

---

## Notification with CallerMemberName

Applying the `CallerMemberName` attribute to an optional parameter of the notification method makes the compiler fill in the caller's member name.
Calling it without arguments from inside a property setter passes that property name automatically.

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PersonViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }
}
```

Calling `OnPropertyChanged()` without arguments makes the compiler embed the setter's property name `"Name"` as the actual argument.
Because the substitution is resolved at compile time, no run-time reflection occurs, and there is no performance cost, just like `nameof`.
The main benefit is that the setter is shorter and the property name is no longer duplicated.

---

## Consolidation with a SetProperty Helper

In practice, notifications often include a guard that raises only when the value actually changes.
Applying `CallerMemberName` to a helper method that combines comparison, assignment, and notification shortens the setter even further.

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class PersonViewModel : ViewModelBase
{
    private string _firstName;
    public string FirstName
    {
        get => _firstName;
        set => SetProperty(ref _firstName, value);
    }
}
```

This `ViewModelBase` holds both `OnPropertyChanged` and `SetProperty`, so it is the complete base class reused in the following sections.
`SetProperty` compares the current and new values with `EqualityComparer<T>.Default` and returns `false` without notifying when they are equal.
When the value changes, it updates the field, calls `OnPropertyChanged`, and returns `true`.
The `bool` return value can be used to conditionally raise additional notifications for dependent properties, described below.
This shape matches the `SetProperty` provided by `ObservableObject` in `CommunityToolkit.Mvvm` and is easy to reproduce in a custom base class.

---

## Notifying Dependent Properties

`CallerMemberName` fills in only the caller's own member name.
Notifying a different property, such as a computed property derived from others, requires passing that name explicitly.
Here, a dependent property means a read-only property that derives its value from other properties, which is a separate concept from a WPF dependency property (`DependencyProperty`).

```csharp
public class PersonViewModel : ViewModelBase
{
    private string _firstName;
    public string FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    public string FullName => $"{FirstName} {LastName}";

    private string _lastName;
    public string LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    }
}
```

`FullName` is derived from `FirstName` and `LastName`, so a change to either must also notify `FullName`.
`CallerMemberName` cannot be used for this notification, so `nameof(FullName)` specifies the target name.
`CallerMemberName` and `nameof` are not mutually exclusive; using `CallerMemberName` for a property's own notification and `nameof` for dependent properties is the standard practice.

---

## Notes

- `CallerMemberName` applies only to methods with an optional parameter, and an explicit argument at the call site takes precedence over the substitution.
- The substituted name is the member that encloses the call site. When it is evaluated outside the setter, such as in a field initializer or a delegate passed to another member, the name will not be the intended property.
- Notification for computed properties and indexers is not automated, so dependency relationships must be managed manually.
- To invalidate every property at once, pass an empty string or `null` to `PropertyChangedEventArgs`; this is an explicit choice separate from the `CallerMemberName` substitution.

---

## Summary

Choose the notification approach for `INotifyPropertyChanged` based on the target and the C# version.

| Approach | Pros | Cons | Best suited for |
|---|---|---|---|
| String literal | Simple to write | Cannot detect typos and does not follow renames | Not recommended (only for compatibility below C# 5.0) |
| nameof operator | Compile-time checked and rename-safe | The name must be written on every call | Notifying other properties such as dependents |
| CallerMemberName | No call-site name and no reflection | Can fill in only the caller's own member name | Ordinary notification from a setter |
| SetProperty helper | Combines comparison, assignment, and notification in one line | Requires a base class | Standard implementation across MVVM |

On C# 5.0 or later, consolidating ordinary property notification into a `SetProperty` helper based on `CallerMemberName` is the most concise and maintainable option.
Only dependent-property notification cannot be automated by `CallerMemberName`, so combine it with `nameof` to specify the target name.
Separating these two by role yields change notification that stays concise while remaining resilient to renames.
