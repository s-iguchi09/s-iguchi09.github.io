---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/datepicker.html
title: "DatePicker"
badge: "Inputs"
lead: "DatePicker combines a text box for typing a date with a button that opens a calendar popup. The selected date is a nullable <code>DateTime</code>."
description: "WPF DatePicker measured on .NET 10: DisplayDateStart and End do not stop typed dates, bad input reverts, and the style sets defaults like IsTodayHighlighted."
---

## Overview

The value of a **DatePicker** is `SelectedDate`, a `DateTime?` that is `null` when no date is chosen and binds two-way by default. The text box shows it as text, and typing a date and pressing Enter sets it. The calendar popup is a `Calendar` control. The DatePicker passes several of its properties to it, such as `FirstDayOfWeek` and `IsTodayHighlighted`.

Two things surprise people. First, `DisplayDateStart` and `DisplayDateEnd` only limit the calendar: out-of-range days are disabled in the popup, but a date typed into the text box or set from code is accepted even when it is outside the range. Second, two defaults come from the default style and differ from the property metadata. `IsTodayHighlighted` is `False` in the metadata and `True` in practice, and `SelectedDateFormat` is `Long` in the metadata and `Short` in practice.

The demo app has a section for each property below. The "Show Code" link under each section displays its XAML. Custom display formats are covered in [Customising the DatePicker Display Format in WPF](/articles/wpf-datepicker-custom-format/).

## Screen Preview

![datepicker demo screen](/images/wpf-standard-control-demo/datepicker.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)). Dates were parsed and displayed with `xml:lang="en-US"`.

| Property | Values | Description |
| --- | --- | --- |
| `SelectedDate` | `DateTime?` | The selected date, `null` when none is chosen; it binds two-way by default. Clearing the text box and pressing Enter set it to `null`. Bind it to a `DateTime?` property. Bound to a `DateTime` property, setting it to `null` produced a binding error: the source kept its old date, and `Validation.HasError` became `True` on the DatePicker. |
| `DisplayDate` | `DateTime` | The month the calendar shows. A new DatePicker had today's date, and setting `SelectedDate` to 2026-04-15 moved `DisplayDate` to the same day. Setting `DisplayDate` afterward did not change `SelectedDate`. The demo app binds one DatePicker's `DisplayDate` to another DatePicker's selection, one month ahead of today. |
| `DisplayDateStart / DisplayDateEnd` | `DateTime?` | The first and last dates the calendar offers. With a range of 2026-04-10 to 2026-04-20, the day button for 04-05 in the popup was disabled. The range is not enforced anywhere else. Setting `SelectedDate` to 04-05 from code, setting `Text` to "4/5/2026", and typing "4/5/2026" followed by Enter all left the date at 04-05, without an exception or a `DateValidationError`. A `DisplayDateStart` later than the selected date does not take effect either: with 04-10 selected, `DisplayDateStart = 04-15` was pulled back to 04-10. The demo app sets the range from last Monday to next Friday. |
| `FirstDayOfWeek` | `DayOfWeek` | The day in the leftmost column of the calendar; the popup's `Calendar` received the same value. A new DatePicker takes it from the thread's current culture: it was `Sunday` for en-US and ja-JP, and `Monday` for de-DE and fr-FR. Set it explicitly only when the calendar should differ from the user's culture. |
| `IsDropDownOpen` | `bool` | Whether the calendar popup is open; it binds two-way by default, so the demo app's check box follows the popup as well as controlling it. Setting it to `True` before the window was shown raised no exception, and the popup was open once the window appeared. |
| `IsTodayHighlighted` | `bool` | Whether today's date is marked in the calendar. The property's metadata default is `False`, but the default style sets `True`, so today is marked unless you turn it off. With `False`, the day button's display state changed from `Today` to `RegularDay` and its today marker became transparent. The button stayed enabled, so today can still be selected. |
| `SelectedDateFormat` | `Short / Long` | Whether the text box shows the date in the culture's short or long pattern. Only these two values exist; the effective default is `Short`, set by the default style. The output of each value in several cultures, and how to show another format, are measured in [Customising the DatePicker Display Format in WPF](/articles/wpf-datepicker-custom-format/). The article changes the format through the DatePicker's text part, because `SelectedDate` itself holds a `DateTime`, not text. |
| `Text` | `string` | The date as text. Unlike `SelectedDate`, it does not bind two-way by default. The demo app binds it one-way from a text box. Setting it is parsed immediately with the DatePicker's culture: "4/15/2026", "2026-04-15", and "April 15, 2026" all gave 2026-04-15, and `Text` then read "4/15/2026". A string that cannot be parsed ("15/4/2026" in en-US) cleared `SelectedDate` to `null`. Typing into the text box behaves differently: "abc" followed by Enter raised `DateValidationError`, kept the previous date, and put the previous text back. |

