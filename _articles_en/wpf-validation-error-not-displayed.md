---
layout: article-en
title: "Why WPF Validation Errors Are Not Displayed, and Choosing Between IDataErrorInfo and INotifyDataErrorInfo"
date: 2026-08-13
category: WPF
excerpt: "Validation code runs but nothing appears. The causes are isolated on .NET 10: the ValidatesOnDataErrors default, a missing adorner layer, and update timing."
image: /images/articles/wpf-validation-error-not-displayed/validation-error-display.png
---

## Overview

A required-field check is in place, yet the `TextBox` never turns red.
The symptom splits into two variants.
In one, a breakpoint in the validation code is never hit; in the other, validation clearly runs and `Validation.GetHasError` returns `true`, but the screen does not change.

Neither is a defect in the validation logic.
WPF validation is split into three independent parts: the path that produces an error, the place that stores it, and the place that draws it.
When one of them is missing, the other two keep working correctly while the UI stays silent.

This article breaks the "no error shown" symptom into those three stages, isolates each cause, and compares `ValidationRule`, `IDataErrorInfo`, `INotifyDataErrorInfo`, and exception-based validation.
One more symptom is covered alongside them: all three stages hold, yet the message alone is missing, which is the documented behavior of the default `ErrorTemplate`.
Every behavior and default value described here was measured on .NET 10 / Windows 11.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (behavior verified on .NET 10 / Windows 11)
- Language: C# 12 or later / XAML (samples use collection expressions; on targets whose default language version is C# 11 or earlier, such as `net6.0`, read `[]` as `new()` and the spread element `[.. messages]` as `new List<string>(messages)`)
- Target features: `Binding` validation (`Validation` attached properties, `ValidationRule` subclasses, `IDataErrorInfo`, `INotifyDataErrorInfo`, `ValidatesOnExceptions`)
- Architecture: MVVM, with the view model holding validation results
- Other constraints: the default `ErrorTemplate` is the baseline; custom templates are covered in the implementation section

---

## Problem

Three `TextBox` controls are stacked in one panel against the same state, an empty required name.
Only the binding syntax and the interface implemented by the view model differ.
The validation logic is identical in all three cases: an empty string yields exactly one "required" error.

```xml
<StackPanel>
    <!-- Bound to a view model that implements IDataErrorInfo -->
    <TextBox x:Name="Plain"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />

    <!-- Same view model, with ValidatesOnDataErrors added -->
    <TextBox x:Name="WithFlag"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged,
                    ValidatesOnDataErrors=True}" />

    <!-- Bound to a view model that implements INotifyDataErrorInfo -->
    <TextBox x:Name="Notify"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
</StackPanel>
```

For the comparison, each `TextBox` receives its own `DataContext`.

```csharp
Plain.DataContext = new DataErrorAccount();     // implements IDataErrorInfo
WithFlag.DataContext = new DataErrorAccount();  // same type
Notify.DataContext = new NotifyErrorAccount();  // implements INotifyDataErrorInfo
```

`DataErrorAccount` and `NotifyErrorAccount` are view models that return the same required-field check through `IDataErrorInfo` and `INotifyDataErrorInfo` respectively; their definitions are omitted here.
When all three are displayed at once, the default error indication, a red border, appears on the lower two only.

<figure class="article-figure">
  <img src="/images/articles/wpf-validation-error-not-displayed/validation-error-display.png" alt="Three TextBox controls stacked vertically. The top one, bound with IDataErrorInfo but without ValidatesOnDataErrors, keeps its normal border, while the one with ValidatesOnDataErrors=True and the one backed by INotifyDataErrorInfo are surrounded by a red border." width="474" height="224" loading="lazy">
  <figcaption>Default error indication for the same "name is empty" state. Only the binding syntax and the interface implemented by the view model differ. The label above each <code>TextBox</code> was added to the figure to identify the corresponding binding (produced on .NET 10 / Windows 11).</figcaption>
</figure>

