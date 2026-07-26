---
layout: article-en
title: "Releasing the Image File Locked by BitmapImage in WPF with BitmapCacheOption.OnLoad"
date: 2026-07-25
category: WPF
excerpt: "An image file shown through BitmapImage cannot be deleted or overwritten. This covers the default caching behavior behind it and the OnLoad fix."
---

## Overview

A local image file displayed through a `BitmapImage` assigned to an `Image` control cannot be deleted or overwritten while the application is running.
This article explains that the behavior does not come from an explicit lock taken by `BitmapImage`.
The actual cause is the default caching policy, which keeps access to the image source open.
It then covers the fix based on `BitmapCacheOption.OnLoad`, the distinction between `UriSource` and `StreamSource`, a comparison of the `BitmapCacheOption` values, and the pitfalls around `Freeze` and memory usage.

---

## Prerequisites / Environment

- Framework: .NET 6 or later / WPF (`BitmapImage` and `BitmapCacheOption` have shipped since .NET Framework 3.0)
- Language: C# / XAML
- Target classes and features: `System.Windows.Controls.Image`, `BitmapImage`, `BitmapCacheOption`, `BitmapCreateOptions`
- Architecture: applicable to both MVVM and code-behind
- Assumption: the displayed image is a local file that changes at run time (a file chosen by the user, a downloaded temporary file), not a resource embedded at build time
- Namespaces: code samples assume `System`, `System.IO`, and `System.Windows.Media.Imaging`

---

## Problem

After creating a `BitmapImage` from a file path and assigning it to `Image.Source`, deleting or overwriting the same file throws an exception.
The following snippet reproduces the issue with the most direct implementation.

```csharp
// Display the image
PreviewImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));

// Deleting or overwriting the same file throws an IOException
File.Delete(path);
```

The image renders correctly, but `File.Delete` throws an `IOException` reporting that the file is in use.
Overwriting and renaming fail in the same way.
The failure is not deterministic.
Once the display is cleared and the reference to the `BitmapImage` is dropped, a garbage collection can release the file and allow the deletion to succeed.
The result is an intermittent defect where the deletion only succeeds some of the time.

Supplying the path from XAML produces the same outcome.
The following markup is the shortest form, assigning the path string directly to `Source`.

```xml
<Image Source="{Binding ImagePath}" Stretch="Uniform" />
```

Assigning a string directly to `Source` delegates the work to `ImageSourceConverter`, which leaves no place to specify `CacheOption`.
The image is therefore loaded with the default caching behavior, and the file is retained exactly as it is in the code-based version.

---

## Cause / Background

The cause is the **caching policy** of `BitmapImage`.
The default value of `BitmapImage.CacheOption` is `BitmapCacheOption.Default`.
The documentation for the `CacheOption` property describes the resulting behavior as follows.

> The default `OnDemand` cache option retains access to the stream until the image is needed, and cleanup is handled by the garbage collector.