## XAML Example

The following XAML is the `DisplayDate Range (Start/End)` section of the demo app (`DatePickerUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `DateTimeAdvancedConverter` is a converter in the demo app that computes last Monday and next Friday from today. `converters` is the prefix for the demo app's own converters:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:sys="clr-namespace:System;assembly=mscorlib"
            xmlns:converters="clr-namespace:WPFStandardControlDemoApp.Common.Converters">
  <DatePicker x:Name="DisplayDateStartSourceDatePicker"
              SelectedDate="{Binding Source={x:Static sys:DateTime.Now},
                                     Converter={converters:DateTimeAdvancedConverter Operation=Previous, TargetDay=Monday},
                                     Mode=OneTime}" />
  <DatePicker x:Name="DisplayDateEndSourceDatePicker"
              SelectedDate="{Binding Source={x:Static sys:DateTime.Now},
                                     Converter={converters:DateTimeAdvancedConverter Operation=Next, TargetDay=Friday},
                                     Mode=OneTime}" />

  <DatePicker x:Name="DisplayDateRangeDatePicker"
              DisplayDateEnd="{Binding SelectedDate, ElementName=DisplayDateEndSourceDatePicker}"
              DisplayDateStart="{Binding SelectedDate, ElementName=DisplayDateStartSourceDatePicker}"
              SelectedDate="{x:Static sys:DateTime.Now}" />
</StackPanel>
```

The third DatePicker already has today selected. Moving the start to a day after today in the first DatePicker therefore does not narrow the range past today, as described under `DisplayDateStart`.

## Common Use Cases

- **Due dates and deadlines:** a single DatePicker bound to a `DateTime?` property, empty until the user picks a date.
- **Date ranges for reports:** a From and a To DatePicker, validated together in the ViewModel.
- **Bookings:** a calendar limited with `DisplayDateStart` / `DisplayDateEnd`, with unavailable days in `BlackoutDates`.

## Tips and Best Practices

- **Validate the range in the ViewModel.** `DisplayDateStart` and `DisplayDateEnd` only disable days in the popup; typed and programmatic dates outside the range are accepted.
- **Use `BlackoutDates` for days that must not be selected.** Unlike the range, blackout dates are enforced: setting `SelectedDate` to one from code threw `ArgumentOutOfRangeException` and kept the previous date, and the popup marked the day as blacked out.
- **Do not rely on `DisplayDateStart` for a From/To pair.** Binding the To picker's `DisplayDateStart` to the From date has no effect while the To picker already holds an earlier date; the start is pulled back to that date. Check the order of the two dates in the ViewModel.
- **Handle `DateValidationError`** to tell the user that typed text was not accepted. Otherwise the text box silently goes back to the previous date.
- **Bind to `DateTime?`.** A non-nullable source rejects the empty state with a binding error.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`DatePickerDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/DatePickerDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Typing was reproduced by setting the text of the DatePicker's text part and sending an Enter key event, and the calendar was checked by opening the popup and reading its day buttons.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/datepicker/datepicker-range-input.svg" alt="Table of DatePicker results with a range of April 10 to 20, 2026: out-of-range dates set from code, through Text, or typed with Enter are accepted, unparsable typed text raises DateValidationError and keeps the previous date, out-of-range day buttons are disabled, blackout dates throw ArgumentOutOfRangeException from code, and a later DisplayDateStart is pulled back to the selected date" width="1226" height="560" loading="lazy">
  <figcaption>Range, blackout dates, and input, with <code>xml:lang="en-US"</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [Customising the DatePicker Display Format in WPF](/articles/wpf-datepicker-custom-format/) — measured output of `SelectedDateFormat` by culture, and how to show a custom format.
- [TextBox](/apps/wpf-standard-control-demo/textbox.html) — the DatePicker's text part is a `DatePickerTextBox`, which derives from TextBox.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View DatePicker source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/DatePickerUsage){: target="_blank" rel="noopener noreferrer"}
