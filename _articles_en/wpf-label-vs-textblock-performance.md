---
layout: article-en
title: "Why WPF Slows Down with Many Labels and When to Switch to TextBlock"
seo_title: "Why Many Labels Slow Down WPF and When to Use TextBlock"
date: 2026-06-10
category: WPF
excerpt: "Why many WPF Labels slow rendering, from measured visual trees: the gap against TextBlock, underscore costs, and whether it survives virtualization."
image: /images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-measurement.png
---

## Overview

Initial layout takes longer on screens that place a large number of WPF `Label` controls.
This article analyzes the cause based on the structural difference between `Label` and `TextBlock`, and presents an implementation policy for performance-sensitive screens.

The conclusion first.
The gap between `Label` and `TextBlock` is measurable, but **it only matters on screens where UI virtualization is not in effect**.
With 10,000 items bound to a virtualized `ListBox`, no practical difference in layout time remains between the two.
Replacing controls is worth considering only after virtualization has been confirmed.

---

## Prerequisites / Environment

- Framework / Language: .NET 10 / C# / WPF
- Target controls: `Label`, `TextBlock`, `ContentPresenter`
- Architecture: MVVM (the same applies to code-behind)
- Target screens: lists, dashboards, and other screens displaying many text elements
- Measurement environment: Windows 11, default theme (Aero2), display scaling 100%

The figures in this article were obtained by running an actual application in the environment above and timing the interval from `Measure` to `UpdateLayout`.
Each condition was run 15 times in alternation, and the minimum was taken.
Elapsed time depends on the execution environment, so read the values **as ratios between conditions rather than as absolute numbers**.

---

## Problem

Screens that place dozens to hundreds of `Label` controls tend to exhibit the following:

- Slower initial display.
- Higher memory usage than an equivalent `TextBlock` layout, even for the same string display.

Carrying an implementation that uses `Label` for form captions into a list display is a common way to degrade rendering performance.

---

## Cause / Background

`TextBlock` is a lightweight element whose primary purpose is text rendering, and it derives directly from `FrameworkElement`.
`Label`, by contrast, derives from `ContentControl` and is designed as a general-purpose UI part capable of hosting content other than strings.

`Label` renders through a `ContentPresenter` and provides access-key handling and `Target` integration as needed.
For workloads that display large amounts of plain text, this overhead exceeds that of `TextBlock`.

### Visual Tree Composition

The substance of the difference is the number of visuals constructed per element.
With the default template and a string content, the composition is as follows.

| Element | Visual tree that gets built | Visuals |
| --- | --- | --- |
| `TextBlock` | `TextBlock` | 1 |
| `ContentPresenter` | `ContentPresenter` → `TextBlock` | 2 |
| `Label` | `Label` → `Border` → `ContentPresenter` → `TextBlock` | 4 |

Each `Label` additionally constructs a `Border`, a `ContentPresenter`, and the `TextBlock` that actually draws the characters.
The number of visuals processed during measure and arrange quadruples as well.

### Measurements Without Virtualization

Placing the same string in a `StackPanel` and measuring the time to complete layout, along with the total number of visuals produced, gives the following.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-measurement.png" alt="A measurement table comparing Label and TextBlock for 250, 1,000, and 4,000 elements placed in a StackPanel. Label always produces four times the visuals of TextBlock, while layout time is roughly twice as long." width="578" height="190" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 with the default theme (Aero2) <code>ControlTemplate</code>, giving <code>Content</code> a string that contains no access key. Elements are placed in a non-virtualized <code>StackPanel</code>. Visual counts change when the theme or <code>ControlTemplate</code> is replaced, and elapsed time depends on the execution environment, so read the values as ratios rather than absolute numbers.</figcaption>
</figure>

The visual count scales exactly with the element count, and `Label` is consistently four times that of `TextBlock`.
The layout time difference, however, stays around a factor of two and does not match the visual ratio.
The number of visuals alone does not determine the time.

