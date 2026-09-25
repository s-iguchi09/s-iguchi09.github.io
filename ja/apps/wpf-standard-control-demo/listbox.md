---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/listbox.html
title: "ListBox"
badge: "List"
lead: "ListBox はスクロールできる一覧から項目を選ばせるコントロールです。<code>SelectionMode</code> で複数の項目も選べます。"
description: "WPF の ListBox の選択モード、SelectedItem・SelectedValue、仮想化とスクロールを .NET 10 で実測して解説します。SelectedItem は Equals で照合され、コードから選んでもスクロールしないことも確かめます。"
---

## 概要

**ListBox** は `Selector` を継承しており、`ListView` は ListBox を継承しています。項目は `Items` か `ItemsSource` から与え、それぞれ `ListBoxItem` というコンテナーで表示されます。既定のスタイルは項目を `VirtualizingStackPanel` に並べるため、作られるのは見えている範囲のコンテナーだけです。高さ 100 の一覧に 1,000 項目を入れると、`ListBoxItem` は 6 個でした。

コードから選んだ項目が見えないことがあるのも、仮想化のためです。`SelectedIndex = 500` としても一覧はスクロールせず、500 番目の項目のコンテナーも作られませんでした。`ScrollIntoView` を呼ぶと、その項目までスクロールしました。複数選択にも影響します。`ItemContainerStyle` でバインドした選択がスクロールでずれることは、[WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/)で実測しています。

デモアプリには以下の各プロパティの欄があり、各欄の下の「Show Code」リンクでその欄の XAML を表示できます。

## 画面キャプチャ

