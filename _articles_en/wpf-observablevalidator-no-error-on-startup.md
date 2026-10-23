---
layout: article-en
title: "Why CommunityToolkit.Mvvm ObservableValidator Shows No Validation Error on Startup in WPF"
seo_title: "WPF ObservableValidator Shows No Error on Startup"
date: 2026-10-24
category: WPF
excerpt: "ObservableValidator shows no error on an empty required field at startup: HasErrors is False until validation runs. ValidateAllProperties fixes it two ways."
image: /images/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-startup.png
---

## Overview

When a name is marked as required with the `ObservableValidator` of CommunityToolkit.Mvvm (the Toolkit below), the empty field shows neither a red border nor a message right after the window opens.
Even with the save command's `CanExecute` set to "`HasErrors` is `False`", the Save button is enabled on startup, and clicking it carries the save through with an empty name.

This article shows, by measuring `Validation.HasError` for each way of writing the view model, that `ObservableValidator` validates only when a value is set.
It then compares calling `ValidateAllProperties()` in the constructor with calling it at the start of the save logic, using measurements of the Save button.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF
- Libraries: CommunityToolkit.Mvvm 8.4.0 (Prism.Core 8.1.97 for comparison. The package is Prism.Core and the assembly is Prism. 8.1.97 is the last version released under the MIT license; the license changed from 9 onward)
- Test environment: .NET 10 / Windows 11
- Additional check: built for .NET Framework 4.8 and run on 4.8.1 (4.8.9363) / Windows 11 (outside the stated target range, but checked because readers include applications still maintained on .NET Framework 4.8)
- Architecture: MVVM (the view works only through bindings and has no code-behind)

The additional check ran an MVVM sample that reproduces the same rules in a different view on both .NET 10 and .NET Framework 4.8.1 and compared the two.
It covered the setter that calls `SetProperty(ref _name, value, true)` on startup and after typing 21 characters, and the same setter plus `ValidateAllProperties()` in the constructor on startup and after typing 21 characters; all were the same on both runtimes.
The source-generated variants, the Prism variants, and the Save button table were not measured on .NET Framework.
The code in this article uses C# 9 or later syntax such as the lambda discard parameters `(_, _)`.
The default language version for .NET Framework is C# 7.3, so the sample for the additional check was built with `LangVersion` set to `latest` in the project and with PolySharp, which supplies types for language features missing from .NET Framework.
Using a language version newer than the default for the target framework is [unsupported by Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-versioning).

---

## Problem

A field whose name is required and limited to 20 characters is written with the Toolkit source generators.

```csharp
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class AccountViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(20, ErrorMessage = "Name must be 20 characters or fewer.")]
    private string _name = "";

    public AccountViewModel()
    {
        // Re-query CanExecute whenever errors are added or removed.
        ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // Save
    }

    private bool CanSave() => !HasErrors;
}
```

With `[NotifyDataErrorInfo]`, the generated `Name` setter calls validation whenever the value changes.
`NotifyCanExecuteChanged()` is called from `ErrorsChanged` because Toolkit commands are not updated by the `CommandManager` requery ([Why InvalidateRequerySuggested Does Not Update CommunityToolkit.Mvvm and Prism Commands in WPF](/articles/wpf-mvvm-library-command-requery/)).

The view is shown below; a `Style` applies an `ErrorTemplate` that draws the error message under the `TextBox`.
The default `ErrorTemplate` draws only a red border and no message, so it is replaced ([Why WPF Validation Errors Are Not Displayed, and Choosing Between IDataErrorInfo and INotifyDataErrorInfo](/articles/wpf-validation-error-not-displayed/)).

```xml
<StackPanel Margin="8">
  <StackPanel.Resources>
    <ControlTemplate x:Key="FieldErrorTemplate">
      <StackPanel>
        <Border BorderBrush="#D13438" BorderThickness="1">
          <AdornedElementPlaceholder x:Name="Adorned" />
        </Border>
        <TextBlock Margin="2,2,0,0" FontSize="11" Foreground="#D13438"
                   Text="{Binding ElementName=Adorned,
                          Path=AdornedElement.(Validation.Errors)/ErrorContent}" />
      </StackPanel>
    </ControlTemplate>
    <Style TargetType="TextBox">
      <Setter Property="Validation.ErrorTemplate" Value="{StaticResource FieldErrorTemplate}" />
      <Setter Property="Margin" Value="0,0,0,22" />
    </Style>
  </StackPanel.Resources>

  <TextBlock Text="Name (required, up to 20 characters)" />
  <StackPanel Orientation="Horizontal">
    <TextBox Width="240" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
    <Button Content="Save" Command="{Binding SaveCommand}" Padding="14,2" Margin="12,0,0,22" />
  </StackPanel>
</StackPanel>
```

When this window opens, the name is empty but no error appears, and the Save button is enabled.
Clicking Save runs `Save` all the way through with the empty name.

