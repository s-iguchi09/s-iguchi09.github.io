---
layout: article-en
title: "Customising the DatePicker Display Format in WPF"
date: 2026-04-15
category: WPF
excerpt: "How to fix the WPF DatePicker format with a DatePickerTextBox style, escape separators in XAML, why setting Text in code-behind fails, and when a converter fits."
image: /images/articles/wpf-datepicker-custom-format/datepicker-default-vs-custom-format.png
---

## Overview

When neither the control nor any of its parents sets `Language` (`xml:lang` in XAML), the WPF `DatePicker` renders selected dates in the format of the thread's `CurrentCulture`, which defaults to the system's regional settings (e.g. `4/15/2026` on en-US).  
This behavior is inconvenient when an application must present dates in a fixed layout regardless of the machine's regional settings, such as `yyyy/MM/dd` for logs or `dd MMM yyyy` for reports.  
This article shows how to customise that format so the control always renders dates in the format the application requires, and compares the trade-offs of each approach.  

## Prerequisites / Environment

- Framework / Language: .NET 6 or later / C# 10  
- Target control: WPF `DatePicker` (`System.Windows.Controls`)  
- Architecture: applicable to both code-behind and MVVM  
- Verification environment: .NET 10 / Windows 11 (Japanese regional settings)

The figures in this article come from varying the `DatePicker` settings in the environment above and reading the string shown in its text part.
The following points were confirmed in that environment:

- `SelectedDateFormat` offers only `Short` and `Long`, so it cannot produce an arbitrary format.
- `SelectedDateFormat` defaults to `Short`, and that default comes from the default style rather than from the dependency property metadata.
- Rewriting the text part inside the template does produce an arbitrary format.

The techniques below rely on the default `DatePicker` control template, which contains a `DatePickerTextBox` in its visual tree.  
A fully retemplated `DatePicker` may not expose that element, in which case the format has to be handled inside the custom template; setting `Text` from code-behind does not stick, as measured below. The converter shown later formats companion displays, not the picker itself.  

The text a `DatePicker` actually shows can be confirmed by varying the setting and reading it back.

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-format-matrix.svg" alt="A table of the effective SelectedDateFormat, where that value comes from, and the text shown by a DatePicker per setting. Setting nothing yields Short, sourced from the default style. The default and Short both give 2026/07/17, Long gives a long-form Japanese date, and overwriting the text part gives 2026/07/17 with the weekday appended." width="642" height="200" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 with Japanese regional settings, reading the text part of a <code>DatePicker</code> holding <code>2026-07-17</code>. The parenthesis in the second column is where the value came from, read through <code>DependencyPropertyHelper.GetValueSource</code>.</figcaption>
</figure>

**`SelectedDateFormat` offers only `Short` and `Long`.** Neither can state an arbitrary format.
That is why the workarounds described below are needed.

Setting nothing yields `Short`, but that is not the default value registered in the dependency property metadata.
The metadata default is `Long`; `Short` comes from the default style. The `DefaultStyle` in the second column of the table is that source.

The last row overwrites the text part inside the template directly. A format carrying the weekday, which neither built-in form can express, is reachable that way.

---

## Setting the Format in XAML

`DatePicker` exposes a `SelectedDateFormat` property with two values: `Short` (default) and `Long`.  
For full control, the control template's `DatePickerTextBox` must be targeted through a style:

```xml
<DatePicker x:Name="datePicker" SelectedDate="{Binding SelectedDate}">
  <DatePicker.Resources>
    <Style TargetType="DatePickerTextBox">
      <Setter Property="Text"
              Value="{Binding SelectedDate,
                              RelativeSource={RelativeSource AncestorType=DatePicker},
                              StringFormat='yyyy\\/MM\\/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

Unescaped, `/` is a date-separator placeholder that the binding's culture can replace with another character, so the separators have to be escaped to render literally. Inside a markup extension, however, the backslash is XAML's own escape character: `\/` alone is consumed by the XAML parser, the format becomes `yyyy/MM/dd`, and a `de-DE` picker shows `2026.04.15`. Doubling the backslash, as above, leaves `\/` in the format string. Quoting the separators also works if the quotes themselves are escaped, as in `StringFormat=yyyy\'/\'MM\'/\'dd`; written as `yyyy'/'MM'/'dd` without escaping, the XAML failed to load. The table after the next figure shows all four.  