For the top `TextBox`, `Validation.GetHasError` stays `false` and `Validation.Errors` is empty.
A breakpoint in the `IDataErrorInfo` indexer is never hit.

The two controls that do show a red border have a problem of their own.
`Validation.Errors` holds the message, but no message reaches the screen.

There is also a configuration in which not even the border appears.
In an application that replaces the `ControlTemplate` of its `Window`, `Validation.GetHasError` returns `true` and `Validation.Errors` contains an entry, yet the UI is completely unresponsive to the error.

---

## Cause / Background

WPF validation consists of three independent stages.

1. **Production** — a validation rule associated with the binding runs and creates a `ValidationError`.
2. **Storage** — the `ValidationError` is added to `Validation.Errors` on the binding target element, and `Validation.HasError` becomes `true`.
3. **Rendering** — `Validation.ErrorTemplate` is drawn on the adorner layer of that element.

Stage 2 is handled by the binding engine, so the stages that application code can leave incomplete are 1 and 3.
The sections below separate the symptoms by which of the two is missing.

### Missing rule association on the binding

This stops at stage 1.
Implementing `IDataErrorInfo` does not by itself take part in validation.
The indexer `this[string columnName]` is called only once a `DataErrorValidationRule` has been added to the binding.
`ValidatesOnDataErrors` is the shorthand that adds that rule, and its default value is `false`.

The activation requirements and the measured default behavior of each approach are as follows.

| Validation approach | Rule recorded on the error | Required setting | Default | Error present on initial display |
| --- | --- | --- | --- | --- |
| Custom `ValidationRule` | The custom class | Add to `Binding.ValidationRules` | Inactive unless added | No by default; yes with `ValidatesOnTargetUpdated="True"` |
| `IDataErrorInfo` | `DataErrorValidationRule` | `ValidatesOnDataErrors="True"` | `False` | Yes |
| `INotifyDataErrorInfo` | `NotifyDataErrorValidationRule` | `ValidatesOnNotifyDataErrors` (on by default) | `True` | Yes |
| Type conversion failure | Internal conversion rule | None | Always active | — |
| Exception thrown by a setter | `ExceptionValidationRule` | `ValidatesOnExceptions="True"` | `False` | — |

`ValidatesOnNotifyDataErrors` is the only one that defaults to `true`.
That is why a view model implementing `INotifyDataErrorInfo` produces a red border even when the binding itself declares nothing.

`INotifyDataErrorInfo` also differs in how the error comes into being.
`NotifyDataErrorValidationRule.Validate` reports success regardless of the value passed to it; in the measured run `IsValid` was `true` even for `null` and an empty string.
The error itself is whatever the view model returns from `GetErrors`, which the binding engine reads and then keeps in step with through `ErrorsChanged` notifications to update `Validation.Errors`.
The rule serves as the marker that appears in `RuleInError` on the resulting `ValidationError`.

Behavior on initial display differs by approach.
`DataErrorValidationRule` and `NotifyDataErrorValidationRule` both reported an error before a single character was typed.
A custom `ValidationRule`, by contrast, was not called when the binding was attached; `Validate` ran only at the first source update.
This difference explains why a custom rule stays silent when a required field is empty at startup.

The property behind the difference is `ValidationRule.ValidatesOnTargetUpdated`, which decides whether the rule also runs when the target is updated, that is, when the binding is established and when the source value changes.
In the measured run, the built-in `DataErrorValidationRule` and `NotifyDataErrorValidationRule` both returned `true`, while the default for a custom `ValidationRule` was `false`.
Specifying `ValidatesOnTargetUpdated="True"` makes a custom rule validate from startup as well: the rule was called immediately after launch and then re-evaluated on every change of the source value.

### Missing adorner layer

This stops at stage 3.
`Validation.ErrorTemplate` does not modify the target control itself; it is drawn on top of it, on the adorner layer.
The most common provider of that layer is `AdornerDecorator`, which the default `ControlTemplate` of `Window` contains.

