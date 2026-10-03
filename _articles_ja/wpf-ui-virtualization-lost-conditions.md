---
layout: article-ja
title: "WPF で UI 仮想化が効かなくなる条件と切り分け方"
date: 2026-10-03
category: WPF
excerpt: "WPF の一覧の UI 仮想化は、置き場所・パネル・スクロールの設定のどれかで、例外も出さずに外れる。1,000 件を高さ 200 の領域に表示し、実体化したコンテナーの数で条件を実測した。手元で原因を切り分ける手順と、原因ごとの直し方を示す。"
image: /images/articles/wpf-ui-virtualization-lost-conditions/virtualization-placement.svg
---

## 概要

`ListBox` は既定で UI 仮想化され、表示範囲とキャッシュに入る項目のコンテナー（`ListBoxItem`）だけを作る。
ところが置き方や設定によっては、1,000 件を表示すると 1,000 個すべてのコンテナーが作られる。
仮想化が外れても例外は出ない。
置き場所が原因のときや、`ItemsPanel` を替えたとき、`GroupStyle` を付けてグループ化したときは、`IsVirtualizing` も `True` のままであるため、気付きにくい。

仮想化が外れる主な原因は 3 系統ある。
置き場所が高さを制約していない、項目のパネルかテンプレートが仮想化に対応していない、仮想化または論理スクロールの設定が切られている、の 3 つで、直し方はそれぞれ違う。
本記事では、1,000 件を高さ 200 の領域に表示して実体化したコンテナーを数え、どの条件で仮想化が外れるかを実測した結果を示す。
あわせて、手元のアプリで原因を切り分ける手順と、原因ごとの直し方を示す。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.5 以降 / .NET Core 3.0 以降の WPF
- 対象: `ItemsControl` を継承するコントロール（`ListBox`・`ListView`・`DataGrid`・`TreeView`・`ComboBox`・`ItemsControl`）
- 関係する API: `VirtualizingStackPanel`、`VirtualizingPanel.IsVirtualizing`、`VirtualizingPanel.IsVirtualizingWhenGrouping`、`VirtualizingPanel.ScrollUnit`、`VirtualizingPanel.CacheLength`、`VirtualizingPanel.CacheLengthUnit`、`ScrollViewer.CanContentScroll`、`DataGrid.EnableRowVirtualization`、`ItemsControl.IsGrouping`
- 検証環境: .NET 10 / Windows 11（既定のテーマ）
- 計測方法: 1,000 件の項目を、幅 300・高さ 200 の領域に表示し、レイアウトが済むのを待ってから、`ItemContainerGenerator.ContainerFromItem` が返したコンテナーの数を数えた。この計測は `tools/screenshot-capture` のシーンとして実装している。

コンテナーの数は、項目の高さ（フォントと表示スケールで変わる）によって変わる。
本文では、仮想化が効いているかを「表示範囲とキャッシュの分だけか、1,000 個すべてか」で読む。

---

## 問題

`ListBox` を、上に検索欄を置くために縦の `StackPanel` へ入れた。
件数が少ないうちは、仮想化が外れていても違いは見えない。

```xml
<StackPanel>
  <TextBox Text="{Binding Keyword}" />
  <ListBox ItemsSource="{Binding Items}" DisplayMemberPath="Name" />
</StackPanel>
```

この `ListBox` は 1,000 件のすべてにコンテナーを作る（後掲の置き場所の表）。
しかし項目のパネルは `VirtualizingStackPanel` のままで、`VirtualizingPanel.IsVirtualizing` も `True` のままである。
仮想化の設定を見ても、外れている理由は分からない。

---

## 症状から絞り込めること

