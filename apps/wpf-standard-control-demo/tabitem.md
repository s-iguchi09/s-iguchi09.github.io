---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/tabitem.html
title: "TabItem"
badge: "Selectors"
lead: "TabItem is one page of a TabControl: a header shown in the tab strip, and content shown while the tab is selected."
description: "WPF TabItem measured on .NET 10: selection with IsSelected, TabStripPlacement, disabled tabs, access keys in headers, and when tab content is loaded or reused."
---

## Which tab ends up selected

**TabItem** derives from `HeaderedContentControl`, so it has a `Header` (the label in the tab strip) and a `Content` (the page). The parent `TabControl` selects at most one tab at a time: setting `IsSelected` to `True` on one TabItem moves `SelectedIndex` to it and clears `IsSelected` on the others. Setting `IsSelected="True"` on the third of three tabs gave `SelectedIndex` 2 and cleared the others.

`IsSelected` binds two-way by default. Changing `SelectedIndex` to 1 set the first bound source to `False` and the second to `True`, and so did a click on the second tab's header with the real mouse.

Only one tab can stay selected, and which one wins depends on how the values arrive. Setting two tabs to `True` one after the other from code left the last one selected. Writing `IsSelected="True"` on two tabs in XAML left the first one selected. Select with one source of truth: bind `SelectedIndex` or `SelectedItem` on the TabControl instead of `IsSelected` on every tab.

## A disabled tab can still be selected from code

A tab with `IsEnabled="False"` cannot be selected by the user. Selecting it with `ISelectionItemProvider.Select`, the path an assistive tool uses, threw `ElementNotEnabledException`, and the selection did not move. A click on its header with the real mouse did not select it either.

Code is not blocked: `SelectedIndex = 1` selected the disabled tab and displayed its content. If later tabs stay disabled until earlier steps are complete, keep the code that changes the selection from choosing a tab that must stay closed.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabitem/tabitem-selection.svg" alt="Table of TabItem selection results: the base class HeaderedContentControl, IsSelected binding two-way by default, TabStripPlacement following the TabControl, the first of two tabs marked IsSelected in XAML and the last of two set from code being selected, a bound source following SelectedIndex and a real mouse click on a tab, and a disabled tab selectable from code but not through UI Automation's ISelectionItemProvider.Select or a real mouse click" width="1030" height="470" loading="lazy">
  <figcaption>Selection, metadata, and disabled tabs. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## When the content is created, and when it is reused

Only the selected tab's content is shown and laid out, but that does not mean the other tabs' content is idle. Content written directly in XAML is created when the XAML is loaded. In a test with three tabs, all three content elements raised `Loaded` when the window opened, and only the selected one was measured. Switching from tab 1 to tab 2 and back raised `Loaded` again on the content that came back, and the same instance was shown again. To defer expensive work, start it when the tab is first selected, not in the content's constructor or `Loaded` handler.

With `ItemsSource` and a `ContentTemplate`, the TabControl created one content element and reused it for every tab, changing only its data. State that is not bound to the item, such as a scroll position or a text box that is not bound, therefore carries over when the user switches tabs.

For generated tabs, the headers come from `ItemTemplate`. TabControl has no `HeaderTemplate` property. The templates you set on it are `ItemTemplate` (headers), `ContentTemplate` (pages), and `Template`. `SelectedContentTemplate` is read-only: it returns the template used by the selected tab. `HeaderTemplate` exists on TabItem.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabitem/tabitem-content-lifetime.svg" alt="Table of tab content lifetime: content written as elements receives Loaded for all three tabs at start while only the selected one is measured, and ItemsSource with a ContentTemplate creates a single content instance that is reused for every tab" width="1128" height="170" loading="lazy">
  <figcaption>When tab content is loaded and whether it is reused, for three tabs switched 1 &rarr; 2 &rarr; 1. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## The header, the strip position, and the template

A string header is displayed through an `AccessText`, so an underscore marks an access key. `Header="File_Name"` made `N` the access key, and the underscore itself is not displayed. [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/) confirms that the underscore disappears from TabItem headers as well. It also shows that writing `__` in a Label, which uses the same `AccessText`, displays a single underscore. The tab width follows the header: on the measuring machine, "File\_Name" gave a tab 65.65 wide and "A" one 19.74 wide (the widths depend on the font and display scaling).

`TabStripPlacement` on a TabItem is read-only and follows the parent TabControl. With `TabControl.TabStripPlacement="Left"`, both TabItems reported `Left`. Setting it is done on the TabControl; on the TabItem it is for templates and triggers that need to know which edge the strip is on.

The default TabItem template has no visual state groups; it changes its look with 16 triggers. A copied default template is edited through those triggers.

## Trying it in the demo app

![The TabItem page of the demo app, with the control list on the left and the first section, IsSelected](/images/wpf-standard-control-demo/tabitem.png){: .screenshot-img}

The TabItem page of the demo app has two sections. In the `IsSelected` section, a check box is bound to each tab's `IsSelected` without a `Mode`, so clicking a tab also updates the check boxes. In the `TabStripPlacement` section, a combo box moves the tab strip, and each TabItem shows its own read-only `TabStripPlacement`. The "Show Code" link under each section displays its XAML. The following XAML is the `IsSelected` section (`TabItemUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

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

## Related controls and articles

- [TabControl](/apps/wpf-standard-control-demo/tabcontrol.html) — the parent that selects one TabItem at a time and shows its content.
- [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/) — the access-key behavior that also applies to TabItem headers.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`TabItemDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TabItemDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. Clicks on tab headers were made with the real mouse over the measuring window.

[View TabItem source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TabItemUsage){: target="_blank" rel="noopener noreferrer"}
