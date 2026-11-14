---
layout: article-en
title: "Why WPF PasswordBox.Password Cannot Be Bound, and Two Ways to Pass It to a ViewModel"
seo_title: "WPF PasswordBox.Password Binding: Two Ways for MVVM"
date: 2026-11-14
category: WPF
image: /images/articles/wpf-passwordbox-binding-mvvm/passwordbox-plaintext-in-memory.svg
excerpt: "PasswordBox.Password is not a dependency property, so binding it throws. Attached property vs. CommandParameter, compared on button state and plaintext left."
---

## Overview

When a sign-in screen is built with MVVM, the first attempt is often to bind `PasswordBox.Password` to a `string` on the ViewModel, the same way a `TextBox` is bound.
XAML written as `Password="{Binding Password}"`, however, throws when it is loaded.
The attached-property workaround can fail too: depending on how it is written, the input never reaches the ViewModel, or the typed characters come out in reverse order.

This article confirms the cause from the exception and the type information, then implements two ways of handing the password to a ViewModel: syncing it with a `string` through an attached property, and passing the `PasswordBox` as a `CommandParameter`.
The two differ in whether the button's enabled state can follow the input and whether the plaintext password stays in process memory.
The second point was checked by actually scanning the process memory.

---

## Prerequisites / Environment

- Framework: WPF. The code requires C# 9 or later (`is not` patterns are C# 9; `using` declarations are C# 8). Behavior was measured only in the environment below
- Architecture: MVVM. The ViewModel base class `ObservableObject` and `RelayCommand` / `RelayCommand<T>` are minimal hand-written implementations; `CanExecuteChanged` is raised only when the ViewModel calls `RaiseCanExecuteChanged` (not wired to `CommandManager.RequerySuggested`). `RelayCommand<T>` returns `false` from `CanExecute` and does nothing in `Execute` when the parameter is not a `T`. A non-generic `RelayCommand` with its own event, and one wired to `RequerySuggested`, are shown in the [related article](/articles/wpf-relaycommand-canexecute-not-updating/)
- Verified on: .NET 10 / Windows 11 (64-bit process)
- Input: typed into the `PasswordBox` one character at a time with `TextComposition`, the same path as keyboard input

---

## Problem

The following XAML is meant to show the ViewModel's `Password` property in a `PasswordBox` and send the input back to the ViewModel.

```xml
<PasswordBox Password="{Binding Password}" />
```

Loading this XAML throws `XamlParseException`.
The message says that a `Binding` cannot be set on the `Password` property of `PasswordBox`, and that a `Binding` can only be set on a `DependencyProperty` of a `DependencyObject`.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-binding-error.en.md %}

The exception raised by binding `Password`, and properties of the `PasswordBox` type. Measured on .NET 10 / Windows 11. The XAML was loaded with `XamlReader.Parse`, and the first line of the message was read with the UI culture set to en-US.
{: .table-caption}

---

## Cause / Background

