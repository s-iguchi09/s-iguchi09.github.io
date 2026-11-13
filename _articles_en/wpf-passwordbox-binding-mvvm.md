---
layout: article-en
title: "Why WPF PasswordBox.Password Cannot Be Bound, and Two Ways to Pass It to a ViewModel"
seo_title: "WPF PasswordBox.Password Binding: Two Ways for MVVM"
date: 2026-11-13
category: WPF
image: /images/articles/wpf-passwordbox-binding-mvvm/passwordbox-plaintext-in-memory.svg
excerpt: "PasswordBox.Password is not a dependency property, so binding it in XAML throws. Two MVVM workarounds measured: caret jumps, button state, leftover plaintext."
---

## Overview

When a sign-in screen is built with MVVM, it is natural to bind `PasswordBox.Password` to a `string` on the ViewModel, the same way a `TextBox` is bound.
XAML written as `Password="{Binding Password}"`, however, throws while it is being loaded.
The attached properties that circulate as the workaround are not all correct either: some never deliver the input to the ViewModel, and one reverses the order of the typed characters.

This article confirms the cause from the exception message, then implements two ways of handing the password to a ViewModel: syncing it with a `string` through an attached property, and passing the `PasswordBox` as a `CommandParameter`.
The two differ in whether the button's enabled state can follow the input and whether the plaintext password stays in process memory.
The second point was checked by actually scanning the process memory.

---

## Prerequisites and Environment

- Framework: WPF on .NET 6 or later
- Architecture: MVVM (a minimal hand-written ViewModel base class and `RelayCommand`)
- Verified on: .NET 10 / Windows 11
- Input: typed into the `PasswordBox` one character at a time with `TextComposition`, the same path as keyboard input

---

## Problem

The following XAML is meant to show the ViewModel's `Password` property in a `PasswordBox` and send the input back to the ViewModel.

```xml
<PasswordBox Password="{Binding Password}" />
```

This XAML throws `XamlParseException` while it is loaded, before the window is shown.
The message says that a binding cannot be set because `Password` is not a dependency property.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-binding-error.en.md %}

The exception raised by binding `Password`, and properties of the `PasswordBox` type. Measured on .NET 10 / Windows 11. The message was read with the UI culture set to en-US.
{: .table-caption}

---

## Cause

A `Binding` can be set only on a dependency property of a `DependencyObject`.
`PasswordBox` has neither a `PasswordProperty` nor a `SecurePasswordProperty`, so `Password` and `SecurePassword` are plain CLR properties (third row of the table).
`PasswordBox` does not implement `INotifyPropertyChanged` either.
`Password` therefore cannot be a binding target, and the only way the control reports a change is the `PasswordChanged` event.

The official documentation does not say why `Password` was not made a dependency property.
What it does say is that reading `Password` exposes the password as plain text in memory, and that `SecurePassword` returns it as a `SecureString` to avoid that ([PasswordBox.Password](https://learn.microsoft.com/dotnet/api/system.windows.controls.passwordbox.password)).
Internally, the password is held in a `SecureString` field rather than a `string` (confirmed in the same environment on the [PasswordBox demo page](/apps/wpf-standard-control-demo/passwordbox.html)).

`PasswordBox` is `sealed`, so a derived class cannot add a dependency property.
Passing the value to a ViewModel means adding a mechanism from outside the control.

---

## Two Ways to Hand Over the Password

### Option 1: Sync it with a string property through an attached property

Attach a `BoundPassword` property to the `PasswordBox` and bind it to a `string` on the ViewModel.
When the user types, `PasswordChanged` updates `BoundPassword`; when the ViewModel changes the value, the attached property updates `Password`.

This approach is widely published, but two of the common ways of writing it failed.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-attached-property.en.md %}

How the attached property is written, and the result of typing. Measured on .NET 10 / Windows 11. "abc" was typed one character at a time.
{: .table-caption}

The first one subscribes to `PasswordChanged` inside the change callback of `BoundPassword`.
When the ViewModel starts with an empty string, the value equals the attached property's default (also an empty string), so the change callback is never called.
The event is never subscribed, and the ViewModel's `Password` stayed empty after typing.

The second one sets `Password` again in the change callback without comparing the values.
Every keystroke writes the value back to the ViewModel, and the change callback then sets the same value on `Password`, which moves the caret to the start.
Typing "abc" produced `"cba"`.

The article's implementation subscribes through a separate `Attach` property and does not set `Password` when the value is already the same.

```csharp
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

        // Do not set the value written back by typing; setting it again moves the caret to the start.
        if (box.Password != value) box.Password = value;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}
```

The change callback of `Attach` always runs when the value goes from `false` to `true`, so the event is subscribed whatever the ViewModel's initial value is.
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

With this implementation, "abc" reached the ViewModel unchanged as `"abc"`, and setting `Password = ""` on the ViewModel also emptied the `PasswordBox` (the "article's attached property" rows of the table).
The button was disabled before typing and became enabled after it.

### Option 2: Pass the PasswordBox as a CommandParameter and read SecurePassword when the command runs

This approach keeps the password out of the ViewModel and receives it only when the button is pressed.
Here too, one of the commonly published forms did not work as expected.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-command-parameter.en.md %}

How `CommandParameter` is written, and what the command received. Measured on .NET 10 / Windows 11. "abc" was typed one character at a time.
{: .table-caption}