<figure class="article-figure">
  <img src="/images/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-startup.png" alt="Three sets of a name field and a Save button. The top set, with [NotifyDataErrorInfo] only, has no red border or message on the empty field at startup and an enabled Save button. The middle set, which calls ValidateAllProperties in the constructor, shows a red border and Name is required. from startup with a disabled Save button. The bottom set, which calls ValidateAllProperties at the start of the save logic, shows a red border and Name is required. after Save is clicked." width="408" height="392" loading="lazy">
  <figcaption>.NET 10 / Windows 11, CommunityToolkit.Mvvm 8.4.0: three views created from the same XAML, each given a different view model. The top two sets were captured on startup and the bottom set after clicking Save. The top is the code in this section, the middle calls <code>ValidateAllProperties()</code> in its constructor, and the bottom is the code in the "Call it at the start of the save logic" section.</figcaption>
</figure>

---

## Cause / Background

`ObservableValidator` keeps the errors for each property internally and returns them through `GetErrors` and `HasErrors` of `INotifyDataErrorInfo`.
Errors are added only when validation is called.
The [official documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observablevalidator) explains that passing `true` to the `bool` parameter of `SetProperty` validates the property when its value is updated.
The setter generated for `[NotifyDataErrorInfo]` also calls `ValidateProperty` when the value changes ([ObservableProperty attribute](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty#requesting-property-validation)).
The field initializer `""` does not go through the setter, so nothing has been validated when the window opens.
`GetErrors` is empty and `HasErrors` returns `False`.
The [`INotifyDataErrorInfo.HasErrors` reference](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifydataerrorinfo.haserrors) states that `HasErrors` returns `False` if there are no known errors when it is accessed.
It is therefore also `False` when nothing has been validated yet.

For each variant, `Validation.HasError` was measured on startup, after typing 21 characters, and after clearing the input.

{% include tables/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-display-matrix.en.md %}

.NET 10 / Windows 11, CommunityToolkit.Mvvm 8.4.0 / Prism.Core 8.1.97, using the same view without the Save button (the name field only). 21 characters were typed into the name field through the WPF input pipeline (`InputManager`), then the text was selected with `SelectAll()` and deleted by sending a Backspace key event to the field. `Validation.HasError` of the field was read after each step. For the `True` steps, the test also confirmed that the `ErrorTemplate` drew the required message on startup and after clearing, and the length message after 21 characters.
{: .table-caption}

**Rows 1, 3, and 5, which validate in the setter but not in the constructor, are `False` on startup and become `True` when the field is cleared after typing 21 characters.**
The required error appears only after the value has been changed once and then emptied.
Prism.Core 8.1.97 has no base class that reads validation attributes, so rows 5 and 6 check the rules in the setter and pass the result to `ErrorsContainer.SetErrors`.
When validation is called only from the setter, this Prism variant behaves the same way.

Row 2 is the variant that forgets `[NotifyDataErrorInfo]`.
The validation attributes are copied to the generated property, but the setter does not call validation, so neither 21 characters nor an empty field produces an error.
[`[NotifyDataErrorInfo]` without any validation attributes](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/errors/mvvmtk0026) is error MVVMTK0026, but for the reverse case, validation attributes without `[NotifyDataErrorInfo]`, building the measurement tool with `dotnet build --no-incremental` produced no warnings at all.

Rows 4 and 6, which validate in the constructor, were `True` from startup.
Errors created before the view model was assigned to `DataContext` were already shown when the window was displayed.

---

## Two Places to Call ValidateAllProperties

Validating the values present when the window opens takes a call to `ValidateAllProperties()`.
According to the [official documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observablevalidator), this method validates all public instance properties that have at least one validation attribute.
It can be called in two places: the constructor and the start of the save logic.

### Call it in the constructor

Here the constructor of the code from the Problem section calls `ValidateAllProperties()`.

```csharp
public AccountViewModel()
{
    ValidateAllProperties();

    // Re-query CanExecute whenever errors are added or removed.
    ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
}
```

The required error appears when the window opens, and the Save button with `!HasErrors` as `CanExecute` is disabled.
Even though `ValidateAllProperties()` is called before the subscription, the Save button was disabled from startup.
The view model is assigned to `DataContext` after the constructor finishes, so the errors already exist when the button's `Command` is bound.
According to the [ButtonBase source for .NET 10.0.10](https://github.com/dotnet/wpf/blob/v10.0.10/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/ButtonBase.cs), setting `Command` calls `UpdateCanExecute` from `OnCommandChanged`, and the button reads `CanExecute`.
Once the user starts typing, `[NotifyDataErrorInfo]` validates whenever the value changes, and typing a valid name re-enabled the Save button (the last column of the table in Results by Criterion).

### Call it at the start of the save logic

Here `Save` has no `CanExecute`; it calls `ValidateAllProperties()` and then checks `HasErrors`.

```csharp
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class AccountViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(20, ErrorMessage = "Name must be 20 characters or fewer.")]
    private string _name = "";

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        // Save
    }
}
```

The view XAML from the Problem section is used as is.
No error appears on the empty field until Save is clicked; then `Name is required.` appears and `Save` returns before saving.
Once the user starts typing, `[NotifyDataErrorInfo]` validates whenever the value changes.
Because `CanExecute` is not used, there is no need to subscribe to `ErrorsChanged` to notify the command.

---

## How to Choose

- **When the empty-field error appears.**
  Calling it in the constructor makes untouched fields red from the start.
  Calling it at the start of the save logic keeps them from turning red until Save is clicked.
- **How the save is stopped (disabling the button or cancelling after the click).**
  Disabling it through `CanExecute` requires validation in the constructor so that `HasErrors` is correct on startup.

---

## Results by Criterion

For each way of guarding Save, the result of clicking Save with the name still empty, and the state of the Save button after then typing a valid name, were measured.

{% include tables/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-save-matrix.en.md %}

.NET 10 / Windows 11, CommunityToolkit.Mvvm 8.4.0: with the name still empty, `IsEnabled` of the Save button and `Validation.HasError` of the field were read on startup. If the button was enabled, it was clicked with UI Automation `Invoke` (the command runs from `Click`), and the number of times the save went through (a `Save` call that returned at the validation at its start is not counted) and `Validation.HasError` were read. Finally, a valid name (taro) was typed into the name field through `InputManager`, and `IsEnabled` of the Save button was read. Rows 1 and 2 are the code from the Problem section (row 2 calls `ValidateAllProperties()` in its constructor), and row 3 is the code from the "Call it at the start of the save logic" section.
{: .table-caption}

**With only `!HasErrors` as `CanExecute`, the save went through once with the empty name.**
Validating in the constructor makes the button unclickable, and validating at the start of the save logic keeps a click from going through.
After a valid name was typed, the Save button was enabled in every variant (row 2 went from disabled back to enabled).

Along the two criteria, the results are as follows.

| When to validate | When the empty-field error appears | How the save is stopped | Suitable form |
|---|---|---|---|
| Setter only (`[NotifyDataErrorInfo]`) | After the value is changed once and emptied | Not stopped for the untouched value on startup | Not used alone |
| `ValidateAllProperties()` in the constructor | When the window opens | The button is disabled | Forms that should show missing input from the start |
| `ValidateAllProperties()` at the start of the save logic | When Save is clicked | Cancelled after the click | Forms that should not open red |

---

## Notes

- **Forgetting `[NotifyDataErrorInfo]` still builds.**
  Validation attributes alone do not make the setter validate (row 2 of the display table).
  Without it, errors are created only when a validation method such as `ValidateAllProperties()` or `ValidateProperty()` is called.
- **`ValidateAllProperties()` looks only at public instance properties with validation attributes.**
  This follows the [official documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observablevalidator).
  Properties validated by custom code in the setter instead of attributes are not covered by this method.
  This was not measured in this article, and the measurements cover only one property with validation attributes.
- **`ClearAllErrors()` is provided for resetting a form.**
  The [official documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observablevalidator) says it can be used to reset a form that the user may want to fill in again.
  Setting an entered value back to `""` in code goes through the `[NotifyDataErrorInfo]` setter and runs validation, so it is expected to create the required error just as clearing the field does ("after clearing the input" in the display table).
  Neither the assignment from code nor `ClearAllErrors()` was measured in this article.
- **If `CanExecute` depends on `HasErrors`, notify the command from `ErrorsChanged`.**
  The Toolkit `RelayCommand` does not report a change in `CanExecute` to the button unless `NotifyCanExecuteChanged()` is called ([Why InvalidateRequerySuggested Does Not Update CommunityToolkit.Mvvm and Prism Commands in WPF](/articles/wpf-mvvm-library-command-requery/)).

---

## Summary

`ObservableValidator` errors exist only after validation has been called.
With setter-based validation (`true` in `SetProperty` or `[NotifyDataErrorInfo]`), the field initializer was never validated, and the empty field showed no error on startup.
`HasErrors` returns `False` even when nothing has been validated, so `!HasErrors` as `CanExecute` alone let the save go through with the empty name.
Validating only from the setter with Prism's `ErrorsContainer` gave the same result.

Validating the values present when the window opens takes a call to `ValidateAllProperties()`.
Calling it in the constructor shows the error from startup and disables the Save button; calling it at the start of the save logic shows the error and stops the save when the button is clicked.
The choice depends on whether the empty-field error should be visible as soon as the window opens and whether the Save button should be disabled.

---

## Related Articles

- [Why WPF Validation Errors Are Not Displayed, and Choosing Between IDataErrorInfo and INotifyDataErrorInfo](/articles/wpf-validation-error-not-displayed/)
- [Why InvalidateRequerySuggested Does Not Update CommunityToolkit.Mvvm and Prism Commands in WPF](/articles/wpf-mvvm-library-command-requery/)