Where the default display takes its culture from depends on where the value of `Language` (`xml:lang` in XAML) comes from.  
When neither the control nor its parents set it, so that its value source is still `Default`, the display follows the thread's `CurrentCulture`; the default `Language` value of `en-US` is not used.  
When it is set on the control itself (`Local`) or inherited from a parent such as the `Window` (`Inherited`), the display uses that language regardless of `CurrentCulture`.  

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-culture-matrix.svg" alt="A table of the default DatePicker display measured while varying xml:lang on the control, xml:lang on its parent, and CurrentCulture independently. With no xml:lang anywhere the display follows CurrentCulture: 2026/04/15 for ja-JP and 4/15/2026 for en-US. With xml:lang on the control the display ignores CurrentCulture: 4/15/2026 for en-US and 15.04.2026 for de-DE. With de-DE on the parent only, the inherited language gives 15.04.2026." width="725" height="320" loading="lazy">
  <figcaption>Default display of a <code>DatePicker</code> whose <code>SelectedDate</code> is 2026-04-15, measured on .NET 10 / Windows 11. In rows with no <code>xml:lang</code> anywhere the value source of <code>Language</code> is <code>Default</code> and the display follows <code>CurrentCulture</code>. In rows that set it on the control (<code>Local</code>) or inherit it from the parent (<code>Inherited</code>), changing <code>CurrentCulture</code> does not change the display.</figcaption>
</figure>

Placing the two side by side gives the following.  
Both carry `xml:lang="en-US"` so that the comparison does not depend on the machine's regional settings.  

```xml
<!-- Default display -->
<DatePicker xml:lang="en-US" SelectedDate="2026-04-15" Width="190" />

<!-- With the style above applied -->
<DatePicker xml:lang="en-US" SelectedDate="2026-04-15" Width="190">
  <DatePicker.Resources>
    <Style TargetType="DatePickerTextBox">
      <Setter Property="Text"
              Value="{Binding SelectedDate,
                              RelativeSource={RelativeSource AncestorType=DatePicker},
                              StringFormat='yyyy\\/MM\\/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-default-vs-custom-format.png" alt="Two DatePicker controls holding the same date. The default one displays 4/15/2026 while the one with StringFormat displays 2026/04/15." width="486" height="146" loading="lazy">
  <figcaption>Two <code>DatePicker</code> controls given the same <code>SelectedDate</code>. Both carry <code>xml:lang="en-US"</code> so that the difference in format is visible. The upper one uses the default display, which follows that setting and renders <code>4/15/2026</code>. The lower one applies the style from this section, which fixes the separators and the year-month-day order.</figcaption>
</figure>

What the escaped separator fixes is the separator and the field order, not the calendar itself: with the separators kept, `th-TH`, `ar-SA`, and `fa-IR` still show their own years and months.

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-stringformat-escape.svg" alt="A table of four ways to write StringFormat inside a XAML Binding. 'yyyy\/MM\/dd' becomes yyyy/MM/dd after parsing and shows 2026/04/15 for en-US and ja-JP but 2026.04.15 for de-DE, with invisible direction marks for ar-SA. 'yyyy\\/MM\\/dd' and yyyy\'/\'MM\'/\'dd keep the slash for all six cultures, while th-TH, ar-SA, and fa-IR still use their own calendars. yyyy'/'MM'/'dd without escaping fails to load with XamlParseException." width="1159" height="200" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 by loading the style from this section with each <code>StringFormat</code> and <code>xml:lang</code>, with <code>SelectedDate</code> set to 2026-04-15. Characters outside ASCII are shown as code points.</figcaption>
</figure>

To pin the calendar as well, set `ConverterCulture` on the binding so the culture itself is fixed.  

## Setting the Format in Code-Behind

A common suggestion is to handle `SelectedDateChanged` and assign the formatted text to `DatePicker.Text`. This does not change the display: `DatePicker` writes its own text from `SelectedDate` after the handler, so the text went back to the default format.

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-codebehind.svg" alt="A table of setting DatePicker.Text in SelectedDateChanged with xml:lang en-US: the handler ran twice, and both Text and the displayed text were 4/15/2026, not the yyyy/MM/dd text the handler set." width="626" height="110" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 with a handler that sets <code>Text</code> to <code>yyyy/MM/dd</code> in the invariant culture, after setting <code>SelectedDate</code> to 2026-04-15.</figcaption>
</figure>

To set the format from code, apply the same `DatePickerTextBox` style and binding as the XAML approach, or keep the picker's text as it is and format a companion display with a converter, as in the next section.

## Using a Converter for Companion Displays

A converter cannot change what the `DatePicker` itself shows: its displayed text is produced by the control template from `SelectedDate`, not by the bound value, and `SelectedDate` is a `DateTime?`, so binding a string-returning converter to it does not apply.  
Where a converter is the right tool is a companion display — a summary label or status bar that must show the same date in the chosen format:

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

public class DateFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime d ? d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DateTime.TryParseExact(value as string, "yyyy/MM/dd",
               CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : DependencyProperty.UnsetValue;
}
```

