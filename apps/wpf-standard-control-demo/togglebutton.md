---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/togglebutton.html
title: "ToggleButton"
badge: "Inputs"
lead: "ToggleButton is a button that stays pressed after a click and is released by the next one. CheckBox and RadioButton derive from it."
description: "WPF ToggleButton measured on .NET 10: IsChecked, IsThreeState, the default template, ClickMode, Command, a bound Popup, and why null looks just like unchecked."
---

## Clicks, IsThreeState, and why null looks unchecked

**ToggleButton** derives from `ButtonBase`, and both `CheckBox` and `RadioButton` derive from ToggleButton. Its state is `IsChecked`, a `bool?` that binds two-way by default. With `IsThreeState="True"`, clicks went `false` → `true` → `null` → `false`; with the default `False`, `false` → `true` → `false`. The demo app shows three buttons starting at `null`, `false`, and `true`; none of them is three-state, so the one that starts at `null` goes to `false` on the first click and cannot return to `null`.

The default template has no visual state groups. It changes its look with a trigger on `IsChecked` = `true` only, so a button whose `IsChecked` is `null` looks the same as an unchecked one. The demo app's three-state button looks unpressed in both states; only the text next to it shows the difference. If you use `IsThreeState`, give the template a look for `null`. UI Automation does report all three states: `ToggleState` was `Off`, `On`, and `Indeterminate` for `false`, `true`, and `null`.

ToggleButton has no `GroupName`; for mutually exclusive toggles, use RadioButton.

## ClickMode and Command: what runs, and when

These are not on the demo app's ToggleButton screen, but they are common with toggle buttons and were measured in the same way.

`ClickMode="Press"` toggles when the mouse button goes down, and the new state stays after it is released: `IsChecked` was `true` after the button went down and still `true` after it came up. It does not make a button that is active only while held.

`Command` runs after the state has changed. With `CommandParameter="{Binding IsChecked, RelativeSource={RelativeSource Self}}"`, a click from `false` passed `True` to `Execute`, and `IsChecked` was already `true` there. Toggling the button through UI Automation's `Toggle`, as an assistive technology does, changed `IsChecked` but did not run the command. Do not rely on `Command` alone; keep the state in a bound property if it must be handled for every way the button can be toggled.

## Opening a Popup with a ToggleButton

A `Popup` whose `IsOpen` is bound to `IsChecked` opened when the button was clicked. When the popup closed, `IsChecked` returned to `false`, because `Popup.IsOpen` binds two-way by default. With `StaysOpen="False"` and real clicks, clicking an empty area of the window closed the popup and cleared the button. Clicking the button itself while the popup was open did not close it: afterwards `IsOpen` and `IsChecked` were both still `true`.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/togglebutton/togglebutton-behavior.svg" alt="Table of ToggleButton results: it derives from ButtonBase and is the base of CheckBox and RadioButton, clicks cycle false, true, null only with IsThreeState, the default template has no visual states and only an IsChecked true trigger, UI Automation reports Off, On, Indeterminate, ClickMode Press toggles on button down and stays, a CommandParameter bound to IsChecked receives the new value, a UI Automation toggle does not run the command, and a bound Popup resets IsChecked when it closes, which a real click on an empty area does, while a click on the button while open leaves it open" width="1187" height="530" loading="lazy">
  <figcaption>States, template, <code>ClickMode</code>, <code>Command</code>, and a bound <code>Popup</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The ToggleButton page of the demo app, with the control list on the left and the first section, IsChecked](/images/wpf-standard-control-demo/togglebutton.png){: .screenshot-img}

The ToggleButton page of the demo app has two sections, `IsChecked` and `IsThreeState`, each showing the value next to the button. The "Show Code" link under each section displays its XAML. The following XAML is the `IsThreeState` section (`ToggleButtonUsageControl.xaml`), with the styles and the surrounding layout left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ToggleButton x:Name="IsThreeStateTrueButton" HorizontalAlignment="Stretch"
                Content="ToggleButton" IsThreeState="True" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateTrueButton, TargetNullValue=(Null)}" />

  <ToggleButton x:Name="IsThreeStateFalseButton" HorizontalAlignment="Stretch"
                Content="ToggleButton" IsThreeState="False" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateFalseButton}" />
</StackPanel>
```

## Related controls and articles

- [CheckBox](/apps/wpf-standard-control-demo/checkbox.html) — a ToggleButton drawn as a check box.
- [RadioButton](/apps/wpf-standard-control-demo/radiobutton.html) — a ToggleButton for one choice out of several.
- [Popup](/apps/wpf-standard-control-demo/popup.html) — often opened and closed by a ToggleButton.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ToggleButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToggleButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. State changes were made with UI Automation's `Toggle`. The command was run through the button's click handling (`OnClick`), and `ClickMode` was checked by raising the left-button events.

[View ToggleButton source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToggleButtonUsage){: target="_blank" rel="noopener noreferrer"}
