---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/slider.html
title: "Slider"
badge: "Inputs"
lead: "Slider は、トラック上のつまみのドラッグ、トラックのクリック、キーボードで、範囲内の値を選ばせるコントロールです。目盛りの表示と目盛りへの吸着、範囲の強調表示、現在値のツールチップを標準で備えています。"
description: "WPF の Slider の範囲の補正、目盛りへの吸着、キー操作、ツールチップを .NET 10 で実測して解説します。Value のバインドは既定でドラッグ中に更新されること、目盛りは Minimum と Maximum に必ず描かれることなど、誤解されやすい点も確かめます。"
---

## 範囲、値、バインドしたソース

**Slider** は `RangeBase` を継承しており、`ProgressBar` と `ScrollBar` も同じ `RangeBase` の派生です。`Minimum`・`Maximum`・`Value`・`SmallChange`・`LargeChange` は `RangeBase` のプロパティです。新しく作った Slider の範囲は 0〜10 で、`Value` は `double`、既定値は 0 です。

`Value` は常に範囲内に収まり、例外は出ません。0〜100 の Slider に 150 を設定すると 100 になりました。その後 `Maximum` を 200 に上げると、`Value` は 150 に戻りました。設定した値は保持されているためです。`Maximum` は `Minimum` 以上に補正され、`Minimum="50"` の後に `Maximum="10"` を設定すると、実際の `Maximum` は 50 になりました。このため、2 つを変える順序は結果に影響しません。範囲を 0〜10 から 100〜200 に変えた場合、どちらを先に設定しても同じ状態になりました。

補正した値はソースへは書き戻されません。0〜100 の Slider に値 150 のソースを TwoWay でバインドすると、Slider の表示は 100 でしたが、ソースは 150 のままでした。ソースの値は自分で範囲内に収めないと、Slider に表示されていない値が残ります。

`Value` は既定で TwoWay にバインドされ、既定の `UpdateSourceTrigger` は `PropertyChanged` なので、`UpdateSourceTrigger` の指定は要りません。既定の設定のバインドでも、ドラッグの途中、ボタンを離す前にソースへ値が届きました。ソースのセッターで重い処理をすると、ドラッグの 1 ステップごとにその処理が走ります。

## キー、トラックのクリック、向き

`SmallChange` は矢印キーで変わる量、`LargeChange` は PageUp / PageDown およびトラックのクリックで変わる量です。既定値は 0.1 と 1 で、既定の 0〜10 の範囲向けです。音量のような 0〜100 には小さすぎます。1 と 10 を設定した値 50 の Slider では、右・上矢印で 51、左矢印で 49、PageUp で 60、PageDown で 40、実際のマウスでのつまみの右側のトラックのクリックで 60 になりました。これは `IsMoveToPointEnabled` が既定値の `False` の場合です。`True` では、同じクリックでつまみがクリックした位置へ移り、値は 86.84 になりました。Home と End では `Minimum` と `Maximum` に移りました。

トラック上でマウスボタンを押し続けると、変化が繰り返されます。`Delay` は繰り返しが始まるまでの待ち時間、`Interval` は繰り返しの間隔です。既定値は Windows のキーボードの設定から決まります。計測した環境（`SystemParameters.KeyboardDelay` が 1、`KeyboardSpeed` が 31）では 500 と 33 で、`Delay` の既定値は（`KeyboardDelay` + 1）× 250 と一致しました。Slider はこの値をトラックの 2 つの `RepeatButton` に渡します。デモアプリの 1000 と 50 では、どちらのボタンも `Delay="1000"`・`Interval="50"` になりました。`Delay` が過ぎる前に離したクリックでは、値は 1 回だけ `LargeChange` 動きました。

`Orientation` の既定値は `Horizontal` です。`Value = Maximum` のとき、つまみは水平の Slider では右端、垂直の Slider では上端にありました。垂直の Slider は、もともと大きい値が上に来ます。`IsDirectionReversed` はトラックの向きを反転し、`Value = Maximum` のとき、つまみは水平の Slider では左端、垂直の Slider では下端に移りました。キー操作もトラックに合わせて反転し、右矢印で値が減りました（50 から 49）。

## 目盛りへの吸着はユーザーの操作にだけ働く

目盛りを描いても吸着はしません。`TickPlacement="BottomRight"` だけを設定した状態で 23.4 までドラッグすると、値は 23.4 のままでした。既定値が `False` の `IsSnapToTickEnabled` を有効にすると、ユーザーの操作による値が最も近い目盛りに吸着します。23.4 までのドラッグは、`TickFrequency="10"` で 20、`TickFrequency="1"` で 23 になりました。吸着はボタンを離したときではなく、ドラッグ中から働きます。キーとトラックの動きも変わり、`TickFrequency="10"` では右矢印で 50 が 51 ではなく 60 に、`LargeChange="7"` でのトラックのクリックで 57 ではなく 60 になりました。コードから設定した 23.4 は 23.4 のままでした。

既定値が 1 の `TickFrequency` は、吸着位置の間隔を決めます。`Maximum` も吸着位置になり、0〜100 の Slider で `TickFrequency="30"` とすると、94 までのドラッグは 90 に、97 までのドラッグは 100 に吸着しました。`Ticks` は位置を個別に指定し、`TickFrequency` の代わりに使われます。サイズやレベルを段階的に選ばせるなら、ここに並べて吸着を有効にします。0〜10 の Slider に `1,3,5,7,9` を指定すると、5.8 までのドラッグは 5 に、0.2 までのドラッグは 0 になりました。`Minimum` と `Maximum` は、`Ticks` に含めなくても吸着位置になります。

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/slider/slider-snap.svg" alt="つまみをドラッグした後の Slider の値を示す表。吸着なしや TickPlacement だけでは 23.4、TickFrequency 10 で 20、1 で 23、TickFrequency 30 では 90 または 100、Ticks 1,3,5,7,9 では 5 と 0、コードから設定した値は吸着せず、バインドしたソースはドラッグ中に更新される" width="700" height="380" loading="lazy">
  <figcaption>吸着の有無によるドラッグ後の値。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 目盛り、選択範囲、ツールチップ

