---
layout: article-en
title: "Why WPF Input Limits Such as MaxLength Do Not Apply to Values Set from Code or Bindings"
seo_title: "WPF Input Limits Like MaxLength Ignore Code and Bindings"
date: 2026-09-27
category: WPF
excerpt: "Limits such as MaxLength and DisplayDateStart do not stop values from code or bindings. Three paths are measured on .NET 10, and checks move to the view model."
image: /images/articles/wpf-input-limits-not-applied-to-code/input-limits-by-path.svg
---

## Overview

If a database column holds 50 characters, the `TextBox` gets `MaxLength="50"`.
If deliveries are accepted only within a period, the `DatePicker` gets `DisplayDateStart` and `DisplayDateEnd`.
Input limits on the screen are a common way to keep values within range.
Yet when saved data is loaded, or when values imported from a file flow in through a binding, values beyond these limits appear on the screen as they are and go on to be saved.

This article tries seven common input limits on three paths: user input, assignment from code, and a bound value.
A table shows which path each limit applies to.
It then shows an implementation that moves validation and rounding into the view model, so that values beyond the limits are stopped reliably.
All values in the tables were measured by running the code on .NET 10 / Windows 11.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF
- Tested on: .NET 10 / Windows 11
- Language: C# 10 or later / XAML (the code assumes nullable reference types and implicit usings are enabled)
- Controls: `TextBox` / `PasswordBox` / `DatePicker` / `Slider` / `TabControl` and `TabItem`
- Architecture: MVVM, with input values bound two-way to a view model

---

## Problem

The example is a screen that edits customer details.
The screen has these input limits.

```xml
<TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}"
         MaxLength="50" />
<DatePicker SelectedDate="{Binding DeliveryDate}"
            DisplayDateStart="{Binding FirstDeliveryDate, Mode=OneWay}"
            DisplayDateEnd="{Binding LastDeliveryDate, Mode=OneWay}" />
<Slider Value="{Binding Volume, Mode=TwoWay}"
        Minimum="0" Maximum="100"
        IsSnapToTickEnabled="True" TickFrequency="10" />
```

The intent is a name of at most 50 characters, a delivery date within the period, and a volume from 0 to 100 in steps of 10.
`DisplayDateStart` and `DisplayDateEnd` bind two-way by default, and when the period is held in read-only properties, the default binding throws `InvalidOperationException` when the `DataContext` is set or when the window is shown, whichever comes later (the third table, under Notes).
The bindings that only pass the period from the view model therefore specify `Mode=OneWay`.
As long as the user works with the keyboard and the calendar, the screen mostly behaves as intended.
The 51st character is not accepted, dates outside the period cannot be picked in the calendar, and the slider moves in steps of 10.

The trouble starts when a value arrives by a path other than the user's input.
Typical cases include the following.

- Loading customer data saved by an earlier version that allowed 80 characters
- Setting `DeliveryDate` to a date imported from a CSV file
- Restoring a volume of `23.4` saved in a settings file

Each value appears on the screen as it is, and if the user presses Save, it reaches the save logic still beyond the limit.

---

## The Common Advice

