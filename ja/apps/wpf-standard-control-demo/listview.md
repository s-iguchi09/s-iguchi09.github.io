---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/listview.html
title: "ListView"
badge: "List"
lead: "ListView は、ビューを通して項目を表示できる ListBox です。GridView を使うと、項目が列見出し付きの行になります。"
description: "WPF の ListView を .NET 10 で実測して解説。既定の選択モードが Extended であること、後から広がらない自動幅の列、実際のマウスでの列の並べ替えと見出しのクリック、スクロールバーを無効にしたときのスクロールを確かめます。"
---

## ListBox との違い：既定で Extended の選択

**ListView** は `ListBox` から派生し、項目は `ListViewItem` のコンテナに入ります。`View` の既定値は `null` で、列見出しのない普通の一覧になります。

ListBox との違いの 1 つは選択で、ListView の `SelectionMode` の既定値は `Extended`、ListBox は `Single` です。1 項目だけ選ばせるなら `SelectionMode="Single"` を設定します。選択モード、`SelectedIndex`（範囲外のインデックスを含む）、`IsSelected` の動きは ListBox のページで計測しています。

## GridView の列：幅、順序、並べ替え

`View` に `GridView` を設定すると、項目は列見出し付きの行になります。`Columns` の各列は `DisplayMemberBinding` か `CellTemplate` で値を表示します。両方を設定しても例外は起きず、`DisplayMemberBinding` が使われ、1 行目にはテンプレートの文字ではなく `AliceBlue` が表示されました。

列の `Width` が `Auto`（`NaN`）のとき、Name の列は最初に表示された行に合わせて幅 84.89 になり、その後は広がりませんでした。いちばん長い名前の `LightGoldenrodYellow` までスクロールしても、先頭にもっと長い名前を挿入しても 84.89 のままでした。いちばん長い値が最初の行にないなら、列の幅を固定します。バインドした幅はソースに従い、Value の列の幅をスライダーにバインドすると、スライダーを 300 にしたとき列の幅は 300 になりました。

`AllowsColumnReorder` は、見出しをドラッグして列を動かせるかどうかで、既定値は `True` です。実際のマウスで Name の見出しを Value の見出しの先までドラッグすると、`True` では順序が Value, Name になり、`False` では Name, Value のままでした。変わるのは `Columns` コレクション自体の順序です。

GridView は並べ替えをしません。実際のマウスで Name の見出しをクリックしても、先頭の項目は `AliceBlue` のままで、並べ替えの条件（SortDescription）も追加されませんでした。並べ替えはコードで行い、見出しのクリックを処理して項目のビューに `SortDescription` を追加します。

## スクロールと、Disabled で切れる部分

GridView でも行は仮想化され、高さ 150 の ListView に 1,000 項目を入れると、作られた項目コンテナは 8 個でした。

幅 500 の列が 1 つある、幅 300・高さ 100 の一覧では、スクロールバーを `Disabled` にすると横にスクロールできず、列の右側には届きませんでしたが、縦にはスクロールできました。`ScrollToBottom` でも、下矢印キーで最後の行へ移っても、縦の位置は 2 行分になりました（横の位置は 0 のまま）。`Auto` では、横は 227 DIP、縦は 3 行分まで動きました。列が広いときは、横のスクロールバーを `Auto` にしておきます。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listview/listview-behavior.svg" alt="ListView の計測結果の表。ListBox から派生し、既定では選択モードが Extended でビューがなく、自動幅の列はいちばん長い名前までスクロールしても長い名前を挿入しても 84.89 のまま、バインドした幅はスライダーに従い、実際の見出しのドラッグは AllowsColumnReorder のときだけ列を並べ替え、見出しのクリックでは並べ替わらず、DisplayMemberBinding は例外なく CellTemplate より優先され、Disabled のスクロールバーでは横にスクロールできないが縦には 2 行スクロールでき、1,000 項目で作られるコンテナは 8 個" width="1132" height="470" loading="lazy">
  <figcaption>既定値、GridView の列、並べ替え、見出しのクリック、スクロール。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの ListView のページ。左にコントロールの一覧、右に最初の節の SelectionMode](/images/wpf-standard-control-demo/listview.png){: .screenshot-img}

デモアプリの ListView のページには、`SelectionMode`、`View` と列・`AllowsColumnReorder`、列の `Width`、`SelectedIndex` と `SelectedItem`、`IsSelected`、スクロールバーの欄があります。`SelectionMode` のコンボボックスは先頭の値の `Single` で始まり、一覧の下に `SelectedItems.Count` を表示しています。`View` の欄はブラシの一覧に Name と Value の列を持つ `GridView` を設定し、`AllowsColumnReorder` はチェックを付けた状態で始めます。`Width` の欄は Value の列の幅を 150 で始まるスライダーにバインドし、`SelectedIndex` の欄は `SelectedIndex="0"`（Item A）で始まってテキストボックスをインデックスに TwoWay でバインドしています。`IsSelected` の欄は 1 つ目の項目の `IsSelected` をチェックボックスにバインドし、スクロールバーの欄には幅 500 の列が 1 つあり、コンボボックスは `Disabled` で始まります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`View` の欄（`ListViewUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <CheckBox x:Name="AllowsColumnReorderCheckBox" Content="AllowsColumnReorder" IsChecked="True" />

  <ListView x:Name="ViewColumnsListView"
            Height="150"
            ItemsSource="{markupextensions:StaticBindingSource TargetType=Brushes}">
    <ListView.View>
      <GridView AllowsColumnReorder="{Binding IsChecked, ElementName=AllowsColumnReorderCheckBox}">
        <GridViewColumn Width="auto" DisplayMemberBinding="{Binding Name}" Header="Name" />
        <GridViewColumn Width="auto" DisplayMemberBinding="{Binding Value}" Header="Value" />
      </GridView>
    </ListView.View>
  </ListView>
</StackPanel>
```

## 関連するコントロールと記事

- [ListBox](/ja/apps/wpf-standard-control-demo/listbox.html) — ListView の基底クラスで、選択を詳しく計測しています。
- [DataGrid](/ja/apps/wpf-standard-control-demo/datagrid.html) — 編集と並べ替えができる表です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ListViewDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ListViewDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じブラシの一覧（各ブラシの `Name` と `Value`）で試しました。見出しのドラッグとクリックは実際のマウスで行いました。幅は、計測したマシンでの値です。

[GitHub で ListView のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ListViewUsage){: target="_blank" rel="noopener noreferrer"}