`VirtualizingStackPanel` は、コントロールのテンプレートの中で `ItemsPresenter` を包む `ScrollViewer` が論理スクロール（`CanContentScroll=True`）で動いているとき、表示範囲とキャッシュに入る項目だけのコンテナーを作る。
論理スクロールは、`ScrollViewer` ではなく項目のパネルがスクロールを受け持つ方式である。
`VirtualizingStackPanel` では、スクロールの単位は `VirtualizingPanel.ScrollUnit`（項目単位またはピクセル単位）で決まる。
`StackPanel` も論理スクロールするが、仮想化はしない。
キャッシュの大きさは `VirtualizingPanel.CacheLength` と `VirtualizingPanel.CacheLengthUnit` で決まる（[VirtualizingPanel.CacheLength](https://learn.microsoft.com/dotnet/api/system.windows.controls.virtualizingpanel.cachelength)）。
実測での大きさは、後掲の設定の表で示す。
パネルだけを `VirtualizingStackPanel` に替えた `ItemsControl` を外側の `ScrollViewer` に入れた場合、`CanContentScroll=True` を付けても、項目のパネルの論理スクロールにはならなかった（後掲の対処後の表）。
したがって、仮想化には 3 つの条件がそろう必要がある。
テンプレートの中の `ScrollViewer` の表示範囲が限られていること、項目のパネルが `VirtualizingStackPanel` であること、`IsVirtualizing` と `CanContentScroll` が `True` であることである。
どれが欠けているかは、次の値で読み分けられる。

| 読む値 | 原因の系統 |
|---|---|
| `ItemsControl` の高さ（`ActualHeight`）が表示領域より大きい | 置き場所が高さを制約していない |
| 項目のパネルが `VirtualizingStackPanel`（またはその派生）でない | 項目のパネルが仮想化に対応していない。既定で仮想化しないコントロール、`ItemsPanel` の差し替え、`GroupStyle` を付けたグループ化 |
| `CanContentScroll` が「-」（テンプレートの中に、項目のパネルを包む `ScrollViewer` が無い） | テンプレートが仮想化に対応していない。パネルだけを `VirtualizingStackPanel` に替えた `ItemsControl` |
| `IsVirtualizing` が `False`、または `CanContentScroll` が `False` | 仮想化または論理スクロールの設定が切られている |

`DataGrid` の項目のパネルは `DataGridRowsPresenter` と表示されるが、これは `VirtualizingStackPanel` の派生クラスである（[DataGridRowsPresenter](https://learn.microsoft.com/dotnet/api/system.windows.controls.primitives.datagridrowspresenter)）。
型名ではなく、`VirtualizingStackPanel` かどうかで判断する。
以下の表の「VirtualizingStackPanel か」の列も、派生クラスを `True` としている。

---

## 切り分けの手順

1. 一覧を表示し、レイアウトが済んだ後に、後述の実装例の `VirtualizationReport.Write` を呼ぶ。
2. 実体化したコンテナーの数を見る。件数より少なければ仮想化は効いており、ここで終わる。
3. 件数と同じなら、上の表と行の順に照らし合わせる。
4. 当てはまる行が複数あれば、そのすべてを直す。直したら 1 から測り直す。

原因は重なることがある。
既定の `ItemsControl` を外側の `ScrollViewer` に入れると、高さ・パネル・「-」の 3 行すべてに当てはまる（後掲の既定値の表）。
高さを制約する場所へ移すだけでは、全件のままである（既定値の表の、高さ 200 の `Grid` に直接置いた行）。

以下に、条件ごとの実測を示す。
まず、`ListBox` を置く場所だけを変えた結果である。

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-placement.ja.md %}

.NET 10 / Windows 11（既定のテーマ）で、1,000 件を持つ `ListBox` を幅 300・高さ 200 の領域の中で、置く場所だけを変えて表示した結果。レイアウトが済んだ後に `ItemContainerGenerator.ContainerFromItem` でコンテナーを数えた。「高さ」は `ListBox` の `ActualHeight`、`CanContentScroll` は `ListBox` のテンプレートの中の `ScrollViewer` の値である。
{: .table-caption}

縦の `StackPanel`・`ScrollViewer`・`Height=Auto` の `Grid` の行に置くと、`ListBox` の高さが全項目分まで伸び、1,000 個すべてを作った。
3 つとも、項目のパネル・`IsVirtualizing`・`CanContentScroll` は仮想化が効いている行と同じである。
この系統は高さを見なければ見分けられない。

`Expander` の中は、`Expander` 自体がどこに置かれているかで決まる。
`Grid` の中なら仮想化され、縦の `StackPanel` の中なら全件を作った。

次に、置き場所は `Grid` のままで、設定を変えた結果である（`DataGrid` の行だけはコントロールを替えた）。

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-settings.ja.md %}