Source: [BitmapImage.CacheOption Property](https://learn.microsoft.com/dotnet/api/system.windows.media.imaging.bitmapimage.cacheoption)

Both `Default` and `OnDemand` appear because the `BitmapCacheOption` enumeration defines them with the same value, `0`.
The descriptions attached to those fields, however, do not agree.
`Default` is documented as "Caches the entire image into memory" while `OnDemand` is documented as "Creates a memory store for requested data only", which is a contradiction inside the official reference.
Since the two share one value, only one behavior can apply.
The `CacheOption` remarks name `OnDemand` as the default and state that it "retains access to the stream until the image is needed".

`OnDemand` creates a memory store only for the data that has been requested.
The first request loads the image directly, and subsequent requests are filled from that cache.
To serve those later reads, access to the image source is retained.
Deferring the initialization of the object itself is a separate concern, controlled by `BitmapCreateOptions.DelayCreation` rather than by `CacheOption`.

When `UriSource` points at a local file, what is retained is understood to be the file stream WPF opened internally.
That stream is what holds the file open, and the application has no supported way to close it.
Release is left to the garbage collector, which is why the behavior cannot be reproduced on demand.

What the documentation states explicitly about `OnLoad` is that the stream used to create the `BitmapImage` can be closed afterwards.
No equivalent sentence covers a local file passed through `UriSource`, but since the retained resource is likewise an internal stream, the same mechanism applies.

The core issue is not that `BitmapImage` takes an exclusive lock, but that **the source stays open to serve later reads**.
The fix is therefore not to remove a lock, but to pull the whole image into memory at load time so that retaining the source becomes unnecessary.

---

## Solution

Set `CacheOption` to `BitmapCacheOption.OnLoad`.
`OnLoad` caches the entire image into memory at load time, and every subsequent request for image data is filled from that memory store.
Because the source no longer needs to be read, the file or stream can be released once initialization completes.

The difference between the default behavior and `OnLoad` is shown in the diagram below.

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-bitmapimage-file-lock-cacheoption/bitmapcacheoption-stream-lifetime.svg" alt="A diagram comparing the default cache behavior with OnLoad. With the default, BitmapImage keeps the image file stream open and File.Delete throws IOException, with release left to the garbage collector. With OnLoad, the whole image is cached in memory when EndInit completes, the stream is closed, and File.Delete succeeds." width="880" height="394" loading="lazy">
  <figcaption>The top lane shows the default (<code>Default</code> / <code>OnDemand</code>) handling of the file stream and the bottom lane shows <code>OnLoad</code>. By default the stream stays open to serve later reads, and the timing of its release is left to the garbage collector. With <code>OnLoad</code> the entire image is cached in memory when <code>EndInit</code> completes, so the stream is closed and the file can be deleted or overwritten.</figcaption>
</figure>

`CacheOption` can only be set while the `BitmapImage` is being initialized.
`BitmapImage` implements `ISupportInitialize`, so property initialization must be performed between `BeginInit` and `EndInit` calls.
Property changes made after initialization are ignored.

Two approaches are available.

- **`UriSource` with `OnLoad`** — pass the path directly.
  The markup and code stay short, and this is sufficient in most cases.
- **`StreamSource` with `OnLoad`** — pass a stream opened by the application and dispose it deterministically after initialization.
  This suits cases where the sharing mode or the origin of the data must be controlled.

---

## Implementation

### Combining UriSource with OnLoad

Set `CacheOption` and `UriSource` inside a `BeginInit` / `EndInit` block.
The whole image is pulled into memory at `EndInit`, so the file can be deleted or overwritten once the method returns.

```csharp
private static BitmapImage LoadWithoutLocking(string path)
{
    var bitmap = new BitmapImage();
    bitmap.BeginInit();
    bitmap.CacheOption = BitmapCacheOption.OnLoad;
    bitmap.UriSource = new Uri(path, UriKind.Absolute);
    bitmap.EndInit();
    bitmap.Freeze();
    return bitmap;
}
```

The `Freeze` call is optional, but `BitmapImage` derives from `Freezable`, and freezing removes the cost of change notification while making the object shareable across threads.
For designs that load images asynchronously and hand them to the UI thread, freezing is effectively mandatory.
A local file loaded with `OnLoad` is freezable at this point; where the conditions are less predictable, test with `CanFreeze` first, as described in the notes below.

An important distinction concerns the `BitmapImage(Uri)` constructor.
Objects created with that constructor are **automatically initialized**, and subsequent property changes are ignored.
The following code therefore leaves the file retained because `OnLoad` never takes effect.

```csharp
// Initialization already completed in the constructor, so the CacheOption change is ignored
var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));
bitmap.CacheOption = BitmapCacheOption.OnLoad;
```

Applying `OnLoad` requires the parameterless constructor combined with `BeginInit` and `EndInit`.

### Supplying an explicit stream through StreamSource

When the way the file is opened matters, open a `FileStream` explicitly and assign it to `StreamSource`.
With `OnLoad` specified, the whole image is already in memory by the time `EndInit` returns, so the image remains displayable after the `using` block disposes the stream.

```csharp
private static BitmapImage LoadFromStream(string path)
{
    var bitmap = new BitmapImage();
    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    {
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
    }

    bitmap.Freeze();
    return bitmap;
}
```

`FileShare` specifies the access granted to subsequent opens, by this process or another, while this handle is open.
`FileShare.ReadWrite` therefore allows other processes to open the same file for reading or writing while this `FileStream` is open.
That grant covers reading and writing only; deletion and renaming are not included.
Permitting deletion during the load requires `Delete` as well, written as `FileShare.ReadWrite | FileShare.Delete`.

When both `StreamSource` and `UriSource` are set, the `StreamSource` value is ignored.
`UriSource` is left unset in this approach.

### Specifying the option in XAML and its limits

When the image path is fixed, the whole solution fits in XAML.
Replace the type-converted string assigned to `Source` with an explicit `BitmapImage` object element carrying `CacheOption`.
Property assignments written on the object element are applied as part of its initialization, so `OnLoad` takes effect with this markup.

```xml
<Image Stretch="Uniform">
    <Image.Source>
        <BitmapImage UriSource="C:\work\preview.png" CacheOption="OnLoad" />
    </Image.Source>
</Image>
```

This `BitmapImage` is instantiated once, when the XAML is parsed.
Because property changes after initialization are ignored, binding `UriSource` does not cause a new path to be displayed.
To swap the image at run time, convert the path with an `IValueConverter` that builds a `BitmapImage`, or expose an `ImageSource` property from the view model and bind it to `Image.Source`.
The latter hands a new `ImageSource` to the control on every change, so the `BitmapImage` produced by `LoadWithoutLocking` above can be assigned directly.

---

## Notes

- **Trade-off against memory:** `OnLoad` expands the entire image into memory.
  For large images or numerous thumbnails, set `DecodePixelWidth` or `DecodePixelHeight` so the image decodes at roughly its rendered size.
  Set only one of them, not both, to preserve the aspect ratio.
- **Decode-size savings depend on the codec:** The JPEG and PNG codecs decode natively to the requested size, whereas other codecs decode at full size and then scale to the target.
  Peak memory savings are therefore smaller for other formats, such as BMP and TIFF.
- **A stale image can appear after an overwrite:** WPF keys its image cache by URI, so reloading a replaced file at the same path can still display the previous image.
  Setting `BitmapCreateOptions.IgnoreImageCache` on `CreateOptions` replaces existing cache entries even when they share the same `Uri`.
- **Conditions that prevent freezing:** An object cannot be frozen when it has animated or data-bound properties, has properties set by a dynamic resource, or contains sub-objects that cannot be frozen.
  Where the conditions are not predictable, test with `CanFreeze` before calling `Freeze`.
- **Modifying a frozen object throws:** Attempting to modify a frozen `Freezable` raises an `InvalidOperationException`.
  When post-processing is required, keep the object unfrozen or create a modifiable copy with `Clone`.
- **Unfrozen objects cannot cross threads:** A `Freezable` whose `IsFrozen` is `false` can be accessed only from the thread that created it, and access from another thread throws an `InvalidOperationException`.
  Freeze the image before handing it from a background thread to the UI thread.
- **`BitmapCacheOption.None` is not a fix:** `None` creates no memory store and fills every request directly from the image file.
  That behavior requires access to the source to be retained, so it cannot be used to avoid the lock.

---

## Alternatives / Comparison

| Approach | File release | Memory store | Best suited for |
| --- | --- | --- | --- |
| `UriSource` with the default (`Default` / `OnDemand`) | Not released (left to the GC) | Created for requested data only | Fixed images that are never replaced while the application runs |
| `UriSource` with `OnLoad` | Released when initialization completes | Created for the whole image at load time | Local files that may be deleted or overwritten at run time |
| `StreamSource` with `OnLoad` | Released explicitly via `using` | Created for the whole image at load time | Cases requiring a specific sharing mode or in-memory data |
| `BitmapCacheOption.None` | Not released | Not created | Cases where re-reading from the file on every request is acceptable |

The criterion for choosing between `UriSource` and `StreamSource` is straightforward.
`UriSource` is sufficient when the image is simply read from a path.
`StreamSource` applies when the application must decide how the stream is obtained, such as specifying `FileShare`, reading across a network, or decrypting data before decoding.

---

## Summary

The file lock caused by `BitmapImage` originates in the default caching policy, which keeps the source open to serve later reads.
The selection criteria are as follows.

- **Displaying a local file that may later be deleted or overwritten:** Set `CacheOption` to `OnLoad` inside a `BeginInit` / `EndInit` block.
  This is the primary choice.
- **Controlling the sharing mode or decoding from in-memory data:** Combine `StreamSource` with `OnLoad` and dispose the stream explicitly after initialization.
- **Reloading a replaced image:** Add `BitmapCreateOptions.IgnoreImageCache` to prevent the per-URI cache from returning the previous image.
- **Loading on a background thread:** Call `Freeze` after `EndInit` before passing the image to the UI thread.

One prerequisite applies to every approach above: the `BitmapImage(Uri)` constructor completes initialization automatically and ignores later property changes.
Any implementation that relies on `OnLoad` must therefore use the parameterless constructor together with `BeginInit` and `EndInit`.

---

<!-- Related articles -->
- [Fixing the Cross-Thread Exception When Updating an ObservableCollection in WPF](/articles/wpf-observablecollection-cross-thread-update/)
