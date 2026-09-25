---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/menu.html
title: "Menu"
badge: "Menu"
lead: "Menu is a bar of headers, each opening a list of commands. Each entry is a MenuItem, which can hold its own submenu."
description: "WPF Menu measured on .NET 10: item roles, checkable items and StaysOpenOnClick with real clicks, which keys enter the main menu, and command items' IsEnabled."
---

## Overview

A **Menu** holds `MenuItem` elements, and each MenuItem's `Role` follows from where it is and whether it has children. In the demo app's Role section, the four items were `TopLevelHeader`, `SubmenuHeader`, `SubmenuItem`, and `TopLevelItem`. The role changes with the children: adding a child to the `TopLevelItem` made it a `TopLevelHeader`.

A submenu item's `IsEnabled` is not up to date while its menu is closed. For a `Copy` item with the TextBox focused and nothing selected, `IsEnabled` read `True` although the command's `CanExecute` returned `False`. Once the menu was opened with a real click, the item was disabled.

The demo app has sections for `IsMainMenu`, `IsCheckable` with `IsChecked` and `StaysOpenOnClick`, `Role`, `Command`, `Icon` with `InputGestureText`, and a section showing `IsHighlighted`, `IsPressed`, `IsSubmenuOpen`, and `IsSuspendingPopupAnimation`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![menu demo screen](/images/wpf-standard-control-demo/menu.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `IsMainMenu (Menu)` | `bool` | Whether pressing <kbd>Alt</kbd> moves into the menu; the default is `True`. With a TextBox focused, a real <kbd>Alt</kbd> highlighted the first header and took the focus with `True`, and did nothing with `False`. The documentation says `IsMainMenu` controls both the <kbd>Alt</kbd> and the <kbd>F10</kbd> notifications. On the measuring machine, which uses a Japanese input method, a real <kbd>F10</kbd> with the TextBox focused did nothing in either case. With `True`, a real <kbd>F10</kbd> with a Button focused, and <kbd>F10</kbd> sent to the TextBox through WPF's input manager, both moved into the menu. The Japanese input method, which uses <kbd>F10</kbd> itself, is a possible cause of the difference, but that was not verified: the input method was not turned off for the measurement. The access key worked either way: <kbd>Alt</kbd>+<kbd>M</kbd> opened the demo app's "Main Menu(\_M)" even with `False`. |
| `IsCheckable` | `bool` | Whether a click toggles `IsChecked`; the default is `False`. A real click checked the item, and a second click cleared it. Without `IsCheckable`, the click left it unchecked. Checkable items are not exclusive: clicking two items in one submenu checked both. |
| `IsChecked` | `bool` | Whether the check mark is shown. The demo app binds a check box to it two-way; the check box followed the click, and checking the check box in code checked the item. |
| `StaysOpenOnClick` | `bool` | Whether the submenu stays open after the item is clicked; the default is `False`. After a real click, the submenu closed with `False` and stayed open with `True`. The demo app starts both `IsCheckable` and `StaysOpenOnClick` at `True`. |
| `Role` | `MenuItemRole (ReadOnly)` | The item's kind: a header on the bar or in a submenu, with or without children (see above). |
| `Command` | `ICommand` | The command the item runs. The demo app uses `ApplicationCommands.Copy`. Opened with a real click, the item was disabled with no text selected in the TextBox, enabled with all text selected, and disabled with a Button focused. While the menu was open, the keyboard focus was on a MenuItem, but the item still followed the TextBox's selection. |
| `Icon / InputGestureText` | `object / string` | An image at the left and a shortcut text at the right. `InputGestureText` is only text: the demo app's "Ctrl+O" was shown, but a real <kbd>Ctrl</kbd>+<kbd>O</kbd> did not click the item. For a command with a key gesture it is filled in: the `Copy` item's text was `Ctrl+C` without being set. |
| `IsHighlighted / IsPressed / IsSubmenuOpen / IsSuspendingPopupAnimation` | `bool (ReadOnly except IsSubmenuOpen)` | The state of a header, shown in the demo app's last section. Hovering with the real mouse set `IsHighlighted`. Pressing the button set `IsPressed` and opened the submenu, and releasing it cleared only `IsPressed`. `IsSuspendingPopupAnimation` was `True` once the submenu was open. With two headers, hovering over the second while the first was open closed the first and opened the second. |

## XAML Example

The following XAML is the `IsCheckable` section of the demo app (`MenuUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsCheckableCheckBox" Content="IsCheckable" IsChecked="True" />
  <CheckBox x:Name="StaysOpenOnClickCheckBox" Content="StaysOpenOnClick" IsChecked="True" />

  <Menu IsMainMenu="False">
    <MenuItem Header="Options">
      <MenuItem x:Name="InteractiveMenuItem"
                Header="Interactive Item"
                IsCheckable="{Binding IsChecked, ElementName=IsCheckableCheckBox}"
                StaysOpenOnClick="{Binding IsChecked, ElementName=StaysOpenOnClickCheckBox}" />
    </MenuItem>
  </Menu>

  <CheckBox x:Name="IsCheckedSyncCheckBox"
            Content="IsChecked"
            IsChecked="{Binding IsChecked, ElementName=InteractiveMenuItem, Mode=TwoWay}" />
</StackPanel>
```

## Common Use Cases

- **Application menu bar:** File, Edit, and View at the top of a window, reached with <kbd>Alt</kbd>.
- **Settings that are on or off:** checkable items such as "Word Wrap" or "Show Status Bar".
- **Standard commands:** a Copy item that is enabled by the selection in the focused text box.

## Tips and Best Practices

- **Call the command's `CanExecute` rather than reading a submenu item's `IsEnabled`.** The item is not updated while its menu is closed.
- **Bind the shortcut with a `KeyBinding`.** `InputGestureText` only shows text; a command with a key gesture fills it in by itself.
- **Make exclusive choices yourself.** Checkable items are independent, so uncheck the others in code or use a bound value.
- **Set `IsMainMenu="False"` on every menu except the menu bar.** The default is `True`, which lets <kbd>Alt</kbd> move into it.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`MenuDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/MenuDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with menus like the demo app's sections. Clicks and hovering were made with the real mouse, and <kbd>Alt</kbd>, <kbd>F10</kbd>, and the shortcuts were pressed as real key input; to narrow down <kbd>F10</kbd>, it was also sent through WPF's input manager. The `Copy` item was not clicked, so that the measuring machine's clipboard was left alone; only whether it was enabled was read.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/menu/menu-behavior.svg" alt="Table of Menu results: IsMainMenu defaults to True, the four items of the Role section have the four roles and a TopLevelItem with a child becomes a TopLevelHeader, a real click checks a checkable item and closes the submenu unless StaysOpenOnClick, two checkable items are both checked, Alt enters only a main menu, a real F10 with a TextBox focused enters neither while F10 with a Button focused or through the input manager enters a main menu, Alt+M opens both, Ctrl+O text is shown but Ctrl+O does not click, the Copy item gets Ctrl+C, the Copy item reads enabled while closed and follows the TextBox selection once opened, and hover, press and release set the header states" width="1242" height="770" loading="lazy">
  <figcaption>Roles, checkable items, the main menu, gestures, commands, and header states. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls

- [ToolBar](/apps/wpf-standard-control-demo/toolbar.html) — buttons for frequent commands, usually below the menu bar.
- [Popup](/apps/wpf-standard-control-demo/popup.html) — the kind of element a submenu opens in.
- [CheckBox](/apps/wpf-standard-control-demo/checkbox.html) — an on/off setting on the screen instead of in a menu.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View Menu source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/MenuUsage){: target="_blank" rel="noopener noreferrer"}
