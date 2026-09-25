---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/checkbox.html
title: "CheckBox"
badge: "Inputs"
lead: "CheckBox lets the user turn an option on or off. With <code>IsThreeState</code>, it also has a third, undetermined state."
description: "WPF CheckBox measured on .NET 10: three-state toggling, IsChecked bindings, keys, and label layout, and why null bound to a bool gives a binding error."
---

## Overview

**CheckBox** derives from `ToggleButton`. Its state is `IsChecked`, a `bool?` that binds two-way by default: `true` is checked, `false` unchecked, and `null` undetermined. The `Checked`, `Unchecked`, and `Indeterminate` events bubble. A single `Checked` handler on a parent `StackPanel` received the change of a child CheckBox, which is useful when check boxes are created at run time.

The whole control is clickable, not only the box: a hit test in the middle of the label found the label's `TextBlock` inside the CheckBox. A focused CheckBox also toggled when Space was pressed and released.

The demo app has a section for each property below. The "Show Code" link under each section displays its XAML.

## Screen Preview

![The CheckBox page of the demo app, with the control list on the left and the first section, IsChecked(ToggleButton)](/images/wpf-standard-control-demo/checkbox.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `IsChecked (ToggleButton)` | `bool? (Null / False / True)` | The state of the check box. Bind it to a `bool?` property to keep all three states. A `bool` property is not converted from `null`: with `IsThreeState`, clicking a CheckBox bound to a `bool` source from `true` to `null` caused a binding error, and the source kept `true`. The demo app shows three check boxes starting at `null`, `False`, and `True`. The first one is not three-state, so after one click it cannot return to `null`: clicks took it from `null` to `false`, `true`, and `false`. |
| `IsThreeState (ToggleButton)` | `bool` | Whether clicks go through the undetermined state. With `True`, clicks went `false` → `true` → `null` → `false`. With the default `False`, they went `false` → `true` → `false`. The setting only affects clicks: `IsChecked` can be set to `null` from code or XAML either way, as the demo app's first `IsChecked` example does. |
| `VerticalContentAlignment (Control)` | `Top / Center / Bottom / Stretch` | Where the box and the label are placed vertically inside the CheckBox. The default is `Top`, from the property's default value. With a label that wrapped to three lines, `Top` put the box next to the first line (y = 1, the label at y = -1). `Center` centered both in the CheckBox, so the box lined up with the middle of the label whatever the CheckBox's height: the centers were both at about 100 in a CheckBox 200 high, and at 23.44 and 22.94 in one 46.88 high, as high as the label. The demo app lets you pick the value from a list. |

## XAML Example

The following XAML is the `IsThreeState` section of the demo app (`CheckBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `TargetNullValue` shows "(Null)" for the undetermined state:

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

## Common Use Cases

- **Settings:** independent on/off options, each bound to a `bool` property.
- **Filters:** several categories that can be combined.
- **Select all:** a three-state check box whose `null` means that only some items are selected.
- **Consent:** a check box that enables a button once the user agrees. With the button's `IsEnabled` bound to the check box's `IsChecked`, the button went from disabled to enabled on a real click on the check box.

## Tips and Best Practices

- **Use `bool?` whenever `null` can occur.** A `bool` source rejects it with a binding error instead of turning it into `false`.
- **For a "select all" box, decide what a click on the mixed state means.** A three-state CheckBox moves from `null` to `false` on the next click, so a click on "some selected" clears everything unless the ViewModel handles it differently.
- **Handle `Checked` on a parent** when check boxes are generated in a list; the events bubble.
- **Use `VerticalContentAlignment="Center"` for labels that wrap** if the box should sit beside the middle of the label.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`CheckBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/CheckBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Clicks were reproduced with UI Automation's `Toggle`, which runs the same toggle handling as a click, and the Space key by sending key-down and key-up events to a displayed window. The click on the consent check box was made with the real mouse. Positions are the values on the measuring machine.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/checkbox/checkbox-behavior.svg" alt="Table of CheckBox results: it derives from ToggleButton, IsChecked binds two-way by default, the events bubble, VerticalContentAlignment defaults to Top, three-state clicks cycle false, true, null, a bool source rejects null with a binding error, Space toggles the box, the label is part of the clickable area, and Center centers the box and the label in the CheckBox, lining the box up with the middle of a wrapped label, and a button whose IsEnabled is bound to IsChecked becomes enabled on a real click on the check box" width="1140" height="590" loading="lazy">
  <figcaption>States, bindings, keyboard, hit testing, and layout. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [RadioButton](/apps/wpf-standard-control-demo/radiobutton.html) — also a `ToggleButton`, for choosing one option of several.
- [ToggleButton](/apps/wpf-standard-control-demo/togglebutton.html) — the base class, a button that stays pressed.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View CheckBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/CheckBoxUsage){: target="_blank" rel="noopener noreferrer"}