Replacing the window template with a custom one and omitting `AdornerDecorator` removes the rendering surface.
In the measured run, a template containing only a `ContentPresenter` made `AdornerLayer.GetAdornerLayer` return `null`, and nothing appeared even though `Validation.HasError` was `true`.
Adding a single `AdornerDecorator` to the same template made the layer resolvable and attached one adorner.

| `Window.ControlTemplate` | `Validation.HasError` | `AdornerLayer.GetAdornerLayer` | Red border on screen |
| --- | --- | --- | --- |
| Default | `true` | Resolved | Shown |
| Replaced, without `AdornerDecorator` | `true` | `null` | Not shown |
| Replaced, with `AdornerDecorator` | `true` | Resolved | Shown |

The error is produced and stored correctly, so nothing looks wrong from logs or from `HasError`.
That asymmetry is what makes this cause hard to locate.

The window template is not the only source of a layer.
An `AdornerDecorator` placed anywhere in the visual tree creates a new layer for the subtree beneath it.
The following content sits inside a `Window` whose template omits `AdornerDecorator`.

```xml
<StackPanel>
    <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />

    <AdornerDecorator HorizontalAlignment="Left">
        <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
    </AdornerDecorator>
</StackPanel>
```

Both `TextBox` controls bind to the same property of the same view model and hold the same validation error.
The only difference is whether an `AdornerDecorator` wraps them.

<figure class="article-figure">
  <img src="/images/articles/wpf-validation-error-not-displayed/adorner-layer-required.png" alt="Two TextBox controls stacked vertically. The upper one, which is not wrapped in an AdornerDecorator, keeps its normal border, while the lower one wrapped in an AdornerDecorator is surrounded by a red border." width="355" height="167" loading="lazy">
  <figcaption>Only the lower control, wrapped in an <code>AdornerDecorator</code>, draws a red border. Before the capture, <code>Validation.HasError</code> was confirmed to be <code>true</code> on both and <code>AdornerLayer.GetAdornerLayer</code> to return <code>null</code> for the upper one, so the difference comes from the rendering surface rather than from the error itself (produced on .NET 10 / Windows 11).</figcaption>
</figure>

`AdornerDecorator` is not the only provider.
The `ScrollContentPresenter` inside a `ScrollViewer` carries a layer as well.
In the measured run, a `TextBox` placed inside a `ScrollViewer` drew its red border even under a `Window` template that omits `AdornerDecorator`.
Results therefore diverge between the inside and the outside of a `ScrollViewer` within one screen, and the symptom can surface as "only some fields lack the border".
When the template has been replaced but the symptom does not reproduce, check whether the control sits inside a `ScrollViewer`.

### Not yet validated before a source update

This concerns when stage 1 is reached.
As described earlier, the two built-in rules have `ValidatesOnTargetUpdated` set to `true`, so they are also evaluated when the binding is established and when the source value changes.
Input typed by the user, however, is validated only when the value is transferred from the target to the source.
The default `UpdateSourceTrigger` for `TextBox.Text` is `LostFocus`, so typing alone updates neither the source nor the validation state of that input.
In the measured run, clearing a `TextBox` that started with a valid value left `Validation.HasError` at `false` until focus moved away.

This applies to `IDataErrorInfo` and, as in the implementation below, to an `INotifyDataErrorInfo` that validates inside its setters.
The latter follows `ErrorsChanged` on the view model, but in that arrangement the event is raised by the property setter, and the setter is invoked by the source update.
With `UpdateSourceTrigger=Explicit` the effect is stronger: in the same arrangement the validation state did not change until `UpdateSource` was called.
An implementation that raises `ErrorsChanged` independently of the source update, such as one that reports when an asynchronous lookup completes, is not bound by this.

---

## Solution

Establish each of the three stages explicitly.

