---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/checkbox.html
title: "CheckBox"
badge: "Inputs"
lead: "CheckBox lets the user turn an option on or off. With <code>IsThreeState</code>, it also has a third, undetermined state."
description: "WPF CheckBox measured on .NET 10: three-state toggling, IsChecked bindings, keys, and label layout, and why null bound to a bool gives a binding error."
---

## Three states, and the order clicks go through them

**CheckBox** derives from `ToggleButton`. Its state is `IsChecked`, a `bool?`: `true` is checked, `false` unchecked, and `null` undetermined. `IsThreeState` decides whether clicks go through the undetermined state. With `True`, clicks went `false` → `true` → `null` → `false`. With the default `False`, they went `false` → `true` → `false`.

The setting only affects clicks: `IsChecked` can be set to `null` from code or XAML either way. The demo app shows three check boxes starting at `null`, `False`, and `True`. The first one is not three-state, so after one click it cannot return to `null`: clicks took it from `null` to `false`, `true`, and `false`.

A three-state "select all" box follows the same order. It moves from `null` to `false` on the next click, so a click on "some selected" clears everything unless the ViewModel handles it differently. Decide what that click should mean.

## Binding IsChecked: why null needs bool?

`IsChecked` binds two-way by default. Bind it to a `bool?` property to keep all three states. A `bool` property is not converted from `null`: with `IsThreeState`, clicking a CheckBox bound to a `bool` source from `true` to `null` caused a binding error, and the source kept `true`. Use `bool?` whenever `null` can occur.

Other controls can follow `IsChecked` directly. With a button's `IsEnabled` bound to a check box's `IsChecked`, the button went from disabled to enabled on a real click on the check box, which is how a consent box enables the button it guards.

## Where clicks and events go

The whole control is clickable, not only the box: a hit test in the middle of the label found the label's `TextBlock` inside the CheckBox. A focused CheckBox also toggled when Space was pressed and released.

The `Checked`, `Unchecked`, and `Indeterminate` events bubble. A single `Checked` handler on a parent `StackPanel` received the change of a child CheckBox, which is useful when check boxes are created at run time, for example in a list.

## Lining the box up with a label that wraps

`VerticalContentAlignment` decides where the box and the label are placed vertically inside the CheckBox. The default is `Top`, from the property's default value. With a label that wrapped to three lines, `Top` put the box next to the first line (y = 1, the label at y = -1). `Center` centered both in the CheckBox, so the box lined up with the middle of the label whatever the CheckBox's height: the centers were both at about 100 in a CheckBox 200 high, and at 23.44 and 22.94 in one 46.88 high, as high as the label.

{% include tables/wpf-standard-control-demo/verification/checkbox/checkbox-behavior.en.md %}

States, bindings, keyboard, hit testing, and layout. Measured on .NET 10 / Windows 11.
{: .table-caption}

## Trying it in the demo app

![The CheckBox page of the demo app, with the control list on the left and the first section, IsChecked(ToggleButton)](/images/wpf-standard-control-demo/checkbox.png){: .screenshot-img}

The CheckBox page of the demo app has sections for `IsChecked`, `IsThreeState`, and `VerticalContentAlignment`, which can be picked from a list. The "Show Code" link under each section displays its XAML. The following XAML is the `IsThreeState` section (`CheckBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `TargetNullValue` shows "(Null)" for the undetermined state:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsThreeStateTrueCheckBox" HorizontalAlignment="Center"
            VerticalContentAlignment="Center" Content="CheckBox" IsThreeState="True" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateTrueCheckBox, TargetNullValue=(Null)}" />

  <CheckBox x:Name="IsThreeStateFalseCheckBox" HorizontalAlignment="Center"
            VerticalContentAlignment="Center" Content="CheckBox" IsThreeState="False" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateFalseCheckBox}" />
</StackPanel>
```

## Related controls and articles

- [RadioButton](/apps/wpf-standard-control-demo/radiobutton.html) — also a `ToggleButton`, for choosing one option of several.
- [ToggleButton](/apps/wpf-standard-control-demo/togglebutton.html) — the base class, a button that stays pressed.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`CheckBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/CheckBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Clicks were reproduced with UI Automation's `Toggle`, which runs the same toggle handling as a click, and the Space key by sending key-down and key-up events to a displayed window. The click on the consent check box was made with the real mouse. Positions are the values on the measuring machine.

[View CheckBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/CheckBoxUsage){: target="_blank" rel="noopener noreferrer"}