The standard answer to "how do I limit the number of characters" is to set `MaxLength`.
The Microsoft Learn [reference for `TextBox.MaxLength`](https://learn.microsoft.com/dotnet/api/system.windows.controls.textbox.maxlength) lists postal codes and phone numbers, and also keeping the text within the maximum length of the corresponding database column.
Other controls have properties that their references describe as restricting what the user does.
[`Calendar.DisplayDateStart`](https://learn.microsoft.com/dotnet/api/system.windows.controls.calendar.displaydatestart), which the `DatePicker.DisplayDateStart` reference points to, keeps the user from scrolling to or selecting dates outside the range, and [`Slider.IsSnapToTickEnabled`](https://learn.microsoft.com/dotnet/api/system.windows.controls.slider.issnaptotickenabled) moves the thumb to the closest tick mark.
This article takes up the following seven settings, which are easy to write with the intent of keeping values within range.

| What to protect | Setting |
|---|---|
| Maximum number of characters | `TextBox.MaxLength` |
| Uppercase text | `TextBox.CharacterCasing="Upper"` |
| Maximum password length | `PasswordBox.MaxLength` |
| Date range | `DatePicker.DisplayDateStart` / `DisplayDateEnd` |
| Values in fixed steps | `Slider.IsSnapToTickEnabled` with `TickFrequency` |
| Upper bound | `Slider.Maximum` |
| A tab that cannot be selected | `TabItem.IsEnabled="False"` |

As a way to restrict what the user enters, these are correct.
The mistake is to read them as constraints on the property value, and to expect them to protect values that come from code or a binding too.

---

## Why It Does Not Work

Each limit was tried on the three paths.
For user input, keyboard focus was moved to the control, and keys were sent through WPF's `InputManager` and characters through `TextCompositionManager`.
For the calendar, the test read whether the day button outside the range was enabled, and tab selection was tried by calling `Select` through UI Automation, the interface that assistive technologies use.
Values from code and from bindings were set on controls shown in a window.

{% include tables/articles/wpf-input-limits-not-applied-to-code/input-limits-by-path.en.md %}

Measured on .NET 10 / Windows 11. User input is keys and characters sent through InputManager and TextCompositionManager; for the calendar, the enabled state of the day button was read, and for the tab, UI Automation's Select was called. The DatePicker was tried with the en-US culture. The DisplayDateStart afterwards row shows the state after the out-of-range date in the row above came in, and the April 7 button was checked only after setting the date from code. Keys cannot produce a value above the maximum, so the End key in the Slider Maximum row only shows that the value stops at the maximum. The binding in that row specifies Mode=TwoWay.
{: .table-caption}

**Only the `Slider`'s `Maximum` brought values from code and bindings into range on the screen.**
Assignment from code got past six of the seven limits.
Of the six limits that could be bound, which excludes the `PasswordBox`, bound values got past five on the screen and all six in the source.

For `MaxLength`, `CharacterCasing`, and `PasswordBox.MaxLength`, this is by design.
Each is defined as a limit on characters the user enters, not on the property value.
The `MaxLength` reference defines it as the maximum number of characters that can be entered manually, and states that it does not affect characters added programmatically.
The [`CharacterCasing` reference](https://learn.microsoft.com/dotnet/api/system.windows.controls.textbox.charactercasing) has the same note, and the [`PasswordBox.MaxLength` reference](https://learn.microsoft.com/dotnet/api/system.windows.controls.passwordbox.maxlength) states that it has no effect when `Password` is changed from code.

For the `Slider`'s snapping, the reference does not say whether the path makes a difference.
In the measurement, the arrow key snapped to a tick, while `23.4` set from code or a binding stayed `23.4`.

The `DatePicker` restricts only part of the user input.
The day buttons outside the range are disabled, but a date outside the range typed into the text box became the `SelectedDate` as it was.
Furthermore, whichever path brought in the out-of-range date, `DisplayDateStart` dropped to that date.
After `2026-04-05` was set from code, the calendar had the April 7 button, which is outside the range, enabled as well.
The `Calendar.DisplayDateStart` reference also states that setting `SelectedDate` before `DisplayDateStart` sets `DisplayDateStart` to the same value.
Only the start side was measured, but the [`Calendar.DisplayDateEnd` reference](https://learn.microsoft.com/dotnet/api/system.windows.controls.calendar.displaydateend) says the same for dates after the end.
**On a screen that has loaded an out-of-range value, even the calendar does not keep the range.**

A `TabItem` with `IsEnabled="False"` rejects selection through UI Automation with `ElementNotEnabledException`.
Setting `SelectedIndex`, however, selected the disabled tab, and its content (`SelectedContent`) became `Page 2`.
That a click with the real mouse cannot select a disabled tab either is measured on the [TabItem demo page](/apps/wpf-standard-control-demo/tabitem.html).

---

## When the Limits Do Apply

In the table, only the `Slider`'s `Maximum` brought values from code and bindings into range on the screen.
It is not part of input handling; it coerces `Value` itself into the range.
As the table shows, `150` set from code became `100`, and it went back to `150` when `Maximum` was raised to `200`.
The control keeps the value that was set and only constrains the effective value to the range.
This coercion is described in [Dependency property callbacks and validation](https://learn.microsoft.com/dotnet/desktop/wpf/properties/dependency-property-callbacks-and-validation).

In the measurement, however, the coerced value was not written back to the bound source.
Even with `Mode=TwoWay`, the source stayed at `150` while the `Slider` on the screen showed `100`.
**The screen and the view model disagree, and the view model's `150` goes on to the save logic**, so this does not protect the value either.

A control's input limit can be trusted to guarantee a value only when both of the following hold:

- Only the user operating that control writes the value (no code puts a value in, such as loading, importing, or setting a default)
- The control is not one like `DatePicker`, whose limit covers only part of the user input

Few input fields in business applications meet these conditions.
A screen that edits saved values fails the first one by its nature.
The reliable approach is therefore to **validate the range in the view model and keep the control's limits as an aid to input**.

---

## Implementation

### Validating the length in the view model

The length of the name is validated with `INotifyDataErrorInfo`.
Each time `Name` is set, the setter checks the length, records an error if it is too long, and raises `ErrorsChanged`.
Values from code and values from a binding both always pass through this setter.

```csharp
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class CustomerViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    public const int MaxNameLength = 50;

    private string _name = "";
    private readonly List<string> _nameErrors = new();

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
            _nameErrors.Clear();
            if (value.Length > MaxNameLength)
            {
                _nameErrors.Add($"Name must be at most {MaxNameLength} characters.");
            }

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Name)));
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public bool HasErrors => _nameErrors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName == nameof(Name) ? _nameErrors : Array.Empty<string>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

A value that is too long is kept as an error rather than truncated, so that loaded data is not changed silently.
The user decides what to fix, and saving is allowed only while `HasErrors` is `false`.
Changes to `HasErrors` are also raised through `PropertyChanged`, so a Save button whose `IsEnabled` is bound to the inverse of `HasErrors` follows it (third row of the second table).
WPF has no built-in converter that inverts a bool, so a converter like the following is needed.

```csharp
using System.Globalization;
using System.Windows.Data;

public sealed class InvertBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
}
```

The Save button's `IsEnabled` is bound to `HasErrors` through this converter.
`local` is the XAML prefix for the namespace that holds the converter.

```xml
<Window.Resources>
    <local:InvertBooleanConverter x:Key="InvertBooleanConverter" />
</Window.Resources>

<Button Content="Save"
        IsEnabled="{Binding HasErrors, Converter={StaticResource InvertBooleanConverter}}" />
```

The button is disabled while `HasErrors` is `true`, and enabled again once the length is fixed and it becomes `false`.

`MaxLength` stays in the XAML.
The keyboard can no longer enter more than 50 characters, so user input never goes past the limit.
The value should match `CustomerViewModel.MaxNameLength`.

```xml
<TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}"
         MaxLength="50" />
```

The default of [`ValidatesOnNotifyDataErrors`](https://learn.microsoft.com/dotnet/api/system.windows.data.binding.validatesonnotifydataerrors) is `true`.
A binding to a source that implements `INotifyDataErrorInfo` reflects its errors in the `TextBox`'s `Validation.HasError` without setting it.

### Rounding to the range and step in the setter

For some values, such as a volume, it is more natural to round an out-of-range value than to reject it.
In that case, the setter clamps the value to the range and rounds it to the step.
`NaN` cannot be clamped, so it is ignored and the current value is kept.

```csharp
using System.ComponentModel;

public sealed class VolumeViewModel : INotifyPropertyChanged
{
    private double _volume;

    public double Volume
    {
        get => _volume;
        set
        {
            if (double.IsNaN(value))
            {
                return;
            }

            double clamped = Math.Clamp(value, 0, 100);
            _volume = Math.Round(clamped / 10, MidpointRounding.AwayFromZero) * 10;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

`PropertyChanged` is raised with the rounded value, so the `Slider` also shows the rounded value.
The XAML can stay as the `Slider` in "Problem".

### Checking both paths

Both view models were tried with assignment from code and with user input.
For the table, the maximum length of the name was changed to 5.
To check the view model's rule alone, the `Slider` was widened to 0–200, snapping was turned off, and `SmallChange` was set to 5.
The `Slider` itself does not round the value, so if the source and the screen show a rounded value, the view model rounded it.
To see the value the `Slider` sent to the source on each key, the setter was given extra code, for the measurement only, that records the value it received (it is not in the implementation above).

{% include tables/articles/wpf-input-limits-not-applied-to-code/input-limits-viewmodel.en.md %}

Measured on .NET 10 / Windows 11. In the name rows, a view model with a limit of 5 characters is bound to a TextBox with MaxLength 5 using UpdateSourceTrigger=PropertyChanged, without setting ValidatesOnNotifyDataErrors. In the Save button row, IsEnabled is bound to HasErrors through a converter that inverts a bool. The Slider has Minimum 0, Maximum 200, no snapping, and SmallChange 5, and is bound TwoWay to Volume. The sent value is what the setter received before rounding, recorded for the measurement only; the implementation in this article does not record it.
{: .table-caption}

**The rounding applied both to values from code and to values sent by the `Slider`'s key operations.**
`150` and `23.4` became `100` and `20`; the `200` sent by the End key and the `55` sent by the Right arrow key became `100` and `60`; in every case the source and the `Slider` agreed.
The mismatch seen in the first table when relying on the `Slider`'s `Maximum` alone, with the source at `150` and the screen at `100`, did not occur.
A name that was too long, set from code, gave `HasErrors` `True`, and `Validation.HasError` on the `TextBox` was `True` as well.
The Save button followed the changes of `HasErrors`, both for values from code and for the user's edits.

---

## Notes

- **As long as typing starts from a value within the limit, no validation error appears.**
With `MaxLength` kept, typing stops at the limit and a value that is too long never reaches the view model.
As the first row of the second table shows, `HasErrors` stays `False`, so the error display cannot tell the user about the limit.
The limit should be stated in the screen's text.
- **After a value beyond the limit is loaded, characters cannot be added.**
As the second row of the second table shows, typing at the end of a `TextBox` holding text beyond the limit adds nothing, and deleting one character leaves the error in place.
The error message should say that the value cannot be saved until it is cut down to the limit.
- **`PasswordBox.Password` cannot be bound.**
`PasswordBox` has no `PasswordProperty` to bind to.
`MaxLength` does not apply to a `Password` set from code; in the first table all 10 characters remained.
The length should be checked in the code that reads `Password`.
- **The `DatePicker` range should be validated in the view model.**
Out-of-range dates come in through the text box, and once one is in, the calendar's range widens too.
The range should be validated at the source that `SelectedDate` is bound to.
It is written the same way as the length check; only the comparison changes to the date range.
- **A disabled tab can be selected from code.**
On a screen where the view model sets `SelectedIndex`, the view model itself should decide whether the tab can be selected before setting it.
- **The `Slider`'s coercion does not change the source.**
The value on the screen and the value that is saved are not necessarily the same.
The range should be rounded or validated in the view model.
- **A period held in read-only properties needs `Mode=OneWay`.**
The default binding of `DisplayDateStart` and `DisplayDateEnd` is two-way.
Binding either of them to a read-only property threw `InvalidOperationException` in `SetBinding` when the source was set directly.
Through `DataContext`, the same exception was thrown when the `DataContext` of a shown `DatePicker` was set, or, when it was set before showing, while the window was being shown.
Setting the `DataContext` on a parent element before showing, so that the `DatePicker` inherits it, also threw while the window was being shown.
With a settable property, the source was not changed even when an out-of-range date lowered `DisplayDateStart`, and the result was the same as `OneWay`.

The following table shows the results of binding the period.
The default two-way binding and `OneWay` were tried with `DisplayDateStart`.
Read-only properties were tried with both `DisplayDateStart` and `DisplayDateEnd` in four ways: setting the source directly, setting the `DataContext` while shown, setting the `DataContext` before showing, and setting the `DataContext` on a parent element before showing.

{% include tables/articles/wpf-input-limits-not-applied-to-code/input-limits-displaydate-binding.en.md %}

Measured on .NET 10 / Windows 11. The first two rows are the values after SelectedDate is set to 2026-04-05 from code. The value after the source is set to 04-12 is DisplayDateStart after SelectedDate is reset to null and the source is set to 2026-04-12. "read-only property" means the exception message named the read-only property as the reason. In the parent's DataContext rows, the DataContext was set on a parent element and inherited by the DatePicker; in the other rows, it was set on the DatePicker itself.
{: .table-caption}

---

## Summary

`TextBox.MaxLength`, `CharacterCasing`, `PasswordBox.MaxLength`, `DisplayDateStart`, `IsSnapToTickEnabled`, and `TabItem.IsEnabled` all restrict what the user does; none of them constrains the property value itself.
Assignment from code gets past all of them, bound values get past the ones that can be bound, and the `DatePicker` does not even stop input from its text box.
The `Slider`'s `Maximum` coercion, the only one in the table that brought values from code into range on the screen, was not written back to the bound source.

The responsibility for a value's range is split as follows.

- **Guaranteeing the value:**
Validation in the view model (`INotifyDataErrorInfo`) or rounding in the setter.
This applies to values from any path: code, a binding, or user input.
- **Helping the user enter it:**
Control limits such as `MaxLength` stay, so that the user cannot type a value out of range.
The limit should match the view model's constant.
- **When the control alone is enough:**
Only input fields where the user of that control is the only writer, no code puts a value in, and the control is not one like `DatePicker`, whose limit covers only part of the user input.

---

<!-- Related articles -->
- [Why WPF Validation Errors Are Not Displayed, and Choosing Between IDataErrorInfo and INotifyDataErrorInfo](/articles/wpf-validation-error-not-displayed/)
- [TextBox (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/textbox.html): measures that `MaxLength` and `CharacterCasing` act only on typing
- [PasswordBox (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/passwordbox.html): measures that `Password` cannot be bound and that `MaxLength` limits only typing
- [DatePicker (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/datepicker.html): measures that a date outside the display range is accepted, typed or set from code
- [Slider (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/slider.html): measures snapping to ticks and coercion into the range
- [TabItem (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/tabitem.html): measures that a disabled tab can still be selected from code