1. **Produce the error** — activate the approach in use.
For a view model that owns validation, implement `INotifyDataErrorInfo`.
It is active by default, allows several messages per property, and can report results that are determined later, such as a server lookup.
2. **Secure a rendering surface** — include `AdornerDecorator` when the `Window` template is replaced.
3. **Align the timing** — specify `UpdateSourceTrigger=PropertyChanged` to report results while the user types.

Then supply an `ErrorTemplate` that renders the message, because the default template draws only a red border and never surfaces the contents of `Validation.Errors`.

---

## Implementation

The first piece is a base class that stores validation results.
`INotifyDataErrorInfo` is designed to return a set of messages per property name, so a dictionary keeps the implementation small.

```csharp
// With ImplicitUsings disabled, System and System.Collections.Generic are also required.
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public abstract class ValidatableBase : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> errors = [];

    public bool HasErrors => errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && errors.TryGetValue(propertyName, out List<string>? list)
            ? list
            : Array.Empty<string>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Validate(propertyName!);
    }

    protected void SetErrors(string propertyName, IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            errors.Remove(propertyName);
        }
        else
        {
            errors[propertyName] = [.. messages];
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
    }

    protected abstract void Validate(string propertyName);
}
```

`ErrorsChanged` must be raised when an error is cleared, not only when one is added.
Omitting that leaves the red border in place.
The change notification for `HasErrors` supports binding the enabled state of a save button directly to that property.
Driving `CanExecute` of an `ICommand` instead requires a separate trigger for re-evaluation, such as `CommandManager.InvalidateRequerySuggested`, because the notification alone does not re-query the command ([Fixing a RelayCommand Whose CanExecute Does Not Update the Button State in WPF](/articles/wpf-relaycommand-canexecute-not-updating/)).

The property name passed to `SetErrors` must match the binding path exactly; the behavior on mismatch is covered in the notes.

A derived class only describes the validation of its own properties.

```csharp
public sealed class AccountViewModel : ValidatableBase
{
    private string name = string.Empty;

    public AccountViewModel() => Validate(nameof(Name));

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    protected override void Validate(string propertyName)
    {
        if (propertyName != nameof(Name))
        {
            return;
        }

        List<string> messages = [];
        if (string.IsNullOrWhiteSpace(Name))
        {
            messages.Add("Name is required.");
        }
        else if (Name.Length > 20)
        {
            messages.Add("Name must be 20 characters or fewer.");
        }

        SetErrors(nameof(Name), messages);
    }
}
```

`Validate` is called from the constructor so that `HasErrors` is correct from the initial state, which blocks the save operation immediately after startup.

On the XAML side, define an `ErrorTemplate` that draws the message and apply it through a `Style`.
`AdornedElementPlaceholder` marks where the original control sits, and any decoration can be placed around it.

```xml
<StackPanel Margin="16">
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

    <TextBox Width="240" HorizontalAlignment="Left"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
</StackPanel>
```

The parentheses in `AdornedElement.(Validation.Errors)` denote an attached property, and the trailing `/ErrorContent` refers to the current item of the collection, which by default is the first error.
Running this XAML with an `AccountViewModel` as its `DataContext` rendered `Name is required.` under the `TextBox` right after startup and `Name must be 20 characters or fewer.` once 21 characters were entered; entering a valid value removed the adorner entirely.

The bottom margin in the `Style` reserves space for the message.
Adorners take no part in layout, so without that margin the message overlaps the element below it.

---

## Notes