Bind a `TextBlock` to the same source through the converter:

```xml
<TextBlock Text="{Binding SelectedDate, ElementName=datePicker,
                  Converter={StaticResource DateFormatConverter}}" />
```

The converter centralises the format string in one place, so a single edit changes every companion display that reuses it.  

## Common Format Strings

The format string passed to `ToString` or `StringFormat` follows the standard .NET custom date and time specifiers:

| Pattern            | Example output     | Notes                                        |
| ------------------ | ------------------ | -------------------------------------------- |
| `yyyy/MM/dd`       | `2026/04/15`       | Zero-padded; `/` follows the culture         |
| `dd MMM yyyy`      | `15 Apr 2026`      | `MMM` depends on the culture                 |
| `MMMM d, yyyy`     | `April 15, 2026`   | `MMMM` uses the full month name              |
| `yyyy/MM/dd HH:mm` | `2026/04/15 09:30` | Combines date and time in one string         |

Note that `/` and `:` are not literals: they are the date-separator and time-separator placeholders, which are replaced with the separators of the culture used for formatting (a culture whose separator is `.` renders `2026.04.15`). Characters such as `-` and `,` are literals and are preserved verbatim.  
That culture is the thread's `CurrentCulture` for `ToString`. For a binding's `StringFormat` it is `ConverterCulture` when that is specified, and the target element's `Language` otherwise. While `Language` is left at its default, the latter is `en-US` rather than the OS regional settings (measured in [the Binding.StringFormat article](/articles/wpf-binding-stringformat-number-currency-date/)).  
To keep a fixed layout regardless of the machine's regional settings, pass `CultureInfo.InvariantCulture` to `ToString` (as the converter above does), or escape the separators as `yyyy'/'MM'/'dd`.  

## Notes

- The XAML `StringFormat` approach only affects the displayed text. The underlying `SelectedDate` value is unchanged, so bindings that read the date directly are unaffected.  
- In the converter, the `is DateTime` check is what handles an unselected date: a `null` value fails the check instead of being formatted. Returning `string.Empty` then simply shows nothing.  

## Summary

| Method               | Pros                    | Cons                                       |
| -------------------- | ----------------------- | ------------------------------------------ |
| Style + StringFormat | Declarative, no code    | Limited to StringFormat syntax             |
| Value Converter      | MVVM-friendly, reusable | Formats companion displays, not the picker |

The appropriate approach depends on the project's architecture.  
To change the picker's own text, use the Style + StringFormat approach; setting `Text` in `SelectedDateChanged` did not change it. The converter is best for companion displays that must show the same date and grow in number.  
