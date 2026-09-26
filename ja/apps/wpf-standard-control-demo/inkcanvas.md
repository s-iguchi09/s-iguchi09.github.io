---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/inkcanvas.html
title: "InkCanvas"
badge: "Graphics"
lead: "InkCanvas は、マウスやペンで描き込む面です。描いたものはストロークとして保持され、消す・選ぶ・保存することができます。"
description: "WPF の InkCanvas を .NET 10 と実際のマウスで実測して解説。描画、ストローク単位と点単位の消去、ジェスチャーとして消えるストローク、SelectAll が使える条件、ペンの設定の反映、デモアプリの初期の色の見えにくさを確かめます。"
---

## 編集モードごとにドラッグが何をするか

**InkCanvas** は、描かれたものを `StrokeCollection` 型の `Strokes` に集めます。実際のマウスで 1 回ドラッグすると、ストロークが 1 本増えました。

`EditingMode` はマウスで何をするかで、既定値は `Ink` です。左から右へのドラッグは、`Ink` ではストロークを描き、`None` では何も描きませんでした。線を横切るドラッグで、`EraseByStroke` は線全体を消し、`EraseByPoint` は線を 2 つに切りました。`Select` では、線をクリックすると選択されました。読み取り専用の `ActiveEditingMode` は使われているモードで、マウスでドラッグしている間は `EditingMode` と同じ `Ink` でした。`EditingModeInverted` はペンの反対側の端で使うモードで、既定値は `EraseByStroke` です。マウスでは使われず、既定値のままドラッグしている間も `ActiveEditingMode` は `Ink` でした。

`GestureOnly` と `InkAndGesture` では、同じ左から右へのドラッグがジェスチャー `Right` と認識され、どちらのモードでもストロークは残りませんでした。ジェスチャーの認識にはマシンの認識エンジンが要り、計測したマシンでは `IsGestureRecognizerAvailable` が `True` でした。`InkAndGesture` モードでジェスチャーのつもりでないストロークを残すには、`Gesture` のハンドラーで `Cancel` を `True` にします。これでドラッグはストロークとして残り、`SetEnabledGestures` で認識するジェスチャーを円だけに限った場合も残りました。

## ペンと、その後ろの背景

`DefaultDrawingAttributes` は新しいストロークのペンで、既定では黒・約 2 × 2（縦横とも正確には 2.0031496062992127）・楕円の先端・蛍光ペンではない設定です。各ストロークは自分用のコピーを持ちます。デモアプリと同じようにペンの `Color` と `Width` をその場で書き換えると、1 本目のストロークは黒・幅約 2 のままで、次のストロークが赤・幅 10 になりました。既定のペンを変えても影響するのはその後のストロークだけなので、描いたストロークの色を変えるには、そのストロークの `DrawingAttributes` を変えます。

`Background` プロパティ自体の既定値は `null` ですが、既定のスタイルがシステムのウィンドウ色（`SystemColors.WindowBrush`）を設定するので、InkCanvas は透明ではありません。計測したマシンでは白でした。`Background` を `null` にしてもドラッグでストロークが描けました。InkCanvas は領域全体で入力を受け取るので、背景なしの InkCanvas を画像の上に重ねて書き込みに使えます。

ペンの色は背景と比べて選びます。デモアプリはペンの色 `AntiqueWhite` と背景 `AliceBlue` で始まり、この 2 色のコントラスト比は 1.09 : 1 しかないので、描く前に濃いペンの色を選んでください。

## コマンド、保存、元に戻す

InkCanvas に送る `ApplicationCommands` は、モードとストロークによって実行できるかが変わりました。`SelectAll` は、`Ink` モードではストロークがあっても実行できませんでした。`Select` モードではストロークが無いと実行できず、1 本あると実行でき、そのストロークが選択されました。その後は `Copy` も実行できました。`SelectAll` の前には `Select` モードにします。`Undo` は実行できず、InkCanvas はこのコマンドを処理しないので、元に戻す機能は自分で履歴を持ちます。

