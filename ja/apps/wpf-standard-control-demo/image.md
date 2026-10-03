---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/image.html
title: "Image"
badge: "Graphics"
lead: "Image は、ビットマップのファイルなどの <code>ImageSource</code> から画像を表示し、与えられた領域に合わせて拡大・縮小する要素です。"
description: "WPF の Image を .NET 10 で実測して解説。Stretch と StretchDirection の組み合わせごとの大きさ、UniformToFill でどこが切れるか、DPI、ファイルのパスのバインド、ファイルのロック、デコードの大きさを確かめます。"
---

## Stretch と StretchDirection ごとの大きさ

**Image** は `Control` ではなく `FrameworkElement` から派生し、既定ではフォーカスを受け取りません（`Focusable` が `False`）。`Stretch` は領域に合わせた拡大・縮小の仕方で、既定値は縦横比を保つ `Uniform` です。300 × 200 の領域で、600 × 300 の画像は `None` で 600 × 300、`Fill` で 300 × 200、`Uniform` で 300 × 150、`UniformToFill` で 400 × 200 でした。

`StretchDirection` は、拡大だけ・縮小だけ・両方のどれを許すかで、既定値は `Both` です。`UpOnly` では、600 × 300 の画像はどの Stretch でも 600 × 300 のままで、`DownOnly` では 100 × 50 の画像が 100 × 50 のままでした。原寸より大きくしたくないアイコンには `DownOnly` を使います。

{% include tables/wpf-standard-control-demo/verification/image/image-matrix.ja.md %}

<code>Stretch</code> と <code>StretchDirection</code> ごとの表示の大きさ。.NET 10 / Windows 11 で計測。
{: .table-caption}

## UniformToFill と None で切れる場所

領域からはみ出した部分は切れ、切れるのは右と下です。`UniformToFill` の 400 × 200 の画像は、領域の左上から始まっていました。`HorizontalAlignment` と `VerticalAlignment` を `Center` にすると -50 から始まり、左右が均等に切れました。サムネイルのように同じ大きさの枠を `UniformToFill` で埋めるときは、Image を中央に揃えます。そうしないと、切り取りで画像の左上が残ります。

## DPI とデコードの大きさ

拡大・縮小しないときの大きさはデバイス非依存の単位で、ピクセル数 × 96 / DPI です。72 DPI の 100 × 50 ピクセルは計算では 133.33 × 66.67 ですが、測った 72 DPI の画像は 133.36 × 66.68 でした。同じピクセル数でも DPI が低いほど大きく表示されるので、`Stretch="None"` ではファイルの DPI を確かめます。ここで使った PNG は解像度を `pHYs` チャンクに 1 メートルあたりのピクセル数の整数で保存しているため、実際に効く DPI は公称値よりわずかに低くなります。測った大きさは、72 DPI で 2834 ピクセル/メートル（71.98 DPI）、96 DPI で 3779 ピクセル/メートル（95.99 DPI）と一致し、そのため 96 DPI の画像でも幅は 100.01 になりました。

サムネイルには `DecodePixelWidth` を設定します。100 にすると、600 × 300 の画像は 100 × 50 ピクセルにデコードされました。画像自体の大きさも変わり、拡大・縮小しないときの幅は 600 ではなく 100 でした。

## ファイルのパスのバインドと、ファイルのロック

`Source` にバインドしたパスの文字は画像に変換され、`Source` は `BitmapFrameDecode` になりました。存在しないファイルや SVG ファイルのパスでは、`Source` は `null`、Image は 0 × 0 のままで、例外は起きませんでした。WPF はインストールされている Windows Imaging Component のコーデックで画像をデコードし、計測したマシンには SVG のコーデックがありませんでした。SVG は変換してから表示します。

パスで表示したファイルはロックされます。そうしてバインドした PNG を表示している間、ファイルの削除は `IOException` で失敗し、`Source` を `null` にした後も失敗しました。削除できたのはガベージコレクションの後でした。`CacheOption` を `OnLoad` にした `BitmapImage` はファイルをすぐに読み込み、表示している間でもファイルを削除できました。変わる可能性のあるファイルは、この方法で読み込みます。

{% include tables/wpf-standard-control-demo/verification/image/image-behavior.ja.md %}

型、切り取り、DPI、パス、ファイルのロック、デコード。.NET 10 / Windows 11 で計測。
{: .table-caption}

## デモアプリで試す

![デモアプリの Image のページ。左にコントロールの一覧、右に最初の節の Source / Stretch / StretchDirection](/images/wpf-standard-control-demo/image.png){: .screenshot-img}

デモアプリの Image のページは、`Source` をファイルのパスを入れた TextBox の文字にバインドし、`Stretch` と `StretchDirection` を 2 つのコンボボックスで選びます。パス `C:\Windows\Web\Wallpaper\Windows\img0.jpg` で始まり、計測したマシン（Windows 11）には、3840 × 2400 ピクセル・96 DPI の画像としてありました。コンボボックスは先頭の値の `None` と `UpOnly` で始まるので、画像は原寸で表示され、見えるのは左上の部分だけです。欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、その欄（`ImageUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。デモアプリでは Image はウィンドウの結果の領域いっぱいに置かれますが、このページの計測では大きさを固定した領域（300 × 200）に配置しました。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

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

## 関連するコントロールと記事

- [Viewbox](/ja/apps/wpf-standard-control-demo/viewbox.html) — 同じ `Stretch` と `StretchDirection` を、任意の要素に使えます。
- [InkCanvas](/ja/apps/wpf-standard-control-demo/inkcanvas.html) — マウスやペンで描き込む面です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ImageDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ImageDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。画像はツールが書き出した 100 × 50 ピクセルと 600 × 300 ピクセルの PNG ファイルで、300 × 200 の領域に配置しました。パスはデモアプリと同じく TextBox からバインドし、ロックはファイルの削除を試して調べました。

[GitHub で Image のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ImageUsage){: target="_blank" rel="noopener noreferrer"}
