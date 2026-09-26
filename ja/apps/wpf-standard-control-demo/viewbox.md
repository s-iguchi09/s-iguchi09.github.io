---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/viewbox.html
title: "Viewbox"
badge: "Layout"
lead: "Viewbox は、1 つの子を子自身の大きさでレイアウトしてから、Viewbox の持つ領域に合わせて拡大縮小します。"
description: "WPF の Viewbox を .NET 10 で実測して解説。Stretch と StretchDirection のすべての組み合わせの拡大率を横長と小さい領域で比べ、UniformToFill で切れる部分、デモアプリの初期値の意味を確かめます。"
---

## 子は自分の大きさでレイアウトされてから拡大縮小される

**Viewbox** は `Decorator` を継承しているので、子は 1 つ（`Child`）です。子は無限の大きさ（Infinity × Infinity）で測られるので、自分の本来の大きさになります。デモアプリのラベルは 72.34 × 57.96 でした。Viewbox はこの大きさを拡大縮小します。子自身の `ActualWidth` と `ActualHeight` は本来の大きさのままで、拡大縮小されるのは描画だけです。表示上の大きさは、`ActualWidth` ではなく `TransformToAncestor` で読みます。

既定値は `Stretch="Uniform"` と `StretchDirection="Both"` です。デモアプリはここから始まりません。コンボボックスが列挙型の最初の値の `None` と `UpOnly` で始まるので、`Stretch` を `None` 以外にするまでラベルは拡大縮小されません。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/viewbox/viewbox-behavior.svg" alt="Viewbox の計測結果の表。Decorator を継承し既定値は Uniform と Both だがデモアプリは None と UpOnly で始まり、子は無限の大きさで測られてラベルは 72.34 × 57.96、UniformToFill では収まらない部分が Viewbox のレイアウト上の高さで切れ、横の StackPanel の中の Viewbox は高さだけに合わせて拡大する" width="1108" height="230" loading="lazy">
  <figcaption>型、既定値、測り方、切り抜き。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## Stretch ごとの倍率

300 × 100 の領域で、デモアプリのラベルは、`Uniform` で縦横とも 1.73 倍（高さに合わせる）、`UniformToFill` で縦横とも 4.15 倍（幅に合わせる）、`Fill` で横 4.15 倍・縦 1.73 倍になって形が変わりました。`None` は 1 倍のままでした。

`UniformToFill` では、収まらない部分は切れます。300 × 100 の Viewbox で、ラベルは Viewbox の上端から 300 × 240.35 になり、Viewbox のレイアウト上の高さ 100 より 5 下のヒットテストでは何も当たりませんでした。Viewbox のレイアウトのクリップは 300 × 100 でした。全体を見せる必要がある内容には `UniformToFill` を使いません。

## 拡大か縮小か：StretchDirection

`StretchDirection` は、子を拡大してよいか、縮小してよいか、両方かを決めます。ラベルが拡大される 300 × 100 の領域では、`DownOnly` でどの `Stretch` も 1 倍のままでした。ラベルが縮小される 40 × 20 の領域では、`UpOnly` でどの `Stretch` も 1 倍のままで、ラベルは領域より大きくなりました。`Both` は、当てはまる方向と同じ倍率になりました。縮めても大きくはしないなら `DownOnly` を使います。大きなウィンドウで文字が大きくなりすぎるのを防げます。

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/viewbox/viewbox-matrix.svg" alt="300 × 100 と 40 × 20 の領域での、Stretch と StretchDirection ごとのデモアプリのラベルの倍率の表。None は常に 1、Fill は 4.15 と 1.73 または 0.55 と 0.35、Uniform は 1.73 または 0.35、UniformToFill は 4.15 または 0.55 で、UpOnly は小さい領域で、DownOnly は大きい領域で 1 のまま" width="572" height="320" loading="lazy">
  <figcaption><code>Stretch</code>、領域、<code>StretchDirection</code> ごとの倍率（横, 縦）。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 拡大縮小には大きさの制限が要る

幅に制限のない、高さ 100 の横の StackPanel の中では、`Uniform` の Viewbox はラベルを高さだけに合わせて 1.73 倍にしました。縦横の両方に合わせたいなら、Viewbox の大きさを両方向で限ります。

## デモアプリで試す

![デモアプリの Viewbox のページ。左にコントロールの一覧、右に最初の節の Stretch / StretchDirection](/images/wpf-standard-control-demo/viewbox.png){: .screenshot-img}

デモアプリの Viewbox のページには、`Stretch` と `StretchDirection` の 1 つの欄があります。欄の下の「Show Code」リンクで、その XAML を表示できます。次の XAML は、その欄（`ViewboxUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。デモアプリでは Viewbox はウィンドウの結果の領域いっぱいに置かれますが、このページの計測では Viewbox の大きさを固定しました（300 × 100 と 40 × 20）。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="StretchComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=Stretch}"
            SelectedValuePath="Value" SelectedIndex="0" />
  <ComboBox x:Name="StretchDirectionComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=StretchDirection}"
            SelectedValuePath="Value" SelectedIndex="0" />

  <Viewbox x:Name="StretchViewBox"
           Stretch="{Binding SelectedValue, ElementName=StretchComboBox}"
           StretchDirection="{Binding SelectedValue, ElementName=StretchDirectionComboBox}">
    <Label Padding="20" BorderBrush="Black" BorderThickness="1" Content="Item1" />
  </Viewbox>
</StackPanel>
```

## 関連するコントロールと記事

- [Image](/ja/apps/wpf-standard-control-demo/image.html) — 画像のために、自分の `Stretch` と `StretchDirection` を持っています。
- [Canvas](/ja/apps/wpf-standard-control-demo/canvas.html) — Viewbox で拡大縮小できる、固定サイズの絵です。
- [Grid](/ja/apps/wpf-standard-control-demo/grid.html) — 内容を拡大縮小せず、領域を広げることで大きさを変えます。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ViewboxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ViewboxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じラベル（Padding 20、枠 1）で試しました。倍率は、Viewbox から見たラベルの大きさを、ラベル自身の大きさで割った値です。大きさは、計測したマシンでの値です。

[GitHub で Viewbox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ViewboxUsage){: target="_blank" rel="noopener noreferrer"}