With `CommandParameter="{Binding SecurePassword, ElementName=PasswordInput}"`, the `SecureString` passed to the command had a length of 0 even after typing "abc".
`SecurePassword` does not report changes either (third row of the table in the Cause section), so the binding reads the value at load time, an empty `SecureString`, and never reads it again.

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
Even with a `CanExecute` that checks `SecurePassword.Length > 0`, the button stayed disabled both before and after typing (last row of the table).
The article's `RelayCommand` raises `CanExecuteChanged` through its own event, so `CanExecute` is not evaluated again unless the ViewModel raises it.
A command wired to `CommandManager.RequerySuggested` may behave differently; that case was not measured here.

---

## Decision Criteria

- **Should the button's enabled state follow the input?** Disabling the button while the password is empty requires the ViewModel to learn about every change.
- **May the plaintext password remain in process memory?** A `string` is immutable and cannot be erased by code after use (String versus SecureString in the [SecureString documentation](https://learn.microsoft.com/dotnet/api/system.security.securestring)).
- **May the ViewModel depend on a WPF control type?** The Option 2 ViewModel takes a `PasswordBox` argument, so even a unit test has to create a `PasswordBox`.

The second criterion was checked by reading all committed memory of the process and searching for the password's UTF-16 sequence.
A new random password was generated for each case, and the scanning code held it only in masked form and compared it one character at a time, so that the scanner itself never built the sequence.

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-plaintext-in-memory.en.md %}

Whether the UTF-16 sequence of a 12-character password was found in process memory after each operation. Measured on .NET 10 / Windows 11. A new random password was generated for each case and typed one character at a time with `TextComposition`. Only the contiguous UTF-16 sequence was searched for; other forms (ANSI, split fragments, and so on) were not.
{: .table-caption}

Typing into a `PasswordBox`, or only reading `SecurePassword`, left no such sequence.
Copying the `SecureString` to unmanaged memory with `Marshal.SecureStringToGlobalAllocUnicode` left none either, once `ZeroFreeGlobalAllocUnicode` had cleared it.

Reading `Password` once, on the other hand, left the sequence in memory even after the reference was dropped.
It was also found after Option 1 synced the password to a ViewModel `string`, and still found after the ViewModel was cleared and `GC.Collect` was run.
Clearing the ViewModel property does not erase the plaintext created before it.

---

## Results by Criterion

| Criterion | Option 1: sync with a string via an attached property | Option 2: pass the PasswordBox as a CommandParameter |
|---|---|---|
| Button IsEnabled (before / after typing) | False / True; follows the input | False / False; does not follow |
| Clearing the value from the ViewModel | `Password = ""` also empties the `PasswordBox` | The ViewModel holds no value |
| Plaintext sequence left in memory | Left once synced, and still left after GC | Not found when only `SecurePassword` is read |
| Type the ViewModel depends on | `string` only | `PasswordBox` (WPF) |

Choose Option 1 when the button must be enabled by the input or the ViewModel must be able to clear the value.
In exchange, a plaintext `string` stays in the process from the moment the user types.

Choose Option 2 to reduce how often the plaintext becomes a `string`.
The design then keeps the button always enabled and rejects an empty password inside the command.

---

## Notes

- **Option 2's advantage lasts only until the password becomes a `string`.** If the authentication API takes a `string`, the string created for it is in the same state as the "read Password once" row. Option 2 is worth it only when the password can be passed to an API that accepts a `SecureString` (such as the `NetworkCredential` constructor that takes one), or through unmanaged memory that is cleared right away.
- **`SecureString` is not recommended for new development.** The documentation recommends against using it for new development on .NET (Core) or when migrating from .NET Framework, and suggests handling passwords through an opaque handle to credentials stored outside the process ([SecureString](https://learn.microsoft.com/dotnet/api/system.security.securestring), Remarks). It also states that the contents are not encrypted on non-Windows platforms. Option 2 only reduces exposure in memory; it does not prevent leaks through a dump or a swap file.
- **The memory measurement in this article checks only whether the whole sequence remains.** Not finding it does not show that no fragments or other encodings remain.
- **The comparison in Option 1 (`box.Password != value`) also reads `Password`.** A `string` is created on every sync. Option 1 is a method that assumes a plaintext `string` is created.
- **Do not bind `SecurePassword` or `Password` to `CommandParameter`.** Neither reports changes, so the button is pressed with the value from load time.

---

## Summary

`PasswordBox.Password` is not a dependency property, so binding it in XAML throws `XamlParseException`.
`PasswordBox` is `sealed`, so passing the value to a ViewModel requires an attached property or a `CommandParameter`.

On screens where the button's enabled state must follow the input, use the Option 1 attached property.
Subscribe through a dedicated `Attach` property, and set `Password` only when the value differs.
On screens that should avoid leaving a plaintext `string` in the process, use Option 2: pass the `PasswordBox` and read and dispose of `SecurePassword` inside the command.
With either option, the plaintext reaches memory once the authentication API requires a `string`, so when keeping the password out of memory is a hard requirement, consider handling it as credentials stored outside the process.

---

## Related Articles

- [Binding to a WPF UserControl's Own Dependency Property from Inside the Control](/articles/wpf-usercontrol-dependencyproperty-binding-not-working/)
- [Fixing a RelayCommand Whose CanExecute Does Not Update the Button State in WPF](/articles/wpf-relaycommand-canexecute-not-updating/)
- [Reading WPF Binding Errors and Diagnosing Them with the Output Window](/articles/wpf-binding-error-debugging-output-window/)
