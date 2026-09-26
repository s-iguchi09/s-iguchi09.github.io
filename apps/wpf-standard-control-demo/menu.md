---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/menu.html
title: "Menu"
badge: "Menu"
lead: "Menu is a bar of headers, each opening a list of commands. Each entry is a MenuItem, which can hold its own submenu."
description: "WPF Menu measured on .NET 10: item roles, checkable items and StaysOpenOnClick with real clicks, which keys enter the main menu, and command items' IsEnabled."
---

## Which keys move into the menu

`IsMainMenu` on a **Menu** decides whether pressing <kbd>Alt</kbd> moves into the menu; the default is `True`. With a TextBox focused, a real <kbd>Alt</kbd> highlighted the first header and took the focus with `True`, and did nothing with `False`. Because the default is `True`, set `IsMainMenu="False"` on every menu except the menu bar.

The documentation says `IsMainMenu` controls both the <kbd>Alt</kbd> and the <kbd>F10</kbd> notifications. On the measuring machine, which uses a Japanese input method, a real <kbd>F10</kbd> with the TextBox focused did nothing in either case. With `True`, a real <kbd>F10</kbd> with a Button focused, and <kbd>F10</kbd> sent to the TextBox through WPF's input manager, both moved into the menu. The Japanese input method, which uses <kbd>F10</kbd> itself, is a possible cause of the difference, but that was not verified: the input method was not turned off for the measurement.

The access key worked either way: <kbd>Alt</kbd>+<kbd>M</kbd> opened the demo app's "Main Menu(\_M)" even with `False`.

## Item roles and header states

A Menu holds `MenuItem` elements, and each MenuItem's read-only `Role` follows from where it is and whether it has children. In the demo app's Role section, the four items were `TopLevelHeader`, `SubmenuHeader`, `SubmenuItem`, and `TopLevelItem`. The role changes with the children: adding a child to the `TopLevelItem` made it a `TopLevelHeader`.

A header's state is exposed by `IsHighlighted`, `IsPressed`, `IsSubmenuOpen`, and `IsSuspendingPopupAnimation`, all read-only except `IsSubmenuOpen`. Hovering with the real mouse set `IsHighlighted`. Pressing the button set `IsPressed` and opened the submenu, and releasing it cleared only `IsPressed`. `IsSuspendingPopupAnimation` was `True` once the submenu was open. With two headers, hovering over the second while the first was open closed the first and opened the second.

## Checkable items, and keeping the submenu open

`IsCheckable` decides whether a click toggles `IsChecked`; the default is `False`. A real click checked the item, and a second click cleared it. Without `IsCheckable`, the click left it unchecked. `IsChecked` binds both ways with a check box: the check box followed the click, and checking the check box in code checked the item.

Checkable items are not exclusive: clicking two items in one submenu checked both. For exclusive choices, uncheck the others in code or use a bound value.

`StaysOpenOnClick` decides whether the submenu stays open after the item is clicked; the default is `False`. After a real click, the submenu closed with `False` and stayed open with `True`.

## Commands, IsEnabled, and shortcut text

With `Command` set to `ApplicationCommands.Copy` and the menu opened with a real click, the item was disabled with no text selected in the TextBox, enabled with all text selected, and disabled with a Button focused. While the menu was open, the keyboard focus was on a MenuItem, but the item still followed the TextBox's selection.

A submenu item's `IsEnabled` is not up to date while its menu is closed. With the TextBox focused and nothing selected, the `Copy` item's `IsEnabled` read `True` although the command's `CanExecute` returned `False`. Once the menu was opened with a real click, the item was disabled. Call the command's `CanExecute` rather than reading the item's `IsEnabled`.

`Icon` is an image at the left, and `InputGestureText` a shortcut text at the right. `InputGestureText` is only text: "Ctrl+O" was shown, but a real <kbd>Ctrl</kbd>+<kbd>O</kbd> did not click the item. For a command with a key gesture it is filled in: the `Copy` item's text was `Ctrl+C` without being set. With a `KeyBinding` on the window, a real <kbd>Ctrl</kbd>+<kbd>O</kbd> ran a command that has no key gesture of its own, but the item's `InputGestureText` stayed empty. Bind the shortcut with a `KeyBinding`, and set `InputGestureText` too.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/menu/menu-behavior.svg" alt="Table of Menu results: IsMainMenu defaults to True, the four items of the Role section have the four roles and a TopLevelItem with a child becomes a TopLevelHeader, a real click checks a checkable item and closes the submenu unless StaysOpenOnClick, two checkable items are both checked, Alt enters only a main menu, a real F10 with a TextBox focused enters neither while F10 with a Button focused or through the input manager enters a main menu, Alt+M opens both, Ctrl+O text is shown but Ctrl+O does not click, the Copy item gets Ctrl+C, the Copy item reads enabled while closed and follows the TextBox selection once opened, and hover, press and release set the header states, and a window KeyBinding runs a command on a real Ctrl+O while the item's InputGestureText stays empty" width="1242" height="800" loading="lazy">
  <figcaption>Roles, checkable items, the main menu, gestures, commands, and header states. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The Menu page of the demo app, with the control list on the left and the first section, IsMainMenu](/images/wpf-standard-control-demo/menu.png){: .screenshot-img}

The Menu page of the demo app has sections for `IsMainMenu`, `IsCheckable` with `IsChecked` and `StaysOpenOnClick`, `Role`, `Command`, `Icon` with `InputGestureText`, and a last section showing `IsHighlighted`, `IsPressed`, `IsSubmenuOpen`, and `IsSuspendingPopupAnimation`. The `IsCheckable` section starts both `IsCheckable` and `StaysOpenOnClick` at `True` and binds a check box to `IsChecked` two-way; the `Command` section uses `ApplicationCommands.Copy`; and the `InputGestureText` section shows "Ctrl+O". The "Show Code" link under each section displays its XAML. The following XAML is the `IsCheckable` section (`MenuUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

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

## Related controls and articles

- [ToolBar](/apps/wpf-standard-control-demo/toolbar.html) — buttons for frequent commands, usually below the menu bar.
- [Popup](/apps/wpf-standard-control-demo/popup.html) — the kind of element a submenu opens in.
- [CheckBox](/apps/wpf-standard-control-demo/checkbox.html) — an on/off setting on the screen instead of in a menu.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`MenuDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/MenuDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with menus like the demo app's sections. Clicks and hovering were made with the real mouse, and <kbd>Alt</kbd>, <kbd>F10</kbd>, and the shortcuts were pressed as real key input; to narrow down <kbd>F10</kbd>, it was also sent through WPF's input manager. The `Copy` item was not clicked, so that the measuring machine's clipboard was left alone; only whether it was enabled was read.

[View Menu source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/MenuUsage){: target="_blank" rel="noopener noreferrer"}
