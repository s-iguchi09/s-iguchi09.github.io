---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/toolbar.html
title: "ToolBar"
badge: "Menu"
lead: "ToolBar はコマンドを帯状に並べ、入りきらない項目をオーバーフローのメニューへ移すコントロールです。複数のツールバーは ToolBarTray の中に並べられます。"
description: "WPF の ToolBar のオーバーフロー、OverflowMode、ToolBarTray のバンドとロック、向き、ツールバー内のボタンに当たるスタイルを .NET 10 で実測して解説します。トレイがなくてもオーバーフローが働くことも確かめます。"
---

## 概要

**ToolBar** は `HeaderedItemsControl` を継承しています。入りきらない項目は、右端のボタンで開くオーバーフローのポップアップへ移ります。これに `ToolBarTray` は要りません。デモアプリのオーバーフローの欄は、幅 100〜400 の ToolBar を単独で置いており、幅 100 では 5 つのボタンのうち 4 つがオーバーフローへ移りました。トレイが加えるのは、バンド・ドラッグ・ロックです。

ToolBar に置いた項目には、ツールバー用のスタイルが当たります。`Button`・`ToggleButton`・`ComboBox`・`Separator` には、それぞれ `ToolBar.ButtonStyleKey`・`ToggleButtonStyleKey`・`ComboBoxStyleKey`・`SeparatorStyleKey` のスタイルが適用されていました。横向きのツールバーの Separator は、幅 1 の縦線になりました。ツールバー自体の `Background` は直接変えられます。`LightYellow` を設定すると、テンプレートの枠の既定の `#FFEEF5FD` が置き換わりました。

デモアプリには以下の各プロパティの欄があり、各欄の下の「Show Code」リンクでその欄の XAML を表示できます。

## 画面キャプチャ

