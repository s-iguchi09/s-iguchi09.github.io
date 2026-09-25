---
layout: article-en
title: "Applying Fluent Design in WPF Without Extra Libraries"
date: 2026-05-30
category: WPF
excerpt: "Fluent styling in WPF with built-in features only: App.xaml theme setup, Fluent theme brushes that follow light and dark, and what SystemColors do not follow."
image: /images/articles/wpf-fluent-design-with-systemcolors/fluent-systemcolors-card.png
---

## Overview

This article describes how to bring Fluent-style visual design to a WPF application without adding external UI libraries.  
The approach uses built-in WPF styling, spacing, corner radius, visual hierarchy, and the Fluent theme's own brushes so the UI follows the light or dark theme. It also shows what `SystemColors` do and do not follow.  

---

## Prerequisites / Environment

- Framework / Language: .NET 9 / C# 13
- Target UI: WPF `Window`, `UserControl`, `Button`, `TextBlock`
- Architecture: MVVM or code-behind (the XAML patterns in this article work for both)
- Constraint: no external Fluent UI library (for example, MahApps.Metro or ModernWpf)
- Verification environment: .NET 10 / Windows 11

The measurements in this article were taken in the environment above, on a machine set to the Windows dark mode. `systemcolors-values.svg` reads the color each `SystemColors` key returns and its relative luminance; `systemcolors-tracking.svg` reads the referenced values before and after an application resource is swapped; `theme-brush-values.svg` reads the brush keys under `ThemeMode` `Light` and `Dark`. The screenshots render the article's XAML.
The following points were confirmed in that environment:

