---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/dockpanel.html
title: "DockPanel"
badge: "Layout"
lead: "DockPanel は、子を順に自分の端に沿って置き、既定では最後の子に残りの領域を与えるパネルです。"
description: "WPF の DockPanel を .NET 10 で実測して解説。Dock ごとに子がどこへ置かれるか、LastChildFill で最後の子の Dock がどうなるか、子を書く順序で配置がどう変わるかを確かめます。空いた場所のクリックと ZIndex も示します。"
---

## 子は書いた順に端を取る

**DockPanel** の子は、添付プロパティ `DockPanel.Dock` で置く端を決めます。設定しない子は `Left` に置かれます。子は書いた順に、それまでの子が残した領域から自分の端を取ります。300 × 100 のパネルに上・下・左・内容の順で子を置くと、左の子は上と下の間の高さ 44.08 になりました。同じ左の子を最初に書くと、高さ 100 をすべて使いました。そのため、子は領域を取らせたい順に書きます。上下のバーより先に書いた横のペインは、ウィンドウの高さいっぱいに伸びます。

どの端に置くかで、子が自分の大きさを保つ向きが決まります。300 × 100 のパネルでは、`Left` と `Right` でラベルは自分の幅と全体の高さになり、x=0 と x=257.66 に置かれました。`Top` と `Bottom` では全体の幅と自分の高さ 27.96 になり、y=0 と y=72.04 に置かれました。

## LastChildFill が最後の子に与える影響

`LastChildFill` の既定値は `True` です。このとき最後の子は残りを埋め、その子の `Dock` は無視されます。`Dock="Top"` を設定した最後の子も、1 つ目の子の横の残り 257.66 × 100 を埋めました。この場合、最後の子に `Dock` を書いても効きません。

すべての子を端に置くなら `LastChildFill="False"` にします。残りの領域は空いたままになります。幅 300 のパネルに 2 つのラベルを置くと、`False` では 2 つ目のラベルは自分の幅の 42.34 のままで、`True` では 257.66 に伸びて残りを埋めました。デモアプリはチェックの外れたチェックボックスにバインドしているので、既定値の `True` ではなく `False` で始まります。

## 重なりと空いた場所

`LastChildFill="False"` で左に子を 1 つ置いたとき、空いた場所のヒットテストでは、`Background` が既定値の `null` なら何も当たらず、色を設定するとパネルが当たりました。空いた場所でクリックを受けるなら、パネルに `Background` を設定します。

子が重なったときは、`Panel.ZIndex` でどれを上にするかが決まります。デモアプリでは 2 つ目のラベルの左余白が -30 で、1 つ目に重なっています。`ZIndex` が 1 と 2 では重なった部分で 2 つ目が、3 と 2 では 1 つ目が上になりました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/dockpanel/dockpanel-behavior.svg" alt="DockPanel の計測結果の表。LastChildFill の既定値は True で Dock を設定しない子は Left、デモアプリの 2 つ目のラベルは LastChildFill が True のときだけ伸び、埋める最後の子の Dock Top は無視され、デモアプリの 1 つのラベルは Left と Right で高さいっぱい、Top と Bottom で幅いっぱいになり、先に書いた左の子は高さいっぱいに伸び、空いた場所は Background があるときだけ当たり、ZIndex がデモアプリの重なったラベルの順序を決める" width="1210" height="500" loading="lazy">
  <figcaption>端への配置、<code>LastChildFill</code>、子の順序、背景、<code>ZIndex</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの DockPanel のページ。左にコントロールの一覧、右に最初の節の LastChildFill](/images/wpf-standard-control-demo/dockpanel.png){: .screenshot-img}

デモアプリの DockPanel のページには、`LastChildFill`、`Dock`、`Background`、`ZIndex` の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。
次の XAML は、`Dock` の欄（`DockPanelUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="DockComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=Dock}"
            SelectedValuePath="Value"
            SelectedIndex="0" />

  <DockPanel x:Name="DockUsageDockPanel" Height="100" LastChildFill="False">
    <Label Background="LightBlue" BorderBrush="Black" BorderThickness="1"
           Content="Item1"
           DockPanel.Dock="{Binding SelectedValue, ElementName=DockComboBox}" />
  </DockPanel>
</StackPanel>
```

## 関連するコントロールと記事

- [Grid](/ja/apps/wpf-standard-control-demo/grid.html) — スター指定のある行と列で、DockPanel の順序では表せない配置を作ります。
- [StackPanel](/ja/apps/wpf-standard-control-demo/stackpanel.html) — 残りを埋めずに、子を 1 列に並べます。
- [Menu](/ja/apps/wpf-standard-control-demo/menu.html) — ウィンドウの上端によく置かれます。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`DockPanelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/DockPanelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じラベル（枠 1）を 300 × 100 のパネルに置いて試しました。パネルは `Measure` と `Arrange` でレイアウトしました。大きさは、計測したマシンでの値です。

[GitHub で DockPanel のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/DockPanelUsage){: target="_blank" rel="noopener noreferrer"}
