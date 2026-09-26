---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/scrollviewer.html
title: "ScrollViewer"
badge: "Layout"
lead: "ScrollViewer は、自分より大きい内容の一部を表示し、残りをスクロールして見せるコンテナーです。ListBox、TextBox、TreeView、DataGrid も、それぞれ内部に 1 つ持っています。"
description: "WPF の ScrollViewer を .NET 10 で実測して解説。スクロールバーの 4 つの設定を内容が収まる場合とはみ出す場合で比べ、CanContentScroll と仮想化の関係、遅延スクロールの動きを確かめます。ListBox の中の設定の変え方も示します。"
---

## スクロールバーの 4 つの設定を、収まる内容とはみ出す内容で比べる

`VerticalScrollBarVisibility` と `HorizontalScrollBarVisibility` は、バーを表示するかと、内容をスクロールできるかを決めます。デモアプリと同じく、幅 200、高さ 100 の **ScrollViewer** に、収まる 2 つのラベルと、はみ出す 9 つのラベルを入れて比べました。

- `Disabled` ではスクロールできません。内容には ScrollViewer の高さしか与えられず（9 つのラベルで `ExtentHeight` 100）、残りは切れて、50 までスクロールしても位置は 0 のままでした。
- `Auto` はラベルが 9 つのときだけバーを表示しました。
- `Hidden` はバーを表示しませんが、50 までのスクロールはできました。バーを隠してスクロールは残すなら、`Disabled` ではなく `Hidden` を使います。
- `Visible` はラベルが 2 つでもバーを表示しました。バーは幅を取り、`ViewportWidth` はバーがあると 183、ないと 200 でした。スクロールするものがない場合もあるなら `Auto` を使います。

バーが実際に表示されているかを表す `ComputedVerticalScrollBarVisibility` が `Visible` だったのは、`Auto` でラベルが 9 つのときと `Visible` のときだけで、それ以外はすべて `Collapsed` でした。計測したのは縦のバーです。`HorizontalScrollBarVisibility` にも同じ 4 つの値を設定できますが、横方向は計測していません。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/scrollviewer/scrollviewer-visibility.svg" alt="幅 200、高さ 100 の ScrollViewer にラベルを 2 つと 9 つ入れたときの、VerticalScrollBarVisibility の 4 つの値の計測結果の表。Disabled はバーがなく、内容の高さは 100 になり、スクロールしない。Auto はラベルが 9 つのときだけバーを表示し、Hidden はバーを表示しないが 50 までスクロールでき、Visible は常にバーを表示し、バーがあると表示領域の幅は 200 から 183 になる" width="946" height="320" loading="lazy">
  <figcaption>内容が収まる場合とはみ出す場合の、<code>VerticalScrollBarVisibility</code> の各値。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 既定値と、コントロールの中の ScrollViewer の変え方

ScrollViewer の既定値は、`VerticalScrollBarVisibility` が `Visible`、`HorizontalScrollBarVisibility` が `Disabled` です。内部に持つコントロールは、それぞれ独自の値を設定しています。ListBox、TreeView、DataGrid の中の ScrollViewer は横方向が `Auto`、TextBox の中のものは `Hidden` でした。

コントロールの中の ScrollViewer を変えるには、コントロール自身に添付プロパティを設定します。ListBox に `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` を設定すると、中の ScrollViewer に届きました。`Disabled` にした別の ScrollViewer で ListBox を包んでも届かず、中のものは `Auto` のままでした。

## CanContentScroll：ピクセルか項目か、そして仮想化

`CanContentScroll` は、内容が自分の単位でスクロールするかどうかを決めます。デモアプリの 9 つのラベルの StackPanel では、`False` で `ExtentHeight` 251.64、`ViewportHeight` 100 とピクセル単位になり、`LineDown` 1 回で 16 動きました。`True` では 9 と 3 と項目の数になり、`LineDown` 1 回で 1 項目動きました。

一覧では仮想化にも関わります。既定の設定の、1000 項目、高さ 200 の ListBox は、`True` で 10 個、`False` で 1000 個すべての `ListBoxItem` を作りました。`False` にすると仮想化は止まりますが、`True` だけで仮想化されるわけではなく、ListBox の既定のように項目が `VirtualizingStackPanel` に並び、`VirtualizingPanel.IsVirtualizing` が `True` のままである必要があります。長い一覧では `True` のままにします。