`PasswordBox` has neither a `PasswordProperty` nor a `SecurePasswordProperty`, so `Password` and `SecurePassword` are not dependency properties (third row of the table; for `Password`, the exception in the first row shows the same).
`PasswordBox` does not implement `INotifyPropertyChanged` either.
`Password` therefore cannot be a binding target.
When it was used as a binding source, changes made by typing were not propagated either (the CommandParameter table below).
The mechanism provided for observing input changes is the `PasswordChanged` event (Examples in [PasswordBox](https://learn.microsoft.com/dotnet/api/system.windows.controls.passwordbox)).

The official documentation does not say why `Password` was not made a dependency property.
What it does say is that reading `Password` exposes the password as plain text in memory, and that `SecurePassword` returns it as a `SecureString` to avoid that ([PasswordBox.Password](https://learn.microsoft.com/dotnet/api/system.windows.controls.passwordbox.password)).
Internally, the password is held in a `SecureString` field rather than a `string` (confirmed in the same environment on the [PasswordBox demo page](/apps/wpf-standard-control-demo/passwordbox.html)).

`PasswordBox` is `sealed` (second row of the table), so a derived class cannot add a dependency property.
Passing the value to a ViewModel means adding a mechanism from outside the control.
This article compares two such mechanisms: an attached property and a `CommandParameter`.

---

## Two Ways to Hand Over the Password

### Option 1: Sync it with a string property through an attached property

Attach a `BoundPassword` property to the `PasswordBox` and bind it to a `string` on the ViewModel.
When the user types, `PasswordChanged` updates `BoundPassword`; when the ViewModel changes the value, the attached property updates `Password`.

Depending on how this approach is written, two failures appeared.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-attached-property.en.md %}

How the attached property is written, and the result of typing. Measured on .NET 10 / Windows 11. "abc" was typed one character at a time.
{: .table-caption}

The first one subscribes to `PasswordChanged` inside the change callback of `BoundPassword`.
A change callback runs when the effective value of the property changes ([PropertyChangedCallback](https://learn.microsoft.com/dotnet/api/system.windows.propertychangedcallback)).
When the ViewModel starts with an empty string, the value equals the attached property's default (also an empty string), so the effective value does not change and, by that rule, the callback is not called.
In the measurement, the ViewModel's `Password` indeed stayed empty after typing.

The second one sets `Password` again in the change callback without comparing the values.
Every keystroke updates `BoundPassword` through `PasswordChanged`, and its change callback then sets the same value on `Password` again.
After the same value is set on `Password` again from code, the next typed character goes to the start (the two "no attached property" rows: without setting it the result is `"abc"`; setting it again after "ab" and then typing "c" gives `"cab"`).
Because this happens on every keystroke, typing "abc" produced `"cba"`.

The article's implementation subscribes through a separate `Attach` property and does not set `Password` when the value is already the same.

```csharp
using System.Windows;
using System.Windows.Controls;

public static class PasswordBoxAssist
{
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached("Attach", typeof(bool), typeof(PasswordBoxAssist),
            new PropertyMetadata(false, OnAttachChanged));

    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxAssist),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static bool GetAttach(DependencyObject d) => (bool)d.GetValue(AttachProperty);
    public static void SetAttach(DependencyObject d, bool value) => d.SetValue(AttachProperty, value);
    public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        box.PasswordChanged -= OnPasswordChanged;
        if ((bool)e.NewValue) box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        string value = (string?)e.NewValue ?? string.Empty;

        // Skip setting Password when it already equals the value written back by typing;
        // after Password is set again, the next typed character goes to the start.
        if (box.Password != value) box.Password = value;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}
```

The change callback of `Attach` runs when the value goes from `false` to `true`, so the event is subscribed whatever the ViewModel's initial value is.
`BoundPassword` is registered with `BindsTwoWayByDefault`, so the input flows back to the ViewModel without writing `Mode=TwoWay` in XAML.

The ViewModel holds the password as a `string` property.
It calls `RaiseCanExecuteChanged` on every change so that the button can be pressed only when the password is not empty.

```csharp
public sealed class LoginViewModel : ObservableObject
{
    private string _password = string.Empty;

    public LoginViewModel() => SignInCommand = new RelayCommand(SignIn, () => Password.Length > 0);

    public RelayCommand SignInCommand { get; }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value)) SignInCommand.RaiseCanExecuteChanged();
        }
    }

    private void SignIn()
    {
        // Authenticate here, then clear the value.
        Password = string.Empty;
    }
}
```

In XAML, set both `Attach` and `BoundPassword`.
`local` maps to the namespace that defines `PasswordBoxAssist`.

```xml
<StackPanel>
  <PasswordBox local:PasswordBoxAssist.Attach="True"
               local:PasswordBoxAssist.BoundPassword="{Binding Password}" />
  <Button Content="Sign in" Command="{Binding SignInCommand}" />
</StackPanel>
```

With this implementation, "abc" reached the ViewModel unchanged as `"abc"`, and setting `Password = ""` on the ViewModel also emptied the `PasswordBox` (the "article's attached property" row and the row after it).
The button was disabled before typing and became enabled after it (last row of the table).

### Option 2: Pass the PasswordBox as a CommandParameter and read SecurePassword when the command runs

This approach keeps the password out of the ViewModel and receives it only when the button is pressed.
Here too, binding `SecurePassword` or `Password` did not work as expected.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-command-parameter.en.md %}

How `CommandParameter` is written, and what the command received. Measured on .NET 10 / Windows 11. "abc" was typed one character at a time.
{: .table-caption}

With `{Binding SecurePassword, ElementName=PasswordInput}` or `{Binding Password, ElementName=PasswordInput}` as the `CommandParameter`, the value passed to the command had a length of 0 even after typing "abc".
The value from before the input, an empty one, was passed as is.
`PasswordBox` does have a `PasswordChanged` event, but the binding did not read the value again after the input.

Passing the `PasswordBox` itself and reading `SecurePassword` when the command runs gave the typed length, 3.

```xml
<StackPanel>
  <PasswordBox x:Name="PasswordInput" />
  <Button Content="Sign in"
          Command="{Binding SignInCommand}"
          CommandParameter="{Binding ElementName=PasswordInput}" />
</StackPanel>
```

The ViewModel exposes a command that takes the `PasswordBox` and handles the `SecureString` only inside the execution.
`SecurePassword` returns a new copy on every read (confirmed on the demo page), so the copy is disposed with `using`.

```csharp
using System.Security;
using System.Windows.Controls;

public sealed class SignInViewModel
{
    public SignInViewModel() => SignInCommand = new RelayCommand<PasswordBox>(SignIn);

    public RelayCommand<PasswordBox> SignInCommand { get; }

    private void SignIn(PasswordBox box)
    {
        using SecureString password = box.SecurePassword;

        // Pass password to the authentication code here.
    }
}
```

With this approach, the ViewModel does not know when the input changes.
Even when the same `RelayCommand<PasswordBox>` gets a `CanExecute` that checks `SecurePassword.Length > 0`, the button stayed disabled both before and after typing (last row of the table).
The `CommandParameter` had resolved to the `PasswordBox`, and the button became enabled once the ViewModel called `RaiseCanExecuteChanged`.
Nothing raises `CanExecuteChanged` when the user types, so the button keeps the state it had before the input.
For commands wired to `CommandManager.RequerySuggested`, the [related article](/articles/wpf-relaycommand-canexecute-not-updating/) measured that typing into a `TextBox` causes a requery.
With such a command and a `PasswordBox`, the result may differ from the one in this article (the button not following the input); that combination was not measured here.

---

## Decision Criteria

- **Whether the button's enabled state must follow the input.** Disabling the button while the password is empty requires the ViewModel to learn about every change.
- **Whether the ViewModel must be able to clear the value.** An example is emptying the field after a failed sign-in.
- **Whether the plaintext password may remain in process memory.** A `string` is immutable and cannot be erased by code after use (String versus SecureString in [SecureString](https://learn.microsoft.com/dotnet/api/system.security.securestring)).
- **Whether the ViewModel may depend on a WPF control type.** The Option 2 ViewModel takes a `PasswordBox` argument, so even a unit test has to create a `PasswordBox`.

---

## Results by Criterion

Whether the plaintext remains in memory was checked by reading all readable committed memory of the process and searching for the password's UTF-16 sequence.
A new random password was generated for each case, and the scanning code held it only in masked form and compared it one character at a time, so that the scanner itself never built the sequence.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-plaintext-in-memory.en.md %}

Whether the UTF-16 sequence of a 12-character password was found in process memory after each operation. Measured on .NET 10 / Windows 11 (64-bit). A new random password was generated for each case, and typed one character at a time with `TextComposition`. Each row confirmed that all 12 characters arrived (rows with a ViewModel by the length of its string before the operation, the others by the length of `SecurePassword` after the scan). The scan covered regions that `VirtualQuery` reported as committed and readable (excluding no-access and guard pages), and searched only for contiguous sequences aligned to 2-byte boundaries. Ranges that failed to read were skipped. ANSI and split fragments were not searched for.
{: .table-caption}

Typing into a `PasswordBox`, or only reading `SecurePassword`, left no such sequence.
Copying the `SecureString` to unmanaged memory with `Marshal.SecureStringToGlobalAllocUnicode` left the sequence there right after the copy, and none after `ZeroFreeGlobalAllocUnicode` had cleared it.
Finding it right after the copy shows that the scan reads unmanaged memory as well.

Reading `Password` once, on the other hand, left the sequence in memory even after the reference was dropped.
It was also found after Option 1 synced the password to a ViewModel `string`.
It was still found after the synced box was emptied with `PasswordBox.Clear()` (which also sets the ViewModel to an empty string) and `GC.Collect` was run.

| Criterion | Option 1: sync with a string via an attached property | Option 2: pass the PasswordBox as a CommandParameter |
|---|---|---|
| Button enabled state | Followed the input (last row of the attached-property table) | Did not follow with the article's `RelayCommand` (last row of the CommandParameter table) |
| Clearing the value from the ViewModel | `Password = ""` also emptied the `PasswordBox` | The ViewModel holds no value |
| Plaintext sequence left in memory | Left once synced, and still left after clearing and GC | Not found when only `SecurePassword` is read |
| Type the ViewModel depends on | `string` only | `PasswordBox` (WPF) |

Choose Option 1 when, with the article's `RelayCommand`, the button must be enabled by the input, or when the ViewModel must be able to clear the value.
In exchange, a plaintext `string` stays in the process from the moment the user types.

Choose Option 2 to reduce how often the plaintext becomes a `string`.
With the article's `RelayCommand`, the design then does not check the input in `CanExecute` and rejects an empty password inside the command.

---

## Notes

- **Option 2's advantage lasts only until the password becomes a `string`.** If the authentication API takes a `string`, a string has to be created for it, and the plaintext sequence appears just as in the "read Password once" row of the memory table. Even with an API that accepts a `SecureString` (such as the `NetworkCredential` constructor that takes one), whether it is converted to a `string` downstream has to be checked for each API.
- **`SecureString` is not recommended for new development.** The documentation recommends against using it for new development on .NET (Core) or when migrating from .NET Framework, and suggests handling passwords through an opaque handle to credentials stored outside the process. It also states that the contents are not encrypted on non-Windows platforms, and that the strings may still be exposed through a process dump or a swap file (Remarks of [SecureString](https://learn.microsoft.com/dotnet/api/system.security.securestring): the Important note at the top and How secure is SecureString?). Option 2 only reduces the chances of a plaintext `string` appearing in memory.
- **The memory measurement in this article checks only whether the whole sequence remains.** Not finding it does not show that no fragments or other encodings remain.
- **The comparison in Option 1 (`box.Password != value`) also reads `Password`.** A `string` is created on every sync. Option 1 is a method that assumes a plaintext `string` is created.
- **Do not bind `SecurePassword` or `Password` to `CommandParameter`.** With either, the command receives the value from before the input.

---

## Summary

`PasswordBox.Password` is not a dependency property, so binding it in XAML throws `XamlParseException`.
`PasswordBox` is `sealed`, so a mechanism has to be added from outside; this article compared an attached property and a `CommandParameter` for that purpose.

Use the Option 1 attached property on screens where the ViewModel must empty the field, or where the button's enabled state must follow the input with a command whose `CanExecuteChanged` is raised by the ViewModel.
Subscribe through a dedicated `Attach` property, and set `Password` only when the value differs.
On screens that should reduce how often a plaintext `string` is created, use Option 2: pass the `PasswordBox` and read and dispose of `SecurePassword` inside the command.
With either option, the plaintext reaches memory once the authentication API requires a `string`, so when keeping the password out of memory is a hard requirement, consider handling it as credentials stored outside the process.

---

## Related Articles

- [Binding to a WPF UserControl's Own Dependency Property from Inside the Control](/articles/wpf-usercontrol-dependencyproperty-binding-not-working/)
- [Fixing a RelayCommand Whose CanExecute Does Not Update the Button State in WPF](/articles/wpf-relaycommand-canexecute-not-updating/)
- [Reading WPF Binding Errors and Diagnosing Them with the Output Window](/articles/wpf-binding-error-debugging-output-window/)