`Strokes` は、署名を残すときなどに保存できます。2 本のストロークを `Save` で ISF（Ink Serialized Format）として保存すると 66 バイトで、読み込むと 2 本に戻りました。`Strokes.Clear()` ですべて消せます。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/inkcanvas/inkcanvas-behavior.svg" alt="InkCanvas の計測結果の表。既定値は Ink・EraseByStroke・既定のスタイルによるシステムのウィンドウ色（白）の背景・幅約 2 の黒いペン、デモアプリは Ink・InkAndGesture・AliceBlue の上の AntiqueWhite で始まりコントラスト比は 1.09 : 1、実際のドラッグは背景が null でもストロークを 1 本描き、各ストロークはペンのコピーを持ち、None とジェスチャーのモードではストロークが残らず、ジェスチャーのモードでは Right と認識され、EraseByStroke は線を消し EraseByPoint は線を分け、クリックで選択でき、SelectAll は Ink モードでは実行できず Select モードではストロークが要り、Undo は実行できず、2 本のストロークの ISF は 66 バイト" width="1242" height="710" loading="lazy">
  <figcaption>既定値、デモアプリの初期値、描画、モード、コマンド。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの InkCanvas のページ。左にコントロールの一覧、右に最初の節の EditingMode / ActiveEditingMode / EditingModeInverted / DefaultDrawingAttributes / Strokes / Background](/images/wpf-standard-control-demo/inkcanvas.png){: .screenshot-img}

デモアプリの InkCanvas のページには、`EditingMode`、`EditingModeInverted`、`ActiveEditingMode`、ペンの色と太さ、`Background` と、Copy・Cut・Paste・Select All・Clear All のボタンをまとめた欄が 1 つあります。ボタンは `ApplicationCommands` を InkCanvas に送り、Clear All は `Strokes.Clear()` を呼びます。ペンの色 `AntiqueWhite` と背景 `AliceBlue` は、コンボボックスの `Colors` の 2 番目と `Brushes` の 1 番目の項目で、`EditingModeInverted` は既定値ではなく列挙型の 4 番目の値の `InkAndGesture` で始まります。欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、その欄（`InkCanvasUsageControl.xaml`）の一部から、スタイル、ペンの設定、ほかのボタン、周囲の GroupBox を省き、名前空間の宣言を加えたものです。InkCanvas には、デモアプリのレイアウトの代わりに高さ 200 を指定しています。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="EditingModeComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=InkCanvasEditingMode}"
            SelectedValuePath="Value"
            SelectedIndex="1" />
  <ComboBox x:Name="EditingModeInvertedComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=InkCanvasEditingMode}"
            SelectedValuePath="Value"
            SelectedIndex="3" />
  <ComboBox x:Name="BackgroundBrushComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:StaticBindingSource TargetType=Brushes}"
            SelectedValuePath="Value"
            SelectedIndex="0" />

  <Button Command="ApplicationCommands.SelectAll"
          CommandTarget="{Binding ElementName=DemoInkCanvas}"
          Content="Select All" />

  <InkCanvas x:Name="DemoInkCanvas"
             Height="200"
             Background="{Binding SelectedValue, ElementName=BackgroundBrushComboBox}"
             EditingMode="{Binding SelectedValue, ElementName=EditingModeComboBox}"
             EditingModeInverted="{Binding SelectedValue, ElementName=EditingModeInvertedComboBox}">
    <InkCanvas.DefaultDrawingAttributes>
      <DrawingAttributes Color="Black" />
    </InkCanvas.DefaultDrawingAttributes>
  </InkCanvas>
</StackPanel>
```

## 関連するコントロールと記事

- [Canvas](/ja/apps/wpf-standard-control-demo/canvas.html) — インクを持たず、座標で要素を配置するパネルです。
- [Image](/ja/apps/wpf-standard-control-demo/image.html) — 注釈のために InkCanvas を重ねる画像です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`InkCanvasDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/InkCanvasDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、300 × 200 の InkCanvas で試しました。ストロークは実際のマウスのドラッグとクリックで描き・消し・選び、ペンは使っていません。計測したマシンのクリップボードを書き換えないよう、Copy・Cut・Paste は実行せず、実行できるかどうかだけを読みました。デモアプリの初期値は、デモアプリのマークアップ拡張と同じ方法で項目を並べて求めました。

[GitHub で InkCanvas のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/InkCanvasUsage){: target="_blank" rel="noopener noreferrer"}