.NET 10 / Windows 11（既定のテーマ）で、1,000 件を持つ `ListBox` を幅 300・高さ 200 の `Grid` に置き、設定を 1 つずつ変えて測った結果。`DataGrid` の行だけは、`ListBox` の代わりに `DataGrid` を置いた。「グループ化」の 2 行は、`ListCollectionView` の `GroupDescriptions` で 100 件ずつ 10 グループに分けている。項目のパネルはコントロールの最上位のパネル、`IsGrouping` はコントロールの `ItemsControl.IsGrouping` の値である。
{: .table-caption}

高さはどの行も 200 のままである。
`CanContentScroll=False` と `IsVirtualizing=False` は、項目のパネルを `VirtualizingStackPanel` のまま残して全件を作った。
`ItemsPanel` を `StackPanel` や `WrapPanel` に替えた場合も全件を作った。
`DataGrid` の `EnableRowVirtualization="False"` は、`IsVirtualizing` を `False` にし、1,000 行すべてを作った（[DataGrid.EnableRowVirtualization](https://learn.microsoft.com/dotnet/api/system.windows.controls.datagrid.enablerowvirtualization)）。

グループ化は、`GroupStyle` の有無で結果が分かれた。
`GroupStyle` を付けないと、`GroupDescriptions` があっても `IsGrouping` は `False` のままで、グループ化は働いていない。
仮想化が保たれたのは、グループ化していないのと同じ状態だからである。
`GroupStyle` を付けると `IsGrouping` が `True` になり、最上位のパネルが `StackPanel` に替わり、`CanContentScroll` が `False` になって、全件を作った。

`VirtualizingPanel.ScrollUnit=Pixel` は仮想化を保った。
`ScrollUnit=Pixel` でスクロールがピクセル単位になることは、[WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/) で実測している。
ピクセル単位でスクロールさせるために `CanContentScroll=False` を使う必要はない。

表の最後の 2 行は、キャッシュの設定だけを変えた結果である。
`CacheLength=0` にすると、既定の 11 個が 10 個になった。
既定の `ListBox` は、表示範囲の 10 個の先に 1 個を余分に作っていたことになる。
`CacheLengthUnit=Page` にすると 20 個になり、表示範囲 1 ページ分が先に作られた。

最後に、コントロールの既定値の違いである。

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-defaults.ja.md %}

.NET 10 / Windows 11（既定のテーマ）で、1,000 件を各コントロールで幅 300・高さ 200 の領域に表示した結果。`ComboBox` の 3 行は、ドロップダウンを開く前、開いた後、開いて閉じた後に数えた。`ComboBox` の「高さ」は閉じた本体の高さで、ドロップダウンの表示範囲はこの値では決まらない。`CanContentScroll` の「-」は、項目のパネルを包む `ScrollViewer` がコントロールのテンプレートの中に無いことを示す。開く前の `ComboBox` は項目のパネル自体が無いため、「項目のパネル」も「-」になり、「VirtualizingStackPanel か」は `False` になる。
{: .table-caption}

`ListBox`・`ListView`（`GridView`）・`DataGrid` は、既定で仮想化された。
`ItemsControl` の項目のパネルは `StackPanel` で、テンプレートに `ScrollViewer` も無い。
外側の `ScrollViewer` に入れても、全件を作った。
`ItemsControl` の高さが全項目分まで伸びており、外側の `ScrollViewer` は、伸びた `ItemsControl` 全体をスクロールしているだけである。
高さ 200 の `Grid` に直接置くと高さは 200 に収まったが、それでも全件を作った。
ルートに 1,000 個のノードを並べた `TreeView` は、`IsVirtualizing` も `CanContentScroll` も `False` で、項目のパネルは `StackPanel` だった。
`ComboBox` は、開く前はコンテナーも項目のパネルも作らず、ドロップダウンを開くと全件のコンテナーを作り、閉じた後も 1,000 個を持ち続けた。

`ItemsControl` の行の `IsVirtualizing` は `True` である。
`IsVirtualizing` は、仮想化できるパネルへの指示である。
`ItemsControl` のようにパネルが `StackPanel` のままなら何も起きない。
`IsVirtualizing` だけを見て「仮想化されている」と判断しない。