- `HighlightColor` for a selected item and `AccentColor` from personalization settings are different values.
- A color read directly and baked in does not follow a later swap.
- A resource key referenced dynamically (through `DynamicResource` in this article's XAML) does follow the swap.
- `SystemColors` did not change with the Windows dark mode or between `ThemeMode` `Light` and `Dark`; the Fluent theme brush keys did.

---

## Problem

The default WPF theme is stable and predictable, but its visual density and spacing often diverge from current Windows design language.  
In multi-window business applications, default control styles can make interaction priority less clear, especially when all elements have similar weight and low hierarchy contrast.  

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/fluent-default-theme.png" alt="A WPF window using the default theme. A heading, body text and a square-cornered button sit on the same surface as the window background." width="426" height="293" loading="lazy">
  <figcaption>The same controls under the default theme (Aero2). The card surface is not separated from the background and the button corners are square, which makes it hard to tell which element is the primary action.</figcaption>
</figure>

---

## Cause / Background

WPF provides flexible rendering and templating, but Fluent-specific visuals are not automatically applied by default.  
A Fluent-like result requires explicit decisions for:

- corner radius and spacing,
- hierarchy separation through background and border contrast,
- limited and intentional accent usage,
- colors that follow the light or dark theme.

Two sets of colors are built in. The Fluent theme's own brush keys, such as `ApplicationBackgroundBrush`, switch with the theme. `SystemColors` return the Windows system colors instead of hard-coded values, but, as measured below, they do not follow the dark mode.  

---

What `SystemColors` actually returns can be read back and checked.

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/systemcolors-values.svg" alt="A table of the color each SystemColors key returns along with its relative luminance. WindowColor is white, WindowTextColor is black, HighlightColor is a blue, and AccentColor is a red, all matching the OS settings. The first row records that the machine was set to the Windows dark mode." width="603" height="320" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 by reading each <code>SystemColors</code> key, on a machine set to the Windows dark mode (the first row). <code>relative luminance</code> is the WCAG relative luminance, included to gauge foreground-to-background contrast.</figcaption>
</figure>

`WindowColor` and `WindowTextColor` come out at `1.00` and `0.00` although this machine is set to the dark mode: `SystemColors` did not follow the Windows dark mode.

`HighlightColor` is the highlight color of a selected item. The accent color the user picks in personalization settings is a separate key, `AccentColor`, and the two are easily confused.
The machine used here has its accent set to a red, so the table shows `HighlightColor` as `#FF0078D7` and `AccentColor` as `#FFE2241A`. A hard-coded accent will disagree with that setting.

**Reading these keys is not by itself enough to follow a later replacement, though.**
Reading a color directly, as in `SystemColors.WindowColor`, bakes in the value as of that read.

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/systemcolors-tracking.svg" alt="A table of the value before and after the system brush is replaced, per way of referencing the color. A brush built from SystemColors.WindowColor stays white; only the side referencing SystemColors.WindowBrushKey through DynamicResource takes the new color." width="697" height="140" loading="lazy">
  <figcaption><code>SystemColors.WindowBrushKey</code> in the application's resources replaced, with both values read on either side of the replacement. What this measures is whether each side follows that replacement; an OS theme switch itself is not measured here.</figcaption>
</figure>

The directly read side keeps its value; only the side referencing the resource key through `DynamicResource` takes the new color.
**Following a replacement requires referencing a resource key such as `SystemColors.WindowBrushKey` through `DynamicResource`, not the color.**

What `DynamicResource` cannot give is a change that never happens. Switching `ThemeMode` between `Light` and `Dark` changed every Fluent brush key, but none of the `SystemColors` keys:

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/theme-brush-values.svg" alt="A table of brush keys under ThemeMode Light and Dark. The Fluent keys ApplicationBackgroundBrush, CardBackgroundFillColorDefaultBrush, TextFillColorPrimaryBrush, TextFillColorSecondaryBrush, and AccentFillColorDefaultBrush all change, for example the background from FAFAFA to 202020. SystemColors.WindowBrushKey stays white, ControlTextBrushKey stays black, and AccentColorBrushKey stays the same red in both." width="610" height="320" loading="lazy">
  <figcaption>Measured on .NET 10 / Windows 11 by showing a window with each <code>ThemeMode</code> and looking up every key from it.</figcaption>
</figure>

Background, surface, and text colors that should follow the theme therefore have to come from the Fluent brush keys. `SystemColors` suit colors that come from Windows itself; `SystemColors.AccentColorBrushKey`, for example, returned the personalization accent under both themes.

---

## Solution

Without external libraries, combine the following:

- Define Fluent theme activation at the application level in `App.xaml`, using either `ThemeMode` or the Fluent resource dictionary merge.
- Use `DynamicResource` with the Fluent theme's brush keys, such as `ApplicationBackgroundBrush` and `TextFillColorPrimaryBrush`, so colors switch with the light or dark theme.
- Define control templates for corner radius, spacing, and hover/press feedback.
- Separate page background, card surface, and accent roles to improve visual hierarchy and readability.

For .NET 9 Fluent theme adoption across an entire app, `App.xaml` configuration is the key step.  
If setup is done only per window, consistency becomes difficult as new screens are added.  

---

## Implementation

### 1. Enable Fluent Theme in App.xaml

To apply Fluent styling at the application level, configure `App.xaml`.  
In .NET 9, there are two valid options.  

Use `ThemeMode`:

```xml
<Application x:Class="Sample.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml"
             ThemeMode="System">
  <Application.Resources>
    <ResourceDictionary />
  </Application.Resources>
</Application>
```

Use the Fluent resource dictionary:

```xml
<Application x:Class="Sample.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

The two are not equivalent. The reference for the [`Application.ThemeMode` property](https://learn.microsoft.com/dotnet/api/system.windows.application.thememode) notes that `ThemeMode` also controls the window backdrop and dark mode, and recommends against also merging the Fluent dictionaries by hand, because the manual merge takes precedence. The sample in the next step uses `ThemeMode`.  
Defining one of them in `App.xaml` first keeps window-level styling focused on local adjustments and reduces theme drift across screens.  

### 2. Use Fluent Theme Brushes in Window-Level Styling

After app-level theme setup, define local styles for layout hierarchy and interaction feedback.
The following sample takes the background, card, and text colors from the Fluent theme's own brush keys, and the button color from its accent keys, all through `DynamicResource`, so they switch with the light or dark theme.

```xml
<Window x:Class="Sample.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Fluent Without External Libraries"
        Width="440" Height="300"
        Background="{DynamicResource ApplicationBackgroundBrush}">

  <Window.Resources>
    <Style x:Key="CardBorderStyle" TargetType="Border">
      <Setter Property="Padding" Value="24" />
      <Setter Property="CornerRadius" Value="12" />
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="Background" Value="{DynamicResource CardBackgroundFillColorDefaultBrush}" />
      <Setter Property="BorderBrush" Value="{DynamicResource CardStrokeColorDefaultBrush}" />
    </Style>

    <Style x:Key="AccentButtonStyle" TargetType="Button">
      <Setter Property="Padding" Value="14,8" />
      <Setter Property="Margin" Value="0,12,0,0" />
      <Setter Property="HorizontalAlignment" Value="Left" />
      <Setter Property="Foreground" Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
      <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}" />
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="Root"
                    Background="{TemplateBinding Background}"
                    CornerRadius="8"
                    Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center"
                                VerticalAlignment="Center" />
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Root" Property="Opacity" Value="0.92" />
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="Root" Property="Opacity" Value="0.82" />
              </Trigger>
              <Trigger Property="IsEnabled" Value="False">
                <Setter TargetName="Root" Property="Opacity" Value="0.55" />
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>

  <Grid Margin="32">
    <Border Style="{StaticResource CardBorderStyle}">
      <StackPanel>
        <TextBlock FontSize="24"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}"
                   Text="WPF Fluent Style" />

        <TextBlock Margin="0,10,0,0"
                   TextWrapping="Wrap"
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                   Text="Fluent theme brushes follow the light and dark theme." />

        <Button Style="{StaticResource AccentButtonStyle}"
                Content="Run Action" />
      </StackPanel>
    </Border>
  </Grid>