範囲を割り切れない間隔でも、`Minimum` と `Maximum` には必ず目盛りが描かれます。0〜100 の Slider で `TickFrequency="30"` とすると、目盛りは 0・30・60・90・100 に描かれ、0〜10 の Slider の `Ticks="1,3,5,7,9"` では 0・1・3・5・7・9・10 に描かれました。`IsDirectionReversed` では目盛りも反転し、0〜10 の Slider に `Ticks="1,2"` を設定すると、目盛りはトラックの右端側に移りました。手で並べ直す必要があるのは、Slider の横に自分で置いたラベルだけです。`TickPlacement` の既定値は `None` で、目盛りの分だけ高さが増えます。水平の Slider の高さは `None` で 18、`BottomRight` で 24、`Both` で 30 になりました。

`SelectionStart` と `SelectionEnd` は、トラックの一部を強調表示します。0〜10 の Slider で 2〜8 を指定すると、既定値の `IsSelectionRangeEnabled="False"` のあいだは表示されず、`True` にするとトラックの 2 から 8 までを覆いました。選択範囲は `Value` を制限せず、範囲外の 9.5 も設定できました。

`AutoToolTipPlacement` は、つまみのドラッグ中に現在値のツールチップを表示します。既定値の `None` では表示されません。表示されるのは数値だけです。Slider には書式を指定するプロパティがなく、関係するのは `AutoToolTipPlacement` と `AutoToolTipPrecision` だけです。数値は現在のカルチャに従い、1234.56 を小数 1 桁で表示すると、en-US では `1,234.6`、de-DE では `1.234,6` になりました。既定値が 0 の `AutoToolTipPrecision` は、切り捨てではなく四捨五入します。0 のとき、33.6 は `34`、33.4 は `33` と表示されました。2 のとき、33.456 は `33.46` と表示されました。丸められるのはツールチップの表示だけで、`Value` は元の精度のままです。「%」などの単位を付けたい場合は、`StringFormat` を使った別の `TextBlock` で値を表示します。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/slider/slider-ticks-tooltip.svg" alt="目盛り、選択範囲、自動ツールチップを示す表。目盛りは Minimum と Maximum に必ず描かれ、IsDirectionReversed で反転し、TickPlacement で高さが変わり、選択範囲は有効なときだけ表示されて Value を制限せず、ツールチップは AutoToolTipPrecision の桁で四捨五入される" width="1046" height="590" loading="lazy">
  <figcaption>目盛りの位置（左から順に、値に換算）、高さ、選択範囲、自動ツールチップの文字列。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの Slider のページ。左にコントロールの一覧、右に最初の節の Minimum(RangeBase) / Maximum(RangeBase) / Value(RangeBase)](/images/wpf-standard-control-demo/slider.png){: .screenshot-img}

デモアプリの Slider のページには、ここまでの各プロパティの欄があります。`SmallChange` / `LargeChange` の欄の初期値は 0〜10 の Slider に 1 と 10 なので、PageUp 1 回やトラックのクリック 1 回で端から端まで動きます。`Ticks` の欄は `1,3,5,7,9`、選択範囲の欄は 2〜8、`Delay` / `Interval` の欄は 1000 と 50 です。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`Minimum` / `Maximum` / `Value` の欄（`SliderUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`behaviors` はデモアプリ独自の添付ビヘイビアの接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:behaviors="clr-namespace:WPFStandardControlDemoApp.Common.Behaviors">
  <TextBox x:Name="MinTextBox"
           behaviors:TextBoxDoubleInputBehavior.IsEnabled="True"
           Text="0" />
  <TextBox x:Name="MaxTextBox"
           behaviors:TextBoxDoubleInputBehavior.IsEnabled="True"
           Text="100" />
  <TextBox x:Name="ValueTextBox"
           behaviors:TextBoxDoubleInputBehavior.IsEnabled="True"
           Text="{Binding Value, ElementName=MinMaxSlider, UpdateSourceTrigger=PropertyChanged}" />

  <Slider x:Name="MinMaxSlider"
          Maximum="{Binding Text, ElementName=MaxTextBox}"
          Minimum="{Binding Text, ElementName=MinTextBox}" />
</StackPanel>
```

## 関連するコントロールと記事

- [ProgressBar](/ja/apps/wpf-standard-control-demo/progressbar.html) — 同じく `RangeBase` の派生で、範囲内の値を表示します。
- [RepeatButton](/ja/apps/wpf-standard-control-demo/repeatbutton.html) — Slider のトラックがクリックと繰り返しに使っているボタンです。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`SliderDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/SliderDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。つまみのドラッグは、マウスでドラッグしたときにつまみが発生させるのと同じ `DragStarted`・`DragDelta` イベントで、キー操作は表示したウィンドウへキー入力イベントを送って再現しています。トラックのクリックは、計測用のウィンドウの上で実際のマウスを使い、トラックの長さの 85% の位置を押して約 0.1 秒後に離して行いました。

[GitHub で Slider のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/SliderUsage){: target="_blank" rel="noopener noreferrer"}
