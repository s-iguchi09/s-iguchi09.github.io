---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/canvas.html
title: "Canvas"
badge: "Graphics"
lead: "Canvas は、添付プロパティ Canvas.Left・Top・Right・Bottom で指定した座標に、子をその子自身の大きさで置くパネルです。"
description: "WPF の Canvas を .NET 10 で実測して解説。Left と Right のどちらが優先されるか、Right と Bottom の基準、Canvas の大きさが 0 でも子が表示される理由、ZIndex による重なりの順序を確かめます。"
---

## 子は自分の大きさのまま置かれる

**Canvas** は子を測りますが、渡す大きさは無限で、子は Infinity × Infinity で測られました。そのため子は自分の大きさのままです。幅 300 の Canvas で、TextBlock は幅 60.23、`HorizontalAlignment="Stretch"` の Border も文字の幅の 37.29 のままでした。Canvas の中では配置の指定で子は伸びないので、何かを埋めたい子には大きさを明示します。

## Left と Right のどちらが優先されるか

位置を指定しない子は (0, 0) に置かれます。デモアプリの初期値の `Canvas.Top` 20・`Canvas.Left` 20 では、四角形 A は (20, 20) に 100 × 100 で置かれ、-30 のような負の `Canvas.Left` では一部が Canvas の左外に出ました。

`Canvas.Right` と `Canvas.Bottom` は、`Left` と `Top` に負けます。4 つとも 20 にしても、四角形は (20, 20) の 100 × 100 のままで、動きも伸びもしませんでした。`Left` と `Right` はどちらか一方だけを設定します。デモアプリは `FallbackValue` を NaN にしてバインドしているので、空のテキストボックスでは未設定（NaN）になり、「30」では 30 になります。

## Right と Bottom の基準

`Right` と `Bottom` の基準は Canvas の実際の大きさなので、Canvas に `Width` は要りません。`Right` 20・`Bottom` 20 だけを設定すると、300 × 200 に広がった Canvas では四角形は (180, 80) に置かれました。隅から決まった距離にバッジを置くときに使えます。大きさ 0 の Canvas では、同じ四角形が (-120, -120) に置かれました。

## 大きさ 0 の Canvas でも子が表示される理由

Canvas 自身は大きさを求めません。(20, 20) に四角形を置いた Canvas を横の StackPanel に入れると、`DesiredSize` は 0 × 0 でした。大きさを子に合わせる親の中では領域を得られないので、Canvas の大きさは親か自分の `Width`・`Height` で決めます。それでも子が表示されるのは、`ClipToBounds` の既定値が `False` だからです。幅 200 の Canvas で 150〜250 に置いた四角形は、Canvas の外の x=230 でもヒットテストに当たりました。デモアプリが設定している `ClipToBounds="True"` では、そこでは何も当たりませんでした。子を中に収めるなら設定します。

## ZIndex

デモアプリの初期値は、四角形 A の `ZIndex` が 0、B が 1 で、重なった (85, 85) では B が上でした。A を 2 にすると A が上に、両方 0 では後の子の B が上になりました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/canvas/canvas-behavior.svg" alt="Canvas の計測結果の表。子は無限の大きさで測られて伸びず、子を持つ Canvas は 0 × 0 を求めて ClipToBounds は False、Left と Top が Right と Bottom より優先されて子は伸びず、Right と Bottom は大きさ 0 も含めて Canvas の実際の大きさから測られ、位置のない子は (0, 0)、Canvas の外の子は切り抜かないときだけ当たり、ZIndex がデモアプリの四角形の順序を決めて等しければ後の子が上、空のテキストをバインドすると NaN になる" width="1179" height="470" loading="lazy">
  <figcaption>大きさ、位置、切り抜き、<code>ZIndex</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの Canvas のページ。左にコントロールの一覧、右に最初の節の Top / Left / Right / Bottom / ZIndex(Panel)](/images/wpf-standard-control-demo/canvas.png){: .screenshot-img}

デモアプリの Canvas のページには 1 つの欄があり、100 × 100 の 2 つの四角形があります。四角形 A は `Top`・`Left`・`Right`・`Bottom`・`ZIndex` をテキストボックスにバインドし、四角形 B は (50, 50) にあって自分の `ZIndex` を持っています。欄の下の「Show Code」リンクで、その XAML を表示できます。次の XAML は、結果の部分（`CanvasUsageControl.xaml`）に、四角形がバインドしているテキストボックスと名前空間の宣言を加えたものです。バインドの `TargetNullValue` と `FallbackValue`、スタイル、テキストボックスの入力制限のビヘイビア、GroupBox は省き、周囲のレイアウトは高さ 300 の `DockPanel` に置き換えています。

```xml
<DockPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           Height="300">
  <UniformGrid DockPanel.Dock="Top" Columns="6">
    <TextBox x:Name="TopTextBox" Text="20" />
    <TextBox x:Name="LeftTextBox" Text="20" />
    <TextBox x:Name="RightTextBox" Text="" />
    <TextBox x:Name="BottomTextBox" Text="" />
    <TextBox x:Name="ZIndexTextBox" Text="0" />
    <TextBox x:Name="ZIndexTextBoxRed" Text="1" />
  </UniformGrid>

  <Canvas Background="#F0F0F0" ClipToBounds="True">
    <Rectangle x:Name="TargetRect"
               Canvas.Left="{Binding Text, ElementName=LeftTextBox}"
               Canvas.Top="{Binding Text, ElementName=TopTextBox}"
               Canvas.Right="{Binding Text, ElementName=RightTextBox}"
               Canvas.Bottom="{Binding Text, ElementName=BottomTextBox}"
               Width="100" Height="100"
               Panel.ZIndex="{Binding Text, ElementName=ZIndexTextBox}"
               Fill="SkyBlue" Stroke="DodgerBlue" StrokeThickness="2" />
    <Rectangle Canvas.Left="50" Canvas.Top="50"
               Width="100" Height="100"
               Panel.ZIndex="{Binding Text, ElementName=ZIndexTextBoxRed}"
               Fill="LightCoral" Stroke="IndianRed" StrokeThickness="2" />
  </Canvas>
</DockPanel>
```

## 関連するコントロールと記事

- [Grid](/ja/apps/wpf-standard-control-demo/grid.html) — ウィンドウの大きさに合わせる行と列に子を置きます。
- [Viewbox](/ja/apps/wpf-standard-control-demo/viewbox.html) — 決まった大きさの Canvas などの内容を、拡大縮小して収めます。
- [InkCanvas](/ja/apps/wpf-standard-control-demo/inkcanvas.html) — マウスやペンで描くための面です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`CanvasDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/CanvasDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じ 100 × 100 の四角形で試しました。Canvas は `Measure` と `Arrange` でレイアウトし、上にある要素は指定した位置のヒットテストで調べました。大きさは、計測したマシンでの値です。

[GitHub で Canvas のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/CanvasUsage){: target="_blank" rel="noopener noreferrer"}
