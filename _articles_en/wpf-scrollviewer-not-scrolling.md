---
layout: article-en
title: "Why a WPF ScrollViewer Does Not Scroll and How to Fix It"
date: 2026-07-17
category: WPF
excerpt: "A ScrollViewer inside a StackPanel does not scroll because the StackPanel never constrains its height. This covers the cause and the Grid and DockPanel fixes."
---

## Overview

The WPF `ScrollViewer` shows scrollbars and enables scrolling when its content is larger than the available viewport.
When a `ScrollViewer` is placed inside a `StackPanel`, however, the scrollbars never appear and the content keeps growing downward regardless of how many items it holds.
This article explains that the behavior comes from how the layout system measures elements, and it organizes the container-based fixes along with the criteria for choosing between them.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF
- Language: C# / XAML
- Target controls: `ScrollViewer`, `StackPanel`, `Grid`, `DockPanel`
- Architecture: applicable to both MVVM and code-behind

---

## Problem

Placing a `ScrollViewer` inside a vertical `StackPanel` and filling it with many items often results in no scrollbar; the content extends past the visible area instead.

```xml
<StackPanel>
    <TextBlock Text="Header" />
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <!-- many items -->
        </StackPanel>
    </ScrollViewer>
</StackPanel>
```

Even though `VerticalScrollBarVisibility="Auto"` is set, the `ScrollViewer` expands to the full height of its content and no scrollbar is shown.

---

## Cause / Background

The cause lies in the available size that the `StackPanel` passes to its children during measurement.
In its stacking direction, which is height for a vertical panel, a `StackPanel` measures each child with an **infinite available size**.
The official documentation notes that a `StackPanel` does not constrain its children in the direction it stacks.

A `ScrollViewer` shows a scrollbar only when its content is larger than the height it is given.
When the `StackPanel` passes an infinite height, the `ScrollViewer` requests exactly the height needed to fit all of its content, so no overflow occurs.
No scrollbar appears, and the `ScrollViewer` itself grows to the full height of its content.

The root of the problem is therefore not the `ScrollViewer` but the surrounding `StackPanel`, which never constrains the height.

---

## Solution

Place the `ScrollViewer` in a container that constrains its height.
Specifically, put it in a `Grid` row sized with `*` (star), let a `DockPanel` fit it into the remaining space, or give it an explicit `Height` or `MaxHeight`.
A finite height is then passed to the `ScrollViewer`, and a scrollbar appears once the content exceeds that height.

---

## Implementation

### Place it in a star-sized Grid row

Split the `Grid` into a fixed-size row and a `*` row that fills the remaining space, and put the scrollable `ScrollViewer` in the `*` row.

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <TextBlock Grid.Row="0" Text="Header" />

    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <!-- many items -->
        </StackPanel>
    </ScrollViewer>
</Grid>
```

The `*` row receives the parent's remaining height, so the `ScrollViewer` is given a finite height.
When the content exceeds that height, the scrollbar appears automatically.

### Fit it into the remaining space with a DockPanel

A `DockPanel` expands its last child into the remaining area through `LastChildFill`, which is enabled by default.
Dock the header to the top and place the `ScrollViewer` as the last child.

```xml
<DockPanel>
    <TextBlock DockPanel.Dock="Top" Text="Header" />

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <!-- many items -->
        </StackPanel>
    </ScrollViewer>
</DockPanel>
```

The last child without a `DockPanel.Dock` value fills the remaining area, so the height of the `ScrollViewer` is constrained.

---

## Notes

- **`VerticalScrollBarVisibility="Disabled"` turns scrolling off:** setting `Disabled` prevents scrolling in that direction through user interaction.
  To hide the scrollbar while keeping scrolling, use `Hidden` instead.
- **Avoid nesting scrollable controls directly:** placing a control with its own scrolling, such as a `ListBox`, directly inside a `ScrollViewer` can make the mouse wheel act on the wrong element.
  A `ListBox` already scrolls internally, so it is better given a height-constrained layout, such as a `*` row of a `Grid`, than wrapped in an outer `ScrollViewer`.
- **Physical versus logical scrolling:** when `ScrollViewer.CanContentScroll` is `false`, which is the default, scrolling is physical, in pixel units; when it is `true`, scrolling is logical, by item.
  A `ListBox` hosts its items in a `VirtualizingStackPanel`, which relies on logical scrolling to virtualize items; if `CanContentScroll` becomes `false`, the panel falls back to physical scrolling and its virtualization is lost, so this should be avoided for long lists.

---

## Alternatives / Comparison

| Container | Scrolling behavior | Suited for |
| --- | --- | --- |
| `StackPanel` (vertical) | Does not constrain height, so the `ScrollViewer` does not scroll unless given an explicit `Height` or `MaxHeight` | Short content that needs no scrolling |
| `Grid` with a `*` row | Passes the remaining height; scrolls when content overflows | Screens that separate headers or footers from a variable area |
| `DockPanel` (`LastChildFill`) | Fits into the remaining area; scrolls when content overflows | Screens combining edge-docked elements with a main area |
| Explicit `Height` / `MaxHeight` | Scrolls once the specified height is exceeded | A partial list with a fixed height cap |

---

## Summary

A `StackPanel` passes an infinite height to its children in the stacking direction, so a `ScrollViewer` inside it cannot detect overflow and does not scroll.
The root of the problem is the surrounding container that fails to constrain the height, not the `ScrollViewer` itself.

The selection criteria are as follows.

- **To separate headers or footers from a variable area:** place the `ScrollViewer` in a `*` row of a `Grid`.
  Rows can be assigned by role, which makes the layout intent explicit.
- **To combine edge-docked elements with a main area:** use a `DockPanel` and place the `ScrollViewer` as the last child in the remaining space.
- **To cap only the height of a partial list:** set `MaxHeight` and scroll once that height is exceeded.

In every case, the key is a layout that passes a finite height to the `ScrollViewer`.

---

<!-- Related articles -->
- [How to Prevent SelectedItems from Appearing Lost in a Virtualized WPF ListBox](/articles/wpf-listbox-virtualization-selecteditems/)
