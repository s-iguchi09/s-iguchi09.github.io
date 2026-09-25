---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/radiobutton.html
title: "RadioButton"
badge: "Inputs"
lead: "RadioButton lets the user pick one option out of a group. Checking one button unchecks the others in its group. Which buttons form a group depends on GroupName."
description: "WPF RadioButton measured on .NET 10: which buttons form a group with and without GroupName, how bool bindings stay in sync, and what the arrow keys and Tab do."
---

## Overview

**RadioButton** derives from `ToggleButton`. Unlike a ToggleButton, clicking a checked RadioButton does not uncheck it: `IsChecked` stayed `True`. Only checking another button in the group clears it.

Without `GroupName`, a group is the buttons with the same parent element. Three buttons in one StackPanel formed a group, and buttons in another StackPanel formed a separate one. This breaks when each button has its own parent. Buttons wrapped in a `Border` each could all be checked at once. So could buttons created by an `ItemsControl`'s `ItemTemplate`, whose `Parent` was `null`. Give such buttons a `GroupName`.

The keyboard moves the focus, not the selection. With the first button checked and focused, the Down arrow moved the focus to the second button, but the first stayed checked. <kbd>Tab</kbd> also went to the next button of the same group, not to the next group.

The demo app has two sections, `GroupName` and `VerticalContentAlignment`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![The RadioButton page of the demo app, with the control list on the left and the first section, GroupName](/images/wpf-standard-control-demo/radiobutton.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `GroupName` | `string` | The name of the group. The demo app shows six buttons without it, and six with the names `1` and `2`. Buttons with the same name form one group even under different parents: two buttons in two different GroupBoxes unchecked each other. The group does not reach beyond the window. The same name in another window and in a `Popup` formed separate groups, and all three buttons stayed checked. Named and unnamed buttons do not affect each other: under the same parent, checking the unnamed ones did not uncheck the one with `GroupName="1"`. |
| `VerticalContentAlignment (Control)` | `Top / Center / Bottom / Stretch` | Where the round mark and the content are placed vertically inside the RadioButton. The default is `Top`, from the property's default value; it stayed `Top` after the default style was applied. With a label that wraps to three lines, `Top` put the mark at the top of the label. `Center` centered both in the RadioButton, so the mark lined up with the middle of the label whatever the RadioButton's height: the centers were both at about 100 in a RadioButton 200 high, and at 23.44 and 22.94 in one 46.88 high, as high as the label. The demo app chooses the value from a combo box. |

## Binding IsChecked

`IsChecked` binds two-way by default. With three buttons bound to three `bool` properties, clicking the second button set the properties to `A=False, B=True, C=False`. The first button's binding was still in place, so there is no need to clear the other properties by hand. Setting `C` to `true` in the source checked the third button and set `B` to `false`.

Binding the buttons to one enum property through a converter is another way. The article linked below measures what the converter's `ConvertBack` should return, and how `GroupName` affects the initial selection.

## XAML Example

The following XAML is the named groups in the `GroupName` section of the demo app (`RadioButtonUsageControl.xaml`), with the styles, the surrounding GroupBoxes, and the alignment attributes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RadioButton x:Name="GroupName1ARadioButton" Content="A(Group:1)" GroupName="1" IsChecked="True" />
  <RadioButton x:Name="GroupName1BRadioButton" Content="B(Group:1)" GroupName="1" />
  <RadioButton x:Name="GroupName1CRadioButton" Content="C(Group:1)" GroupName="1" />
  <RadioButton x:Name="GroupName2DRadioButton" Content="D(Group:2)" GroupName="2" IsChecked="True" />
  <RadioButton x:Name="GroupName2ERadioButton" Content="E(Group:2)" GroupName="2" />
  <RadioButton x:Name="GroupName2FRadioButton" Content="F(Group:2)" GroupName="2" />
</StackPanel>
```

## Common Use Cases

- **Settings:** one choice out of a few, such as Light, Dark, or System.
- **Forms:** a question with one valid answer.
- **Filters:** All, Active, or Completed, where choosing one replaces the others.

## Tips and Best Practices

- **Set `GroupName` when the buttons do not share a parent.** This includes buttons in an item template or wrapped one by one in another element.
- **Use a name that is unique in the window.** The same name in two sections of one window makes them one group.
- **Do not rely on the same name to link windows or popups.** Each of them has its own groups.
- **Check one button at the start.** A group starts with nothing selected unless one button is set to `IsChecked="True"`.
- **Set `VerticalContentAlignment="Center"` for labels of several lines.** The default `Top` leaves the mark at the first line.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`RadioButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/RadioButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Clicks were made through the button's click handling (`OnClick`). The arrow keys and <kbd>Tab</kbd> were sent through WPF's input manager, the same path as keys typed on a keyboard.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/radiobutton/radiobutton-groups.svg" alt="Table of which RadioButtons form a group: a checked button stays checked when clicked, buttons without GroupName group by parent, buttons wrapped in Borders or created by an ItemTemplate can all be checked, unnamed and named buttons do not affect each other, the same GroupName links two GroupBoxes, and the same GroupName in another window and in a Popup does not" width="959" height="350" loading="lazy">
  <figcaption>Which buttons uncheck each other. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/radiobutton/radiobutton-binding-keys.svg" alt="Table of RadioButton bindings and keys: with IsChecked bound to three bools, clicking B sets A and C to false and keeps the bindings, setting C in the source unchecks B, the Down arrow moves the focus but not the check, Tab goes to the next button, VerticalContentAlignment defaults to Top, and Center centers the mark and the label in the RadioButton, lining the mark up with the middle of a three-line label" width="1061" height="320" loading="lazy">
  <figcaption>Bound <code>IsChecked</code>, the keyboard, and <code>VerticalContentAlignment</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [CheckBox](/apps/wpf-standard-control-demo/checkbox.html) — for options that are on or off independently.
- [ToggleButton](/apps/wpf-standard-control-demo/togglebutton.html) — the base class, which has no `GroupName`.
- [ComboBox](/apps/wpf-standard-control-demo/combobox.html) — one choice out of many, in a drop-down list.
- [Why a WPF RadioButton Bound to an Enum Shows No Initial Selection — The Role of GroupName](/articles/wpf-radiobutton-enum-binding/) — binding a group to one enum property.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View RadioButton source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/RadioButtonUsage){: target="_blank" rel="noopener noreferrer"}
