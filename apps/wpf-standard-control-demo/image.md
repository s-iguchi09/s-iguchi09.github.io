---
layout: control-demo
permalink: /apps/wpf-standard-control-demo/image.html
title: "Image"
badge: "Graphics"
lead: "Image displays a picture from an <code>ImageSource</code>, such as a bitmap file, and scales it to the space it is given."
description: "WPF Image measured on .NET 10: the size for each Stretch and StretchDirection, where UniformToFill crops, DPI, binding a file path, file locks, and decoding."
---

## The size for each Stretch and StretchDirection

**Image** derives from `FrameworkElement`, not `Control`, and is not focusable. `Stretch` decides how the picture is scaled to the space; the default is `Uniform`, which keeps the proportions. In a 300 × 200 area, a 600 × 300 picture was 600 × 300 with `None`, 300 × 200 with `Fill`, 300 × 150 with `Uniform`, and 400 × 200 with `UniformToFill`.

`StretchDirection` decides whether the picture may be enlarged, reduced, or both; the default is `Both`. `UpOnly` kept the 600 × 300 picture at 600 × 300 in every mode, and `DownOnly` kept the 100 × 50 picture at 100 × 50. Use `DownOnly` for icons that should never be enlarged past their own size.

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/image/image-matrix.svg" alt="Table of Image sizes in a 300 by 200 area: None keeps 100 by 50 and 600 by 300, Fill, Uniform and UniformToFill give 300 by 200, 300 by 150 and 400 by 200 unless UpOnly keeps the large picture or DownOnly keeps the small one" width="690" height="320" loading="lazy">
  <figcaption>Displayed size for each <code>Stretch</code> and <code>StretchDirection</code>. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Where UniformToFill and None cut the picture

The part outside the area is cut off, and it is cut from the right and bottom: with `UniformToFill`, the 400 × 200 picture started at the area's top-left corner. With `HorizontalAlignment` and `VerticalAlignment` set to `Center`, it started at -50, so both sides were cut evenly. Center the Image when you fill equal cells, such as thumbnails, with `UniformToFill`; otherwise the crop keeps the top-left of the picture.

## DPI and the decoded size

Without scaling, the size is in device-independent units, the pixels × 96 / DPI: 100 × 50 pixels at 72 DPI were 133.36 × 66.68. The same pixels are shown larger at a lower DPI, so check the file's DPI when you use `Stretch="None"`. PNG stores the resolution in pixels per metre, so even the 96 DPI PNGs came back as 100.01 wide.

Set `DecodePixelWidth` for thumbnails. With 100, a 600 × 300 picture was decoded to 100 × 50 pixels. It also changes the picture's own size: without scaling it was shown 100 wide, not 600.

## Binding a file path, and the file lock

A path string bound to `Source` is converted to an image: the `Source` became a `BitmapFrameDecode`. A path to a missing file, or to an SVG file, left `Source` `null` and the Image 0 × 0, without an exception. WPF decodes pictures with the Windows Imaging Component codecs installed, and the measuring machine had none for SVG. Convert SVG before showing it.

A file shown through a path is locked. While a PNG bound that way was shown, deleting the file failed with an `IOException`, and it still failed after `Source` was set to `null`; it succeeded only after a garbage collection. A `BitmapImage` with `CacheOption` set to `OnLoad` reads the file at once, and its file could be deleted while the image was shown. Load files that may change that way.

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/image/image-behavior.svg" alt="Table of Image results: it derives from FrameworkElement and is not focusable with Uniform and Both as defaults, UniformToFill and None cut the picture from the top-left unless centered, 72 DPI enlarges it, a bound path gives BitmapFrameDecode while missing and SVG files give null, the bound file stays locked until a garbage collection, OnLoad does not lock, DecodePixelWidth 100 gives 100 by 50, and the demo's start picture is 3840 by 2400 at 96 DPI" width="1140" height="440" loading="lazy">
  <figcaption>Type, cropping, DPI, paths, file locks, and decoding. Measured on .NET 10 / Windows 11.</figcaption>
</figure>

## Trying it in the demo app

![The Image page of the demo app, with the control list on the left and the first section, Source / Stretch / StretchDirection](/images/wpf-standard-control-demo/image.png){: .screenshot-img}

The Image page of the demo app binds `Source` to the text of a TextBox holding a file path, and sets `Stretch` and `StretchDirection` from two combo boxes. It starts with the path `C:\Windows\Web\Wallpaper\Windows\img0.jpg`, which existed on the measuring machine (Windows 11) as a picture of 3840 × 2400 pixels at 96 DPI. The combo boxes start at the first values, `None` and `UpOnly`, so the picture is shown at full size and only its top-left corner is visible. The "Show Code" link under the section displays its XAML. The following XAML is that section (`ImageUsageControl.xaml`), with the styles and the surrounding GroupBoxes left out and the namespace declarations added. In the demo app, the Image fills the result area of the window; the measurements on this page laid it out in an area of a fixed size (300 × 200) instead. `markupextensions` is the prefix for the demo app's own markup extensions:

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <TextBox x:Name="SourceTextBox" Text="C:\Windows\Web\Wallpaper\Windows\img0.jpg" />

  <ComboBox x:Name="StretchComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=Stretch}"
            SelectedValuePath="Value"
            SelectedIndex="0" />
  <ComboBox x:Name="StretchDirectionComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=StretchDirection}"
            SelectedValuePath="Value"
            SelectedIndex="0" />

  <Grid Background="#F0F0F0">
    <Image x:Name="DemoImage"
           Source="{Binding Text, ElementName=SourceTextBox}"
           Stretch="{Binding SelectedValue, ElementName=StretchComboBox}"
           StretchDirection="{Binding SelectedValue, ElementName=StretchDirectionComboBox}" />
  </Grid>
</StackPanel>
```

## Related controls and articles

- [Viewbox](/apps/wpf-standard-control-demo/viewbox.html) — the same `Stretch` and `StretchDirection`, for any element.
- [InkCanvas](/apps/wpf-standard-control-demo/inkcanvas.html) — a surface to draw on with the mouse or a pen.

## Source code and how it was measured

Every behavior on this page was measured by running it on .NET 10 / Windows 11, using [`ImageDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ImageDemoScene.cs){: target="_blank" rel="noopener noreferrer"} in this site's screenshot tool. The pictures were PNG files written by the tool, of 100 × 50 and 600 × 300 pixels, laid out in a 300 × 200 area. The path was bound from a TextBox as in the demo app, and a lock was detected by trying to delete the file.

[View Image source code on GitHub →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ImageUsage){: target="_blank" rel="noopener noreferrer"}