Memory was measured too.
After laying out 1,000 elements and forcing a garbage collection, `Label` still held about 2.6 times the managed heap that `TextBlock` did.
A `Label` given a string containing an underscore held about 5.1 times.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-memory.svg" alt="A table of the managed heap still held after laying out 1,000 elements and forcing a garbage collection. Label holds 4,111 KB, 2.6 times TextBlock; a Label whose content contains an underscore holds 8,135 KB, 5.1 times; TextBlock holds 1,588 KB." width="422" height="170" loading="lazy">
  <figcaption>Measured in the same environment: 1,000 elements laid out in a <code>StackPanel</code>, taking the difference of <code>GC.GetTotalMemory(true)</code> before and after, median of five runs.</figcaption>
</figure>

### Extra Cost from Underscores in the Content

The cost of `Label` also varies with the content itself.
When the string contains an underscore (`_`), the `ContentPresenter` produces an `AccessText` rather than a `TextBlock`, adding one visual layer for a total of five.

Fixing the count at 1,000 and varying the composition gives the following.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-variants.png" alt="A table of visual counts and layout times for 1,000 elements in a StackPanel. Label takes 199 ms, a Label whose content contains an underscore 694 ms, a Label with a ContentTemplate 249 ms and 243 ms when given an underscore, ContentPresenter 147 ms, AccessText alone 234 ms, and TextBlock 129 ms. Only the Label containing an underscore stands out as markedly slower." width="441" height="311" loading="lazy">
  <figcaption>Measured in the same environment with 1,000 elements in a <code>StackPanel</code>. A <code>Label</code> whose <code>Content</code> contains an underscore has an <code>AccessText</code> inserted, one more visual than without an underscore, and its layout time was roughly 3.5 times as long. How much of the increase comes from the <code>AccessText</code> itself and how much from the extra visual was not measured. A <code>Label</code> with a <code>TextBlock</code> in its <code>ContentTemplate</code> takes no longer when given an underscore. The <code>AccessText</code> row is for comparison.</figcaption>
</figure>

The visual count grows only 25%, from four to five, yet layout time rises from 199 ms to 694 ms, roughly a factor of 3.5.
`AccessText` alone took about 1.8 times as long as `TextBlock` alone (234 ms against 129 ms).
The increase inside a `Label` (about 500 ms), however, is larger than the gap between the two alone (about 100 ms), so the cost of `AccessText` itself does not explain it. The breakdown of the increase was not measured.

A `Label` with a `TextBlock` in its `ContentTemplate` got no `AccessText` even for a string containing an underscore, and kept four visuals.
Its layout time was 243 ms, no different from a string without an underscore (249 ms).

This matters in practice on screens that display underscore-prone data such as file paths and identifiers.
The separate problem of underscores disappearing from the display is covered in [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/).

### With Virtualization Enabled

