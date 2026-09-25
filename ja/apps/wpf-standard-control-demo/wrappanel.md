---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/wrappanel.html
title: "WrapPanel"
badge: "Layout"
lead: "WrapPanel は、子を順に並べ、今の行（列）がいっぱいになると次の行（列）へ折り返すパネルです。"
description: "WPF の WrapPanel を .NET 10 で実測して解説。デモアプリの項目がどこで折り返すか、行の高さの決まり方、ItemWidth と ItemHeight の働き、ScrollViewer の中で折り返さなくなる理由を確かめます。"
---

## 概要

**WrapPanel** の既定値は `Horizontal` で、`ItemWidth` と `ItemHeight` は `NaN` なので、各子は自分の大きさのままです。デモアプリの 5 つのラベルは、幅 150 で 3 つと 2 つの 2 行になりました。行の高さは、その行で最も高い子に合わせられます。高さ 40 の子の隣の高さ 20 の子は、`VerticalAlignment` が `Stretch` なら 40 に引き伸ばされ、`Top` なら 20 のままでした。

折り返すには幅が限られている必要があります。横スクロールが無効（既定）の ScrollViewer の中では、5 つのラベルは 2・2・1 の 3 行になりました。縦のスクロールバーが 150 のうちの一部を使うためです。`HorizontalScrollBarVisibility="Auto"` にするとパネルの幅が無限になり、5 つすべてが 1 行に並びました。

仮想化はしません。300 × 200 の ListBox の `ItemsPanel` を WrapPanel にすると、1000 項目のうち 1000 個すべてが作られました。

デモアプリには、`Orientation`、`ItemHeight`、`ItemWidth`、`Background`、`ZIndex` の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![デモアプリの WrapPanel のページ。左にコントロールの一覧、右に最初の節の Orientation](/images/wpf-standard-control-demo/wrappanel.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `Orientation` | `Horizontal / Vertical` | 折り返す前に子を並べる方向です。`Vertical` で高さ 60 のとき、高さ約 32 のデモアプリの 5 つのラベルは、1 つずつの 5 列になりました。 |
| `ItemHeight` | `double` | すべての子の枠の高さを固定します。デモアプリの初期値は 100 です。100 ではラベルが 96（100 から余白を引いた値）に引き伸ばされ、2 行目は 100 から始まり、その先頭のラベルは余白 2 の分だけ下の 102 に置かれました。 |
| `ItemWidth` | `double` | すべての子の枠の幅を固定します。デモアプリの初期値は 100 です。100 ではラベルが 96 に引き伸ばされました。幅 150 の子には 100 が割り当てられてそこで切れ、レイアウトの切り抜きの幅は 100、その 20 先のヒットテストでは何も当たりませんでした。 |
| `Background (Panel)` | `Brush` | パネルの塗りと、子と子の間をクリックできるかどうかです。既定値の `null` では 2 つのラベルの間のヒットテストで何も当たらず、`Transparent` ではパネルが当たりました。 |
| `ZIndex (Panel 添付)` | `int` | 子が重なったときに、どれを上にするかです。15 重なった 2 つのラベルで、`ZIndex` が 1 と 2 なら 2 つ目が、3 と 2 なら 1 つ目が上になりました。 |

## XAML 使用例

デモアプリ（`WrapPanelUsageControl.xaml`）の `ItemWidth` の欄から、スタイル、周囲の GroupBox、テキストボックスの入力制限のビヘイビアを省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="ItemWidthText" Text="100" />

  <WrapPanel x:Name="ItemWidthWrapPanel" ItemWidth="{Binding Text, ElementName=ItemWidthText}">
    <Label Margin="2" BorderBrush="Black" BorderThickness="1" Content="Item1" />
    <Label Margin="2" BorderBrush="Black" BorderThickness="1" Content="Item2" />
    <Label Margin="2" BorderBrush="Black" BorderThickness="1" Content="Item3" />
  </WrapPanel>
</StackPanel>
```

## 主な使用例

- **タグやチップ** — ウィンドウの幅に合わせて折り返す、短いラベルの並びです。
- **小さなギャラリー** — `ItemWidth` と `ItemHeight` で大きさをそろえたタイルです。
- **ボタンの並び** — ウィンドウが狭いと 2 行目に移るボタンです。

## ヒントとベストプラクティス

- **横の WrapPanel を囲む ScrollViewer では横スクロールを無効にする** — 横スクロールできると、折り返しません。
- **WrapPanel に置く一覧は短くする** — ListBox のパネルにしても、すべての項目を作ります。
- **`ItemWidth` と `ItemHeight` は最も大きい子より大きくする** — 大きい子は縮まずに切れます。
- **子の間をクリックさせるなら `Background="Transparent"` を設定する**

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`WrapPanelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/WrapPanelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じラベル（余白 2、枠 1）で試しました。パネルは `Measure` と `Arrange` でレイアウトしました。大きさは、計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/wrappanel/wrappanel-behavior.svg" alt="WrapPanel の計測結果の表。既定値は Horizontal で項目の大きさは NaN、デモアプリの 5 つのラベルは幅 150 で 3 つと 2 つの 2 行、高さ 60 で 5 列になり、低い子は Stretch のときだけ行の高さまで伸び、ItemWidth と ItemHeight を 100 にするとラベルは 96 に伸びて幅 150 の子は 100 で切れ、ScrollViewer の中では 3 行、横スクロールできると 1 行になり、子の間は Background があるときだけ当たり、ZIndex が重なったラベルの順序を決め、ListBox では 1000 項目すべてが作られる" width="1038" height="530" loading="lazy">
  <figcaption>折り返し、行の大きさ、<code>ItemWidth</code> と <code>ItemHeight</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [StackPanel](/ja/apps/wpf-standard-control-demo/stackpanel.html) — 折り返さずに 1 列に並べます。
- [UniformGrid](/ja/apps/wpf-standard-control-demo/uniformgrid.html) — 決まった行数と列数の、同じ大きさのセルに並べます。
- [ScrollViewer](/ja/apps/wpf-standard-control-demo/scrollviewer.html) — 中のパネルの幅が限られるかどうかを決めます。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で WrapPanel のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/WrapPanelUsage){: target="_blank" rel="noopener noreferrer"}