- **The default `ErrorTemplate` does not display a message.**
It draws only a red border on the adorner layer.
In the measured run, the `ToolTip` of the failing `TextBox` stayed `null`.
Displaying the message requires either replacing the `ErrorTemplate` or setting `ToolTip` from a `Style` trigger on `Validation.HasError`.
- **Adorners do not expand the layout.**
Measuring `ActualHeight` of the parent panel with and without an error produced the same value.
An `ErrorTemplate` that grows vertically therefore overlaps the elements below unless space is reserved in advance.
- **`(Validation.Errors)[0].ErrorContent` reports a binding error when the error clears.**
A path that addresses the first element by index is re-evaluated the moment the collection becomes empty, which logged `System.Windows.Data Error: 17` to the output window.
The display itself clears correctly, so the trace is easy to miss.
Rewriting the path as `/ErrorContent`, which refers to the current item, keeps the same display and produces no trace.
How to read output window messages is covered in [Reading WPF Binding Errors and Diagnosing Them with the Output Window](/articles/wpf-binding-error-debugging-output-window/).
- **`Mode=OneWay` does not validate user input, and a red border once shown never clears.**
`OneWay` has no target-to-source transfer, so typed input never becomes subject to validation.
That does not mean validation never runs.
Through `ValidatesOnTargetUpdated` described above, the rules were evaluated under `OneWay` both when the binding was established and when the source property changed, producing a red border for an invalid value.
The problem is that no user action clears that border: typing a valid string into a `TextBox` bound with `OneWay` left `Validation.HasError` at `true`.
A field switched to `OneWay` for display purposes therefore keeps its error indication permanently.
- **Assigning to the target of a `OneWay` binding from code removes the binding.**
After a plain assignment to `TextBox.Text`, `BindingOperations.GetBinding` returned `null` and the red border disappeared.
`OneTime` behaves the same way.
User input and `SetCurrentValue` both keep the binding intact, so only assignment from code produces this result.
- **An `ErrorsChanged` property name that differs from the binding path suppresses the display.**
Raising `ErrorsChanged` with `Namee` while the binding path was `Name` left `Validation.HasError` at `false` even though `HasErrors` was `true`.
Using `nameof` instead of string literals prevents this.
- **Validation results settled asynchronously must be applied on the UI thread.**
The binding engine subscribes to `ErrorsChanged` to update `Validation.Errors`, so that notification has to be raised on the UI thread.
When results are settled by background work, move the whole `SetErrors` call — the dictionary update and both notifications — onto the UI thread through the `Dispatcher`.
- **The `Validation.Error` attached event is not raised by default.**
Handling errors outside the visual layer, for logging or for blocking navigation, requires `Binding.NotifyOnValidationError` to be `True`.
The default is `False`, and without it the handler is never invoked.
- **Exceptions thrown by a setter are swallowed by default.**
Feeding an invalid value to a view model whose setter throws `ArgumentOutOfRangeException` left `Validation.HasError` at `false` without `ValidatesOnExceptions`, and the value was not updated.
With `ValidatesOnExceptions="True"`, `ExceptionValidationRule` caught the exception and `Exception.Message` became the error content.
Messages from the `ArgumentException` family carry a suffix such as `(Parameter 'value')`, which is rarely acceptable as user-facing text.
- **Only type conversion failures surface without any setting.**
Typing `abc` into a `TextBox` bound to an `int` property produced an error with neither `ValidatesOnExceptions` nor any validation rule in place, because the binding engine treats a conversion failure as a validation error.
That message is generated by the framework and localized to the running UI language, appearing as `Value 'abc' could not be converted.` under `en-US`, so replacing it with domain wording requires a custom `ValidationRule` or an `IValueConverter`.
- **A custom `ValidationRule` receives the value before conversion.**
The default `ValidationStep` is `RawProposedValue`, and `Validate` received a `string` even though the binding targeted an `int` property.
Validating it as a number requires parsing inside the method or setting `ValidationStep="ConvertedProposedValue"`.
- **`DataGrid` cells behave differently depending on the column type.**
For columns where the framework creates the editing control at run time, such as `DataGridTextColumn`, the official documentation states that `Validation.ErrorTemplate` cannot be used the way it is with simple controls and that no dedicated error template exists for cells. Feedback then goes through `DataGridBoundColumn.EditingElementStyle` per cell and `DataGrid.RowValidationErrorTemplate` per row.
A `DataGridTemplateColumn`, by contrast, carries a hand-written editing control in `CellEditingTemplate`, and setting `Validation.ErrorTemplate` on that control drew the message inside the cell being edited. `EditingElementStyle` is a member of `DataGridBoundColumn` and does not exist on this column type.
Switching controls between display and edit modes is covered in [Switching Controls Between Display and Edit Modes in WPF DataGrid Cells](/articles/wpf-datagrid-cell-editing-template/).
- **Validation timing follows `UpdateSourceTrigger`.**
Whether results appear while typing or after focus leaves is a decision about update timing, not about presentation.
The differences between the values are covered in [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/).
Driving the update from the view when `Explicit` is chosen is covered in [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/).

