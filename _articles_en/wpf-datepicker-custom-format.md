---
layout: article-en
title: "Customising the DatePicker Display Format in WPF"
date: 2026-04-15
category: WPF
excerpt: "A practical guide to changing the date display format of WPF DatePicker from both XAML and code-behind."
---

## Overview

By default, the WPF `DatePicker` renders selected dates using the system locale format (e.g.  
`4/15/2026` on en-US).  
This behavior is inconvenient when an application must present dates in a fixed layout regardless of the machine's regional settings, such as `yyyy/MM/dd` for logs or `dd MMM yyyy` for reports.  
This article shows how to customise that format so the control always renders dates in the format the application requires, and compares the trade-offs of each approach.  

## Prerequisites / Environment

- Framework / Language: .NET 6 or later / C# 10  
- Target control: WPF `DatePicker` (`System.Windows.Controls`)  
- Architecture: applicable to both code-behind and MVVM  

The techniques below rely on the default `DatePicker` control template, which contains a `DatePickerTextBox` in its visual tree.  
A fully retemplated `DatePicker` may not expose that element, in which case the code-behind or converter approaches are safer choices.  

## Setting the Format in XAML

`DatePicker` exposes a `SelectedDateFormat` property with two values: `Short` (default) and `Long`.  
For full control you need to reach into the control template's `DatePickerTextBox` via a style:

```xml
<DatePicker x:Name="datePicker" SelectedDate="{Binding SelectedDate}">
  <DatePicker.Resources>
    <Style TargetType="DatePickerTextBox">
      <Setter Property="Text"
              Value="{Binding SelectedDate,
                              RelativeSource={RelativeSource AncestorType=DatePicker},
                              StringFormat='yyyy/MM/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

## Setting the Format in Code-Behind

If you prefer code-behind, subscribe to the `SelectedDateChanged` event and format the text manually:

```csharp
private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
{
    if (datePicker.SelectedDate.HasValue)
    {
        datePicker.Text = datePicker.SelectedDate.Value.ToString("yyyy/MM/dd");
    }
}
```

## Using a Converter

A cleaner MVVM approach uses a value converter:

```csharp
public class DateFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime d ? d.ToString("yyyy/MM/dd") : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DateTime.TryParse(value as string, out var d) ? d : DependencyProperty.UnsetValue;
}
```

Bind with:

```xml
<DatePicker SelectedDate="{Binding SelectedDate,
                           Converter={StaticResource DateFormatConverter}}" />
```

The converter centralises the format string in one place, so a single edit changes every date-displaying control in the application.  

## Common Format Strings

The format string passed to `ToString` or `StringFormat` follows the standard .NET custom date and time specifiers:

| Pattern            | Example output     | Notes                                |
| ------------------ | ------------------ | ------------------------------------ |
| `yyyy/MM/dd`       | `2026/04/15`       | Zero-padded, culture-independent     |
| `dd MMM yyyy`      | `15 Apr 2026`      | `MMM` depends on the culture         |
| `MMMM d, yyyy`     | `April 15, 2026`   | `MMMM` uses the full month name      |
| `yyyy/MM/dd HH:mm` | `2026/04/15 09:30` | Combines date and time in one string |

Any character that is not a format specifier is treated as a literal and preserved verbatim, which is how separators such as `/`, `-`, or a comma are inserted.  
For culture-sensitive output, pass an explicit `CultureInfo` to `ToString`; otherwise the current UI culture is used, which may differ between machines.  

## Notes

- The XAML `StringFormat` approach only affects the displayed text. The underlying `SelectedDate` value is unchanged, so bindings that read the date directly are unaffected.  
- The `SelectedDateChanged` approach overwrites the `Text` property manually. If the user then types into the box, two-way parsing must still succeed, so avoid formats the parser cannot round-trip.  
- Converters that return `string.Empty` for a null date prevent a `NullReferenceException` when no date is selected.  

## Summary

| Method               | Pros                    | Cons                           |
| -------------------- | ----------------------- | ------------------------------ |
| Style + StringFormat | Declarative, no code    | Limited to StringFormat syntax |
| SelectedDateChanged  | Simple, explicit        | Code-behind coupling           |
| Value Converter      | MVVM-friendly, reusable | Requires extra class           |

Choose the approach that fits your project's architecture.  
For new MVVM projects the converter approach scales best as the number of date-displaying controls grows.  
