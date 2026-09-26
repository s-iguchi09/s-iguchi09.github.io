---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/expander.html
title: "Expander"
badge: "Display"
lead: "Expander shows a header at all times and shows or hides its content when the header is clicked."
description: "WPF Expander measured on .NET 10: what collapsed content costs, whether expanding animates, each ExpandDirection, and what reaches the header and content."
---

## What collapsed content costs

**Expander** derives from `HeaderedContentControl`. Its `Content` is what is shown and hidden, and collapsed content costs less than it may seem. In an Expander collapsed from the start, the content element was created and received `Loaded`, but it was not measured. A ListBox of 1000 items inside it created no `ListBoxItem` at all, so lists inside a collapsed Expander are cheap. After expanding, the content was measured, the ListBox created 11 items, and `Loaded` was raised on the content a second time. Do not start heavy work in the content's `Loaded` handler: it runs while the Expander is collapsed, and again when it is expanded.

## Expanding and collapsing, with no animation

`IsExpanded` decides whether the content is shown; the default is `False`. It binds two-way by default, so a view model property bound to it follows the user's clicks. The demo app binds it to a check box without a `Mode`, so the two stay in step. A click on the header with the real mouse expanded the Expander, checked the box, and raised `Expanded`. Clearing the box collapsed it and raised `Collapsed`.

The header in the default template is a `ToggleButton` named `HeaderSite`. The template has no visual state groups and no animation: it switches with triggers on `IsExpanded`, `ExpandDirection`, and `IsEnabled`. When `IsExpanded` was set to `False`, the content was `Collapsed` at once. To animate, use a template of your own.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/expander/expander-behavior.svg" alt="Table of Expander behavior: it derives from HeaderedContentControl with IsExpanded False and two-way by default, its header is a ToggleButton named HeaderSite, the template has no visual states and only triggers, collapsing is immediate, a real click on the header expands it and checks the bound CheckBox, and collapsed content is loaded but not measured, with no ListBox items created until it is expanded" width="1132" height="320" loading="lazy">
  <figcaption>The template, clicks, and collapsed content. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Where the header and the content go

`ExpandDirection` decides where the content appears relative to the header; the default is `Down`. With a content 80 × 40, `Down` placed the header on top and the content below it, and `Up` placed the content above the header. With `Left` and `Right`, the header became a column beside the content, 49 wide and 39 high, with its text still horizontal in the lower part of the column. The content was on the left for `Left` and on the right for `Right`.

`HasHeader` is read-only and tells whether there is a `Header`. With `Header` `null`, `HasHeader` was `False`, but the header's ToggleButton, with its arrow, stayed visible, 19 high. Hiding it takes a template of your own.

## Properties that reach the header as well as the content

The `Control` properties set on the Expander apply to the header too, not only to the content.

- **`Padding`:** with `Padding="20"`, the header grew from 19 to 59 high, and the content moved down with it.
- **Font properties:** `FontWeight="Bold"` set on the Expander made both the header's text and a TextBlock in the content bold.
- **`Foreground`:** with `Red`, the text of both the header and the content was red.
- **`Background`:** it is painted behind both the header and the content. With a 200-wide Expander, the painted area was 200 × 38.96 and contained the header and the content. `Background` itself is not an inherited property, so it is the area behind the content that is colored. Set it on the Expander to color the header and the content together.
- **`BorderBrush` / `BorderThickness`:** a border around the whole Expander, header included. Measured on its own, without `Padding`, `BorderThickness="5"` moved the header in by 5 from each edge, from (1, 1) to (6, 6), and the content down by 5.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/expander/expander-layout.svg" alt="Table of Expander layout: the header and content positions for Down, Up, Left, and Right, where the header text stays horizontal, a null header that leaves the ToggleButton visible, FontWeight and Foreground reaching the header and the content, Background painted behind both the header and the content, and Padding and BorderThickness, measured separately, applying around the header too" width="1163" height="380" loading="lazy">
  <figcaption><code>ExpandDirection</code>, the header, and the <code>Control</code> properties. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The Expander page of the demo app, with the control list on the left and the first section, ExpandDirection](/images/wpf-standard-control-demo/expander.png){: .screenshot-img}

The Expander page of the demo app has sections for `ExpandDirection`, `IsExpanded`, the header, the content, and the `Control` properties `Padding`, the font properties, `Background`, `Foreground`, and the border. The header section turns an empty text box into `null`. The "Show Code" link under each section displays its XAML. The following XAML is the `IsExpanded` section (`ExpanderUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsExpandedCheckBox" VerticalContentAlignment="Center" Content="IsExpanded" />

  <Expander x:Name="IsExpandedExpander"
            Content="CONTENT"
            Header="Expander"
            IsExpanded="{Binding IsChecked, ElementName=IsExpandedCheckBox}" />
</StackPanel>
```

## Related controls and articles

- [GroupBox](/apps/wpf-standard-control-demo/groupbox.html) — a header and a border around content that is always shown.
- [ToggleButton](/apps/wpf-standard-control-demo/togglebutton.html) — the element the header is made of.
- [TreeView](/apps/wpf-standard-control-demo/treeview.html) — folding for a hierarchy of items rather than a few sections.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ExpanderDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ExpanderDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The click on the header was made with the real mouse over the measuring window. Positions are measured from the Expander's top-left corner, and sizes are the values on the measuring machine.

[View Expander source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ExpanderUsage){: target="_blank" rel="noopener noreferrer"}
