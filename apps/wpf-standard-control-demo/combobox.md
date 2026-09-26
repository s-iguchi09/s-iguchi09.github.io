---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/combobox.html
title: "ComboBox"
badge: "Inputs"
lead: "ComboBox shows the selected item and opens a drop-down list to choose another. It can also be made editable, so the user can type."
description: "WPF ComboBox measured on .NET 10: editable autocomplete and typed prefixes, IsReadOnly, drop-down height, StaysOpenOnEdit with a real click, and synced lists."
---

## What Text, SelectedValue, and SelectedItem show

The demo app fills every **ComboBox** with the days of the week, an enum shown by name and bound by value: items that have a `Name` and a `Value`, with `DisplayMemberPath="Name"` and `SelectedValuePath="Value"`. With such items, `Text` and `SelectedValue` both read `Monday` for the second day, but `SelectedItem` is the item object itself. Its text is the full type name, because the item class does not override `ToString`: `ScreenshotCapture.Scenes.ComboBoxDemoScene+EnumItem` for the measured items, and `WPFStandardControlDemoApp.Common.MarkupExtensions.EnumBindingSourceExtension+EnumItem` for the demo app's items, which is what its `SelectedItem` display shows. Bind `SelectedValue` with `SelectedValuePath` rather than showing `SelectedItem`, unless the item class overrides `ToString`.

## Typing into an editable ComboBox

`IsEditable` defaults to `False`. When it is `True`, typing completes a matching item: "tue" became `Tuesday`, and `SelectedValue` became Tuesday. Text that matches no item stays as typed: "xyz" left `SelectedIndex` -1 and `SelectedValue` `null`. An editable ComboBox can complete known values and still accept others, so check `SelectedIndex` as well as `Text`.

`ShouldPreserveUserEnteredPrefix` decides whether autocomplete keeps the letters as typed; the default is `False`. After typing "tue", the text was `Tuesday` with `False` and `tuesday` with `True`. `SelectedValue` was Tuesday either way.

With both `IsEditable` and `IsReadOnly` set to `True`, typing "tue" left the text empty, but the <kbd>Down</kbd> key still selected an item (Sunday).

`StaysOpenOnEdit` decides whether the list stays open when the user clicks into the edit box; the default is `False`. With a real mouse click into the edit box of an open, editable ComboBox, the list closed with `False` and stayed open with `True`.

## Opening the list, and how tall it gets

`IsDropDownOpen` binds two-way by default. The demo app binds it to a check box without a `Mode`. After the check box opened the list, <kbd>Down</kbd>, <kbd>Down</kbd>, and <kbd>Enter</kbd> selected Monday, closed the list, and cleared the check box.

`MaxDropDownHeight` is the largest height of the list. The default was 480, a third of the screen height on the measuring machine; the seven days took 141.72. With the demo app's 100, the list was 100 high, less than the seven days need. Set it for long lists.

## ComboBoxes that move together

`IsSynchronizedWithCurrentItem` makes the selection follow the current item of the list's view. Two ComboBoxes on one list with `True` both started at index 0 and moved together: setting the first to 3 set the second to 3. Unset, both started at -1 and were independent. Set it only when lists should move together; it also selects the first item at the start.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/combobox/combobox-behavior.svg" alt="Table of ComboBox results: defaults are not editable, not read-only, StaysOpenOnEdit and ShouldPreserveUserEnteredPrefix False and text search on, MaxDropDownHeight 480 is a third of the screen, IsDropDownOpen is two-way so a bound check box clears when Enter closes the list, SelectedItem prints the full type name of the item class, typing tue completes Tuesday or tuesday with the prefix preserved, xyz leaves no selection, IsReadOnly blocks typing but not the Down key, the list is 141.72 high or 100 with the demo value, a real click into the edit box closes the list unless StaysOpenOnEdit, and synchronized ComboBoxes start at 0 and move together" width="1171" height="530" loading="lazy">
  <figcaption>Selection, editing, the drop-down, and synchronization. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The ComboBox page of the demo app, with the control list on the left and the first section, IsDropDownOpen](/images/wpf-standard-control-demo/combobox.png){: .screenshot-img}

The ComboBox page of the demo app has sections for `IsDropDownOpen`, `IsEditable`, `IsReadOnly`, `MaxDropDownHeight`, `Text`, `StaysOpenOnEdit`, `ShouldPreserveUserEnteredPrefix`, the selection properties, and `IsSynchronizedWithCurrentItem`. The `Text` section shows the text next to `SelectedValue`, and the `IsSynchronizedWithCurrentItem` section starts its three ComboBoxes at `True`, so they all start on Sunday. The "Show Code" link under each section displays its XAML. How to bind `ItemsSource` and read the selection is covered in the article linked below. The following XAML is the `ShouldPreserveUserEnteredPrefix` section (`ComboBoxUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:sys="clr-namespace:System;assembly=mscorlib"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <CheckBox x:Name="ShouldPreserveUserEnteredPrefix" Content="ShouldPreserveUserEnteredPrefix" />

  <ComboBox x:Name="ShouldPreserveUserEnteredPrefixComboBox"
            DisplayMemberPath="Name"
            IsEditable="True"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=sys:DayOfWeek}"
            SelectedValuePath="Value"
            ShouldPreserveUserEnteredPrefix="{Binding IsChecked, ElementName=ShouldPreserveUserEnteredPrefix}" />

  <TextBlock Text="{Binding Text, ElementName=ShouldPreserveUserEnteredPrefixComboBox}" />
  <TextBlock Text="{Binding SelectedValue, ElementName=ShouldPreserveUserEnteredPrefixComboBox}" />
</StackPanel>
```

## Related controls and articles

- [ListBox](/apps/wpf-standard-control-demo/listbox.html) — shows all items at once instead of in a drop-down.
- [RadioButton](/apps/wpf-standard-control-demo/radiobutton.html) — a few choices that are all visible.
- [Popup](/apps/wpf-standard-control-demo/popup.html) — the element that holds the drop-down list.
- [WPF ComboBox ItemsSource Binding Patterns and Selected Value Retrieval](/articles/wpf-combobox-itemssource-patterns/) — ways to fill a ComboBox and read its selection.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ComboBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ComboBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool, with items like the demo app's (`Name` and `Value` of each day, no `ToString`). Typing was sent one character at a time to the edit box, the keys through WPF's input manager, and the click into the edit box was made with the real mouse. Heights are the values on the measuring machine.

[View ComboBox source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ComboBoxUsage){: target="_blank" rel="noopener noreferrer"}
