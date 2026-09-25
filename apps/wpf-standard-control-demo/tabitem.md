---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/tabitem.html
title: "TabItem"
badge: "Selectors"
lead: "TabItem is one page of a TabControl: a header shown in the tab strip, and content shown while the tab is selected."
description: "WPF TabItem measured on .NET 10: selection with IsSelected, TabStripPlacement, disabled tabs, access keys in headers, and when tab content is loaded or reused."
---

## Overview

**TabItem** derives from `HeaderedContentControl`, so it has a `Header` (the label in the tab strip) and a `Content` (the page). The parent `TabControl` selects at most one tab at a time: setting `IsSelected` on one TabItem moves `SelectedIndex` to it and clears `IsSelected` on the others.

Only the selected tab's content is shown and laid out, but that does not mean the other tabs' content is idle. Content written directly in XAML is created when the XAML is loaded. In a test with three tabs, all three content elements raised `Loaded` when the window opened, and only the selected one was measured. With `ItemsSource` and a `ContentTemplate`, the TabControl created one content element and reused it for every tab, changing only its data.

The demo app has two sections. In the `IsSelected` section, a check box is bound to each tab's `IsSelected`. In the `TabStripPlacement` section, a combo box moves the tab strip, and each TabItem shows its own read-only `TabStripPlacement`. The "Show Code" link under each section displays its XAML.

## Screen Preview

![tabitem demo screen](/images/wpf-standard-control-demo/tabitem.png){: .screenshot-img}

## Demonstrated Properties

The following properties are demonstrated interactively in the WPF Standard Control Demo App. The behavior described for each one was measured on .NET 10 (see [Measured Behavior](#measured-behavior)).

| Property | Values | Description |
| --- | --- | --- |
| `IsSelected` | `bool` | Whether this tab is the selected one. It binds two-way by default. In the demo app, the check boxes are bound to `IsSelected` without a `Mode`, so clicking a tab also updates the check boxes. In a test, changing `SelectedIndex` to 1 set the first source to `False` and the second to `True`, and so did a click on the second tab's header with the real mouse. Setting `IsSelected="True"` on the third of three tabs gave `SelectedIndex` 2 and cleared the others. Only one tab can stay selected, and which one wins depends on how the values arrive. Setting two tabs to `True` one after the other from code left the last one selected. Writing `IsSelected="True"` on two tabs in XAML left the first one selected. |
| `TabStripPlacement (ReadOnly)` | `Dock` | A read-only property of TabItem that follows the parent TabControl. With `TabControl.TabStripPlacement="Left"`, both TabItems reported `Left`. Setting it is done on the TabControl; on the TabItem it is for templates and triggers that need to know which edge the strip is on. |

## Header, IsEnabled, and Content

These properties are not in the demo app's TabItem screen, but they are used with almost every TabItem. Their behavior was measured in the same way.

- **`Header`:** a string header is displayed through an `AccessText`, so an underscore marks an access key. `Header="File_Name"` made `N` the access key, and the underscore itself is not displayed. [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/) confirms that the underscore disappears from TabItem headers as well. It also shows that writing `__` in a Label, which uses the same `AccessText`, displays a single underscore. The tab width follows the header: on the measuring machine, "File\_Name" gave a tab 65.65 wide and "A" one 19.74 wide (the widths depend on the font and display scaling).
- **`IsEnabled`:** a disabled tab cannot be selected by the user. Selecting it with `ISelectionItemProvider.Select`, the path an assistive tool uses, threw `ElementNotEnabledException`, and the selection did not move. A click on its header with the real mouse did not select it either. Code is not blocked: `SelectedIndex = 1` selected the disabled tab and displayed its content. If a tab must stay closed, keep the code that changes the selection from choosing it.
- **`Content`:** content written directly in XAML is created with the window, and every content element received `Loaded` at start, including those of unselected tabs. Only the selected one was measured. Switching from tab 1 to tab 2 and back raised `Loaded` again on the content that came back, and the same instance was shown again. To defer expensive work, start it when the tab is first selected, not in the content's constructor or `Loaded` handler.

## XAML Example

The following XAML is the `IsSelected` section of the demo app (`TabItemUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsSelectedTab1" VerticalContentAlignment="Center" Content="Tab1" />
  <CheckBox x:Name="IsSelectedTab2" VerticalContentAlignment="Center" Content="Tab2" />

  <TabControl x:Name="IsSelectedTabControl">
    <TabItem Content="Item1" Header="Tab1"
             IsSelected="{Binding IsChecked, ElementName=IsSelectedTab1}" />
    <TabItem Content="Item2" Header="Tab2"
             IsSelected="{Binding IsChecked, ElementName=IsSelectedTab2}" />
  </TabControl>
</StackPanel>
```

Because `IsSelected` binds two-way by default, the check boxes follow the tabs as well as control them. With Tab2 selected, setting Tab1's source to `True` (what checking its box does) selected Tab1 and set Tab2's source to `False`.

## Common Use Cases

- **Settings pages:** one TabItem per group of settings, with a string header.
- **Document tabs:** tabs generated from a collection with `ItemsSource`, the header from `ItemTemplate` and the page from `ContentTemplate`.
- **Step-by-step input:** later tabs disabled until earlier steps are complete, with the selection changed from code.

## Tips and Best Practices

- **Select with one source of truth.** Because the result depends on the order in which `IsSelected` values arrive, bind `SelectedIndex` or `SelectedItem` on the TabControl instead of `IsSelected` on every tab.
- **Headers of generated tabs come from `ItemTemplate`.** TabControl has no `HeaderTemplate` property. The templates you set on it are `ItemTemplate` (headers), `ContentTemplate` (pages), and `Template`. `SelectedContentTemplate` is read-only: it returns the template used by the selected tab. `HeaderTemplate` exists on TabItem.
- **Expect shared content with `ItemsSource`.** The content element is reused across tabs. State that is not bound to the item, such as a scroll position or a text box that is not bound, carries over when the user switches tabs.
- **Customize the look with triggers.** The default TabItem template has no visual state groups; it changes its look with 16 triggers. A copied default template is edited through those triggers.

## Measured Behavior {#measured-behavior}

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TabItemDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TabItemDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Clicks on tab headers were made with the real mouse over the measuring window.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabitem/tabitem-selection.svg" alt="Table of TabItem selection results: the base class HeaderedContentControl, IsSelected binding two-way by default, TabStripPlacement following the TabControl, the first of two tabs marked IsSelected in XAML and the last of two set from code being selected, a bound source following SelectedIndex and a real mouse click on a tab, and a disabled tab selectable from code but not through UI Automation's ISelectionItemProvider.Select or a real mouse click" width="1030" height="470" loading="lazy">
  <figcaption>Selection, metadata, and disabled tabs. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabitem/tabitem-content-lifetime.svg" alt="Table of tab content lifetime: content written as elements receives Loaded for all three tabs at start while only the selected one is measured, and ItemsSource with a ContentTemplate creates a single content instance that is reused for every tab" width="1128" height="170" loading="lazy">
  <figcaption>When tab content is loaded and whether it is reused, for three tabs switched 1 &rarr; 2 &rarr; 1. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Related Controls and Articles

- [TabControl](/apps/wpf-standard-control-demo/tabcontrol.html) — the parent that selects one TabItem at a time and shows its content.
- [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/) — the access-key behavior that also applies to TabItem headers.

## Source Code

The source code for this demo screen is available on GitHub. In the app, the "Show Code" link under each section displays the XAML of that section.

[View TabItem source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TabItemUsage){: target="_blank" rel="noopener noreferrer"}
