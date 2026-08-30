---
layout: article-en
title: "Why a WPF RadioButton Bound to an Enum Shows No Initial Selection — The Role of GroupName"
date: 2026-08-22
category: WPF
excerpt: "A ViewModel holds the right enum value, yet its radio button appears cleared. Omitting GroupName merges separate enum groups into one. Measured cause and fix."
image: /images/articles/wpf-radiobutton-enum-binding/radiobutton-enum-groupname.png
---

## Overview

A WPF UI that lets the user pick an enum value is usually built by binding `RadioButton.IsChecked` to an enum property through a converter.
With this arrangement, the ViewModel can hold the correct value while the matching `RadioButton` is displayed as cleared.
Using measured results, this article traces that behavior to a grouping mistake — the omission of `GroupName` — and then covers how to set `GroupName`, what `ConvertBack` should return, and how to choose among four approaches: a converter, wrapper properties, an attached behavior, and replacing the radio buttons with a selection control.

---

## Prerequisites / Environment

- Framework: .NET 8 or later / WPF (the behavior is the same on WPF for .NET Framework)
- Verified on: .NET 10 / Windows 11 (all measured results and the figures come from this environment); the grouping decision, the `ConvertBack` calls, and the validation error were reproduced identically on .NET Framework 4.8
- Language: C# / XAML (code samples assume nullable reference types are enabled)
- Target controls: `System.Windows.Controls.RadioButton`, `System.Windows.Data.IValueConverter`
- Architecture: MVVM, with the selection held in an enum property on the ViewModel
- Namespaces: `System`, `System.ComponentModel`, `System.Globalization`, `System.Windows.Data`
- The `local` prefix in XAML: the CLR namespace that declares the enums and the converter (for example, `xmlns:local="clr-namespace:PrintSettingsApp"`)

---

## Problem

Consider a print settings dialog.
Two enums back it — `Quality` for print quality and `PageLayout` for imposition — each presented as a set of radio buttons.

```csharp
public enum Quality
{
    Draft,
    Standard,
    Fine,
}

public enum PageLayout
{
    Single,
    Dual,
}
```

Both are ordinary enums, with no `Flags` attribute and no explicit member values.
The fact that these two share a single view is the precondition for the problem described below.

The ViewModel holds both enum properties, initialized to `Quality.Standard` and `PageLayout.Single`.
`Quality.Standard` is the second member in declaration order, so an initial selection that arrives correctly shows `Standard` checked rather than `Draft`.

```csharp
public sealed class PrintSettingsViewModel : INotifyPropertyChanged
{
    private Quality _quality = Quality.Standard;
    private PageLayout _pageLayout = PageLayout.Single;

    public Quality Quality
    {
        get => _quality;
        set
        {
            if (_quality == value) return;
            _quality = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quality)));
        }
    }

    public PageLayout PageLayout
    {
        get => _pageLayout;
        set
        {
            if (_pageLayout == value) return;
            _pageLayout = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageLayout)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

Change notification is implemented for both properties, so a missing notification is not the cause of the problem covered here.
That fact is the starting point for isolating the real cause below.

A converter maps between an enum value and `bool`, with the target value passed through `ConverterParameter`.
The converter itself appears under Implementation.
The version used here returns `Binding.DoNothing` on clearing — the implementation generally regarded as correct.
The following markup, a layout commonly used in production code, places all five radio buttons in a single `StackPanel`.

```xml
<StackPanel>
    <StackPanel.Resources>
        <local:EnumToBooleanConverter x:Key="EnumToBoolean" />
    </StackPanel.Resources>

    <RadioButton Content="Draft"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Draft}}" />
    <RadioButton Content="Standard"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Standard}}" />
    <RadioButton Content="Fine"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Fine}}" />

    <RadioButton Content="Single"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Single}}" />
    <RadioButton Content="Dual"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Dual}}" />
