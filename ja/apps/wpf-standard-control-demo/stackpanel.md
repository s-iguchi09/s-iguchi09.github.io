---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/stackpanel.html
title: "StackPanel"
badge: "Layout"
lead: "StackPanel は、子を 1 列または 1 行に順に並べるパネルです。最も単純なパネルですが、大きさの決まり方で最もつまずきやすいパネルでもあります。"
description: "WPF の StackPanel を .NET 10 で実測して解説。子に無限の大きさを渡すことと中の ListBox への影響、収まらない子がどうなるか、空いた場所がクリックに反応しない理由、ZIndex の効く範囲を、実際に動かして確かめます。"
---

## 概要

**StackPanel** は、並べる方向には子に無限の大きさを渡します。200 × 100 の縦の StackPanel では子は 200 × Infinity で、横の StackPanel では Infinity × 100 で測られました。もう一方の方向では子は引き伸ばされ、40 × 20 を望んだ子は、縦のパネルでは幅 200、横のパネルでは高さ 100 に配置されました。無限の大きさのため、縦の StackPanel の中の一覧は高さが限られません。高さ 200 の領域に置いた 1000 項目の ListBox は、高さ 200 で 991 スクロールでき、項目を 10 個作りました。同じ領域で縦の StackPanel に入れると、高さが 19964 になってスクロールできず、1000 個すべての項目を作りました。ScrollViewer でも同じことを、下にリンクした記事で示しています。

StackPanel は仮想化しません。高さ 200 の ScrollViewer の中の StackPanel に 1000 個の子を置くと、1000 個すべてが測られました。また、`Spacing` プロパティはないので、子の間隔は子の `Margin` で空けます。

デモアプリには、`Orientation`、`Background`、`ZIndex` の 3 つの欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![stackpanel demo screen](/images/wpf-standard-control-demo/stackpanel.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `Orientation` | `Horizontal / Vertical` | 並べる方向で、既定値は `Vertical` です。子は折り返しません。デモアプリの 3 つのラベルを幅 80 の横の StackPanel に並べると、3 つ目のラベルは 94.69 から始まり、パネルの端を越えました。越えた部分は切れます。`ClipToBounds` は `False` でしたが、WPF のレイアウトによる切り抜きでパネルは 80 に限られ、端の外の 3 つ目のラベルをヒットテストしても何も当たりませんでした。折り返すには WrapPanel を使います。 |
| `Background (Panel)` | `Brush` | パネルの塗りと、空いた場所をクリックできるかどうかです。デモアプリは高さ 30 の空の StackPanel を表示しています。既定値の `null` では中央のヒットテストで何も当たらず、そこでのクリックは後ろの要素に届きます。`Transparent` や色を設定すると、同じヒットテストでパネルが当たりました。 |
| `ZIndex (Panel 添付)` | `int` | 子が重なったときに、どれを上に描くかです。デモアプリでは 2 つ目のラベルが 1 つ目に 15 重なっています。`ZIndex` が 1 と 2 では重なった部分で 2 つ目が上に、3 と 2 では 1 つ目が上になりました。順序が決まるのは同じパネルの子どうしだけです。内側のパネルの子に `ZIndex="100"` を付けても、その内側のパネルの兄弟（`ZIndex` 0）より下のままでした。 |

## XAML 使用例

デモアプリ（`StackPanelUsageControl.xaml`）の `ZIndex` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="ZIndexItem1Text" Text="1" />
  <TextBox x:Name="ZIndexItem2Text" Text="2" />

  <StackPanel x:Name="ZIndexStackPanel">
    <Label VerticalAlignment="Top"
           Panel.ZIndex="{Binding Text, ElementName=ZIndexItem1Text}"
           Background="LightBlue" BorderBrush="Black" BorderThickness="1"
           Content="Item1" />
    <Label Margin="0,-15,0,0" VerticalAlignment="Top"
           Panel.ZIndex="{Binding Text, ElementName=ZIndexItem2Text}"
           Background="SkyBlue" BorderBrush="Black" BorderThickness="1"
           Content="Item2" />
  </StackPanel>
</StackPanel>
```

## 主な使用例

- **ボタンの列** — 横の StackPanel に OK とキャンセルを並べます。
- **短いフォーム** — 縦の StackPanel にラベルと入力欄を上下に並べます。
- **ScrollViewer の内容** — いくつかの区切りを縦に並べ、ScrollViewer でまとめてスクロールします。

## ヒントとベストプラクティス

- **スクロールさせたい ListBox や ScrollViewer を縦の StackPanel に入れない** — Grid の行などで高さを限ります。
- **長い一覧に StackPanel を使わない** — すべての子を測ります。高さを限った ListBox は、表示する分の項目だけを作りました。
- **空いた場所をクリックさせたいなら `Background="Transparent"` を設定する** — 既定値の `null` ではクリックが通り抜けます。
- **`ZIndex` は 1 つのパネルの中で使う** — 自分のパネルの外の要素より上には出せません。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`StackPanelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/StackPanelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。パネルは `Measure` と `Arrange` でレイアウトし、上にある要素は指定した位置のヒットテストで調べました。大きさは、計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/stackpanel/stackpanel-behavior.svg" alt="StackPanel の計測結果の表。既定値は Vertical で Spacing プロパティはなく、子は並べる方向に無限の大きさで測られて交差する方向に引き伸ばされ、はみ出したラベルはレイアウトの切り抜きでパネルの端で切れ、ScrollViewer の中の 1000 個の子はすべて測られ、StackPanel の中の 1000 項目の ListBox はスクロールしなくなってすべての項目を作り、空いた場所は Background があるときだけ当たり、デモアプリの重なったラベルは ZIndex で順序が決まり、ZIndex は自分のパネルの外には効かない" width="1077" height="500" loading="lazy">
  <figcaption>子に渡す大きさ、はみ出し、背景、<code>ZIndex</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール・記事

- [WrapPanel](/ja/apps/wpf-standard-control-demo/wrappanel.html) — 子を並べ、収まらなければ次の行へ折り返します。
- [DockPanel](/ja/apps/wpf-standard-control-demo/dockpanel.html) — 子を端に沿って置き、最後の子で残りを埋めます。
- [Grid](/ja/apps/wpf-standard-control-demo/grid.html) — 大きさの限られた行と列で配置します。
- [WPF で ScrollViewer がスクロールしない原因と解決方法](/ja/articles/wpf-scrollviewer-not-scrolling/) — StackPanel の無限の高さが ScrollViewer に与える影響を扱います。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で StackPanel のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/StackPanelUsage){: target="_blank" rel="noopener noreferrer"}
