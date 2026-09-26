---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/tooltip.html
title: "ToolTip"
badge: "Overlays"
lead: "ToolTip は、ポインターが要素の上にとどまったとき、または要素にキーボードフォーカスが来たときに表示されます。時間と位置は ToolTipService の添付プロパティで設定します。"
description: "WPF の ToolTip を .NET 10 と実際のマウス・キーボードで実測して解説。時間の本当の既定値、表示位置、キーボードフォーカスでの表示、無効な要素での扱い、ツールチップが閉じる条件を確かめます。文字列のツールチップが開くたびに作り直されることも示します。"
---

## ツールチップが開くときと閉じるとき

要素に `ToolTip="文字"` を設定するだけで表示されます。WPF は開くときに文字列を **ToolTip** で包みますが、その都度包みます。同じ文字列のツールチップを 2 回開くと、別々の 2 つの ToolTip のインスタンスになりました。

時間は `ToolTipService` の添付プロパティで設定し、既定値はよく書かれている数値とは違います。開くまでの待ち時間の `InitialShowDelay` は 1000 ms でした。ツールチップが閉じたあと、次のツールチップが最初の待ち時間なしに開く時間の `BetweenShowDelay` は 100 ms でした。`ShowDuration` は `Int32.MaxValue` で、ポインターがとどまっている間、ツールチップは自分では閉じません。`ShowDuration="1000"` では、ポインターをボタンに乗せたままでも、開いて 2.5 秒後には閉じていました。

実際のマウスで試すと、`InitialShowDelay` 500 のツールチップは回によって約 600〜700 ms 後（下の表の回では 700 ms 後）、2000 では約 2050 ms 後に開きました。

## キーボードフォーカスと無効な要素

キーボードの利用者にもツールチップは表示されます。マウスポインターを別のボタンの上に置いたまま、実際の <kbd>Tab</kbd> キーでボタンにフォーカスを移すと、`ShowsToolTipOnKeyboardFocus` が未設定（既定値の `null`）か `True` ではツールチップが開き、`False` では開きませんでした。有効のままにしておきます。

無効な要素では、`ShowOnDisabled` が `True` でない限り表示されません。無効なボタンの上ではツールチップは開かず、`ShowOnDisabled="True"` では約 350 ms 後に開きました（この計測のボタンは、待ち時間を短くするため `InitialShowDelay` を 300 にしていました）。無効な理由をツールチップで伝えるなら設定します。

## 表示される位置

`Placement` の既定値は `Mouse` で、ツールチップの左上がポインターの 17 下に来ました。`Bottom` では、ポインターの位置にかかわらず、ボタンの左下の角（ボタンの上端から 30 下）に来たので、ポインターに左右されない位置にするなら `Bottom` を使います。`HorizontalOffset` と `VerticalOffset` で位置をずらせ、`HorizontalOffset` 50 では、ツールチップはボタンの角から見て (0, 30) から (50, 30) へ移りました。

`PlacementTarget` と `PlacementRectangle` は、位置の基準にする要素と矩形です。Popup のページで、同じプロパティを Popup について実測しています。

ボタンで読んだ添付プロパティ `HasDropShadow` の既定値は `False` でした。実際に影が描かれるかは計測していません。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tooltip/tooltip-behavior.svg" alt="ToolTip の計測結果の表。既定値は開くまで 1000 ms、表示時間 Int32.MaxValue、間隔 100 ms、位置は Mouse、キーボードの設定は null、ShowOnDisabled と HasDropShadow はどちらも False、待ち時間 500 と 2000 でそれぞれ約 700 と 2050 ms 後に開き、ShowDuration 1000 で閉じ、開くたびに新しい ToolTip のインスタンスになり、無効なボタンでは ShowOnDisabled のときだけ開き、Bottom と Mouse の位置と 50 のずらし、キーボードフォーカスでは False 以外で開く" width="1116" height="440" loading="lazy">
  <figcaption>既定値、時間、再利用、無効な要素、位置、キーボードフォーカス。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの ToolTip のページ。左にコントロールの一覧、右に最初の節の Placement](/images/wpf-standard-control-demo/tooltip.png){: .screenshot-img}

デモアプリの ToolTip のページには、`Placement` の 11 個の値それぞれのボタンを並べた位置の欄、`Placement="Bottom"` のボタンにスライダーから設定するずらし量の欄、基準の要素と矩形の欄があります。ほかに、キーボード（`True` と `False`）、チェックボックスで切り替える影、スライダーが 500（`InitialShowDelay`）・5000（`ShowDuration`）・2000（`BetweenShowDelay`）から始まる 3 つの時間、ツールチップの幅・高さ・色・枠・余白・内容の配置の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`InitialShowDelay` の欄（`ToolTipUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Slider x:Name="ValueInitialShowDelaySlider" Maximum="5000" Minimum="0" Value="500" />

  <Button x:Name="InitialShowDelayToolTipButton"
          Content="Show ToolTip on hover."
          ToolTip="Delayed ToolTip"
          ToolTipService.InitialShowDelay="{Binding Value, ElementName=ValueInitialShowDelaySlider}" />
</StackPanel>
```

## 関連するコントロールと記事

- [Popup](/ja/apps/wpf-standard-control-demo/popup.html) — アプリケーションが自分で開閉する、浮いたウィンドウです。
- [TextBlock](/ja/apps/wpf-standard-control-demo/textblock.html) — 省略された文字の全文を、ツールチップで見せられます。
- [Button](/ja/apps/wpf-standard-control-demo/button.html) — アイコンだけのボタンには、ツールチップと UI オートメーションの名前が必要です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ToolTipDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToolTipDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。ツールチップは、計測用のウィンドウのボタンの上に実際のマウスを置いて開き、<kbd>Tab</kbd> は実際のキーボード入力として押しました。時間は、ツールがカーソルをボタンの上へ動かした時点から、ツールチップが開いたのを確かめるまでで、約 10 ms ごとに確かめ、50 ms 単位に丸めています。位置はデバイスに依存しないピクセルで表しています。どちらも計測したマシンでの値です。

[GitHub で ToolTip のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToolTipUsage){: target="_blank" rel="noopener noreferrer"}
