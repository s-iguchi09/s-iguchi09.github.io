---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/treeview.html
title: "TreeView"
badge: "List"
lead: "TreeView は階層的なデータを、展開・折りたたみのできるノードとして表示するコントロールです。ノードは入れ子にした <code>TreeViewItem</code> か、データと <code>HierarchicalDataTemplate</code> から作ります。"
description: "WPF の TreeView を .NET 10 で実測して解説します。既定では仮想化されないこと、IsExpanded は既定で TwoWay にならず展開ボタンでバインドが外れること、HierarchicalDataTemplate とキー操作の挙動を確かめます。"
---

## 既定では仮想化されない

**TreeView** は `ItemsControl` を継承しています。各ノードは `HeaderedItemsControl` を継承した `TreeViewItem` で、自分の子ノードを持ちます。ListBox と違い、TreeView は既定では仮想化されません。高さ 100 の TreeView に最上位のノードを 1,000 個入れると、`VirtualizingPanel.IsVirtualizing` は `False` で、項目は `StackPanel` に並び、`TreeViewItem` が 1,000 個すべて作られました。`VirtualizingPanel.IsVirtualizing="True"` にすると、パネルは `VirtualizingStackPanel` になり、作られたのは 12 個でした。大きなツリーでは有効にします。

## IsExpanded のバインドは TwoWay でないと外れる

TreeViewItem の `IsExpanded` は、ノードが子を表示しているかどうかです。`IsSelected` と違い、既定では TwoWay にバインドされません。チェックボックスを `Mode` を指定せずに Node 1 の `IsExpanded` にバインドした状態で、Node 1 をその展開ボタンで折りたたむと、このバインドが外れました。ノードは折りたたまれましたが、チェックボックスはオンのままで、その後チェックを外して付け直してもノードは展開されませんでした。このようなバインドには `Mode=TwoWay` を指定します。

`ItemContainerStyle` で TwoWay にバインドすると、状態はデータ側に保たれます。折りたたまれた親の下で、ソースの `IsExpanded` を `true` にした子は、まだコンテナーがありませんでしたが、親を展開した時点で展開されました。展開の状態はデータに持たせます。

展開したときに子を読み込むフォルダーのツリーでは、仮の子を置いて `Expanded` のハンドラーで入れ替える方法が使えました。展開ボタンを実際にクリックすると、本来の 2 つの子が表示されました。

## 選択は SelectedItem ではなく IsSelected で行う

選択できるノードは一度に 1 つです。`SelectedItem` と `SelectedValue` は読み取り専用で、`SelectedItem` にバインドを設定しようとすると `ArgumentException` が発生しました。選択は、各ノードの `IsSelected` で変えます。詳しくは[WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/)で扱っています。`SelectedItem` は、XAML に書いたノードでは `TreeViewItem` を、`ItemsSource` から作ったノードではデータのオブジェクトを返しました。

`IsSelected` は既定で TwoWay にバインドされます。ノードを選ぶと前のノードの選択が外れ、そのソースに `False` が書き戻されます。`ItemContainerStyle` で `IsSelected` をバインドし、データで Desktop、Mobile の順に `True` にすると、Mobile が選ばれ、Desktop のソースは `False` になりました。

読み取り専用の `IsSelectionActive`（添付プロパティ `Selector.IsSelectionActive`）は、選択がキーボードフォーカスを持っているかどうかを表します。フォーカスのない状態でコードから Node 2 を選ぶと `IsSelectionActive` は `False`、フォーカスを得ると `True` でした。フォーカスが TextBox へ移ると、`IsSelected` は `True` のまま、`IsSelectionActive` は `False` に戻りました。

`SelectedValuePath` には、`SelectedValue` として返す選択中のノードのプロパティを指定します。`Header` で "Gaming PC" を選ぶと、`SelectedValue` は "Gaming PC" になりました。

## HierarchicalDataTemplate と、ノードのデータが届く先

`HierarchicalDataTemplate` は、ノードの見た目と、`ItemsSource` で子ノードの取得先を表します。TreeView の `ItemTemplate` に指定すると、すべての階層に適用され、2 階層目のノードもテンプレートを通して "Workstation PC" と表示されました。リソースに置いた暗黙のテンプレートとして使う場合は、`DataType` の型の項目にだけ適用されます。`DataType` が合わないとき、ノードは空にはならず、項目の `ToString()` が表示され、展開もできませんでした。

