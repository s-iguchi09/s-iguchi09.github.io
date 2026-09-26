---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/listbox.html
title: "ListBox"
badge: "List"
lead: "ListBox はスクロールできる一覧から項目を選ばせるコントロールです。<code>SelectionMode</code> で複数の項目も選べます。"
description: "WPF の ListBox の選択モード、SelectedItem・SelectedValue、仮想化とスクロールを .NET 10 で実測して解説します。SelectedItem は Equals で照合され、コードから選んでもスクロールしないことも確かめます。"
---

## SelectionMode ごとにクリックで何が選ばれるか

**ListBox** は `Selector` を継承しており、`ListView` は ListBox を継承しています。項目は `Items` か `ItemsSource` から与え、それぞれ `ListBoxItem` というコンテナーで表示されます。

`SelectionMode` はクリックでの選び方で、既定値は `Single` です。修飾キーを押さずに Item 1、Item 2、Item 1 の順にクリックした結果は次のとおりでした。`Single` は Item 1、Item 2、Item 1 の順に選択が移りました。`Multiple` はクリックごとに選択が切り替わるので、Item 1、Item 1 と 2、Item 2 になりました。`Extended` は、修飾キーなしのクリックでは選択が置き換わるので、`Single` と同じになりました（まとめて処理するファイルなどを `Extended` で複数選ぶには Shift や Ctrl を使います。これは再現していません）。`SelectAll()` は `Multiple` と `Extended` では 4 項目すべてを選び、`Single` では `NotSupportedException` が発生したので、呼ぶ前に `SelectionMode` を確認します。

## コードからの選択は Equals で照合される

`SelectedIndex = 2` とすると 3 番目の項目が選ばれ、`SelectedItem` も変わり、`SelectionChanged` が 1 回発生しました。範囲外の番号（4 項目に対して 10）は例外を出さず、選択も変わりませんでした。-1 で選択が外れました。

`SelectedItem` は参照ではなく `Equals` で照合されます。`Equals` を上書きしていないクラスでは、同じ内容の新しいインスタンスを設定しても何も選ばれませんでした。レコード型では、同じ値の新しいインスタンスで該当の項目が選ばれました。このとき `SelectedItem` が保持していたのは、一覧の中のインスタンスではなく、設定した新しいインスタンスでした。保存したデータから ViewModel が選択を戻すなら、項目のクラスに `Equals` を用意するか、`SelectedValue` と ID で選びます。

`SelectedValuePath` には、`SelectedValue` として返す選択項目のプロパティ（たとえば `Id`）を指定します。設定の順序は問いません。`ItemsSource` より前に `SelectedValue = 2` と設定しても、項目が入った時点で `Id` が 2 の項目が選ばれました。

`DisplayMemberPath` は、各項目のどのプロパティを文字として表示するかです。`DisplayMemberPath="Name"` では、各項目は "Desktop" と表示する `TextBlock` で表示されました。存在しないパスを指定すると、例外は出ず、`TextBlock` は空になりました。`ItemTemplate` とは併用できず、両方を設定すると `InvalidOperationException` が発生しました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listbox/listbox-values.svg" alt="ListBox の選択の値を示す表。範囲外の SelectedIndex は無視され、-1 で選択が外れ、Equals を上書きしないクラスの新しいインスタンスでは何も選ばれないがレコード型では選ばれ、ItemsSource より前に設定した SelectedValue は後から反映され、存在しない DisplayMemberPath は空の文字列になり、ItemTemplate との併用は InvalidOperationException になる" width="1077" height="350" loading="lazy">
  <figcaption>SelectedIndex・SelectedItem・SelectedValue・DisplayMemberPath。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 仮想化：何が作られ、何がスクロールするか

既定のスタイルは項目を `VirtualizingStackPanel` に並べるため、作られるのは見えている範囲のコンテナーだけです。高さ 100 の一覧に 1,000 項目を入れると、`ListBoxItem` は 6 個でした。

コードから選んだ項目が見えないことがあるのはこのためです。`SelectedIndex = 500` としても一覧はスクロールせず、500 番目の項目のコンテナーも作られませんでした。`ScrollIntoView` を呼ぶと、その項目までスクロールしました。コードから選んだら `ScrollIntoView` を呼びます。