</Window>
```

This keeps the implementation dependency-free while improving hierarchy and interaction feedback.
The same XAML under `ThemeMode` `Light` and `Dark`:

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/fluent-systemcolors-card.png" alt="The window from the XAML above under ThemeMode Light. A light gray background, a white rounded card with a dark heading and gray body text, and a red rounded accent button." width="426" height="293" loading="lazy">
  <figcaption>The XAML above under <code>ThemeMode="Light"</code>, with no external libraries involved. Compared with the figure in the Problem section, the card surface separates from the background, and the rounded corners, spacing, and accent button establish a hierarchy.</figcaption>
</figure>

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/fluent-card-dark.png" alt="The same window under ThemeMode Dark. A dark background, a slightly lighter card with white heading and light gray body text, and a salmon accent button with dark text." width="426" height="293" loading="lazy">
  <figcaption>The same XAML under <code>ThemeMode="Dark"</code>. Every color comes from a theme brush key, so the whole window switches.</figcaption>
</figure>

Building the same window from `SystemColors` does not work under the dark theme. With the background, card, and text taken from `SystemColors` keys, the window stayed white while the Fluent button style switched to dark:

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/systemcolors-under-dark.png" alt="A window whose background, card, and text use SystemColors keys, under ThemeMode Dark. The window and the card stay white and light gray with black text, while the button turns dark gray with nearly invisible text." width="426" height="293" loading="lazy">
  <figcaption>Background, card, and text from <code>SystemColors</code> keys through <code>DynamicResource</code>, under <code>ThemeMode="Dark"</code>. The <code>SystemColors</code> values do not change, so only the controls styled by Fluent turn dark.</figcaption>
</figure>

---

## Notes

- This approach reproduces Fluent design principles, not a full WinUI material stack. `ThemeMode` does apply the window backdrop (Mica), according to its reference, but Acrylic and other WinUI materials are outside this baseline.
- For app-wide Fluent adoption in .NET 9, configure either `ThemeMode` or the Fluent dictionary in `App.xaml`. Relying only on per-window configuration increases the chance of theme omissions.
- Reference theme brushes through `DynamicResource`. A `StaticResource` reference resolves once and does not switch with the theme. `SystemColors` do not switch with the dark mode or `ThemeMode` at all, so do not use them for surfaces that must follow the theme.
- For larger applications, centralize shared styles in `App.xaml` or a common `ResourceDictionary` to avoid duplication.

---

## Alternatives / Comparison

| Method                               | Advantages                                                                           | Disadvantages                                              | Best suited for                                        |
| ------------------------------------ | ------------------------------------------------------------------------------------ | ---------------------------------------------------------- | ------------------------------------------------------ |
| WPF built-in styles + theme brushes  | No additional package dependencies, easier long-term maintenance, follows light/dark | Limited advanced Fluent material effects                   | Existing WPF systems with maintenance-first priorities |
| External Fluent UI library           | Faster visual unification with ready-made themes                                     | Dependency lifecycle and compatibility checks are required | New apps with high UI delivery speed requirements      |
| Fully custom rendering               | Maximum visual freedom                                                               | Highest implementation and testing cost                    | Products with strict custom branding requirements      |

---

## Summary

Fluent-style UI in WPF can be implemented without extra libraries.  
The practical baseline is: configure Fluent activation in `App.xaml` (either `ThemeMode` or Fluent dictionary), then build visual hierarchy with spacing and corner radius, and reference the Fluent theme brushes through `DynamicResource` so colors follow the light or dark theme; `SystemColors` do not.  
This approach is generally the most maintainable option for long-lived WPF applications.  

---

## Related Articles

- [Hiding the Clear Button on a Fluent-Themed WPF TextBox](/articles/wpf-fluent-textbox-hide-clear-button/)
- [Controls with Custom Styles Fall Back to the Old Look Under the WPF Fluent Theme](/articles/wpf-fluent-theme-custom-style-not-applied/)
- [Diagnosing Why Parts of a WPF App Stay Light After Switching ThemeMode at Runtime](/articles/wpf-fluent-thememode-runtime-switch/)
