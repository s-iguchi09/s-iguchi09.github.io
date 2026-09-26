---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/repeatbutton.html
title: "RepeatButton"
badge: "Inputs"
lead: "RepeatButton は、押している間クリックを繰り返すボタンです。WPF 自身も、ScrollBar の矢印とトラック、Slider のトラックに使っています。"
description: "WPF の RepeatButton を .NET 10 と実際のマウスで実測して解説。クリックが繰り返される時点、Delay と Interval の既定値、バインドした値が不正なときに何になるかを確かめます。ポインターが外へ出たときと Space キーでの動きも示します。"
---

## クリックが繰り返される時点

**RepeatButton** は `ButtonBase` を継承しています。`ClickMode` の既定値は `Press` で、マウスボタンを押した時点で最初のクリックになります。`Delay="300"`・`Interval="100"` で約 1 秒押し続けると、最初のクリックはすぐに、2 回目はおよそ `Delay` 後に、それ以降はおよそ `Interval` ごと（計測したマシンでは 110 ms）に来ました。`Interval` は正確ではありません。離したときに追加のクリックはありませんでした。バインドした `Command` は、クリックのたびに 1 回実行されました。範囲の制限はコマンドの中で行わないと、スピナーは範囲を越えてしまいます。

ポインターがボタンの外へ出ると繰り返しは止まります。マウスボタンを押したまま外にいる間はクリックがなく、`IsPressed` は `False` でした。ポインターが戻るとクリックが再開しました。<kbd>Space</kbd> を押し続けても繰り返します。Windows のキーリピートなしで、キーを押したイベント 1 回だけでも、1 秒で 8 回クリックになりました。UI オートメーションの `Invoke` では 1 回のクリックでした。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/repeatbutton/repeatbutton-repeat.svg" alt="RepeatButton の時間の計測結果の表。キーボードの設定が 1 と 31 のマシンで既定値は Delay 500、Interval 33、ClickMode の既定値は Press、Delay 300・Interval 100 で約 1 秒押すと 8 回クリックになり、最初はすぐ、2 回目は約 320 ms 後、以後は約 110 ms ごと、離したときのクリックはなく、コマンドはクリックごとに 1 回実行され、ポインターが外にある間はクリックがなく、Space を押し続けると 8 回クリックになる" width="998" height="350" loading="lazy">
  <figcaption>既定値と、マウスボタンや <kbd>Space</kbd> を押し続けたときのクリック。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## Delay と Interval は利用者のキーボードの設定で決まる

`Delay` と `Interval` の既定値は、決まった数ではありません。Windows のキーボードの「表示までの待ち時間」の設定が 1 のマシンでは `Delay` は 500、「表示の間隔」の設定が 31 のマシンでは `Interval` は 33 でした。速さが重要なら、どちらも明示します。

コードから負の `Delay` を設定すると `ArgumentException` になり、0 は受け付けられました。`Interval` は 0 より大きい値が必要で、コードから 0 を設定すると `ArgumentException` になりました。

## バインドした値が不正だと既定値に戻る

デモアプリは `Delay` と `Interval` をテキストボックスにバインドしています。使えない値を入れても、直前の値は保たれません。`"200"` の後に `"-1"`、空文字、`"abc"` を入れると、いずれも `Delay` は既定値の 500 になりました。`"50"` の後で `"0"` を入れると、`Interval` は既定値の 33 になりました。値はボタンに渡す前に検証します。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/repeatbutton/repeatbutton-values.svg" alt="RepeatButton の値の計測結果の表。コードから Delay に -1、Interval に 0 を設定すると ArgumentException になり、テキストボックスからバインドした不正な値では Delay は 500、Interval は 33 に戻り、ScrollBar のテンプレートには LineUp・PageUp・PageDown・LineDown の、Slider のテンプレートには DecreaseLarge・IncreaseLarge の RepeatButton がある" width="983" height="290" loading="lazy">
  <figcaption>不正な値と、WPF 自身のテンプレートの中の RepeatButton。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## WPF 自身が RepeatButton を使っている場所

縦方向の `ScrollBar` には `LineUp`・`PageUp`・`PageDown`・`LineDown` のコマンドを持つ RepeatButton が 4 つあり（横方向では `LineLeft`・`PageLeft`・`PageRight`・`LineRight`）、両端の矢印とつまみの両側のトラックに当たります。`Slider` にはつまみの両側の `DecreaseLarge` と `IncreaseLarge` の 2 つがあり、矢印のボタンはありません。

## デモアプリで試す

![デモアプリの RepeatButton のページ。左にコントロールの一覧、右に最初の節の Delay / Interval](/images/wpf-standard-control-demo/repeatbutton.png){: .screenshot-img}

デモアプリの RepeatButton のページは、`Delay` と `Interval` を 2 つのテキストボックスにバインドし、最後にクリックした時刻を表示しています。欄の下の「Show Code」リンクで、その XAML を表示できます。次の XAML は、その欄（`RepeatButtonUsageControl.xaml`）から、スタイル、周囲の GroupBox、テキストボックスの入力制限のビヘイビアを省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="DelayTextBox" Text="100" />
  <TextBox x:Name="IntervalTextBox" Text="100" />

  <RepeatButton x:Name="RepeatButtonResult"
                Command="{Binding ClickCommand}"
                Content="Hold down"
                Delay="{Binding Text, ElementName=DelayTextBox}"
                Interval="{Binding Text, ElementName=IntervalTextBox}" />

  <TextBlock x:Name="ClickDateTimeTextBlock" Text="{Binding ClickDateTimeText}" />
</StackPanel>
```

## 関連するコントロールと記事

- [Button](/ja/apps/wpf-standard-control-demo/button.html) — 1 回だけクリックする ButtonBase で、既定ではマウスボタンを離した時点でクリックになります。
- [Slider](/ja/apps/wpf-standard-control-demo/slider.html) — つまみの両側のトラックが RepeatButton です。
- [ScrollViewer](/ja/apps/wpf-standard-control-demo/scrollviewer.html) — スクロールバーの矢印とトラックに RepeatButton を使います。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`RepeatButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/RepeatButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。押し続ける操作は、計測用のウィンドウの上で実際のマウスを使って行いました。キーは、キーボードから打ったときと同じく WPF の入力管理（InputManager）を通して送りました。時間と既定値は、計測したマシンでの値です。

[GitHub で RepeatButton のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/RepeatButtonUsage){: target="_blank" rel="noopener noreferrer"}