---

## Alternatives / Comparison

The four approaches that produce errors compare as follows.

| Approach | Activation | Multiple messages per property | Deferred or async validation | Best suited for |
| --- | --- | --- | --- | --- |
| Custom `ValidationRule` | Add to `ValidationRules` | No (only one entry reaches `Validation.Errors`) | No | Input format checks kept inside the view |
| `IDataErrorInfo` | `ValidatesOnDataErrors="True"` | No (a single `string`) | No | Existing code already built on `IDataErrorInfo` |
| `INotifyDataErrorInfo` | Active by default | Yes | Yes, reported later through `ErrorsChanged` | New implementations where the view model owns validation |
| `ValidatesOnExceptions` | `ValidatesOnExceptions="True"` | No | No | Domain models that enforce invariants in setters |

Registering several custom `ValidationRule` objects still yields at most one entry in `Validation.Errors`, because no rule is evaluated after an earlier one fails.
Carrying several messages in a single error is possible instead, since `ValidationResult.ErrorContent` is typed as `object` and accepts a collection.
Doing so requires a display side that can render a collection, such as an `ItemsControl`; binding `ErrorContent` to `TextBlock.Text` as the implementation above does would show the type name.

Being active by default is an advantage of `INotifyDataErrorInfo` and, at the same time, a path for unintended validation.
Once a view model base class implements the interface, validation is enabled even where nothing is written on the individual bindings.
Disabling it for a specific binding requires an explicit `ValidatesOnNotifyDataErrors="False"`.

Several approaches can be active at once.
Combining `IDataErrorInfo` with `INotifyDataErrorInfo` accumulated two entries in `Validation.Errors` in the measured run.
Adding a custom rule at the `RawProposedValue` step to that configuration and making it fail, however, removed the `DataErrorValidationRule` entry.
The `UpdatedValue` step sits later on the target-to-source validation path, and the path stops at the first failing step.
`NotifyDataErrorValidationRule` occupies the same `UpdatedValue` step, yet its entry survived, because that error is supplied from the state the view model holds through `ErrorsChanged` and is therefore maintained independently of that path.
An `ErrorTemplate` that shows only the first entry therefore depends on this evaluation order for which message appears, so keeping to a single approach is easier to reason about unless the display requirements demand otherwise.

---

## Summary

Start from the value of `Validation.GetHasError` when an error fails to appear.

- **`false`** — either no validation rule is associated with the binding, or the typed input has not reached the source yet.
Check `ValidatesOnDataErrors="True"` for `IDataErrorInfo`, and the entry in `ValidationRules` for a custom rule.
Specify `UpdateSourceTrigger=PropertyChanged` to react while the user types.
- **`true` with nothing on screen** — the rendering surface is missing.
Include `AdornerDecorator` if the `Window` template has been replaced.
- **A red border but no message** — that is the behavior of the default `ErrorTemplate`.
Supply a custom template containing `AdornedElementPlaceholder` and display `(Validation.Errors)/ErrorContent`.

Choose the approach on these criteria.
For new implementations where the view model owns validation, adopt `INotifyDataErrorInfo`.
Use a custom `ValidationRule` only when an input format check belongs inside the view, and combine `ValidatesOnExceptions` when the domain model enforces its invariants in setters.
For existing code built on `IDataErrorInfo`, check first that `ValidatesOnDataErrors="True"` has not been omitted.