</StackPanel>
```

The markup is syntactically valid and produces no binding errors.
Even so, `Single` appears selected at startup while all three `Quality` buttons appear cleared.
The ViewModel still holds `Standard` for `Quality`, so the screen and the ViewModel disagree.

<figure class="article-figure">
  <img src="/images/articles/wpf-radiobutton-enum-binding/radiobutton-enum-groupname.png" alt="Two sets of radio buttons showing the same ViewModel values. On the left, without GroupName, Draft, Standard and Fine are all cleared even though Quality equals Standard; on the right, with GroupName, Standard is selected." width="419" height="201" loading="lazy">
  <figcaption>The left column corresponds to the markup in this section, the right column to the markup under Implementation. Both sides hold the same ViewModel values, <code>Quality = Standard</code> and <code>PageLayout = Single</code>; only the <code>GroupName</code> attribute differs. Without <code>GroupName</code>, the <code>PageLayout</code> radio button that is checked later clears the <code>Quality</code> selection. The labels at the top and the two lines at the bottom were added for comparison and are not part of the markup shown here. Captured on .NET 10 / Windows 11.</figcaption>
</figure>

---

## Cause / Background

The official documentation gives two ways to group `RadioButton` controls: placing them inside a parent, or setting the `GroupName` property on each control ([RadioButton Class](https://learn.microsoft.com/dotnet/api/system.windows.controls.radiobutton)).
In WPF, the default value of `RadioButton.GroupName` is an empty string.
An empty `GroupName` disables grouping by name, and grouping falls back to the logical parent (`FrameworkElement.Parent`).
Radio buttons that share a logical parent form one group; buttons with different logical parents fall into different groups.
All five buttons above share the same `StackPanel` as their logical parent, so they form one group **even though they bind to different properties**.

Radio buttons within a group are mutually exclusive.
Initialization follows the order the buttons appear in the markup, so `Standard` on the `Quality` side becomes checked first, and `Single` on the `PageLayout` side becomes checked next.
The moment `Single` is checked, the grouping mechanism clears `Standard`, which it treats as a member of the same group.

The problem is that this clearing propagates back toward the source through the binding.
`ToggleButton.IsChecked` is a `bool?` dependency property whose metadata enables two-way binding by default, and its default `UpdateSourceTrigger` is `PropertyChanged`.
Clearing the control is therefore treated as an immediate source update, and the converter's `ConvertBack` is invoked with `false`.
The measured run confirmed this: `ConvertBack(value: false, parameter: Quality.Standard)` fired exactly once, right after `Single` became checked.

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-radiobutton-enum-binding/radiobutton-group-convertback-path.svg" alt="Three-row diagram of the path by which clearing reaches the source when GroupName is omitted. The first row shows that an empty GroupName produces one implicit group, so checking PageLayout.Single flips IsChecked on Quality.Standard from true to false, and that the binding is two-way with UpdateSourceTrigger set to PropertyChanged. The second row shows that change causing ConvertBack to be called with value false and parameter Quality.Standard. The third row gives the result for each of the four return values: Binding.DoNothing and DependencyProperty.UnsetValue both leave Quality as Standard with IsChecked false, the latter adding a Validation.Errors entry; returning the parameter restores IsChecked to true; and throwing produces an unhandled NotImplementedException." width="880" height="466" loading="lazy">
  <figcaption>The path that turns a grouping mistake into a visible disagreement, and how the result differs per <code>ConvertBack</code> return value. No return value resolves the underlying missing <code>GroupName</code>. Returning <code>parameter</code> is the one case where the missing initial selection never appears, but the read-back merely hides the symptom. Each result was observed on .NET 10 / Windows 11.</figcaption>
</figure>

What follows traces the case this article assumes: a converter that returns `Binding.DoNothing`.
The other three return values are covered individually under Notes.

If `ConvertBack` returns `Binding.DoNothing`, the binding transfers no value and uses neither `FallbackValue` nor the default value ([Binding.DoNothing Field](https://learn.microsoft.com/dotnet/api/system.windows.data.binding.donothing)).
Since nothing is written to the source, no read-back follows the write either, so the target stays cleared.
The ViewModel keeps `Standard` while the radio button alone remains cleared, which is precisely the disagreement described above.

**Among radio buttons bound to the same enum property, this `ConvertBack(false)` does not normally occur.**
Changing the selection first calls `ConvertBack(true)` on the newly checked button, updating the source; that change flows through `Convert` and clears the other buttons.
By the time the grouping mechanism tries to clear them, they are already `false`, so no value changes and no source update follows.
The measured run agreed: switching within one property invoked `ConvertBack` once with `true` and never with `false`.
`ConvertBack` receives `false` when **anything other than a button bound to the same property on the same source joins the group**.
That covers a button bound to a different property, a button bound to the same property name on a different object, and a button with no binding at all.
A test run reproduced `ConvertBack(false)` both in a list where each row was bound to the same property name on a separate ViewModel and every row shared one `GroupName`, and in a panel where a single unbound radio button sat among the bound ones.

---

The figure below records the converter calls and the checked state, varying only whether `GroupName` is set.

<figure class="article-figure">
  <img src="/images/articles/wpf-radiobutton-enum-binding/radiobutton-grouping.svg" alt="A table of ConvertBack calls and checked state with and without GroupName. The GroupName default is the empty string. Without GroupName, ConvertBack runs once with false and only Single stays checked. With GroupName set, no ConvertBack call occurs while the view initializes and both Standard and Single stay checked. The source values remain Standard and Single in both rows." width="764" height="170" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 with two pairs of radio buttons — one for <code>Quality</code>, one for <code>Layout</code> — under a single <code>StackPanel</code>. The presence of the <code>GroupName</code> attribute is the only difference between the two rows.</figcaption>
</figure>

**On the row without `GroupName`, only `Single` remains checked.** `Standard` has been cleared even though it is bound to a different property, and that is the initial selection failing to appear.
`ConvertBack` runs once with `false` at that moment.

What deserves attention is that **the source values stay `Standard / Single` on both rows.**
Because the converter returns `Binding.DoNothing` for `false`, the view model is never corrupted.
Only the display is wrong, which is why logging the view model never leads to the cause.

With `GroupName` set, no `ConvertBack` call occurs while the view initializes, and both stay checked.
This row measures initialization only. Changing the selection afterward still calls `ConvertBack(true)` on the newly checked button, as described above.

---

## Solution

The root cause lies in UI-side grouping, so the fix belongs there as well.

- **Assign a distinct `GroupName` per enum property.**
  This is the essential fix.
  Radio buttons with different `GroupName` values form separate groups even when they share a parent.
- **Return `Binding.DoNothing` from `ConvertBack` when the control is cleared.**
  With the groups separated correctly, `ConvertBack` is never called on clearing.
  Returning `Binding.DoNothing` still protects the source if a later layout change merges the groups again.
- **Pass the enum value through `x:Static` in `ConverterParameter`.**
  A plain string never compares equal, as described below.

Because grouping falls back to the logical parent, giving each enum property its own parent panel also resolves the symptom.
A test run split the five buttons from Problem into one `StackPanel` for `Quality` and another for `PageLayout`, and both initial selections appeared without a single `GroupName`.
That arrangement depends on the layout structure, however, and merging the panels again in a later change brings the symptom back.
Nor does it help where the logical parent stays the same despite visual distance, such as a `GroupBox` split between `Header` and `Content`, or separate `Grid` cells (see Notes).
Setting `GroupName` makes the group boundary explicit, and that is why it is the recommended fix.

---

## Implementation

The converter turns an equality check between the enum value and the parameter into a `bool`, and returns the parameter from `ConvertBack` only when the control is checked.
The `value` argument arrives as a boxed `bool` or as `null`.
The pattern `value is true` rejects both `null` and a boxed `false` in a single expression.

```csharp
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter : Binding.DoNothing;
}
```

For the button being checked, `ConvertBack` returns `parameter` — the target enum value — and the ViewModel property is updated.
If it is called for a button being cleared, it returns `Binding.DoNothing`, leaving the source untouched.
That call does not occur once the groups are separated correctly, but getting the return value right is what prevents the pitfalls covered below.

In XAML, give each enum property its own `GroupName`.
The only difference from the markup shown under Problem is the added `GroupName` attribute.

```xml
<StackPanel>
    <StackPanel.Resources>
        <local:EnumToBooleanConverter x:Key="EnumToBoolean" />
    </StackPanel.Resources>

    <RadioButton Content="Draft" GroupName="quality"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Draft}}" />
    <RadioButton Content="Standard" GroupName="quality"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Standard}}" />
    <RadioButton Content="Fine" GroupName="quality"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Fine}}" />

    <RadioButton Content="Single" GroupName="pageLayout"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Single}}" />
    <RadioButton Content="Dual" GroupName="pageLayout"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Dual}}" />
