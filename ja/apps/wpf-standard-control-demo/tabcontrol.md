---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/tabcontrol.html
title: "TabControl"
badge: "Selectors"
lead: "TabControl は、1 度に 1 つのページを表示し、タブの並びでページを切り替えるコントロールです。各ページは TabItem です。"
description: "WPF の TabControl を .NET 10 で実測して解説。TabStripPlacement ごとのタブの位置、ContentStringFormat の適用のされ方、不正な選択、タブを取り除いたときの選択、キーボードでの操作を確かめます。"
---

## タブを置く辺と、ページに残る広さ

`TabStripPlacement` は、**TabControl** がタブを置く辺で、既定値は `Top` です。300 × 150 の TabControl では、`Top` と `Bottom` で 2 つのタブが横に並び、その下か上に幅 294 の内容の領域がありました。`Left` と `Right` ではタブが縦に積まれ、それぞれ幅約 37、高さ約 20 で、文字は横書きのままでした。内容の領域の幅は 255.34 に狭まりました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabcontrol/tabcontrol-placement.svg" alt="300 × 150 の TabControl のタブの見出しと内容の位置の表。Top と Bottom では 2 つの見出しが横に並び、その下か上に幅 294 の内容の領域があり、Left と Right では見出しが片側に縦に積まれ、内容の領域の幅は 255.34" width="945" height="200" loading="lazy">
  <figcaption><code>TabStripPlacement</code> ごとのタブの見出しと内容の領域。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## どのタブが選ばれ、何が無視されるか

TabControl は `Selector` を継承しています。作ったばかりの TabControl の `SelectedIndex` は表示されるまで -1 で、表示されると最初のタブが選ばれました。タブは `Items` の `TabItem` でも、開いている文書ごとに 1 つのタブを作るように `ItemsSource` からでも作れますが、両方は使えません。`Items` に TabItem があるときに `ItemsSource` を設定しても、`ItemsSource` があるときに `Items` に追加しても、`InvalidOperationException` になりました。

項目数以上のインデックスと一覧にない項目は、拒否されるのではなく無視されます。2 つのタブで `SelectedIndex = 5` としても例外はなく、0 のままでした。一覧にない TabItem を `SelectedItem` に設定しても、2 つ目のタブが選ばれたままでした。-1 未満は拒否され、`SelectedIndex = -2` では `ArgumentException` が発生しました。インデックスは設定する前に確かめます。`SelectedIndex = -1` ではどのページも表示されませんでした。選択中のタブを取り除くと次のタブが選ばれ、3 つのうち 2 つ目を取り除くと、3 つ目（インデックス 1）が選ばれました。

キーボードでは、フォーカスと一緒に選択も移ります。1 つ目のタブの見出しにフォーカスがある状態で右矢印キーを押すと、2 つ目のタブが選ばれ、その見出しにフォーカスが移りました。ページの内容がいつ作られ、どう保たれるかは、TabItem のページで実測しています。

## ContentStringFormat と、タブ自身の書式

`ContentStringFormat` はページの内容の書式です。`"Format: {0}."` では、Tab1 のページは「Format: Item1.」と表示されました。TabItem 自身の `ContentStringFormat` は、そのタブを最初から選んでいれば使われ、`"Own {0}"` の Tab2 は「Own Item2」と表示されました。ところが Tab1 から切り替えると適用されず、`SelectedContentStringFormat` はすでに `"Own {0}"` なのに、ページは「Format: Item2.」と表示されました。`ContentStringFormat` は TabItem ごとに変えず、TabControl に 1 つ設定するかテンプレートを使います。

読み取り専用の `SelectedContent` と `SelectedContentStringFormat` は、選択中のページの内容と書式です。最初のタブを選んでいるときは、書式を適用する前の `Item1` と `"Format: {0}."` でした。どのタブも選んでいないとき、`SelectedContent` は `null` でした。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabcontrol/tabcontrol-behavior.svg" alt="TabControl の計測結果の表。Selector を継承し既定値は Top だがデモアプリは Left で始まり、表示すると最初のタブが選ばれ、デモアプリの書式で Format: Item1. と表示され、タブ自身の書式は最初から選んだときは使われるが切り替えたときは使われず、項目数以上のインデックスと一覧にない SelectedItem は無視され -2 は ArgumentException になり、-1 では何も表示されず、選択中のタブを取り除くと次のタブが選ばれ、Items と ItemsSource を混ぜると例外になり、右矢印キーで次のタブが選ばれる" width="1077" height="440" loading="lazy">
  <figcaption>書式、選択、項目、キーボード。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの TabControl のページ。左にコントロールの一覧、右に最初の節の TabStripPlacement](/images/wpf-standard-control-demo/tabcontrol.png){: .screenshot-img}

デモアプリの TabControl のページには、`TabStripPlacement`、`ContentStringFormat`、`SelectedContent` と `SelectedContentStringFormat` の欄があります。`TabStripPlacement` のコンボボックスは、既定値の `Top` ではなく `Dock` の最初の値の `Left` で始まるので、XAML を写すなら明示します。`ContentStringFormat` の欄は `"Format: {0}."` を使い、最後の欄は `SelectedContent` と `SelectedContentStringFormat` を表示します。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`SelectedContent` の欄（`TabControlUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TabControl x:Name="SelectedContentTabControl" ContentStringFormat="Format: {0}.">
    <TabItem Content="Item1" Header="Tab1" />
    <TabItem Content="Item2" Header="Tab2" />
  </TabControl>

  <TextBlock x:Name="SelectedContentTextBlock"
             Text="{Binding SelectedContent, ElementName=SelectedContentTabControl}" />
  <TextBlock x:Name="SelectedContentStringFormatTextBlock"
             Text="{Binding SelectedContentStringFormat, ElementName=SelectedContentTabControl}" />
</StackPanel>
```

## 関連するコントロールと記事

- [TabItem](/ja/apps/wpf-standard-control-demo/tabitem.html) — 1 つのタブとそのページで、ページの内容の作られ方と使い回しも扱います。
- [Expander](/ja/apps/wpf-standard-control-demo/expander.html) — すべてを同時に開ける区切りです。
- [ListBox](/ja/apps/wpf-standard-control-demo/listbox.html) — ページではなく一覧のための、もう 1 つの Selector です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`TabControlDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TabControlDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じタブ（`Tab1` / `Item1`）で試しました。表示された文字はテンプレートの `PART_SelectedContentHost` から読み、矢印キーはキーボードから打ったときと同じく WPF の入力管理（InputManager）を通して送りました。大きさは、計測したマシンでの値です。

[GitHub で TabControl のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TabControlUsage){: target="_blank" rel="noopener noreferrer"}
