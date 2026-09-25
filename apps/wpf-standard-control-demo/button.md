---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/button.html
title: "Button"
badge: "Inputs"
lead: "Button runs an action when it is clicked, through its Click event or a command bound to Command. ClickMode, IsPressed, and the command properties come from its base class ButtonBase."
description: "WPF Button measured on .NET 10: IsCancel and IsDefault in a UserControl and a dialog, ClickMode with a real mouse and keys, and when CanExecute is re-queried."
---

## Overview

**Button** derives from `ButtonBase`, which derives from `ContentControl`, so its `Content` can be text, an image, or a panel. The default template shows the content with a `ContentPresenter` whose `RecognizesAccessKey` is `True`: with `Content="_Save"`, `S` became the access key and the UI Automation name was `Save`.

The default template has no visual state groups. It changes its look with triggers on `IsDefaulted`, `IsMouseOver`, `IsPressed`, and `IsChecked` (all `true`) and on `IsEnabled` = `false`.

The demo app has sections for `IsCancel` and `IsDefault`, `ClickMode`, `IsPressed`, `Command`, and `CommandParameter`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![button demo screen](/images/wpf-standard-control-demo/button.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `IsCancel` | `bool` | Makes <kbd>Esc</kbd> click the button. The demo app places the button in a `UserControl`, and it works there: with the focus in a TextBox inside the UserControl, <kbd>Esc</kbd> clicked it. Whether the window closes depends on how it was opened. In a window opened with `ShowDialog`, <kbd>Esc</kbd> closed the window and `ShowDialog` returned `false`. In a window opened with `Show`, the button was clicked but the window stayed open. With two `IsCancel` buttons in one window, <kbd>Esc</kbd> clicked neither. It moved the focus to the first one, and pressing it again moved the focus to the second. |
| `IsDefault` | `bool` | Makes <kbd>Enter</kbd> click the button. With the focus in a TextBox, <kbd>Enter</kbd> clicked it, and its `IsDefaulted` was `True`. With the focus in a TextBox that has `AcceptsReturn="True"`, <kbd>Enter</kbd> added a line break and did not click it. With the focus on another button, <kbd>Enter</kbd> clicked that button instead, and `IsDefaulted` was `False`. |
| `ClickMode (ButtonBase)` | `Release / Press / Hover` | When a click happens. With a real mouse, `Release` (the default) clicked when the mouse button was released. If the pointer was moved off the button before the mouse button was released, `IsPressed` became `False` and there was no click. `Press` clicked when the mouse button went down. `Hover` clicked as soon as the pointer entered the button, without any mouse button. A `Hover` button cannot be clicked from the keyboard: <kbd>Space</kbd> and <kbd>Enter</kbd> did nothing. With the other two, <kbd>Space</kbd> clicked on key up with `Release` and on key down with `Press`, and <kbd>Enter</kbd> clicked on key down with both. |
| `IsPressed (ButtonBase)` | `bool (ReadOnly)` | Whether the button is being pressed; the demo app shows it next to a button. It was `True` while the mouse button was held down on the button and while <kbd>Space</kbd> was held down, and `False` after release. A `Hover` button was pressed while the pointer was over it. The keyboard does not press a `Hover` button: `IsPressed` stayed `False` while <kbd>Space</kbd> was held down. |
| `Command (ButtonBase)` | `ICommand` | The command run on a click. While its `CanExecute` returned `false`, the button's `IsEnabled` was `False`, even with `IsEnabled="True"` set on the button. The demo app's `RelayCommand` passes `CanExecuteChanged` on to `CommandManager.RequerySuggested`. With that, the button did not follow a change in `CanExecute`'s result until `CommandManager.InvalidateRequerySuggested` was called, or until something such as a focus change made WPF check again. That check asks every button again: with 20 buttons bound to the same command, one focus change called `CanExecute` 20 times. |
| `CommandParameter (ButtonBase)` | `object` | The value passed to `CanExecute` and `Execute`. The demo app binds it to a TextBox's `Text`, after `Command`. With that XAML, both `CanExecute` calls during loading received `ShowMessageText`, never `null`. A change of the parameter is checked at once: when the text was cleared from code, `CanExecute` ran once and the button became disabled, with no `InvalidateRequerySuggested`. `Execute` received the text at the time of the click. |

## XAML Example

The following XAML is the `CommandParameter` section of the demo app (`ButtonUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="CommandParameterText" Text="ShowMessageText" />
  <Button x:Name="ClickWithParameterCommandButton"
          Command="{Binding ClickWithParameterCommand}"
          CommandParameter="{Binding Text, ElementName=CommandParameterText}"
          Content="ShowMessageBox" />
</StackPanel>
```

The command is defined in the view model (`ButtonUsageViewModel.cs`):

```csharp
public ICommand ClickWithParameterCommand { get; } =
    new RelayCommand(param => MessageBox.Show(param?.ToString()), null);
```

## Common Use Cases

- **Dialog buttons:** OK with `IsDefault` and Cancel with `IsCancel` in a window opened with `ShowDialog`.
- **Commands in MVVM:** a button bound to a view model's `ICommand`, enabled and disabled by `CanExecute`.
- **Buttons in list rows:** a button in an item template that passes its item as `CommandParameter`. With `CommandParameter="{Binding}"`, a real click on the button of the second row passed `Row 2` to `Execute`.

## Tips and Best Practices

- **Keep one `IsCancel` button per window.** With two, <kbd>Esc</kbd> only moves the focus between them.
- **Close a window opened with `Show` yourself.** `IsCancel` closes only a dialog opened with `ShowDialog`.
- **Keep `CanExecute` cheap.** It runs for every button bound to the command on each focus change.
- **Let `CanExecute` decide whether the button is enabled.** Setting `IsEnabled` does not override it.
- **Give buttons that show only an image an `AutomationProperties.Name`.** Without it, the UI Automation name was empty.
- **Do not use `ClickMode="Hover"` for buttons that must work from the keyboard.** <kbd>Space</kbd> and <kbd>Enter</kbd> do not click them.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Keys were sent through WPF's input manager, the same path as keys typed on a keyboard, so that `IsCancel` and `IsDefault`, which are handled there, respond to them. `ClickMode` was measured by moving and clicking the real mouse over the measuring window. The click with a `CommandParameter` was made through UI Automation's `Invoke`.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-keys.svg" alt="Table of IsCancel and IsDefault results: in a UserControl Esc clicks the IsCancel button and the window stays open, Enter clicks the IsDefault button, Enter in a TextBox with AcceptsReturn adds a line break without a click, Enter on another button clicks that button, two IsCancel buttons only move the focus, and a dialog opened with ShowDialog closes and returns false" width="889" height="290" loading="lazy">
  <figcaption><code>IsCancel</code> and <code>IsDefault</code> by where the focus is and how the window was opened. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-clickmode.svg" alt="Table of ClickMode results: the default is Release and IsPressed is read-only, Hover clicks when the pointer enters, Press clicks on button down, Release clicks on button up and not when released outside, and from the keyboard Release clicks on Space up, Press on Space down, Enter clicks both on key down, and a Hover button does nothing" width="1046" height="320" loading="lazy">
  <figcaption><code>ClickMode</code> and <code>IsPressed</code> with a real mouse and with the keyboard. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-command.svg" alt="Table of Command results: CanExecute false disables the button even with IsEnabled True, a RequerySuggested command updates only after InvalidateRequerySuggested, one focus change calls CanExecute 20 times for 20 buttons, the demo XAML passes the text to CanExecute while loading, clearing the text re-queries CanExecute once, Execute receives the current text, and in an item template a real click on a row's button passed that row's item through CommandParameter="{Binding}"" width="1046" height="290" loading="lazy">
  <figcaption><code>Command</code> and <code>CommandParameter</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-template.svg" alt="Table of Button type and template results: Button derives from ButtonBase and ContentControl, the default template has no visual state groups and triggers on IsDefaulted, IsMouseOver, IsPressed, IsChecked, and IsEnabled, the ContentPresenter recognizes access keys, and the UI Automation name is the text, Save for _Save, empty for an image, and the AutomationProperties.Name when set" width="1140" height="290" loading="lazy">
  <figcaption>Base classes, the default template, and UI Automation names. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [RepeatButton](/apps/wpf-standard-control-demo/repeatbutton.html) — a ButtonBase that keeps clicking while it is held down.
- [ToggleButton](/apps/wpf-standard-control-demo/togglebutton.html) — a ButtonBase that stays pressed after a click.
- [Fixing a RelayCommand Whose CanExecute Does Not Update the Button State in WPF](/articles/wpf-relaycommand-canexecute-not-updating/) — why a button does not follow `CanExecute` and how to re-query it.
- [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/) — the access-key behavior that also applies to button content.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View Button source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ButtonUsage){: target="_blank" rel="noopener noreferrer"}