## 遅延スクロールと、大きさが分かる時点

`IsDeferredScrollingEnabled` は、つまみを離すまで内容の移動を待つかどうかです。`False` では、つまみを 30 下へドラッグすると、ドラッグ中に `VerticalOffset` と `ContentVerticalOffset` がどちらも 114.38 になりました。`True` ではドラッグ中はどちらも 0 のままで、離した時点で 114.38 になりました。ドラッグは、実際のマウスではなく、つまみのドラッグのイベント（開始、30 の移動、完了）を送って再現しました。

大きさのプロパティは、レイアウトの後でないと分かりません。`ExtentHeight` と `ViewportHeight` は、最初のレイアウトの前は 0、後は 251.64 と 100 でした。StackPanel の中などで ScrollViewer がまったくスクロールしない理由は、下にリンクした記事で扱っています。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/scrollviewer/scrollviewer-scrolling.svg" alt="ScrollViewer のスクロールの計測結果の表。既定値は Visible と Disabled、CanContentScroll が False なら 16 ピクセル、True なら 1 項目ずつスクロールし、1000 項目の ListBox は True でコンテナーを 10 個、False で 1000 個作り、遅延スクロールではつまみを離すまで VerticalOffset と ContentVerticalOffset が 0 のまま、ListBox・TreeView・DataGrid の中は Auto、TextBox の中は Hidden、ListBox の添付プロパティは中に届き外側の ScrollViewer は届かず、大きさはレイアウト前は 0" width="936" height="380" loading="lazy">
  <figcaption><code>CanContentScroll</code>、遅延スクロール、コントロールの中の ScrollViewer。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの ScrollViewer のページ。左にコントロールの一覧、右に最初の節の CanContentScroll](/images/wpf-standard-control-demo/scrollviewer.png){: .screenshot-img}

デモアプリの ScrollViewer のページには、`CanContentScroll`、`IsDeferredScrollingEnabled`、縦横のスクロールバーの設定、縦横の大きさと位置を表す読み取り専用のプロパティの欄があります。読み取り専用の値は、ラベルが 2 つと 9 つの場合と、ラベルを横に並べた場合について表示します。どの欄も、枠付きのラベルを入れた高さ 100 の ScrollViewer を使っています。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`CanContentScroll` の欄（`ScrollViewerUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。スタイルは、ScrollViewer の `Height` を 100 にし、各ラベルに幅 1 の黒い枠を付けています。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="CanContentScrollCheckBox" Content="CanContentScroll" />

  <ScrollViewer x:Name="CanContentScrollScrollViewer"
                CanContentScroll="{Binding IsChecked, ElementName=CanContentScrollCheckBox}"
                Height="100">
    <StackPanel Orientation="Vertical">
      <Label Content="Item1" />
      <Label Content="Item2" />
      <Label Content="Item3" />
      <Label Content="Item4" />
      <Label Content="Item5" />
      <Label Content="Item6" />
      <Label Content="Item7" />
      <Label Content="Item8" />
      <Label Content="Item9" />
    </StackPanel>
  </ScrollViewer>
</StackPanel>
```

## 関連するコントロールと記事

- [ListBox](/ja/apps/wpf-standard-control-demo/listbox.html) — 添付プロパティで変えられる ScrollViewer を内部に持ちます。
- [StackPanel](/ja/apps/wpf-standard-control-demo/stackpanel.html) — ScrollViewer の子によく使われ、`CanContentScroll` が `True` なら項目単位でスクロールします。
- [Viewbox](/ja/apps/wpf-standard-control-demo/viewbox.html) — スクロールせずに、内容を拡大縮小して収めます。
- [WPF で ScrollViewer がスクロールしない原因と解決方法](/ja/articles/wpf-scrollviewer-not-scrolling/) — ScrollViewer にスクロールバーが出ないレイアウトを扱います。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ScrollViewerDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ScrollViewerDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。内容はデモアプリと同じ枠付きのラベルです。つまみのドラッグは、マウスでドラッグしたときと同じ `DragStarted`・`DragDelta`・`DragCompleted` のイベントをつまみに発生させて再現しました。大きさは、計測したマシンでの値です。

[GitHub で ScrollViewer のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ScrollViewerUsage){: target="_blank" rel="noopener noreferrer"}
