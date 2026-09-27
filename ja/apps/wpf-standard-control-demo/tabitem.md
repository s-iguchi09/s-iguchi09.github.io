---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/tabitem.html
title: "TabItem"
badge: "Layout"
lead: "TabItem は TabControl の 1 ページです。タブの帯に表示するヘッダーと、タブを選んでいる間に表示する内容からなります。"
description: "WPF の TabItem の選択、TabStripPlacement、無効なタブ、ヘッダーのアクセスキーを .NET 10 で実測して解説します。表示していないタブの内容がいつ読み込まれるか、ItemsSource で作ったタブの内容が使い回されることも確かめています。"
---

## どのタブが選ばれたまま残るか

**TabItem** は `HeaderedContentControl` を継承しており、タブの帯に出す `Header` と、ページ本体の `Content` を持ちます。親の `TabControl` で同時に選択できるタブは最大 1 つです。ある TabItem の `IsSelected` を `True` にすると、`SelectedIndex` がそのタブに移り、他のタブの `IsSelected` は外れます。3 つのうち 3 つ目のタブに `IsSelected="True"` を設定すると、`SelectedIndex` は 2 になり、他のタブの選択は外れました。

`IsSelected` は既定で TwoWay にバインドされます。`SelectedIndex` を 1 に変えると、バインドした 1 つ目のソースは `False`、2 つ目は `True` になりました。実際のマウスで 2 つ目のタブの見出しをクリックしても同じでした。

選択中にできるのは 1 つだけで、どれが残るかは値の届き方で変わります。コードから 2 つのタブを順に `True` にすると、後に設定したタブが選ばれました。XAML で 2 つのタブに `IsSelected="True"` を書くと、先に書いたタブが選ばれました。選択は 1 か所で管理し、すべてのタブの `IsSelected` ではなく、TabControl の `SelectedIndex` か `SelectedItem` をバインドします。

## 無効なタブもコードからは選べる

`IsEnabled="False"` のタブは、利用者の操作では選べません。支援技術が使う UI オートメーション（`ISelectionItemProvider.Select`）で選ぼうとすると `ElementNotEnabledException` が発生し、選択は動きませんでした。実際のマウスで見出しをクリックしても選ばれませんでした。

一方、コードからは選べます。`SelectedIndex = 1` で無効なタブが選ばれ、その内容が表示されました。前の手順が終わるまで後のタブを無効にしておく場合は、選択を変えるコードの側でも、開かせたくないタブを選ばないようにします。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabitem/tabitem-selection.svg" alt="TabItem の選択に関する計測結果の表。基底クラスは HeaderedContentControl、IsSelected は既定で TwoWay、TabStripPlacement は TabControl に従い、XAML で IsSelected を書いた 2 つのうち先のタブと、コードから順に設定した 2 つのうち後のタブが選ばれ、バインドしたソースは SelectedIndex の変更にも実際のマウスでのタブのクリックにも追従し、無効なタブはコードからは選べるが UI オートメーションの ISelectionItemProvider.Select でも実際のマウスのクリックでも選べない" width="1030" height="470" loading="lazy">
  <figcaption>選択、メタデータ、無効なタブ。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 内容が作られる時点と、使い回される場合

表示されてレイアウトされるのは選択中のタブの内容だけですが、他のタブの内容が何もしていないわけではありません。XAML に直接書いた内容は、XAML の読み込み時に作られます。3 つのタブで試すと、ウィンドウを開いた時点で 3 つの内容すべてに `Loaded` が発生し、測られた（Measure された）のは選択中の 1 つだけでした。タブ 1 からタブ 2 へ切り替えて戻すと、戻ってきた内容にもう一度 `Loaded` が発生し、同じインスタンスが再び表示されました。重い処理を遅らせたい場合は、内容のコンストラクターや `Loaded` ではなく、そのタブが初めて選ばれたときに始めます。

`ItemsSource` と `ContentTemplate` で作った場合は、内容の要素は 1 つだけ作られ、どのタブでもデータだけを入れ替えて使い回されました。そのため、スクロール位置やバインドしていないテキストボックスの文字など、項目にバインドしていない状態は、タブを切り替えても引き継がれます。