![デモアプリの ToolBar のページ。左にコントロールの一覧、右に最初の節の HasOverflowItems (ToolBar) / IsOverflowItem (ToolBar) / IsOverflowOpen (ToolBar) / OverflowMode (ToolBar)](/images/wpf-standard-control-demo/toolbar.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `OverflowMode (ToolBar attached)` | `AsNeeded / Never / Always` | 幅が足りないときに項目をどこへ置くかで、既定値は `AsNeeded` です。デモアプリと同じく 5 つのボタンの 2 つ目を対象にすると、幅 100 では、`AsNeeded` でオーバーフローへ移り、`Never` ではツールバーに残り、`Always` では幅 400 でも常にオーバーフローにありました。`Never` はすべてを見せる方法ではありません。幅 100 のツールバーで 5 つのボタンをすべて `Never` にすると、オーバーフローへは何も移らず、ボタンは 198.15 の幅を取り、大半が切れて見えなくなりました。 |
| `HasOverflowItems / IsOverflowItem (ToolBar)` | `bool（読み取り専用）` | `HasOverflowItems` はツールバーのオーバーフローに項目があるかどうかを、添付プロパティの `ToolBar.IsOverflowItem` は個々の項目がオーバーフローにあるかどうかを表します。どちらも読み取り専用で、デモアプリではツールバーと対象のボタンについて表示しています。 |
| `IsOverflowOpen (ToolBar)` | `bool` | オーバーフローのポップアップが開いているかどうかで、デモアプリではチェックボックスにバインドしています。オーバーフローに何もないとき（幅 400）も、オーバーフローボタンは表示されたまま無効になっていました。この状態で `IsOverflowOpen = true` にすると、表示する項目のないポップアップが開きました。 |
| `Band / BandIndex (ToolBar)` | `int` | ToolBarTray の何行目に置くかと、その行の中での順番です。デモアプリのトレイでは、バンドが 0・0・1・1、インデックスが 0・1・0・1 のツールバーが、(0, 0)・(147.79, 0)・(0, 30.96)・(147.79, 30.96) に置かれました。つまみでドラッグすると値が変わり、バンド 1・インデックス 0 のツールバーを右へ 250、上へ 30 ドラッグすると、バンド 0・インデックス 0 に移りました。 |
| `Background / IsLocked / Orientation (ToolBarTray)` | `Brush / bool / Orientation` | `IsLocked` はトレイの中のツールバーに引き継がれ、`True` では各ツールバーのつまみが非表示（Collapsed）になり、ドラッグできなくなりました。トレイの `Orientation` がツールバーの向きを決めます。`ToolBar.Orientation` は読み取り専用で、縦向きのトレイでは `Vertical`、トレイの外では `Horizontal` になり、設定しようとすると `InvalidOperationException` が発生しました。2 つの向きが食い違うことはありません。 |

## XAML 使用例

デモアプリ（`ToolBarUsageControl.xaml`）のオーバーフローの欄から、スタイル、周囲の GroupBox、一部の配置の属性を省き、名前空間の宣言を加えたものです。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <CheckBox x:Name="IsOverflowOpenCheckBox" Content="IsOverflowOpen" />
  <ComboBox x:Name="OverflowModeComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=OverflowMode}"
            SelectedValuePath="Value" SelectedIndex="0" />
  <Slider x:Name="WidthSlider" Minimum="100" Maximum="400" Value="200"
          LargeChange="50" TickFrequency="50" TickPlacement="BottomRight"
          AutoToolTipPlacement="BottomRight" />

  <ToolBar x:Name="OverflowToolBar" HorizontalAlignment="Left"
           Width="{Binding Value, ElementName=WidthSlider}"
           IsOverflowOpen="{Binding IsChecked, ElementName=IsOverflowOpenCheckBox}">
    <Button Content="Item 1" />
    <Button x:Name="OverflowTargetButton" Content="Target Item"
            ToolBar.OverflowMode="{Binding SelectedValue, ElementName=OverflowModeComboBox}" />
    <Button Content="Item 2" />
    <Button Content="Item 3" />
    <Button Content="Item 4" />
  </ToolBar>

  <TextBlock Text="{Binding HasOverflowItems, ElementName=OverflowToolBar}" />
  <TextBlock Text="{Binding (ToolBar.IsOverflowItem), ElementName=OverflowTargetButton}" />
</StackPanel>
```

## 主な使用例

- **アプリのコマンド** — 新規・開く・保存のボタンをウィンドウの上部に並べます。
- **書式設定** — 太字・斜体のトグルボタンと、文字サイズのコンボボックスを並べます。
- **複数のツールバー** — コマンドのまとまりごとにツールバーを作り、ToolBarTray のバンドに並べます。

## ヒントとベストプラクティス

- **ToolBarTray はバンドとドラッグのために使う** — オーバーフローは、ToolBar 単独でも働きます。
- **`OverflowMode="Never"` は少数の項目に絞る** — `Never` の項目は、ツールバーが狭すぎるとオーバーフローへ移らずに切れて見えなくなります。
- **レイアウトを復元するなら `Band` と `BandIndex` を保存する** — ツールバーをドラッグすると、これらの値が変わります。
- **レイアウトを固定するならトレイに `IsLocked="True"`** — トレイの中のすべてのツールバーのつまみが非表示になります。
- **項目の見た目はツールバー用のスタイルキーで変える** — ToolBar の中のボタンには、アプリ全体の暗黙の `Button` スタイルではなく、`ToolBar.ButtonStyleKey` のスタイルが使われます。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ToolBarDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToolBarDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。ツールバーのドラッグは、マウスでドラッグしたときと同じ `DragStarted`・`DragDelta`・`DragCompleted` イベントをつまみに発生させて再現しました。大きさと位置は計測した環境での値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/toolbar/toolbar-overflow-matrix.svg" alt="ToolBarTray に入れない ToolBar で、デモアプリの対象のボタンがどこへ行くかを示す表。幅 100 では AsNeeded と Always でオーバーフローへ、Never でツールバーに残り、幅 200 と 400 では Always 以外はツールバーに残る" width="882" height="170" loading="lazy">
  <figcaption>デモアプリのオーバーフローの欄の対象ボタンの行き先（ツールバーの幅と <code>OverflowMode</code> ごと）。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/toolbar/toolbar-tray.svg" alt="ToolBar のスタイルと ToolBarTray の計測結果の表。項目には ToolBar のスタイルキーのスタイルが当たり、ToolBar に設定した Background はテンプレートに反映され、バンドでツールバーが行に分かれ、つまみのドラッグで Band が変わり、IsLocked でつまみが非表示になり、ToolBar.Orientation はトレイに従い設定できない" width="1093" height="380" loading="lazy">
  <figcaption>項目のスタイル、背景、バンド、ロック、向き。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [Menu](/ja/apps/wpf-standard-control-demo/menu.html) — コマンドを階層のあるメニュー項目で提供します。
- [Button](/ja/apps/wpf-standard-control-demo/button.html) — ツールバーに最もよく置かれる項目です。
- [ToggleButton](/ja/apps/wpf-standard-control-demo/togglebutton.html) — 太字のようなオン・オフのコマンドに使います。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で ToolBar のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToolBarUsage){: target="_blank" rel="noopener noreferrer"}