複数選択にも影響します。`ListBoxItem` の `IsSelected` は既定で TwoWay にバインドされますが、`ItemsSource` から作った一覧ではコンテナーは見えている間しか存在しないので、`ItemContainerStyle` に書いたバインドが効くのもその範囲の項目だけです。そうした選択がスクロールでずれることは、[WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/)で実測しています。`SelectionChanged` を処理して `SelectedItems` から ViewModel を更新するか、記事の方法を使います。

スクロールバーは添付プロパティ `ScrollViewer.HorizontalScrollBarVisibility` と `ScrollViewer.VerticalScrollBarVisibility` で決まり、既定のスタイルがどちらも `Auto` に設定しています。1,000 項目では、`Auto` で縦のスクロールバーが表示されました。`Disabled` ではスクロールバーは消えましたが、スクロールは止まりませんでした。スクロールできる範囲は同じ 996 項目分のままで、下矢印キーを 20 回押すと `Auto` と同じ位置までスクロールしました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listbox/listbox-scrolling.svg" alt="1,000 項目の ListBox のスクロールを示す表。コンテナーは 6 個作られ、コードから 500 番目を選んでも ScrollIntoView を呼ぶまでスクロールせず、縦のスクロールバーを Disabled にしてもキー操作ではスクロールする" width="1022" height="230" loading="lazy">
  <figcaption>高さ 100 の一覧に 1,000 項目を入れたときの仮想化とスクロール。位置の単位は項目。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの ListBox のページ。左にコントロールの一覧、右に最初の節の SelectionMode](/images/wpf-standard-control-demo/listbox.png){: .screenshot-img}

デモアプリの ListBox のページには、`SelectionMode`、`DisplayMemberPath`、`SelectedIndex` と `SelectedItem`、`SelectedValuePath` と `SelectedValue`、`IsSelected`、スクロールバーの欄があります。`SelectionMode` の欄では、一覧の下に `SelectedItems.Count` を表示しています。`DisplayMemberPath` と `SelectedValuePath` の欄はブラシの一覧に対してテキストボックスでパスを入力します。`DisplayMemberPath` では `Name` でブラシの名前が表示され、それ以外の文字列では項目が空白になります。`SelectedValuePath` では `Value` でブラシが、`Name` でその名前が返ります。`IsSelected` の欄では 2 つの `ListBoxItem` をチェックボックスにバインドしており、チェックを入れると項目が選ばれ、項目をクリックするとチェックボックスが変わります。スクロールバーの欄では、横のスクロールバーも試せるよう、先頭に長い項目を置いています。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`IsSelected(ListBoxItem)` の欄（`ListBoxUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsSelectedItem1CheckBox" Content="Select Item 1" />
  <CheckBox x:Name="IsSelectedItem2CheckBox" Content="Select Item 2" />

  <ListBox x:Name="IsSelectedListBox" Height="100" SelectionMode="Extended">
    <ListBoxItem Content="Item 1 (Bound to CheckBox)"
                 IsSelected="{Binding IsChecked, ElementName=IsSelectedItem1CheckBox}" />
    <ListBoxItem Content="Item 2 (Bound to CheckBox)"
                 IsSelected="{Binding IsChecked, ElementName=IsSelectedItem2CheckBox}" />
    <ListBoxItem Content="Item 3 (Independent)" />
  </ListBox>

  <TextBlock Text="{Binding SelectedItems.Count, ElementName=IsSelectedListBox}" />
</StackPanel>
```

## 関連するコントロールと記事

- [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/) — 仮想化と複数選択を扱います。
- [ListView](/ja/apps/wpf-standard-control-demo/listview.html) — ListBox を継承し、`GridView` の列などの表示方法を加えたコントロールです。
- [ComboBox](/ja/apps/wpf-standard-control-demo/combobox.html) — 一覧をドロップダウンで開く `Selector` です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ListBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ListBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。クリックは、修飾キーなしのクリックが ListBox に届くのと同じく、`ListBoxItem` にマウスの左ボタンのイベントを発生させて再現しました。Shift や Ctrl を押しながらのクリックは、ListBox が実際のキーボードの状態を読むため再現していません。

[GitHub で ListBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ListBoxUsage){: target="_blank" rel="noopener noreferrer"}