Everything above compares elements placed directly in a `StackPanel`, without virtualization.
An `ItemsControl` with UI virtualization changes the premise.
Only the containers within the visible range and the cache area before and after it, set by [`VirtualizingPanel.CacheLength`](https://learn.microsoft.com/dotnet/api/system.windows.controls.virtualizingpanel.cachelength), are realized, so the number of simultaneously live visuals stays constant no matter how large the collection grows.

Binding 10,000 items to a virtualized `ListBox` and swapping only the contents of `ItemTemplate` gives the following.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-virtualized.png" alt="A table of visual counts and layout times for 10,000 items in a virtualized ListBox. Label and TextBlock differ in visual count, but layout time is essentially the same." width="305" height="160" loading="lazy">
  <figcaption>Measured in the same environment with 10,000 items bound to a <code>ListBox</code> configured with <code>IsVirtualizing="True"</code> and <code>VirtualizationMode="Recycling"</code>. Only the containers in the visible range are realized, so the total visual count does not depend on the item count. The layout time difference is smaller than the difference between repeated runs of the same condition; the roughly 2x gap seen without virtualization does not survive.</figcaption>
</figure>

Compared with 4,000 elements in a non-virtualized `StackPanel`, the item count here is 2.5 times larger while layout time is two orders of magnitude smaller.
The gap between `Label` and `TextBlock` is smaller than the difference between repeated runs of the same condition.
The medians of 15 runs differ by about 4 ms, while `Label` alone ranged from 15.5 ms to 114.5 ms.

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-spread.svg" alt="A table of the minimum, median, and maximum layout time over 15 runs. With 1,000 elements in a StackPanel, Label takes 256.5, 273.8, and 420.9 ms and TextBlock 136.7, 151.1, and 213.4 ms. With 10,000 items in a virtualized ListBox, Label takes 15.5, 20.5, and 114.5 ms and TextBlock 12.8, 16.4, and 75.6 ms." width="474" height="200" loading="lazy">
  <figcaption>The distribution of the 15 runs behind the two tables above, whose values are the minimums.</figcaption>
</figure>

**The primary cause of "many Labels make WPF slow" is not `Label` itself but the absence of virtualization.**
Switching controls buys roughly a factor of two; enabling virtualization buys far more.

---

## Order of Improvements

Work through the improvements in this order.

1. **Confirm that virtualization is in effect first.** For list displays, use an `ItemsControl`-family control and keep UI virtualization enabled. If this is broken, replacing controls yields limited benefit.
2. **Move display-only text to `TextBlock`.** Effective on screens where virtualization is unavailable, or where a fixed but large number of elements is present.
3. **Use `ContentTemplate` for underscore-bearing data.** This keeps `Label` while avoiding the additional cost of `AccessText`.

The separation by purpose is as follows.

- Display-only text: use `TextBlock` as the default.
- Input form captions: use `Label` only where `Target` or access keys are required.
- Reworking existing screens: avoid a blanket replacement; convert to `TextBlock` incrementally within what the requirements allow.

### Step 1: keep virtualization in effect

For list displays, choose a control backed by a virtualizing panel rather than using `ItemsControl` as-is.
The default `ItemsPanel` of `ItemsControl` is a `StackPanel`, which does not virtualize.

```xml
<!-- Not virtualized. Containers are created in proportion to the item count -->
<ItemsControl ItemsSource="{Binding Items}" />

<!-- Virtualized. ListBox defaults to VirtualizingStackPanel -->
<ListBox ItemsSource="{Binding Items}"
         ScrollViewer.CanContentScroll="True"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling" />
```

To virtualize while staying on `ItemsControl`, the `ItemsPanel` must be set to `VirtualizingStackPanel` and the template must supply a `ScrollViewer`.
Even when selection is unnecessary, using `ListBox` and disabling `Focusable` and the selection visuals through a style is the more reliable route.

Note that setting `ScrollViewer.CanContentScroll` to `False` switches to pixel-based scrolling at the cost of virtualization.
In a `ListBox` with 2,000 items, 23 `ListBoxItem`s were realized by default, but all 2,000 were realized with `CanContentScroll="False"`.
`CanContentScroll` defaults to `False`; the `ListBox` default style sets it to `True`.
For smooth scrolling, keep virtualization and set `VirtualizingPanel.ScrollUnit="Pixel"` instead. The realized `ListBoxItem`s stayed at 23 and only the scroll unit changed to pixels (`ExtentHeight` went from the item count, 2,000, to 39,920 pixels).

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-scrolling.svg" alt="A table for a ListBox with 2,000 items after scrolling halfway. ScrollViewer.CanContentScroll defaults to False. The default ListBox has CanContentScroll True, ExtentHeight 2,000, and 23 realized items. With ScrollUnit set to Pixel, ExtentHeight is 39,920 with 23 realized items. With CanContentScroll set to False, ExtentHeight is 39,920 and all 2,000 items are realized." width="834" height="200" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 by showing a <code>ListBox</code> with 2,000 items, scrolling to the middle, and counting the <code>ListBoxItem</code>s. <code>ExtentHeight</code> is the item count for item-based scrolling and the pixel count for pixel-based scrolling.</figcaption>
</figure>

### Step 2: replace display-only Labels with TextBlock

Replace `Label` controls used purely for display with `TextBlock`.
The goal is to reduce rendering cost while preserving appearance.

```xml
<!-- Before -->
<Label Content="Status: Running" />

<!-- After -->
<TextBlock Text="Status: Running" />
```

This removes `Label`-specific features from display-only locations and unifies them into a lightweight rendering structure.
Because `Label`'s default `Padding` is lost, specify `Margin` or `Padding` explicitly where needed.

### Step 3: use a ContentTemplate for underscore-bearing data

To keep the appearance of `Label` while avoiding `AccessText`, specify a `TextBlock` in `ContentTemplate`.

```xml
<Label Content="{Binding FilePath}">
    <Label.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding}" />
        </DataTemplate>
    </Label.ContentTemplate>
</Label>
```

The `ContentPresenter` no longer produces an `AccessText`, so underscores render as-is and the rendering cost returns to that of a `Label` without underscores.
Because `ContentTemplate` also applies when `Content` is an object rather than a string, restrict its scope to string display.

---

### Where Label stays

The example below shows an input form caption that requires `Label`.
This pattern keeps `Label` to preserve usability and accessibility.

```xml
<StackPanel Orientation="Horizontal">
    <Label Content="_Name:"
           Target="{Binding ElementName=NameTextBox}"
           VerticalAlignment="Center" />
    <TextBox x:Name="NameTextBox" Width="180" />
</StackPanel>
```

`Label` is appropriate where focus movement via `Alt + N` and focus movement on label click are required.
Performance work therefore calls for requirement-based separation rather than a blanket ban.

An `AccessText` is constructed in this example because the access key is intentional, but form captions are few in number and the cost is immaterial.
`AccessText` cost matters only when displaying **large volumes** of underscore-bearing data.

## Notes and Limitations

- Replacing `Label` with `TextBlock` loses access keys (`_`) and focus integration through `Target`.
- For long text, `TextWrapping="Wrap"` or `TextTrimming` must be specified on the `TextBlock` side, or the display may not match expectations.
- Existing UI sometimes relies on `Label`'s default padding, so line spacing and alignment need verification after replacement.
- When rendering many elements inside a `DataGrid` or `ItemsControl` template, evaluate the virtualization settings before the control choice. Replacing with `TextBlock` while virtualization remains disabled yields limited improvement.
- The measurements here cover layout time from `Measure` to `UpdateLayout`. They exclude actual rendering and the container-recycling cost incurred while scrolling. Evaluating scroll smoothness requires separate measurement under scroll operations.
- At a scale of a few dozen elements, the difference between `Label` and `TextBlock` is imperceptible. Replace only after identifying a screen that is actually slow.

---

## Alternatives / Comparison

| Approach | Advantages | Disadvantages | Best suited for |
| --- | --- | --- | --- |
| Enable virtualization | Makes cost independent of item count; the largest single win | Scrolling is item-based by default (`ScrollUnit="Pixel"` makes it pixel-based); incompatible with `CanContentScroll="False"` | Lists and grids with a variable item count |
| Keep everything as `Label` | Retains access keys and `Target` exactly as specified | High rendering cost at large volumes | Form-centric screens with few elements |
| Replace display-only spots with `TextBlock` | Reduces rendering and memory load | Requires revisiting padding and wrapping settings | Fixed layouts with many elements that cannot virtualize |
| `Label` + `ContentTemplate` | Avoids `AccessText` cost while preserving appearance | More verbose XAML | Screens displaying underscore-bearing data in a `Label` |
| Convert the whole screen to `TextBlock` | Simple and lightweight | Loses `Label`-specific interaction | Read-only screens with no input integration |

---

## Summary

The slowdown from placing many WPF `Label` controls stems from the rendering overhead of its general-purpose `ContentControl` features.
Each `Label` constructs four visuals, four times that of `TextBlock`.
Without virtualization, that translates to roughly 2x in layout time and 2.6x in the managed heap still held after a garbage collection (measured with `GC.GetTotalMemory(true)`, not total memory use).

The practical priority, however, is as follows.

- **Check virtualization first.** With virtualization enabled, no practical layout-time difference remains between `Label` and `TextBlock`. This outweighs any control replacement.
- **Move to `TextBlock` on screens that cannot virtualize.** Control choice does matter where a fixed, large number of elements is present.
- **Use `ContentTemplate` when displaying underscore-bearing data in a `Label`.** With an `AccessText` inserted, the tree had one more visual and layout took roughly 3.5 times as long.
- **Keep `Label` where `Target` and access keys are required.** At the scale of form captions, the cost is immaterial.

Rather than "avoid `Label` because it is slow," first confirm whether virtualization is in effect and whether the screen genuinely renders at volume, then choose according to the requirements.

---

<!-- Related articles -->
- [Why WPF Label Hides Underscores and How to Fix It](/articles/wpf-label-underscore-issue/)
- [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/)
