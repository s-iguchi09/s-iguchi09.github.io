---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/image.html
title: "Image"
badge: "Graphics"
lead: "Image は、ビットマップのファイルなどの <code>ImageSource</code> から画像を表示し、与えられた領域に合わせて拡大・縮小する要素です。"
description: "WPF の Image を .NET 10 で実測して解説。Stretch と StretchDirection の組み合わせごとの大きさ、UniformToFill でどこが切れるか、DPI、ファイルのパスのバインド、ファイルのロック、デコードの大きさを確かめます。"
---

## 概要

**Image** は `Control` ではなく `FrameworkElement` から派生し、フォーカスを受け取りません。デモアプリは `Source` を、ファイルのパスを入れた TextBox の文字にバインドしています。パスの文字は画像に変換され、`Source` は `BitmapFrameDecode` になりました。存在しないファイルや SVG ファイルのパスでは、`Source` は `null`、Image は 0 × 0 のままで、例外は起きませんでした。WPF はインストールされている Windows Imaging Component のコーデックで画像をデコードし、計測したマシンには SVG のコーデックがありませんでした。

パスで表示したファイルはロックされます。そうしてバインドした PNG を表示している間、ファイルの削除は `IOException` で失敗し、`Source` を `null` にした後も失敗しました。削除できたのはガベージコレクションの後でした。`CacheOption` を `OnLoad` にした `BitmapImage` はファイルをすぐに読み込み、表示している間でもファイルを削除できました。

デモアプリはパス `C:\Windows\Web\Wallpaper\Windows\img0.jpg` で始まります。計測したマシン（Windows 11）には、3840 × 2400 ピクセル・96 DPI の画像としてありました。コンボボックスは先頭の値の `None` と `UpOnly` で始まるので、画像は原寸で表示され、見えるのは左上の部分だけです。欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![デモアプリの Image のページ。左にコントロールの一覧、右に最初の節の Source / Stretch / StretchDirection](/images/wpf-standard-control-demo/image.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `Source` | `ImageSource` | 表示する画像です。バインドしたパスの文字は変換され、ファイルはロックされたままになります（上を参照）。拡大・縮小しないときの大きさはデバイス非依存の単位で、ピクセル数 × 96 / DPI です。72 DPI の 100 × 50 ピクセルは 133.36 × 66.68 でした。PNG は解像度を 1 メートルあたりのピクセル数で保存するので、96 DPI の PNG でも幅は 100.01 になりました。 |
| `Stretch` | `None / Fill / Uniform / UniformToFill` | 領域に合わせた拡大・縮小の仕方で、既定値は `Uniform` です。300 × 200 の領域で、600 × 300 の画像は `None` で 600 × 300、`Fill` で 300 × 200、`Uniform` で 300 × 150、`UniformToFill` で 400 × 200 でした。領域からはみ出した部分は切れ、切れるのは右と下です。画像は領域の左上から始まっていました。`HorizontalAlignment` と `VerticalAlignment` を `Center` にすると -50 から始まり、左右が均等に切れました。 |
| `StretchDirection` | `UpOnly / DownOnly / Both` | 拡大だけ・縮小だけ・両方のどれを許すかで、既定値は `Both` です。`UpOnly` では、600 × 300 の画像はどの Stretch でも 600 × 300 のままで、`DownOnly` では 100 × 50 の画像が 100 × 50 のままでした。デモアプリは `UpOnly` で始まります。 |

## XAML 使用例

デモアプリ（`ImageUsageControl.xaml`）の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。デモアプリでは Image はウィンドウの結果の領域いっぱいに置かれますが、このページの計測では大きさを固定した領域（300 × 200）に配置しました。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

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

## 主な使用例

- **ロゴや図** — `Uniform` で縦横比を保って表示します。
- **サムネイル** — 同じ大きさの枠を `UniformToFill` で埋め、小さい大きさでデコードします。
- **原寸のアイコン** — `DownOnly` にして、小さいアイコンを拡大しないようにします。

## ヒントとベストプラクティス

- **変わる可能性のあるファイルは `BitmapCacheOption.OnLoad` で読み込む** — パスのバインドでは、`Source` を空にしてもファイルはロックされたままです。
- **`UniformToFill` を使うときは Image を中央に揃える** — そうしないと、切り取りで画像の左上が残ります。
- **サムネイルには `DecodePixelWidth` を設定する** — 100 にすると、600 × 300 の画像は 100 × 50 ピクセルにデコードされました。画像自体の大きさも変わり、拡大・縮小しないときの幅は 600 ではなく 100 でした。
- **`Stretch="None"` ではファイルの DPI を確かめる** — 同じピクセル数でも、DPI が低いほど大きく表示されます。
- **SVG は変換してから表示する** — SVG のパスでは、画像も出ず、エラーも起きません。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ImageDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ImageDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。画像はツールが書き出した 100 × 50 ピクセルと 600 × 300 ピクセルの PNG ファイルで、300 × 200 の領域に配置しました。パスはデモアプリと同じく TextBox からバインドし、ロックはファイルの削除を試して調べました。

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/image/image-matrix.svg" alt="300 x 200 の領域での Image の大きさの表。None は 100 x 50 と 600 x 300 のまま、Fill・Uniform・UniformToFill は 300 x 200・300 x 150・400 x 200 になり、ただし UpOnly では大きい画像が、DownOnly では小さい画像がそのままの大きさ" width="690" height="320" loading="lazy">
  <figcaption><code>Stretch</code> と <code>StretchDirection</code> ごとの表示の大きさ。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/image/image-behavior.svg" alt="Image の計測結果の表。FrameworkElement から派生してフォーカスを受け取らず、既定値は Uniform と Both、UniformToFill と None は中央に揃えなければ左上から切り取られ、72 DPI では大きくなり、バインドしたパスは BitmapFrameDecode、存在しないファイルと SVG は null になり、バインドしたファイルはガベージコレクションまでロックされ、OnLoad はロックせず、DecodePixelWidth 100 では 100 x 50 になり、デモアプリの初期の画像は 3840 x 2400・96 DPI" width="1140" height="440" loading="lazy">
  <figcaption>型、切り取り、DPI、パス、ファイルのロック、デコード。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [Viewbox](/ja/apps/wpf-standard-control-demo/viewbox.html) — 同じ `Stretch` と `StretchDirection` を、任意の要素に使えます。
- [InkCanvas](/ja/apps/wpf-standard-control-demo/inkcanvas.html) — マウスやペンで描き込む面です。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で Image のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ImageUsage){: target="_blank" rel="noopener noreferrer"}
