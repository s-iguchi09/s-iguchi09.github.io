---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/uniformgrid.html
title: "UniformGrid"
badge: "Layout"
lead: "UniformGrid は、子を同じ大きさのセルに、行ごとに左から右へ並べるパネルです。指定するのは行数か列数だけで、行や列の定義はありません。"
description: "WPF の UniformGrid を .NET 10 で実測して解説。行と列がいくつになるか、収まらない子がどこへ置かれるか、FirstColumn が 0 に戻される条件、各セルの大きさの決まり方を確かめます。折りたたんだ子や空いたセルの扱いも示します。"
---

## 行と列がいくつになるか

**UniformGrid** は `System.Windows.Controls.Primitives` 名前空間にあります。`Rows`・`Columns`・`FirstColumn` の既定値は 0 です。`Rows` と `Columns` の 0 は「自動で決める」という意味で、どちらも設定しないと、デモアプリの 5 つのラベルは 2 行 3 列になりました。一方を設定するとその数が固定され、もう一方は必要なだけ増えます。300 × 200 のグリッドで、5 つのラベルは `Columns="1"` で 300 × 40 のセルの 5 行に、`Columns="2"` で 150 × 66.67 のセルの 3 行になりました。`Rows="1"` では 5 列の 1 行に、`Rows="2"` では 2 行 3 列になりました。

`Collapsed` の子はセルを使いません。5 つのうち 2 つ目を折りたたんで `Columns="2"` にすると、3 つ目のラベルが 2 つ目のセルに入り、行数は 2 のままでした。グリッドから外したい子は折りたたみます。

## 収まらない子はどこへ置かれるか

`Rows` と `Columns` は、どちらか一方だけを設定します。両方を設定して子が収まらなくても、余った子は捨てられず、グリッドの外に置かれます。`Rows="2"`・`Columns="2"` で 5 つのラベルを置くと、5 つ目は 4 つのセルの下の (0, 200)、高さ 200 のグリッドの外に置かれました。

## FirstColumn が 0 に戻される条件

`FirstColumn` は、最初の子の前に空けるセルの数です。カレンダーで、月の 1 日をその曜日の位置から始めるときに使います。デモアプリの初期値は `Columns="3"`・9 つのラベルで 1 です。1 つ目のラベルは 2 つ目のセルの (100, 0) に置かれ、3 つ目のラベルが 2 行目の先頭になり、グリッドは 4 行になりました。

`FirstColumn` は `Columns` より小さくします。`FirstColumn` が 3 や 4 と `Columns` 以上のときは、グリッドが 0 に戻しました。レイアウト後の値は 0 で、1 つ目のラベルは (0, 0)、グリッドは 3 行でした。0 に戻すとバインドも外れます。デモアプリと同じくテキストボックスにバインドした場合、4 を入力すると 0 になってバインドが外れ、その後 1 を入力しても 0 のままでした。

## セルの大きさの決まり方

セルはすべて同じ大きさです。幅 300 に広げた 3 列のグリッドでは、幅 150 の子があってもセルの幅は 100 でした。大きさを内容に合わせる場合は、最も幅の広い子がすべてのセルの大きさを決めます。同じグリッドを横の StackPanel に入れると、セルの幅は 150 になりました。1 つだけ大きい子があると、すべてのセルがその大きさになります。

## 空いたセルと ZIndex

2 列に 1 つのラベルを置くと、空いたセルのヒットテストで、`Background` が既定値の `null` では何も当たらず、`Transparent` ではグリッドが当たりました。子が重なったときは `Panel.ZIndex` で順序が決まります。デモアプリでは 2 つ目のラベルの上余白が -15 で、1 つ目に重なっています。`ZIndex` が 1 と 2 では 2 つ目が、3 と 2 では 1 つ目が上になりました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/uniformgrid/uniformgrid-behavior.svg" alt="UniformGrid の計測結果の表。Primitives 名前空間にあり Rows・Columns・FirstColumn の既定値は 0、デモアプリの 5 つのラベルは 5 行 1 列、3 行 2 列、1 行 5 列、2 行 3 列、2 行 3 列に並び、2 行 2 列では 5 つ目がグリッドの下に置かれ、3 列で FirstColumn 1 は 2 つ目のセルから始まり 3 や 4 は 0 に戻されてバインドも外れ、折りたたんだ子はセルを使わず、セルは広げると幅 100、幅 150 の子に合わせると 150 になり、空いたセルは Background があるときだけ当たり、ZIndex がデモアプリの重なったラベルの順序を決める" width="1093" height="590" loading="lazy">
  <figcaption>行、列、<code>FirstColumn</code>、セルの大きさ。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの UniformGrid のページ。左にコントロールの一覧、右に最初の節の Columns](/images/wpf-standard-control-demo/uniformgrid.png){: .screenshot-img}

デモアプリの UniformGrid のページには、`Columns`、`Rows`、`FirstColumn`、`Background`、`ZIndex` の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`FirstColumn` の欄（`UniformGridUsageControl.xaml`）から、スタイル、周囲の GroupBox、テキストボックスの入力制限のビヘイビア、9 つのうち 6 つのラベルを省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="FirstColumnTextBox" Text="1" />
  <TextBox x:Name="ColumnsForFirstColumnTextBox" Text="3" />

  <UniformGrid x:Name="FirstColumnUniformGrid"
               Columns="{Binding Text, ElementName=ColumnsForFirstColumnTextBox}"
               FirstColumn="{Binding Text, ElementName=FirstColumnTextBox}">
    <Label BorderBrush="Black" BorderThickness="1" Content="Item1" />
    <Label BorderBrush="Black" BorderThickness="1" Content="Item2" />
    <Label BorderBrush="Black" BorderThickness="1" Content="Item3" />
  </UniformGrid>
</StackPanel>
```

## 関連するコントロールと記事

- [Grid](/ja/apps/wpf-standard-control-demo/grid.html) — 大きさの違う行と列に、結合もできるパネルです。
- [WrapPanel](/ja/apps/wpf-standard-control-demo/wrappanel.html) — 決まった数ではなく、使える幅で子を折り返します。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`UniformGridDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/UniformGridDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じラベル（枠 1）で試しました。グリッドは `Measure` と `Arrange` でレイアウトし、行と列は子の位置から数えました。大きさは、計測したマシンでの値です。

[GitHub で UniformGrid のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/UniformGridUsage){: target="_blank" rel="noopener noreferrer"}