`ItemContainerStyle` に設定した `ContextMenu` は、開く前は `DataContext` が空でしたが、開いている間はノードのデータオブジェクトが `DataContext` になりました。

## キー操作

ノード A（子を持ち、最初の子にもさらに子がある）を選び、フォーカスを置いた状態で試すと、下矢印キーで次のノードが選ばれました。右矢印キーで A が展開され、左矢印キーで再び折りたたまれました。テンキーの `*` で、A とその下のすべてのノードが展開されました。Space キーと Enter キーでは、選択も展開も変わりませんでした。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/treeview/treeview-structure.svg" alt="TreeView の計測結果の表。デモアプリの IsExpanded のバインドは展開ボタンで折りたたむと外れ、データで展開した子は親を開くと展開され、HierarchicalDataTemplate はすべての階層に適用され、DataType が合わないと ToString が表示され、既定では仮想化されず、矢印キーとテンキーの * で展開・折りたたみが変わり Space と Enter では何も起きず、ContextMenu は開いている間ノードを DataContext にする。Expanded のハンドラーで仮の子を入れ替えると展開ボタンの実際のクリックで本来の子が表示される" width="1210" height="590" loading="lazy">
  <figcaption>展開、テンプレート、仮想化、キー操作、コンテキストメニュー。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの TreeView のページ。左にコントロールの一覧、右に最初の節の IsExpanded(TreeViewItem)](/images/wpf-standard-control-demo/treeview.png){: .screenshot-img}

デモアプリの TreeView のページには、`IsExpanded`、`IsSelected` と `IsSelectionActive`、`SelectedValuePath` と `SelectedValue`、`ItemTemplate` と `HierarchicalDataTemplate`、スクロールバーの欄があります。`IsExpanded` の欄は、チェックボックスを `Mode` を指定せずに Node 1 の `IsExpanded` にバインドしているので、上で述べたようにバインドが外れます。`SelectedValuePath` の欄は、XAML に書いたノードに対してテキストボックスでパスを入力します。スクロールバーの欄には、縦横どちらのスクロールバーも試せるよう、長い最初のノードに 5 つの子を置いています。既定のスタイルは `ScrollViewer.HorizontalScrollBarVisibility` と `ScrollViewer.VerticalScrollBarVisibility` をどちらも `Auto` に設定しています。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`ItemTemplate` / `HierarchicalDataTemplate` の欄（`TreeViewUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。デモアプリでは GroupBox のリソースにあるテンプレートを、ここでは StackPanel のリソースに置いています。`DeviceTree` は、`Name` と `Children` を持つ項目のコレクションです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <StackPanel.Resources>
    <HierarchicalDataTemplate x:Key="MyTreeViewTemplate" x:Name="MyTreeViewTemplate"
                              ItemsSource="{Binding Children}">
      <StackPanel Orientation="Horizontal">
        <Path Margin="0,0,5,0" VerticalAlignment="Center"
              Data="M0,0 L8,4 L0,8 Z" Fill="Orange" />
        <TextBlock VerticalAlignment="Center" Text="{Binding Name}" />
      </StackPanel>
    </HierarchicalDataTemplate>
  </StackPanel.Resources>

  <TreeView x:Name="ItemTemplateTreeView" Height="150"
            ItemTemplate="{StaticResource MyTreeViewTemplate}"
            ItemsSource="{Binding DeviceTree}" />
</StackPanel>
```

## 関連するコントロールと記事

- [WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/) — コードからの選択と、コンテナーが作られるタイミングを扱います。
- [ListBox](/ja/apps/wpf-standard-control-demo/listbox.html) — 階層のない一覧で、既定で仮想化されます。
- [Expander](/ja/apps/wpf-standard-control-demo/expander.html) — 1 つだけの折りたたみ可能な領域です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`TreeViewDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TreeViewDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。キー操作は表示したウィンドウへキーのイベントを送り、展開ボタンは、クリックと同じ切り替え処理を実行する UI オートメーションで切り替えて再現しました。仮の子を持つノードの展開ボタンだけは、実際のマウスでクリックしました。

[GitHub で TreeView のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TreeViewUsage){: target="_blank" rel="noopener noreferrer"}