![listbox demo screen](/images/wpf-standard-control-demo/listbox.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `SelectionMode` | `Single / Multiple / Extended` | クリックでの選び方で、既定値は `Single` です。修飾キーを押さずに Item 1、Item 2、Item 1 の順にクリックした結果は次のとおりでした。`Single` は Item 1、Item 2、Item 1 の順に選択が移りました。`Multiple` はクリックごとに選択が切り替わるので、Item 1、Item 1 と 2、Item 2 になりました。`Extended` は、修飾キーなしのクリックでは選択が置き換わるので、`Single` と同じになりました（`Extended` で複数選ぶには Shift や Ctrl を使います。これは再現していません）。`SelectAll()` は `Multiple` と `Extended` では 4 項目すべてを選び、`Single` では `NotSupportedException` が発生しました。デモアプリでは、一覧の下に `SelectedItems.Count` を表示しています。 |
| `DisplayMemberPath (ItemsControl)` | `string` | 各項目のどのプロパティを文字として表示するかです。`DisplayMemberPath="Name"` では、各項目は "Desktop" と表示する `TextBlock` で表示されました。存在しないパスを指定すると、例外は出ず、`TextBlock` は空になりました。`ItemTemplate` とは併用できず、両方を設定すると `InvalidOperationException` が発生しました。デモアプリでは、ブラシの一覧に対し、`DisplayMemberPath` をテキストボックスにバインドしています。`Name` と入力するとブラシの名前が表示され、それ以外の文字列では項目が空白になります。 |
| `SelectedIndex / SelectedItem (Selector)` | `int / object` | 選んだ項目の位置とオブジェクトです。`SelectedIndex = 2` とすると 3 番目の項目が選ばれ、`SelectedItem` も変わり、`SelectionChanged` が 1 回発生しました。範囲外の番号（4 項目に対して 10）は例外を出さず、選択も変わりませんでした。-1 で選択が外れました。`SelectedItem` は参照ではなく `Equals` で照合されます。`Equals` を上書きしていないクラスでは、同じ内容の新しいインスタンスを設定しても何も選ばれませんでした。レコード型では、同じ値の新しいインスタンスで該当の項目が選ばれました。このとき `SelectedItem` が保持していたのは、一覧の中のインスタンスではなく、設定した新しいインスタンスでした。 |
| `SelectedValuePath / SelectedValue (Selector)` | `string / object` | `SelectedValuePath` には、`SelectedValue` として返す選択項目のプロパティ（たとえば `Id`）を指定します。設定の順序は問いません。`ItemsSource` より前に `SelectedValue = 2` と設定しても、項目が入った時点で `Id` が 2 の項目が選ばれました。デモアプリでは、ブラシの一覧に対し、テキストボックスでパスを入力します。`Value` ではブラシが、`Name` ではその名前が返ります。 |
| `IsSelected (ListBoxItem)` | `bool` | コンテナーが選ばれているかどうかで、既定で TwoWay にバインドされます。デモアプリでは 2 つの `ListBoxItem` の `IsSelected` をチェックボックスにバインドしており、チェックを入れると項目が選ばれ、項目をクリックするとチェックボックスが変わります。`ItemsSource` から作った一覧では、コンテナーは見えている間しか存在しません。そのため `ItemContainerStyle` に書いたバインドが効くのも、その範囲の項目だけです（上の記事を参照）。 |
| `HorizontalScrollBarVisibility / VerticalScrollBarVisibility (ScrollViewer)` | `Disabled / Auto / Hidden / Visible` | ListBox のスクロールバーを決める添付プロパティで、既定のスタイルがどちらも `Auto` に設定しています。1,000 項目では、`Auto` で縦のスクロールバーが表示されました。`Disabled` ではスクロールバーは消えましたが、スクロールは止まりませんでした。スクロールできる範囲は同じ 996 項目分のままで、下矢印キーを 20 回押すと `Auto` と同じ位置までスクロールしました。デモアプリでは、横のスクロールバーも試せるよう、先頭に長い項目を置いています。 |

## XAML 使用例

デモアプリ（`ListBoxUsageControl.xaml`）の `IsSelected(ListBoxItem)` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

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

## 主な使用例

- **常に見せておく選択肢** — ドロップダウンに隠さず、画面に出しておきたい選択肢に使います。
- **一覧と詳細** — 左の一覧の `SelectedItem` で右の詳細を切り替えます。
- **複数の項目の選択** — まとめて処理するファイルやレコードを `SelectionMode="Extended"` で選ばせます。

## ヒントとベストプラクティス

- **保存したデータから選択を戻すなら、項目のクラスに `Equals` を用意する** — あるいは `SelectedValue` と ID で選びます。`Equals` を上書きしていないと、同じ内容に見える新しいインスタンスでは何も選ばれません。
- **コードから選んだら `ScrollIntoView` を呼ぶ** — 仮想化した一覧は、選ぶだけではスクロールしません。
- **複数選択はコンテナーに頼らずに同期する** — `SelectionChanged` を処理して `SelectedItems` から ViewModel を更新するか、上の記事の方法を使います。`ItemContainerStyle` のバインドが効くのは、作られたコンテナーだけです。
- **`SelectAll()` の前に `SelectionMode` を確認する** — `Single` では例外が発生します。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ListBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ListBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。クリックは、修飾キーなしのクリックが ListBox に届くのと同じく、`ListBoxItem` にマウスの左ボタンのイベントを発生させて再現しました。Shift や Ctrl を押しながらのクリックは、ListBox が実際のキーボードの状態を読むため再現していません。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listbox/listbox-values.svg" alt="ListBox の選択の値を示す表。範囲外の SelectedIndex は無視され、-1 で選択が外れ、Equals を上書きしないクラスの新しいインスタンスでは何も選ばれないがレコード型では選ばれ、ItemsSource より前に設定した SelectedValue は後から反映され、存在しない DisplayMemberPath は空の文字列になり、ItemTemplate との併用は InvalidOperationException になる" width="1077" height="350" loading="lazy">
  <figcaption>SelectedIndex・SelectedItem・SelectedValue・DisplayMemberPath。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/listbox/listbox-scrolling.svg" alt="1,000 項目の ListBox のスクロールを示す表。コンテナーは 6 個作られ、コードから 500 番目を選んでも ScrollIntoView を呼ぶまでスクロールせず、縦のスクロールバーを Disabled にしてもキー操作ではスクロールする" width="1022" height="230" loading="lazy">
  <figcaption>高さ 100 の一覧に 1,000 項目を入れたときの仮想化とスクロール。位置の単位は項目。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール・記事

- [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/) — 仮想化と複数選択を扱います。
- [ListView](/ja/apps/wpf-standard-control-demo/listview.html) — ListBox を継承し、`GridView` の列などの表示方法を加えたコントロールです。
- [ComboBox](/ja/apps/wpf-standard-control-demo/combobox.html) — 一覧をドロップダウンで開く `Selector` です。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で ListBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ListBoxUsage){: target="_blank" rel="noopener noreferrer"}
