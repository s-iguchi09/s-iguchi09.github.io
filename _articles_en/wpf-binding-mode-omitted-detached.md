---
layout: article-en
title: "How User Input Silently Removes a WPF Binding Written Without Mode"
seo_title: "WPF Bindings Without Mode Removed by User Input"
date: 2026-10-06
category: WPF
excerpt: "Bindings to TreeViewItem.IsExpanded or ColumnDefinition.Width without Mode break after the expander button or a GridSplitter is used. Mode=TwoWay keeps them."
image: /images/articles/wpf-binding-mode-omitted-detached/binding-mode-user-input.svg
---

## Overview

Keeping the open state of `TreeView` nodes in a view model and binding `IsExpanded` through `ItemContainerStyle` is a common MVVM pattern.
When the binding is written without `Mode`, however, a node that the user opened once with the expander button no longer follows the view model.
No exception is thrown, and no binding error appears in the Output window.

The cause is a combination of two things.
First, the direction of a binding written without `Mode` follows the default of the target property, and the default of `TreeViewItem.IsExpanded` is one-way.
Second, the expander button writes to `IsExpanded` through a binding inside the template, and that write stops the one-way binding from working.

This article measures the default direction of properties that are often bound without `Mode`, and what happens to the binding when real mouse and keyboard input is used.
It then shows the conditions under which the binding is removed, and how to write the binding so that it stays.

---

## Prerequisites / Environment

- Framework: WPF (.NET Framework 4.0 or later / .NET Core 3.0 or later)
- Scope: `{Binding}` without `Mode`, `TreeViewItem.IsExpanded`, `ColumnDefinition.Width` with `GridSplitter`, `Expander.IsExpanded`, and similar properties
- Architecture: MVVM (the view model implements `INotifyPropertyChanged`)
- Verification environment: .NET 10 / Windows 11 (default theme; the Fluent theme was not measured)
- Measurement: Each condition was operated once with operating system mouse and keyboard input (`SendInput`).
  After each input, `BindingOperations.GetBindingExpressionBase`, `DependencyPropertyHelper.GetValueSource`, and the source value were read.
  A value was then assigned to the source (raising a change notification) and the displayed value was read, and the binding errors and warnings raised during the input were counted through `PresentationTraceSources.DataBindingSource`.
  The measurement is implemented as a scene in `tools/screenshot-capture`.

---

## Symptom

A folder tree is displayed, and the open state of each node is bound to `IsExpanded` in the view model.
The `Binding` in the `Setter` has no `Mode`.

```xml
<TreeView x:Name="folderTree" ItemsSource="{Binding Folders}">
  <TreeView.ItemContainerStyle>
    <Style TargetType="TreeViewItem">
      <Setter Property="IsExpanded" Value="{Binding IsExpanded}" />
    </Style>
  </TreeView.ItemContainerStyle>
  <TreeView.ItemTemplate>
    <HierarchicalDataTemplate ItemsSource="{Binding Children}">
      <TextBlock Text="{Binding Name}" />
    </HierarchicalDataTemplate>
  </TreeView.ItemTemplate>
</TreeView>
```

Changing `IsExpanded` in the view model opens and closes the node.
A node opened with the expander button, however, does not close when the view model then sets `IsExpanded` to `false`.
The following table shows what happened to the binding after each input.
Rows 1 to 8 use this XAML (rows 6 to 8 add `Mode=TwoWay`), rows 9 to 16 are other setups for comparison, and row 17 checks how errors are counted.

{% include tables/articles/wpf-binding-mode-omitted-detached/binding-mode-user-input.en.md %}

Measured on .NET 10 / Windows 11 (default theme) by operating each condition once with real mouse and keyboard input. Row 4 set the value from code without real input, and rows 5 and 10 called `ClearValue` from code after the click. The `GridSplitter` was dragged 60 DIP (the WPF unit that does not depend on display scaling). "Binding after the input" is whether `BindingOperations.GetBindingExpressionBase` returned null, and "removed" includes a `Setter` binding hidden by a local value. The source starts at `False` (100 in the `ColumnDefinition` rows), and "displayed after the source is assigned" is the value after the source was assigned its value from before the input (in rows 5 and 10 only, after `True` was assigned to the source). "Value source after the input" is the `BaseValueSource` from `DependencyPropertyHelper.GetValueSource`; "expression" means `IsExpression` is `True`, and "current" means `IsCurrent` is `True`. "Binding errors" counts the errors and warnings written to `PresentationTraceSources.DataBindingSource` during the input, and the last row confirms that a binding to a missing path is counted as 1 by this method.
{: .table-caption}