---

## 原因別の対処

### 置き場所が高さを制約していない

`ListBox` を、高さが決まる場所へ移す。
冒頭の検索欄の例なら、`Grid` の `Height="*"` の行へ置く。

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
  </Grid.RowDefinitions>
  <TextBox Grid.Row="0" Text="{Binding Keyword}" />
  <ListBox Grid.Row="1" ItemsSource="{Binding Items}" DisplayMemberPath="Name" />
</Grid>
```

`ListBox` の行は `*` なので、`Grid` に残った高さが `ListBox` の高さになる。
`DockPanel` で、`TextBox` を上に寄せて `ListBox` を最後の子（`LastChildFill`）にしても同じ結果になった（後掲の対処後の表）。
レイアウトを変えられない場合は、`ListBox` に `MaxHeight` を指定すれば、縦の `StackPanel` の中でも仮想化される。
ただし、`ListBox` は `MaxHeight` より高くはならない。

`StackPanel` が子の高さを制約しない理由と、`ScrollViewer` がスクロールしなくなる同じ現象は、[WPF で ScrollViewer がスクロールしない原因と解決方法](/ja/articles/wpf-scrollviewer-not-scrolling/) で扱っている。

### 項目のパネルかテンプレートが仮想化に対応していない

`TreeView` は、`VirtualizingPanel.IsVirtualizing="True"` を指定する。

```xml
<TreeView ItemsSource="{Binding Roots}"
          VirtualizingPanel.IsVirtualizing="True" />
```

この指定だけで、項目のパネルが `StackPanel` から `VirtualizingStackPanel` に替わり、`CanContentScroll` も `True` になって、コンテナーは 1,000 個から 25 個になった（後掲の対処後の表）。
`ListBox` より多いのは、主にキャッシュが大きいためである。
`VirtualizingPanel.CacheLength="0"` を加えると 13 個になり、表示範囲とほぼ同じ数のノードがキャッシュとして作られていた。
測ったのはルートにノードを並べた場合で、展開した下の階層のノードは数えていない。
仮想化した `TreeView` では、画面外のノードにはコンテナーが無い。
画面外のノードをコードから選択する方法は、[WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/) で扱っている。

`ItemsControl` は、項目のパネルを替え、さらにテンプレートの中で `ItemsPresenter` を、論理スクロールする `ScrollViewer` で包む。
`ItemsControl` で、手順 3 の `CanContentScroll` が「-」と出たときも、この対処になる。

```xml
<ItemsControl ItemsSource="{Binding Items}">
  <ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel />
    </ItemsPanelTemplate>
  </ItemsControl.ItemsPanel>
  <ItemsControl.Template>
    <ControlTemplate TargetType="ItemsControl">
      <ScrollViewer CanContentScroll="True" Focusable="False">
        <ItemsPresenter />
      </ScrollViewer>
    </ControlTemplate>
  </ItemsControl.Template>
</ItemsControl>
```

この `ItemsControl` のコンテナーは、14 個になった（既定の `ItemsControl` を外側の `ScrollViewer` に入れた場合は 1,000 個）。
パネルだけを `VirtualizingStackPanel` に替えた場合は、`CanContentScroll=True` を付けた外側の `ScrollViewer` に入れても、高さ 200 の `Grid` に直接置いても、全件を作った（対処後の表）。
どちらも、パネルを包む `ScrollViewer` がテンプレートの中に無い（`CanContentScroll` が「-」）。
選択が要らない一覧でも、件数が多いなら `ListBox` を使うほうが記述は少ない。

`ComboBox` は、`ItemsPanel` を `VirtualizingStackPanel` に替える。

```xml
<ComboBox ItemsSource="{Binding Items}" DisplayMemberPath="Name">
  <ComboBox.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel />
    </ItemsPanelTemplate>
  </ComboBox.ItemsPanel>
