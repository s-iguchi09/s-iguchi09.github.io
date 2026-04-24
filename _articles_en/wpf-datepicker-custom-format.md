---
layout: article-en
title: "Customising the DatePicker Display Format in WPF"
date: 2026-04-15
category: WPF
excerpt: "A practical guide to changing the date display format of WPF DatePicker from both XAML and code-behind."
---

## Overview

By default, the WPF `DatePicker` renders selected dates using the system locale format (e.g. `4/15/2026` on en-US). This article shows how to customise that format so the control always renders dates in the format your application requires.

## Setting the Format in XAML

`DatePicker` exposes a `SelectedDateFormat` property with two values: `Short` (default) and `Long`. For full control you need to reach into the control template's `DatePickerTextBox` via a style:

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

## Summary

| Method | Pros | Cons |
|---|---|---|
| Style + StringFormat | Declarative, no code | Limited to StringFormat syntax |
| SelectedDateChanged | Simple, explicit | Code-behind coupling |
| Value Converter | MVVM-friendly, reusable | Requires extra class |

Choose the approach that fits your project's architecture. For new MVVM projects the converter approach scales best as the number of date-displaying controls grows.