生成したタブのヘッダーは `ItemTemplate` で決めます。TabControl には `HeaderTemplate` プロパティがありません。TabControl に設定するテンプレートは、`ItemTemplate`（ヘッダー）、`ContentTemplate`（ページ）、`Template` です。`SelectedContentTemplate` は、選択中のタブで使われているテンプレートを返す読み取り専用のプロパティです。`HeaderTemplate` は TabItem 側のプロパティです。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/tabitem/tabitem-content-lifetime.svg" alt="タブの内容の寿命を示す表。要素として書いた内容は起動時に 3 つとも Loaded が発生するが測られるのは選択中の 1 つだけで、ItemsSource と ContentTemplate では内容の要素が 1 つだけ作られてすべてのタブで使い回される" width="1128" height="170" loading="lazy">
  <figcaption>3 つのタブを 1 &rarr; 2 &rarr; 1 と切り替えたときの、内容が読み込まれるタイミングと使い回し。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## ヘッダー、タブの帯の位置、テンプレート

文字列のヘッダーは `AccessText` で表示されるため、アンダースコアはアクセスキーの指定になります。`Header="File_Name"` では `N` がアクセスキーになり、アンダースコア自体は表示されません。[WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue/)で、TabItem のヘッダーでも消えることを確かめています。同じ記事では、同じく `AccessText` を使う Label で `__` と書くとアンダースコアが 1 つ表示されることも確かめています。タブの幅はヘッダーに合わせて変わり、計測した環境では「File\_Name」で 65.65、「A」で 19.74 でした（幅はフォントと表示スケールによって変わります）。

TabItem の `TabStripPlacement` は読み取り専用で、親の TabControl に従います。`TabControl.TabStripPlacement="Left"` では、2 つの TabItem のどちらも `Left` になりました。設定するのは TabControl 側です。TabItem 側の値は、タブの帯がどの辺にあるかを知りたいテンプレートやトリガーで使います。

既定の TabItem のテンプレートには表示状態のグループ（VisualStateGroup）がなく、16 個のトリガーで見た目を切り替えています。既定のテンプレートをコピーして直す場合は、このトリガーを編集します。

## デモアプリで試す

![デモアプリの TabItem のページ。左にコントロールの一覧、右に最初の節の IsSelected](/images/wpf-standard-control-demo/tabitem.png){: .screenshot-img}

デモアプリの TabItem のページには 2 つの欄があります。`IsSelected` の欄では、各タブの `IsSelected` にチェックボックスを `Mode` を指定せずにバインドしているので、タブをクリックするとチェックボックスも変わります。`TabStripPlacement` の欄では、コンボボックスでタブの帯の位置を変え、各 TabItem の読み取り専用の `TabStripPlacement` を表示します。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`IsSelected` の欄（`TabItemUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsSelectedTab1" VerticalContentAlignment="Center" Content="Tab1" />
  <CheckBox x:Name="IsSelectedTab2" VerticalContentAlignment="Center" Content="Tab2" />

  <TabControl x:Name="IsSelectedTabControl">
    <TabItem Content="Item1" Header="Tab1"
             IsSelected="{Binding IsChecked, ElementName=IsSelectedTab1}" />
    <TabItem Content="Item2" Header="Tab2"
             IsSelected="{Binding IsChecked, ElementName=IsSelectedTab2}" />
  </TabControl>
</StackPanel>
```

`IsSelected` は既定で TwoWay にバインドされるので、チェックボックスはタブを操作するだけでなく、タブの状態にも追従します。Tab2 を選んでいる状態で Tab1 のソースを `True` にする（チェックボックスをオンにする操作に当たる）と、Tab1 が選ばれ、Tab2 のソースは `False` になりました。

## 関連するコントロールと記事

- [TabControl](/ja/apps/wpf-standard-control-demo/tabcontrol.html) — 同時に 1 つの TabItem を選択し、その内容を表示する親コントロールです。
- [WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue/) — TabItem のヘッダーにも当てはまるアクセスキーの挙動を扱います。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`TabItemDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TabItemDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。タブの見出しのクリックは、計測用のウィンドウの上で実際のマウスを使って行いました。

[GitHub で TabItem のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TabItemUsage){: target="_blank" rel="noopener noreferrer"}