In row 1, where the expander button was clicked, the display became `True` but the source stayed `False`.
The binding stopped working, and assigning `False` to the source again left the display at `True`.
There were no binding errors, so the Output window gives no hint.

---

## What Happens Internally

### Without Mode, the Property Decides the Direction

A `Binding` without `Mode` uses `BindingMode.Default`, and its direction is decided by the metadata of the target property ([BindingMode](https://learn.microsoft.com/dotnet/api/system.windows.data.bindingmode)).
If `BindsTwoWayByDefault` in the metadata is `True`, the binding is two-way; if it is `False`, the binding is one-way.
The following table shows the metadata of properties that are often bound without `Mode`.

{% include tables/articles/wpf-binding-mode-omitted-detached/binding-mode-defaults.en.md %}

Values read on .NET 10 from the `FrameworkPropertyMetadata` returned by `GetMetadata` for each property. Rows with a type in parentheses were read for that type; the other rows were read for the type at the start of the name.
{: .table-caption}

On the same `TreeViewItem`, `IsSelected` is `True` and `IsExpanded` is `False`.
`Expander.IsExpanded` is `True`, so the default direction of "open and close" differs between controls.
`ColumnDefinition.Width` and `RowDefinition.Height` were also `False`.
`DefaultUpdateSourceTrigger` was `LostFocus` only for `TextBox.Text`, and `PropertyChanged` for the others.

### The Expander Button Writes Through a Binding in the Template

The expander button is a `ToggleButton` in the default template of `TreeViewItem`.
How its `IsChecked` is bound to `IsExpanded` was read from the template.

{% include tables/articles/wpf-binding-mode-omitted-detached/binding-mode-template-buttons.en.md %}

Values read on .NET 10 / Windows 11 (default theme) with `BindingOperations.GetBinding` for `IsChecked` of the `ToggleButton` inside a `TreeViewItem` and an `Expander` whose templates were applied.
{: .table-caption}

`IsChecked` of the expander button is bound to `IsExpanded` of the `TreeViewItem` that the template is applied to (`TemplatedParent`), with `{Binding IsExpanded}` and no `Mode`.
The default of `ToggleButton.IsChecked` is two-way (the "ToggleButton.IsChecked" row of the defaults table above), so this binding works in both directions.

Pressing the button makes this binding write a value to `TreeViewItem.IsExpanded`.
Row 4, which set `IsChecked` of the button to `True` from code without any real click, gave the same result as the click.
The result of the click is reproduced by a write along this path alone.

From the point of view of `TreeViewItem.IsExpanded`, this write is an ordinary set of a value (a local value).
As row 1 of the table shows, when a value was set on a property with a one-way binding, the value source became `Local` and the binding stopped working.

### A Setter Binding Is Hidden; a Binding Written on the Element Is Replaced

How the binding stops working depends on where it is written.

A binding in an `ItemContainerStyle` `Setter` has lower precedence than a local value.
Once a local value is set, the `Setter` binding is hidden.
In row 5, where the local value was removed with `ClearValue` after the click, the value source went back to "Style, expression".
Setting the source to `True` afterward made the display `True` as well.
The `Setter` binding had not been removed; it had only been hidden.

With `IsExpanded="{Binding Value}"` written directly on a `TreeViewItem`, the binding itself sits at the local value level.
The value written by the click replaced the binding (row 9), and after `ClearValue` the value source became `Default`, and the display stayed `False` even when the source was set to `True` (row 10).

### When the Target Is Two-Way, the Value Goes to the Source

In row 6, written with `Mode=TwoWay`, the value source stayed "Style, expression" after the same click, and the source was updated to `True`.
A write to a property with a two-way binding went through the binding to the source, and the binding kept working.

### The Right Arrow Key and Double-Click Write with SetCurrentValue

In rows 2 and 3, opened with the right arrow key and a double-click on the header, the binding stayed.
The value source was "Style, expression, current", with `IsCurrent` set to `True`.
This shows that the value was set with `SetCurrentValue` ([ValueSource.IsCurrent](https://learn.microsoft.com/dotnet/api/system.windows.valuesource.iscurrent)).
`SetCurrentValue` changes only the effective value without changing the value source, and keeps the binding ([DependencyObject.SetCurrentValue](https://learn.microsoft.com/dotnet/api/system.windows.dependencyobject.setcurrentvalue)).

The binding is still one-way, however, so the source was not updated.
When the view model raised a change notification for `IsExpanded` (even with the value still `False`), the display was set back to the source value, and the node that the user opened closed.
In rows 7 and 8, written with `Mode=TwoWay`, the same input updated the source to `True`.
With a two-way binding the value went to the source, and the value source was not marked "current".

---

## Consequences of the Write Path

**The same property gives different results depending on the input path.**
Among the real inputs to `TreeViewItem.IsExpanded` (the expander button, the right arrow key, and a double-click on the header), only a click on the expander button removed the binding.
The right arrow key and a double-click on the header did not.
Trying a reported defect with the right arrow key or a double-click does not reproduce it.

**Whether the binding is removed depends on the actual direction of the binding being written to.**
`Expander.IsExpanded` defaults to `True`; without `Mode`, the binding stayed after a click and the source was updated (row 11).
In row 12, with `Mode=OneWay` written on the same `Expander`, the value source became `Local` and the source stayed `False`, even though the template binds the header with `Mode=TwoWay` (see the template table above).
Without `Mode`, that direction is the default of the property.
`MenuItem.IsChecked` and `IsChecked` of a `CheckBox` also default to `True`, and the binding stayed without `Mode` (rows 13 and 14).

**The same happens for a property that is one-way by default and that the input writes with an ordinary set.**
`ColumnDefinition.Width` defaults to `False`; dragging the `GridSplitter` removed the binding, and the source stayed 100 (row 15).
With `Mode=TwoWay`, the binding stayed and the source was updated to 160 (row 16).
`RowDefinition.Height`, `DatePicker.Text`, `Window.Left`, and others also default to `False`, but the results of operating them were not measured in this article.

---

## Implementation Example

### Write Mode=TwoWay on Properties That the User Changes

For `TreeViewItem.IsExpanded`, `Mode=TwoWay` is written on the `Setter` binding.
The `Setter` in the XAML from the "Symptom" section becomes the following.

```xml
<Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}" />
```

Whether the node was opened with the expander button, the right arrow key, or a double-click on the header, `IsExpanded` in the view model became `True` (rows 6 to 8).
Setting it to `False` from the view model afterward closed the node.

A column resized with a `GridSplitter` also gets `Mode=TwoWay`.
The type of `ColumnDefinition.Width` is `GridLength`, so the view model property is a `GridLength` as well.

```xml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="{Binding NavigationWidth, Mode=TwoWay}" />
    <ColumnDefinition Width="Auto" />
    <ColumnDefinition />
  </Grid.ColumnDefinitions>
  <GridSplitter Grid.Column="1" Width="6" HorizontalAlignment="Center" />
</Grid>
```

`HorizontalAlignment` of the `GridSplitter` is set to `Center` because the default `Right` changes which pair of columns is resized (measured on the [GridSplitter demo page](/apps/wpf-standard-control-demo/gridsplitter.html)).
Dragging in this setup updated the width in the view model (row 16).

### Checking Whether the Binding Is Still Attached

A removed binding raises no error, so checking it requires reading it in code.
The following class has a method that returns the default direction of a property, and a method that writes the state of the binding and where the value comes from.
The measurement in this article reads the values with the same APIs.

```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;

public static class BindingReport
{
    // The metadata default that decides the direction of a binding written without Mode.
    // Passing the instance returns the metadata overridden for its type.
    public static bool BindsTwoWayByDefault(DependencyObject target, DependencyProperty property) =>
        property.GetMetadata(target) is FrameworkPropertyMetadata metadata && metadata.BindsTwoWayByDefault;

    // Whether a binding is attached, and where the value comes from (written to the Output window in Debug builds).
    public static void Write(DependencyObject target, DependencyProperty property)
    {
        BindingExpressionBase expression = BindingOperations.GetBindingExpressionBase(target, property);
        ValueSource source = DependencyPropertyHelper.GetValueSource(target, property);
        Debug.WriteLine(
            $"{property.Name}: binding {(expression == null ? "none" : "attached")}, " +
            $"two-way by default {BindsTwoWayByDefault(target, property)}, " +
            $"{source.BaseValueSource}" +
            (source.IsExpression ? ", expression" : "") +
            (source.IsCurrent ? ", current" : ""));
    }
}
```

For a `TreeViewItem`, the container is obtained with an `ItemContainerGenerator` and passed in.
Top-level nodes come from the `ItemContainerGenerator` of the `TreeView`, while child nodes come from the `ItemContainerGenerator` of their parent `TreeViewItem`.

```csharp
var item = (TreeViewItem)folderTree.ItemContainerGenerator.ContainerFromItem(folder);
BindingReport.Write(item, TreeViewItem.IsExpandedProperty);
```

`binding none` with `Local` means the binding was replaced by a local value, a `Setter` binding is hidden by one, or there was no binding to begin with (in the table, rows 1 and 9, for example, appeared this way).
`binding none` with `Default` is, for example, the state after `ClearValue` on a replaced binding (row 10).
A property with neither a binding nor a value set from the start also appears this way.
`expression` with `current` means the binding is still attached, but the value was changed with `SetCurrentValue`.

---

## Caveats

- **A one-way binding that only makes the display follow the view model does not hold for nodes that the user can open and close.**
  With the expander button the binding stopped working, and with the right arrow key the display was set back by the view model's notification.
  A value that the user changes should be received by the view model through a two-way binding.
- **The right arrow key and a double-click do not reproduce it.**
  Among the real inputs to `TreeViewItem.IsExpanded`, only a click on the expander button removed the binding.
  A reported defect should be checked by clicking the expander button.
- **A `Setter` binding is hidden by a local value.**
  A `Style` `Setter` has lower precedence than a local value, so once a local value is set, the `Setter` no longer applies.
  Value precedence is covered in [Why WPF Style Triggers and DataTriggers Do Not Apply — Dependency Property Value Precedence](/articles/wpf-style-trigger-not-working-local-value/).
- **An assignment from code also stops a one-way binding.**
  That assigning to `IsSelected` of a container hides a one-way `Setter` binding behind a local value is measured in [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/).
  In row 4 of this article, the value set from code on `IsChecked` of the expander button was written to `IsExpanded` through the template binding, and the `Setter` binding was hidden in the same way.
- **Writing `Mode` makes the direction readable even when the default is `True`.**
  A binding without `Mode` works two-way on a property whose default is `True`, and it was not removed in this measurement.
  Because some properties such as `IsExpanded` have a counterintuitive default, writing `Mode=TwoWay` on values that the user changes makes the direction visible in the XAML.

---

## Summary

A binding written without `Mode` is removed by user input when the following two conditions are both met.

- `BindsTwoWayByDefault` of the target property is `False` (`TreeViewItem.IsExpanded`, `ColumnDefinition.Width`, and others).
- The user input writes to that property with an ordinary set of a value (the expander button, a `GridSplitter`).

A binding written on the element is then replaced, and a `Setter` binding is hidden; either way, the binding stops working.
The source is not updated, and no error is raised.
When the view model holds a value that the user changes, `Mode=TwoWay` should be written instead of relying on the default of the property.
Whether the binding has been removed can be checked with `BindingOperations.GetBindingExpressionBase` and `DependencyPropertyHelper.GetValueSource`.

---

## Related Articles

- [Selecting and Expanding a WPF TreeView Node from Code, and Why SelectedItem Is Read-Only](/articles/wpf-treeview-select-item-programmatically/)
- [Why WPF Style Triggers and DataTriggers Do Not Apply — Dependency Property Value Precedence](/articles/wpf-style-trigger-not-working-local-value/)
- [Binding to a WPF UserControl's Own Dependency Property from Inside the Control](/articles/wpf-usercontrol-dependencyproperty-binding-not-working/)
- [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/)
- [TreeView (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/treeview.html): the default directions of `IsExpanded` and `IsSelected`, and the binding removed by the expander button
- [GridSplitter (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/gridsplitter.html): the default `HorizontalAlignment` and which pair of columns is resized
