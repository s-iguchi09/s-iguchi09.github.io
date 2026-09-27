---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/progressbar.html
title: "ProgressBar"
badge: "Display"
lead: "ProgressBar は、処理の進み具合を Value に比例した塗りで示すバーです。作業の量が分からないときは、不定モードで動くバーを表示します。"
description: "WPF の ProgressBar を .NET 10 で実測して解説。塗りの長さ、範囲が空や逆のときの表示、不定モードのアニメーションが動く条件、別スレッドからの更新と Progress<T> の使い方を確かめます。UI オートメーションに値が伝わるかも示します。"
---

## 塗りの長さと、範囲が空のときの表示

**ProgressBar** は、Slider や ScrollBar と同じ `RangeBase` を継承しています。既定値は `Minimum` が 0、`Maximum` が 100、`Value` が 0 です。塗り（テンプレートの `PART_Indicator`）の長さは、(`Value` - `Minimum`) / (`Maximum` - `Minimum`) に比例します。幅 200 のバーで、0〜100 の `Value` 30 は塗りの幅 60、20〜120 の `Value` 70 は 100 でした。コピーするファイル数などの項目数を `Maximum` に設定すると、1 項目ごとに `Value` に 1 を足すだけで、割合を計算せずにバーが埋まります。

範囲が空でもバーは壊れませんが、空のバーにもなりません。`Minimum` と `Maximum` がどちらも 50 のとき、バーは満タンに描かれ、例外もありませんでした。`Maximum` を `Minimum` より小さくすると補正されます。`Minimum` 50 で `Maximum = 10` とすると 50 になり、バーはやはり満タンでした。`Maximum` を超える `Value` は `Maximum` として表示されますが、設定した値は保たれます。`Maximum` 100 で `Value = 150` とすると 100 と読め、`Maximum` を 200 に広げると 150 に戻りました。

`Orientation="Vertical"` では、レベル表示のように塗りが下から伸びます。幅 20、高さ 200 のバーで `Value` 75 のとき、塗りは y=50 から下端まで、高さ 150 でした。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/progressbar/progressbar-range.svg" alt="ProgressBar の範囲の計測結果の表。RangeBase を継承し既定値は 0・100・0、幅 200 のバーで 0〜100 の 30 は塗り 60、20〜120 の 70 は 100、空の範囲や Minimum より小さい Maximum では満タンになり、Value 150 は 100 と読めて Maximum を 200 にすると 150 に戻り、縦向きのバーは下から塗られる" width="1022" height="320" loading="lazy">
  <figcaption>範囲、塗り、向き。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 不定モードのアニメーションが動く条件

`IsIndeterminate="True"` は、総数が分かる前にサーバーを待つときのように、どれだけ進んだかを示さずに処理中であることを示します。`Value` にかかわらず塗りがバー全体を覆い、テンプレートは `Indeterminate` の表示状態になりました。アニメーションはバーが表示されている間だけ動きました。`Collapsed` にすると止まり、通常のモードではアニメーションはありませんでした。処理していないときはバーを `Collapsed` にします。

## 別スレッドからの更新と、UI オートメーションに見えるもの

UI スレッドで作った ProgressBar は、別のスレッドからは更新できません。ワーカースレッドから `Value` を設定すると `InvalidOperationException` になりました。進み具合は、UI スレッドで作った `Progress<double>` で報告します。ワーカーから `Report(40)` を呼ぶと、コールバックは UI スレッドで動き、`Value` は 40 になりました。

UI オートメーションからは、読み取り専用の範囲に見えます。`RangeValue` パターンは値 30、`IsReadOnly` `True` を報告しました。不定モードではこのパターンがサポートされず、支援技術には値が伝わりません。数値も文字で表示します。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/progressbar/progressbar-modes.svg" alt="ProgressBar のモードの計測結果の表。不定モードでは塗りがバー全体を覆い Indeterminate の状態になり、アニメーションは表示中だけ動いて Collapsed で止まり、ワーカースレッドから Value を設定すると InvalidOperationException になり、Progress&lt;double&gt; のコールバックは UI スレッドで動き、UI オートメーションは読み取り専用の範囲を報告して不定モードではサポートしない" width="1179" height="230" loading="lazy">
  <figcaption>不定モード、スレッド、UI オートメーション。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの ProgressBar のページ。左にコントロールの一覧、右に最初の節の IsIndeterminate](/images/wpf-standard-control-demo/progressbar.png){: .screenshot-img}

デモアプリの ProgressBar のページには、範囲、`IsIndeterminate`、`Orientation` の欄があります。範囲の欄は `Minimum`・`Maximum`・`Value` をテキストボックスにバインドし、`IsIndeterminate` の欄は `Value` が 50 のバーでチェックボックスから切り替えます。`Orientation` の欄はバーの `Width` を周囲の Grid の幅にバインドしているため、縦向きのバーは領域全体の幅になります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、範囲の欄（`ProgressBarUsageControl.xaml`）から、スタイル、周囲の GroupBox、テキストボックスの入力制限のビヘイビアを省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="MinTextBox" Text="0" />
  <TextBox x:Name="MaxTextBox" Text="100" />
  <TextBox x:Name="ValueTextBox" Text="30" />

  <ProgressBar x:Name="BasicProgressBar"
               Height="20"
               Maximum="{Binding Text, ElementName=MaxTextBox}"
               Minimum="{Binding Text, ElementName=MinTextBox}"
               Value="{Binding Text, ElementName=ValueTextBox}" />
</StackPanel>
```

## 関連するコントロールと記事

- [Slider](/ja/apps/wpf-standard-control-demo/slider.html) — 利用者がドラッグできる RangeBase です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ProgressBarDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ProgressBarDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。塗りの長さは、テンプレートの `PART_Indicator` の矩形から読みました。不定モードのアニメーションが動いているかは、テンプレートの要素を 300 ms 空けて 2 回読み、変化があるかどうかで判断しました。

[GitHub で ProgressBar のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ProgressBarUsage){: target="_blank" rel="noopener noreferrer"}
