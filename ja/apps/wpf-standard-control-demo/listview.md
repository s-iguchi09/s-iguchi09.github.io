---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/listview.html
title: "ListView"
badge: "List"
lead: "ListView は、ビューを通して項目を表示できる ListBox です。GridView を使うと、項目が列見出し付きの行になります。"
description: "WPF の ListView を .NET 10 で実測して解説。既定の選択モードが Extended であること、後から広がらない自動幅の列、実際のマウスでの列の並べ替えと見出しのクリック、スクロールバーを無効にしたときのスクロールを確かめます。"
---

## 概要

**ListView** は `ListBox` から派生し、項目は `ListViewItem` のコンテナに入ります。`View` の既定値は `null` で、列見出しのない普通の一覧になります。ListBox との違いの 1 つは選択で、ListView の `SelectionMode` の既定値は `Extended`、ListBox は `Single` です。デモアプリのコンボボックスは先頭の値の `Single` で始まります。選択モード、`SelectedIndex`、`IsSelected` の動きは ListBox のページで計測しています。

GridView は並べ替えをしません。実際のマウスで Name の見出しをクリックしても、先頭の項目は `AliceBlue` のままで、並べ替えの条件（SortDescription）も追加されませんでした。行は仮想化され、高さ 150 の ListView に 1,000 項目を入れると、作られた項目コンテナは 8 個でした。

デモアプリには、`SelectionMode`、`View` と列・`AllowsColumnReorder`、列の `Width`、`SelectedIndex` と `SelectedItem`、`IsSelected`、スクロールバーの欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![デモアプリの ListView のページ。左にコントロールの一覧、右に最初の節の SelectionMode](/images/wpf-standard-control-demo/listview.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `SelectionMode (ListBox)` | `Single / Multiple / Extended` | クリックで項目をどう選ぶかです。ListView の既定値は `Extended` です（上を参照）。デモアプリは一覧の下に `SelectedItems.Count` を表示しています。 |
| `View` | `ViewBase` | 項目の表示の仕方で、既定値は `null`（普通の一覧）です。デモアプリは、ブラシの一覧に Name と Value の列を持つ `GridView` を設定しています。 |
| `Columns (GridView)` | `GridViewColumnCollection` | 列です。各列は `DisplayMemberBinding` か `CellTemplate` で値を表示します。両方を設定しても例外は起きず、`DisplayMemberBinding` が使われ、1 行目にはテンプレートの文字ではなく `AliceBlue` が表示されました。 |
| `AllowsColumnReorder (GridView)` | `bool` | 見出しをドラッグして列を動かせるかどうかで、既定値は `True`、デモアプリもチェックを付けた状態で始めます。実際のマウスで Name の見出しを Value の見出しの先までドラッグすると、`True` では順序が Value, Name になり、`False` では Name, Value のままでした。変わるのは `Columns` コレクション自体の順序です。 |
| `Width (GridViewColumn)` | `double` | 列の幅です。`Auto`（`NaN`）では、Name の列は最初に表示された行に合わせて幅 84.89 になり、その後は広がりませんでした。いちばん長い名前の `LightGoldenrodYellow` までスクロールしても、先頭にもっと長い名前を挿入しても 84.89 のままでした。デモアプリは Value の列の幅を 150 で始まるスライダーにバインドしており、スライダーを 300 にすると列の幅は 300 になりました。 |
| `SelectedIndex / SelectedItem (Selector)` | `int / object` | 選択している項目の位置とオブジェクトです。デモアプリは `SelectedIndex="0"`（Item A）で始まり、テキストボックスをインデックスに TwoWay でバインドしています。範囲外のインデックスについては ListBox のページを参照してください。 |
| `IsSelected (ListBoxItem)` | `bool` | コンテナが選択されているかどうかです。デモアプリは 1 つ目の項目の `IsSelected` をチェックボックスにバインドしており、その動きは ListBox のページで計測しています。 |
| `HorizontalScrollBarVisibility / VerticalScrollBarVisibility (ScrollViewer)` | `Disabled / Auto / Hidden / Visible` | スクロールバーです。デモアプリの一覧には幅 500 の列が 1 つあり、コンボボックスは `Disabled` で始まります。幅 300・高さ 100 の一覧では、`Disabled` では横にスクロールできず、列の右側には届きませんでしたが、縦にはスクロールできました。`ScrollToBottom` でも、下矢印キーで最後の行へ移っても、縦の位置は 2 行分になりました（横の位置は 0 のまま）。`Auto` では、横は 227 DIP、縦は 3 行分まで動きました。 |

## XAML 使用例

デモアプリ（`ListViewUsageControl.xaml`）の `View` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

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

## 主な使用例

- **ファイルの一覧** — 名前・サイズ・日付の列を、利用者が並べ替えられます。
- **読み取り専用の表** — 編集しない記録を行で表示します。
- **詳細を並べた一覧** — 各項目の複数のプロパティを表示する一覧です。

## ヒントとベストプラクティス

- **1 項目だけ選ばせるなら `SelectionMode="Single"` を設定する** — ListView は `Extended` で始まります。
- **いちばん長い値が最初の行にないなら、列の幅を固定する** — 自動の幅は後から更新されません。
- **並べ替えはコードで行う** — 見出しのクリックだけでは何も起きません。項目のビューに `SortDescription` を追加します。
- **列が広いときは横のスクロールバーを `Auto` にしておく** — `Disabled` では列の右側が切れます。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ListViewDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ListViewDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じブラシの一覧（各ブラシの `Name` と `Value`）で試しました。見出しのドラッグとクリックは実際のマウスで行いました。幅は、計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listview/listview-behavior.svg" alt="ListView の計測結果の表。ListBox から派生し、既定では選択モードが Extended でビューがなく、自動幅の列はいちばん長い名前までスクロールしても長い名前を挿入しても 84.89 のまま、バインドした幅はスライダーに従い、実際の見出しのドラッグは AllowsColumnReorder のときだけ列を並べ替え、見出しのクリックでは並べ替わらず、DisplayMemberBinding は例外なく CellTemplate より優先され、Disabled のスクロールバーでは横にスクロールできないが縦には 2 行スクロールでき、1,000 項目で作られるコンテナは 8 個" width="1132" height="470" loading="lazy">
  <figcaption>既定値、GridView の列、並べ替え、見出しのクリック、スクロール。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [ListBox](/ja/apps/wpf-standard-control-demo/listbox.html) — ListView の基底クラスで、選択を詳しく計測しています。
- [DataGrid](/ja/apps/wpf-standard-control-demo/datagrid.html) — 編集と並べ替えができる表です。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で ListView のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ListViewUsage){: target="_blank" rel="noopener noreferrer"}