</StackPanel>
```

With this markup, both `Standard` and `Single` appear selected at startup.
Selecting `Draft` changes only `Quality`, leaving the `PageLayout` selection intact.

---

## Notes

- **`GroupName` groups across parent elements.**
  Radio buttons that carry a `GroupName` join the same group even when they sit in separate `Border` elements or separate panels.
  In the measured run, two sets marked `GroupName="quality"` under different parents cleared each other.
  The name given to one group must therefore differ from the name of every other group under the same visual tree root; the buttons that belong to one group all share the same name.
  A naming convention applied across the application still assumes that no view shows two sets for the same enum.
  Grouping does not cross the root of the visual tree.
  That root is normally a window, but the contents of a `Popup` or a `ContextMenu` form a separate root, so the boundary holds even within one window.
- **Do not write `ConverterParameter` as a plain string.**
  Writing `ConverterParameter=Draft` passes the string `"Draft"` through unchanged, because `ConverterParameter` is typed as `object` and no target type drives a conversion.
  The comparison in `Convert` then always yields `false`, and no button is ever displayed as selected.
  On click, `ConvertBack` still returns that string, and WPF's default type conversion turns it into the enum value, so only the source update succeeds.
  In the measured run, the ViewModel value changed while every button stayed cleared.
  Pass the enum value with `x:Static`, or call `Enum.Parse` inside `Convert`.
- **`ConverterParameter` cannot be data bound.**
  `Binding` derives from `BindingBase` and ultimately from `MarkupExtension`, not from `DependencyObject`.
  `ConverterParameter` is therefore not a dependency property and cannot host a nested `{Binding ...}`.
  When each item needs a different value, the converter approach does not apply; use an attached behavior or a selection control instead.
- **Do not leave `ConvertBack` throwing `NotImplementedException`.**
  Two-way bindings call `ConvertBack`, and the data binding engine does not catch exceptions thrown by a converter.
  Once groups are mixed, clearing a button triggers a `ConvertBack(false)` call; in the measured run, the application terminated with `NotImplementedException` at that point.
- **Do not return `DependencyProperty.UnsetValue` when the control is cleared.**
  The official documentation states that anticipated problems should be handled by returning `DependencyProperty.UnsetValue`, and that the binding then uses `FallbackValue` when present and the default value otherwise ([IValueConverter.ConvertBack Method](https://learn.microsoft.com/dotnet/api/system.windows.data.ivalueconverter.convertback)).
  In the measured run, however, returning it from `ConvertBack` left the source untouched, applied no `FallbackValue`, and attached the validation error `Value 'False' could not be converted.` to the binding, where it remained in `Validation.Errors`.
  Returning `Binding.DoNothing` under the same conditions raises no validation error.
  `Binding.DoNothing` is the value that expresses only the intent to transfer nothing.
  Reading such errors is covered in [Reading WPF Binding Errors and Diagnosing Them with the Output Window](/articles/wpf-binding-error-debugging-output-window/).
- **Returning `parameter` when the control is cleared hides the grouping mistake.**
  Returning `parameter` for `false` as well updates the cleared button's source with the same value, and the read-back that follows restores the target to checked.
  That restoration lands after the grouping mechanism has already cleared the button, so running this on .NET 10 showed both the `Quality` and the `PageLayout` selection checked despite the two sharing one group.
  The screen looks as intended, and the missing-initial-selection symptom this article covers never appears.
  Mutual exclusion is merely undone by the read-back, though, and the missing `GroupName` remains.
  The absent symptom is what makes it easy to overlook, and every selection change costs the source an extra write and read-back.
  Return `Binding.DoNothing` on clearing and fix the grouping itself.
- **Wrapper properties still need `GroupName`.**
  Exposing one `bool` property per enum value does not change the fact that grouping is a UI-side mechanism, so omitting `GroupName` clears buttons across enums in exactly the same way.
  In the measured run, the setter of the cleared property ran with `false` and ignored it, after which the source was read back and the selection was restored.
  As with a converter that returns `parameter`, the screen looks as intended, yet the redundant round trip and the missing `GroupName` both remain.
- **Implicit grouping is determined by the logical parent, not by the visual region or the visual parent.**
  Radio buttons in separate `Grid` cells take the same `Grid` as their logical parent, because a cell is assigned through the `Grid.Row` and `Grid.Column` attached properties, which add no element to the hierarchy.
  A test run confirmed they form one group.
  The check runs against the logical tree, so radio buttons split between a `GroupBox`'s `Header` and its `Content` still form one group.
  Each sits under a separate `ContentPresenter` in the visual tree, yet both share the `GroupBox` as logical parent (confirmed on .NET 10).
  Even in an `ItemsControl`, radio buttons added directly to `Items` take the `ItemsControl` itself as their logical parent and form one group.
  Giving each enum property its own parent panel, by contrast, changes the logical parent and so separates the groups.
- **Inside a template or a single-child element, the logical parent changes.**
  With an `ItemTemplate`, a radio button placed at the template root has no logical parent at all, so implicit grouping never applies to it.
  Buttons inside a panel within the template take that per-item panel as their parent, so they group within a row and stay independent across rows.
  Wrapping each button individually in a single-child element such as `Border` also changes the logical parent, and the grouping mechanism no longer enforces mutual exclusion.
  With the converter approach the binding keeps the selection exclusive, so the display still holds, but exclusivity no longer rests on the group.
  An unbound radio button in the mix breaks it, which is why the boundary of a selection set belongs in `GroupName`.
- **Make `GroupName` unique per item where repeated rows need independent selections.**
  Giving every row the same `GroupName` in a list merges all rows into one group, since `GroupName` crosses parents, allowing only one selection across the entire list.
  Unlike `ConverterParameter`, `GroupName` is a dependency property, so the row identifier can be bound into it to give every item a unique name.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best suited for |
|---|---|---|---|
| Converter with `ConverterParameter` | No extra ViewModel properties; new options need only markup | A wrong `ConvertBack` return breaks behavior; `ConverterParameter` cannot be bound | Ordinary settings screens whose options are fixed in XAML |
| One wrapper property per enum value | No converter, simpler markup, no `ConvertBack` to reason about | One property and notification per option; the ViewModel changes whenever the enum gains a value | Two or three fixed options, with a deliberately plain ViewModel |
| Attached behavior holding the enum value | The enum is specified directly in XAML, with no `ConvertBack` pitfalls; per-item values can be bound | Requires implementing an attached property and event subscription | Applications that repeat this pattern across many views |
| Replacing with a selection control such as `ListBox` | Selection is handled entirely by `SelectedItem` / `SelectedValue`, and the grouping problem does not arise | A radio button appearance requires an `ItemContainerStyle` | Options that are dynamic or numerous |

The wrapper property approach replaces the `Quality` property of the `PrintSettingsViewModel` shown under Problem with the form below and adds one `bool` property per enum value.
The `_quality` field and the `PropertyChanged` declaration carry over unchanged from that same class.
A wrapper setter updates the enum property only when the value is `true` and ignores the `false` that arrives on clearing.
Because the number of notifications grows, `PropertyChanged?.Invoke` is collected into a `Raise` helper.

```csharp
public Quality Quality
{
    get => _quality;
    set
    {
        if (_quality == value) return;
        _quality = value;
        Raise(nameof(Quality));
        Raise(nameof(IsDraft));
        Raise(nameof(IsStandard));
        Raise(nameof(IsFine));
    }
}

