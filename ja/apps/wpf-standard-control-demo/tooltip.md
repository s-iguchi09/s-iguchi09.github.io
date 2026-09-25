---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/tooltip.html
title: "ToolTip"
badge: "Overlays"
lead: "ToolTip は、ポインターが要素の上にとどまったとき、または要素にキーボードフォーカスが来たときに表示されます。時間と位置は ToolTipService の添付プロパティで設定します。"
description: "WPF の ToolTip を .NET 10 と実際のマウス・キーボードで実測して解説。時間の本当の既定値、表示位置、キーボードフォーカスでの表示、無効な要素での扱い、ツールチップが閉じる条件を確かめます。文字列のツールチップが開くたびに作り直されることも示します。"
---

## 概要

要素に `ToolTip="文字"` を設定するだけで表示されます。WPF は開くときに文字列を **ToolTip** で包みますが、その都度包みます。同じ文字列のツールチップを 2 回開くと、別々の 2 つの ToolTip のインスタンスになりました。

時間の既定値は、よく書かれている数値とは違います。`InitialShowDelay` は 1000 ms、`BetweenShowDelay` は 100 ms、`ShowDuration` は `Int32.MaxValue` で、ポインターがとどまっている間、ツールチップは自分では閉じません。`ShowDuration="1000"` では 2.5 秒後には閉じていました。実際のマウスで試すと、`InitialShowDelay` 500 のツールチップは回によって約 600〜700 ms 後（下の表の回では 700 ms 後）、2000 では約 2050 ms 後に開きました。

キーボードの利用者にもツールチップは表示されます。マウスポインターを別のボタンの上に置いたまま、実際の <kbd>Tab</kbd> キーでボタンにフォーカスを移すと、`ShowsToolTipOnKeyboardFocus` が未設定（既定値の `null`）か `True` ではツールチップが開き、`False` では開きませんでした。無効な要素では、`ShowOnDisabled` が `True` でない限り表示されません。無効なボタンの上ではツールチップは開かず、`ShowOnDisabled="True"` では約 350 ms 後に開きました（この計測のボタンは、待ち時間を短くするため `InitialShowDelay` を 300 にしていました）。