</ComboBox>
```

ドロップダウンを開いたときのコンテナーは、1,000 個から 19 個になった（対処後の表）。

グループ化で `GroupStyle` を付けた場合は、`VirtualizingPanel.IsVirtualizingWhenGrouping="True"` を指定する。
`GroupedItems` は、`GroupDescriptions` を付けた `ListCollectionView`（または `CollectionViewSource` のビュー）である。

```xml
<ListBox ItemsSource="{Binding GroupedItems}"
         VirtualizingPanel.IsVirtualizingWhenGrouping="True">
  <ListBox.GroupStyle>
    <GroupStyle />
  </ListBox.GroupStyle>
</ListBox>
```

`GroupStyle` を付けただけでは、グループ化と引き換えに仮想化が外れる（前掲の設定の表）。
この指定で、コンテナーは 1,000 個から 19 個になった（対処後の表）。

`ItemsPanel` を `StackPanel` や `WrapPanel` に替えた `ListBox` は、仮想化と引き換えになる。
件数が多いなら、`ItemsPanel` を替えずに済む表示方法を選ぶ。
仮想化に対応したパネルを自作する方法は、本記事では確かめていない。

### 仮想化または論理スクロールの設定が切られている

`ScrollViewer.CanContentScroll="False"` と `VirtualizingPanel.IsVirtualizing="False"` は、外せば既定の `ListBox` と同じく仮想化される。
`DataGrid` の `EnableRowVirtualization="False"` も同じで、外せば既定の `DataGrid` と同じく仮想化される。
`CanContentScroll="False"` を、ピクセル単位でスクロールさせる目的で書いている場合は、代わりに `VirtualizingPanel.ScrollUnit="Pixel"` を使う。

### 対処後の実測

次の表は、本章の対処を当てて測り直した結果である。

{% include tables/articles/wpf-ui-virtualization-lost-conditions/virtualization-fixes.ja.md %}

.NET 10 / Windows 11（既定のテーマ）で、1,000 件を幅 300・高さ 200 の領域に表示して測った結果。`TreeView`・`ItemsControl`・`ComboBox`・検索欄の行は XAML を読み込んで測った。そのうち、`TreeView` の `CacheLength=0` の行、`ItemsControl` のパネルだけを替えた 2 行、`DockPanel` の検索欄の行は、本文で述べた構成を XAML にしたもので、本文には XAML を載せていない。グループ化と `MaxHeight` の行はコードで同じ値を設定した。`ComboBox` はドロップダウンを開いた後に数えた。
{: .table-caption}

---

## 実装例

切り分けに使ったコードを、手元のアプリへ組み込める形で示す。
画面が表示され、レイアウトが済んだ後（ボタンのクリックなど）に呼び、デバッグ出力へ書き出す。

```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public static class VirtualizationReport
{
    public static void Write(ItemsControl items)
    {
        // 実体化したコンテナーの数。グループ化していても項目のコンテナーだけを数えられる。
        int realized = items.Items.Cast<object>()
            .Count(item => items.ItemContainerGenerator.ContainerFromItem(item) != null);

        // ComboBox の項目はポップアップの中にあり、ComboBox の visual ツリーの外にある。
        DependencyObject root = items;
        if (items is ComboBox combo && combo.Template?.FindName("PART_Popup", combo) is Popup popup && popup.Child != null)
        {
            root = popup.Child;
        }

        var host = Descendants(root).OfType<Panel>()
            .FirstOrDefault(p => p.IsItemsHost && ItemsControl.GetItemsOwner(p) == items);

        // 項目のパネルを包む ScrollViewer の値。コントロールに付けた添付プロパティの値とは限らない。
        var viewer = host == null ? null : Ancestors(host).TakeWhile(d => d != items).OfType<ScrollViewer>().FirstOrDefault();

        Debug.WriteLine(
            $"{items.Name}: {realized.ToString("#,0", CultureInfo.InvariantCulture)}/{items.Items.Count.ToString("#,0", CultureInfo.InvariantCulture)} realized, " +
            $"panel {host?.GetType().Name ?? "-"} (VirtualizingStackPanel: {host is VirtualizingStackPanel}), " +
            $"IsVirtualizing {VirtualizingPanel.GetIsVirtualizing(items)}, " +
            $"CanContentScroll {(viewer == null ? "-" : viewer.CanContentScroll.ToString())}, " +
            $"IsGrouping {items.IsGrouping}, " +
            $"height {items.ActualHeight.ToString("#,0.##", CultureInfo.InvariantCulture)}");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject node)
    {
        for (DependencyObject current = VisualTreeHelper.GetParent(node); current != null; current = VisualTreeHelper.GetParent(current))
        {
            yield return current;
        }
    }
}
```

`host is VirtualizingStackPanel` は派生クラスでも `True` になるため、`DataGrid` の `DataGridRowsPresenter` も正しく判定できる。
`TreeView` では `Items` に最上位のノードしか入っていないため、数えるのは最上位のノードのコンテナーである。

`ComboBox` は、ドロップダウンを開いてから呼ぶ。
開く前は項目のパネルもコンテナーも作られておらず、「0/件数」と出る（既定値の表の「ドロップダウンを開く前」の行）。
件数より少ないので、手順 2 では仮想化が効いているように見えてしまう。

呼び出しは 1 行で済む。
`OrdersList` は、`x:Name` を付けた `ListBox` である。

```csharp
VirtualizationReport.Write(OrdersList);
```

出力の読み方は「症状から絞り込めること」の表のとおりである。

---

## 注意点

- **件数が少ないうちは気付きにくい。** 仮想化が外れても、コンテナーの数は件数と同じになるだけで、例外は出ない。開発中のサンプルデータが少ないと、件数の多いデータで初めて表に出る。件数を増やした状態で `VirtualizationReport` を一度呼んでおくと、早く気付ける。
- **`IsVirtualizing` が `True` でも仮想化されているとは限らない。** パネルが `VirtualizingStackPanel` でないか、テンプレートの中で論理スクロールする `ScrollViewer` に包まれていなければ働かない。判断はコンテナーの数で行う。
- **`ComboBox` は、ドロップダウンを一度開くと全件のコンテナーを持ち続ける。** 既定の項目のパネルが `StackPanel` のためである。候補が多いなら、`ItemsPanel` を替えるか、候補を絞り込める入力欄に置き換える。
- **仮想化を有効にすると、画面外の項目にはコンテナーが無い。** コンテナーの状態（`IsSelected` など）に頼る処理は、データ側に状態を持たせる。`ListBox` の選択については [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/) で扱っている。
- **コンテナーの数は環境で変わる。** 表の 10〜25 個という値は、項目の高さで決まる表示範囲と、キャッシュの大きさによるもので、フォントや表示スケールでも変わる。`ListBox` と `TreeView` では、キャッシュを外すと 10 個と 13 個になり、残りがキャッシュの分であることを確かめた。

---

## まとめ

仮想化が外れたときは、まず実体化したコンテナーの数を数え、次に高さ・パネル・`CanContentScroll` の「-」・`IsVirtualizing` と `CanContentScroll` の値の順に読む。
当てはまるものが複数あれば、すべてを直す。

- **高さが表示領域より大きい**ときは、置き場所が原因である。`Grid` の `*` の行や `DockPanel` の最後の子へ移す。移せない場合は `MaxHeight` を指定する。
- **パネルが `VirtualizingStackPanel`（またはその派生）でない**ときは、項目のパネルが原因である。`TreeView` には `IsVirtualizing="True"`、`ItemsControl` にはパネルとテンプレートの差し替え、`ComboBox` には `ItemsPanel` の差し替え、`GroupStyle` 付きのグループ化には `IsVirtualizingWhenGrouping="True"` を当てる。
- **`CanContentScroll` が「-」**のときは、テンプレートが原因で、論理スクロールする `ScrollViewer` が無い。`ItemsControl` なら、パネルとあわせてテンプレートを差し替える。
- **`IsVirtualizing` か `CanContentScroll` が `False`** なら、その設定を外す。`DataGrid` なら `EnableRowVirtualization="False"` も探す。ピクセル単位のスクロールが目的なら `ScrollUnit="Pixel"` を使う。

`ListBox` を `StackPanel` に入れる書き方は、件数が少ない画面では違いが見えないため、気付かれないまま残りやすい。
件数が増えうる一覧は、置き場所を決めた時点でコンテナーの数を一度確かめておくのが確実である。

---

## 関連記事

- [WPF で ScrollViewer がスクロールしない原因と解決方法](/ja/articles/wpf-scrollviewer-not-scrolling/)
- [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/)
- [WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/)
- [WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/)