public bool IsDraft
{
    get => Quality == Quality.Draft;
    set { if (value) Quality = Quality.Draft; }
}

private void Raise(string propertyName)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
```

`IsStandard` and `IsFine` follow the same shape as `IsDraft`.
Since the property and the enum are both named `Quality`, `Quality.Draft` resolves against the type.
A missed notification leaves other buttons showing a stale state after the selection changes.
Every added enum member brings another property and another notification, which is why this approach suits a small set of options.

The attached behavior approach stores the target enum value on the `RadioButton` through an attached property and writes it back to the ViewModel when `Checked` fires.
An attached property is a dependency property, so unlike `ConverterParameter` it accepts a binding and can carry a different value per item.

When the options are determined at run time, feeding the enum values to `ListBox.ItemsSource` and binding `SelectedItem` to the ViewModel is easier to manage.
The ways to retrieve the selected value are compared in [WPF ComboBox ItemsSource Binding Patterns and Selected Value Retrieval](/articles/wpf-combobox-itemssource-patterns/).

---

## Summary

A missing initial selection is not a flaw in the binding or the converter; it follows from omitting `GroupName`, which merges radio buttons for different enums into one group.
Choose as follows.

- **Assign a `GroupName` per property for radio buttons that select an enum.**
  Skipping it leaves the group boundary to the layout structure, so the screen starts to disagree with the ViewModel as soon as the panels are merged again.
  Keep the name of one group distinct from the name of every other group under the same visual tree root.
- **Options fixed in XAML:**
  Use the converter approach.
  Return `Binding.DoNothing` from `ConvertBack` on clearing, and pass the enum value with `x:Static` in `ConverterParameter`.
- **Two or three options with a deliberately plain ViewModel:**
  Use wrapper properties.
  Raise change notifications for every related property.
- **Dynamic options, or a different value per item:**
  `ConverterParameter` cannot be bound, so use an attached behavior or a selection control.

The point at which a two-way binding pushes to the source is governed by `UpdateSourceTrigger`, and the default for `IsChecked` is `PropertyChanged`.
How that default affects the timing of input reaching the source is covered in [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/).

---

<!-- Related articles -->
- [WPF ComboBox ItemsSource Binding Patterns and Selected Value Retrieval](/articles/wpf-combobox-itemssource-patterns/)
- [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/)
- [Reading WPF Binding Errors and Diagnosing Them with the Output Window](/articles/wpf-binding-error-debugging-output-window/)