デモアプリには、位置とそのずらし量と基準、キーボード、影、3 つの時間、ツールチップの大きさ・色・枠・余白・内容の配置の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![tooltip demo screen](/images/wpf-standard-control-demo/tooltip.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `Placement` | `PlacementMode` | ツールチップを出す位置で、既定値は `Mouse` です。デモアプリには 11 個の値それぞれのボタンがあります。`Mouse` ではツールチップの左上がポインターの 17 下に来ました。`Bottom` では、ポインターの位置にかかわらず、ボタンの左下の角（ボタンの上端から 30 下）に来ました。 |
| `HorizontalOffset / VerticalOffset` | `double` | 位置に加えるずらし量です。デモアプリは `Placement="Bottom"` のボタンにスライダーから設定します。`HorizontalOffset` 50 では、ツールチップはボタンの角から見て (0, 30) から (50, 30) へ移りました。 |
| `PlacementTarget / PlacementRectangle` | `UIElement / Rect` | 位置の基準にする要素と矩形です。デモアプリにはそれぞれの欄があります。Popup のページで、同じプロパティを Popup について実測しています。 |
| `ShowsToolTipOnKeyboardFocus` | `bool?` | 要素にキーボードフォーカスが来たときにツールチップを開くかどうかです。デモアプリは `True` と `False` を示しています。実際の <kbd>Tab</kbd> では、`True` と既定値の `null` で開き、`False` では開きませんでした。 |
| `HasDropShadow` | `bool` | ツールチップを影付きで表示するかどうかです。ボタンで読んだこの添付プロパティの既定値は `False` でした。実際に影が描かれるかは計測していません。デモアプリはチェックボックスで切り替えます。 |
| `InitialShowDelay (ToolTipService)` | `int (ms)` | ツールチップが開くまでの待ち時間です。既定値は 1000 で、デモアプリのスライダーは 500 から始まります。実際のマウスで、500 では回によって約 600〜700 ms 後、2000 では約 2050 ms 後に開きました。 |
| `ShowDuration (ToolTipService)` | `int (ms)` | ポインターがとどまっている間、ツールチップを開いておく時間です。既定値は `Int32.MaxValue` で、自分では閉じません。デモアプリのスライダーは 5000 から始まります。1000 では、ポインターをボタンに乗せたままでも、開いて 2.5 秒後には閉じていました。 |
| `BetweenShowDelay (ToolTipService)` | `int (ms)` | ツールチップが閉じたあと、次のツールチップが最初の待ち時間なしに開く時間です。既定値は 100 で、デモアプリのスライダーは 2000 から始まります。 |
| `Width / MaxWidth / Height / MaxHeight (FrameworkElement)` | `double` | ツールチップの大きさです。デモアプリには幅と高さの欄があります。 |
| `Background / Foreground / BorderBrush / BorderThickness / Padding / HorizontalContentAlignment / VerticalContentAlignment (Control)` | ブラシと配置の値 | ツールチップの見た目です。デモアプリにはそれぞれの欄があります。 |

## XAML 使用例

デモアプリ（`ToolTipUsageControl.xaml`）の `InitialShowDelay` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

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

## 主な使用例

- **アイコンだけのボタン** — コマンドの名前を示します。
- **省略された文字** — 省略記号で切れたセルの全文を示します。
- **入力欄** — 入力の手がかりを短く示します。

## ヒントとベストプラクティス

- **ツールチップが自分で閉じると思わない** — `ShowDuration` の既定値は `Int32.MaxValue` です。
- **無効な理由を伝えるなら `ShowOnDisabled="True"` を設定する** — 設定しないと開きません。
- **キーボードでのツールチップは有効のままにする** — 既定では、<kbd>Tab</kbd> で要素に移ったときにも開きます。
- **ポインターに左右されない位置にするなら `Placement="Bottom"` を使う**

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ToolTipDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToolTipDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。ツールチップは、計測用のウィンドウのボタンの上に実際のマウスを置いて開き、<kbd>Tab</kbd> は実際のキーボード入力として押しました。時間は、ツールがカーソルをボタンの上へ動かした時点から、ツールチップが開いたのを確かめるまでで、約 10 ms ごとに確かめ、50 ms 単位に丸めています。位置はデバイスに依存しないピクセルで表しています。どちらも計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tooltip/tooltip-behavior.svg" alt="ToolTip の計測結果の表。既定値は開くまで 1000 ms、表示時間 Int32.MaxValue、間隔 100 ms、位置は Mouse、キーボードの設定は null、ShowOnDisabled と HasDropShadow はどちらも False、待ち時間 500 と 2000 でそれぞれ約 700 と 2050 ms 後に開き、ShowDuration 1000 で閉じ、開くたびに新しい ToolTip のインスタンスになり、無効なボタンでは ShowOnDisabled のときだけ開き、Bottom と Mouse の位置と 50 のずらし、キーボードフォーカスでは False 以外で開く" width="1116" height="440" loading="lazy">
  <figcaption>既定値、時間、再利用、無効な要素、位置、キーボードフォーカス。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [Popup](/ja/apps/wpf-standard-control-demo/popup.html) — アプリケーションが自分で開閉する、浮いたウィンドウです。
- [TextBlock](/ja/apps/wpf-standard-control-demo/textblock.html) — 省略された文字の全文を、ツールチップで見せられます。
- [Button](/ja/apps/wpf-standard-control-demo/button.html) — アイコンだけのボタンには、ツールチップと UI オートメーションの名前が必要です。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で ToolTip のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToolTipUsage){: target="_blank" rel="noopener noreferrer"}
