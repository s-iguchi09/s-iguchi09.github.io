---
layout: article-ja
title: "WPFのDataGridのソートを初期化する方法"
date: 2026-06-29
category: WPF
excerpt: "WPF DataGrid のソート状態は SortDescriptions と SortDirection の 2 か所にあり、片方だけ戻すと食い違う。明示クリア・Sorting イベント・ICollectionView・Behavior の 4 つの初期化方法を比較する。"
image: /images/articles/wpf-datagrid-sort-reset/datagrid-sort-three-states.png
---

WPF の `DataGrid` はソート機能を標準で持つが、要件によっては「初期状態に戻す（ソートを解除する）」動作の明示的な実装が求められる場合がある。
本記事では、`DataGrid` のソート初期化を実現する代表的な方法を整理する。

## 概要

本記事では、以下の方法で `DataGrid` のソート初期化を扱う。

- コードで明示的にソートをクリアする方法
- `Sorting` イベントで未ソート状態を制御する方法
- `CollectionView` を使って ViewModel から初期化する方法
- Behavior 化して再利用する方法

## 前提・対象環境

- フレームワーク: WPF `DataGrid`
- 対応バージョン: .NET Framework 4.8 / .NET 6 以降
- 言語: C# 9 以降
- アーキテクチャ: MVVM / コードビハインド
- 対象要件: 単一列ソート / 複数列ソート
- 検証環境: .NET 10 / Windows 11

本記事の表は、上記の環境で実際に計測したものである。
計測の条件は、それぞれの表の説明に書いた。
この環境で確認しているのは次の点である。

- 並び替えの状態は、`ICollectionView` の `SortDescriptions` と列の `SortDirection` に分かれて保持される。
- `SortDescriptions` を消しただけでは列の `SortDirection` が残り、ヘッダーの矢印が消えない。
- `SortDescriptions` を足しただけでは、列の `SortDirection` は付かない。
- `SortDescriptions` を 2 つ足すと、複数列ソートになる。
  1 つ目の条件で同点の行は、2 つ目の条件で並ぶ。
- 複数列ソート中に、Shift を押さずに降順の列をもう一度クリックすると、他の列の矢印は消える。
  `Sorting` イベントで対象列の条件だけを外すと、他の列の条件は矢印の無いまま残る。

## 問題

WPF `DataGrid` では、業務要件として「現在の並び替え状態を初期状態へ戻す」操作が必要になる場合がある。
このとき、標準操作だけでは意図したタイミングでの初期化を統一しづらい場合がある。

## 原因・背景

WPF `DataGrid` で `Shift + 列ヘッダークリック` は、標準では**複数列ソートの追加**である。
既存ソートに列を積み増すための操作であり、ソート解除のショートカットではない。

- 単独クリック: その列の昇順/降順を切り替え
- Shift+クリック: 複数列ソートとして追加

そのため、「初期状態に戻す」にはコードによる制御が必要である。

並び替えの状態は 2 か所に分かれている。
操作ごとに両方を測った結果が次の表である。

{% include tables/articles/wpf-datagrid-sort-reset/datagrid-sort-state.ja.md %}

.NET 10 / Windows 11 での実測結果。
<code>SortDescriptions</code> はビュー側の並び替え条件の件数、<code>column.SortDirection</code> は列ヘッダーの矢印を決めるプロパティである。
最終行以外はコードから直接操作した場合である。
「同点あり」の 2 行は、carol と同じ <code>Score</code> の行（anna）を足している。
{: .table-caption}

**`SortDescriptions.Clear()` を呼んだ行を見ると、並び順は初期状態に戻っているのに `column.SortDirection` は `Ascending` のまま残っている。**
この状態ではヘッダーに矢印が表示されたままになり、並び替えが効いているように見える。

逆に `SortDescriptions` を足しただけの行では、並び順が変わっているのに `SortDirection` は `null` のままである。
**コードから一方を操作しても、もう一方は追随しない。**
どちらの向きにも、両方を明示的に設定する必要がある。

「同点あり」の 2 行が複数列ソートを示している。
`Score` だけでは同点の carol と anna が元の順のままで、`Name` を足すと anna が前になる。
Shift + クリックはこの状態を作る操作であり、解除ではない。

最終行はその対照で、列ヘッダーのクリックで走る標準の並び替えである。
**この経路では `SortDescriptions` と `SortDirection` が同時に更新される。**
ユーザーが並び替えたときに矢印と並び順が食い違わないのはこのためであり、食い違いが生じるのはコードから一方だけを触ったときである。

---

## 4 つの初期化方法

要件に応じて、以下の方針で初期化方法を選択する。

- 明示的にソート条件と表示状態をクリアする
- `Sorting` イベントを処理して列単位の未ソート状態を制御する
- MVVM 構成では `ICollectionView` 側でソート状態を管理する
- 共通化が必要な場合は Behavior として再利用する

### コードで明示的にソートをクリアする

最も単純なのは、`SortDescriptions` と列ヘッダーの矢印の両方をクリアする方法である。

```csharp
using System.Windows.Controls;

public static class DataGridSortHelper
{
    public static void ClearDataGridSort(DataGrid dataGrid)
    {
        if (dataGrid == null) return;

        // データ側のソート条件をクリア
        dataGrid.Items.SortDescriptions.Clear();

        // ヘッダー矢印をクリア
        foreach (var column in dataGrid.Columns)
        {
            column.SortDirection = null;
        }

        // 表示更新
        dataGrid.Items.Refresh();
    }
}
```

#### ポイント

- `SortDescriptions.Clear()` だけでは見た目の矢印が残る
- `column.SortDirection = null` を併用して UI 整合性を保つ

### 3 回目のクリックで自動的に初期化する（カスタム挙動）

「昇順 → 降順 → 未ソート」の 3 状態にする場合は、`Sorting` イベントを使って制御する。
3 つの状態を実際に表示すると次のようになる。

<figure class="article-figure">
  <img src="/images/articles/wpf-datagrid-sort-reset/datagrid-sort-three-states.png" alt="3 つの DataGrid を並べた画面。左は Name 列に昇順の矢印が付き名前順、中央は降順の矢印が付き逆順、右は矢印が無くデータ本来の並びになっている。" width="650" height="171" loading="lazy">
  <figcaption>同じデータに対する 3 状態の表示。未ソート（右）では対象列の <code>SortDescription</code> とヘッダーの矢印がどちらも消える。この例は 1 列だけでソートしているため、解除するとコレクション本来の並びに戻る。複数列でソートしていて Shift を押しながら解除した場合は他の列の条件が残るため、必ずしも元の並びには戻らない。</figcaption>
</figure>

#### XAML

```xml
<DataGrid x:Name="MyDataGrid"
          Sorting="DataGrid_Sorting" />
```

この設定により、列ヘッダークリック時の既定ソート処理へ独自ロジックを差し込める。
また、`DataGridTemplateColumn` などで列ソートを解除対象にする場合は、事前に `SortMemberPath` を設定しておく必要がある。

#### C\#

解除の処理は、後の Behavior からも使えるよう静的クラスにまとめる。

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

public static class TriStateSort
{
    // DataGrid の Sorting イベントから呼ぶ。降順の列をもう一度クリックしたときだけ、標準の処理の代わりに解除する
    public static void OnSorting(DataGrid dataGrid, DataGridSortingEventArgs e)
    {
        if (e.Column.SortDirection != ListSortDirection.Descending) return;

        e.Handled = true; // 標準処理をキャンセル

        SortDescriptionCollection sorts = dataGrid.Items.SortDescriptions;
        int groupingCount = CountGroupingSorts(dataGrid.Items);

        using (dataGrid.Items.DeferRefresh()) // 外し終えてから 1 回だけ並べ直す
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                // Shift + クリック: 対象列の条件だけを外す。他の列の条件と矢印は残る
                for (int i = groupingCount; i < sorts.Count; i++)
                {
                    if (sorts[i].PropertyName == e.Column.SortMemberPath)
                    {
                        sorts.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                // 通常のクリック: 他の列の矢印は、DataGrid がこのイベントの前に消している。
                // 矢印の無い条件が残らないよう、グループ化用を除く条件をすべて外す
                while (sorts.Count > groupingCount)
                {
                    sorts.RemoveAt(sorts.Count - 1);
                }
            }
        }

        e.Column.SortDirection = null;
    }

    // グループ化していると、DataGrid は並び替えの条件の先頭に、グループ化用の昇順の条件を入れる。その数を数える
    // GroupDescriptions の順を後から入れ替えても条件は入れ直されないので、順ではなくプロパティ名で照らす
    private static int CountGroupingSorts(ItemCollection items)
    {
        var groupProperties = new List<string>();
        foreach (GroupDescription group in items.GroupDescriptions)
        {
            if (group is PropertyGroupDescription property
                && property.Converter == null
                && property.StringComparison == StringComparison.Ordinal)
            {
                groupProperties.Add(property.PropertyName);
            }
        }

        int count = 0;
        while (count < items.SortDescriptions.Count
            && items.SortDescriptions[count].Direction == ListSortDirection.Ascending
            && groupProperties.Remove(items.SortDescriptions[count].PropertyName))
        {
            count++;
        }

        return count;
    }
}
```

コードビハインドのハンドラーは、これを呼ぶだけである。

```csharp
private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
{
    if (sender is DataGrid dataGrid) TriStateSort.OnSorting(dataGrid, e);
}
```

この実装は、降順の列をもう一度クリックしたときだけ標準の処理を止めて解除する。
解除する範囲は、Shift を押しているかどうかで分けている。

Shift を押さないクリックでは、`DataGrid` は `Sorting` イベントを発生させる前に、クリックした列以外の `SortDirection` を `null` にする（[DataGrid.cs の PerformSort](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7084-L7099) と [PrepareForSort](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7119-L7136)）。
ここで対象列の条件だけを外すと、他の列の条件が矢印の無いまま残る。
そのため、Shift なしのクリックでは列の条件をすべて外す。
Shift を押しながらのクリックでは他の列の矢印は消えないので、対象列の条件だけを外す。

グループ化しているときに列ヘッダーで並び替えると、`DataGrid` は `PropertyGroupDescription`（`Converter` が無く、比較が `Ordinal` のもの）ごとに、昇順の条件を `SortDescriptions` の先頭に入れる（[AddGroupingSortDescriptions](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7334-L7361)）。
グループ化と同じプロパティの列で並び替えると、同じ `PropertyName` の条件が 2 つ並ぶ。
`CountGroupingSorts` は、先頭にあるグループ化用の条件をプロパティ名で照らして数え、解除の対象から外している。

対象列と同じ `PropertyName` の最初の条件を外す書き方（`FirstOrDefault`）と比べた結果が次の表である。

{% include tables/articles/wpf-datagrid-sort-reset/datagrid-tristate-sort.ja.md %}

.NET 10 / Windows 11 で、列ヘッダーを実際のマウスでクリックして測った結果。
クリックの間はダブルクリックの時間より長く空け、Shift は実際のキー入力で押した。
「列の矢印」は <code>SortDirection</code> が付いている列である。
{: .table-caption}

**最初に一致した条件を外す書き方では、Shift なしのクリックで `Name` の条件が残り、並びは名前順のままなのに矢印はどの列にも出ない。**
グループ化した場合は、外れるのがグループ化用の条件で、降順の並びが残る。
`TriStateSort` では、グループ化用の条件を除けば、どちらの場合も条件と矢印が一致する。
Shift を押しながらのクリックでは、どちらの書き方でも `Name` の条件と矢印が残る。

### CollectionView を使って ViewModel から初期化する

MVVM 構成では、`DataGrid` を直接操作するより、`ICollectionView` を使うほうが管理しやすい。

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

public class SampleViewModel
{
    public ObservableCollection<RowItem> Items { get; } = new();
    public ICollectionView ItemsView { get; }

    public SampleViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
    }

    public void ClearSort()
    {
        ItemsView.SortDescriptions.Clear();
        ItemsView.Refresh();
    }
}

public class RowItem
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}
```

`SortDescriptions` を `ItemsView` 側で管理することで、UI コンポーネントへの依存を減らし、テスト容易性を確保できる。

**ただし、この `ClearSort` が解除するのはビューの並び順だけである。**
`DataGridColumn.SortDirection` は `DataGrid` 側に残るため、並び順は初期状態に戻ってもヘッダーの矢印は表示されたままになる（前掲の表の `ItemsSource = ICollectionView` の行がまさにこれを測っており、ビューの `SortDescriptions` を消しても `SortDirection` は `Ascending` のまま残った）。
また、この `ClearSort` は `SortDescriptions` だけを消すため、`ListCollectionView.CustomSort` で並べ替えている場合は解除されない。
`CustomSort` を設定したビューに `ClearSort` と同じ手順を実行しても、`CustomSort` は残り、並びも変わらなかった。
`CustomSort` を使う画面では、`ClearSort` で `CustomSort` にも `null` を代入する。

{% include tables/articles/wpf-datagrid-sort-reset/datagrid-sort-customsort.ja.md %}

.NET 10 / Windows 11 で、サンプルデータの <code>ListCollectionView</code> に操作を順に行って測った結果。
<code>SortDescriptions</code> の列は条件の数である。
{: .table-caption}

矢印まで戻すには、ViewModel から列の状態を触れない以上、`DataGrid` 側で `SortDirection` を `null` にする処理が別に要る。
Behavior 化する場合は、その処理を Behavior に含めるとコマンドから一度に初期化できる。

```xml
<DataGrid ItemsSource="{Binding ItemsView}" />
```

ビューは `ItemsView` を表示するだけに限定されるため、ソート初期化の責務を ViewModel 側に集約できる。

### Behavior として共通化する

同じカスタムソート挙動を複数画面で使うなら、Behavior 化が有効である。
以下は `Microsoft.Xaml.Behaviors.Wpf` を利用する例である。

```csharp
using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;

namespace YourApp.Behaviors
{
    public class TriStateSortBehavior : Behavior<DataGrid>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Sorting += OnSorting;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Sorting -= OnSorting;
            base.OnDetaching();
        }

        // 解除の処理は、3 回目のクリックの節の TriStateSort に任せる
        private void OnSorting(object sender, DataGridSortingEventArgs e) =>
            TriStateSort.OnSorting(AssociatedObject, e);
    }
}
```

Behavior 化により、同じ三状態ソート解除ロジックを画面ごとに重複実装せず適用できる。

```xml
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
    xmlns:local="clr-namespace:YourApp.Behaviors">
    <DataGrid>
        <i:Interaction.Behaviors>
            <local:TriStateSortBehavior />
        </i:Interaction.Behaviors>
    </DataGrid>
</Window>
```

XAML 側は Behavior を宣言するだけで済むため、利用画面追加時の実装コストを抑制しやすい。

## 選択の分岐点

どの方法を使うかは、初期化を起こす操作と、同じ挙動を何画面に広げるかで決まる。

**ボタンやメニューから即時に全解除するなら、明示クリア。**
`SortDescriptions.Clear()` と `SortDirection = null` を並べるだけで済み、導入が最も速い。
ただしコードビハインドから `DataGrid` を参照するため、MVVM の分離は緩む。

**ヘッダークリックだけで完結させるなら、`Sorting` イベントで三状態にする。**
「昇順 → 降順 → 未ソート」を 1 つの操作系にまとめられる。
この記事の `TriStateSort` は、Shift の有無で列単位の解除と全解除を分けている。
要件が違う場合は、その分岐を変える。

**MVVM を徹底し、コマンド経由で初期化するなら `ICollectionView`。**
ソート状態を ViewModel 側に置けるため、UI へ依存せずテストできる。
View と ViewModel の責務分離が前提になる。

**同じルールを複数の `DataGrid` へ適用するなら Behavior 化。**
XAML に宣言を 1 行足すだけで横展開でき、重複コードを減らせる。
画面ごとの差分を吸収する拡張ポイントを設計しておく必要がある。

---

## 方法別の比較

| 方法 | メリット | デメリット | 適するケース |
|---|---|---|---|
| 明示クリア（`SortDescriptions.Clear` + `SortDirection = null`） | 実装が単純で導入が速い。 | DataGrid 参照が必要で MVVM 純度は下がる。 | 画面単位で即時に全解除したい場合。 |
| `Sorting` イベントで三状態制御 | UX を「昇順→降順→未ソート」に統一できる。 | イベント処理が複雑化しやすく列単位要件の整理が必要。 | ヘッダークリックだけで完結する操作性を重視する場合。 |
| `ICollectionView` で ViewModel 管理 | テストしやすく UI 依存を最小化できる。 | View と ViewModel の責務分離設計が前提となる。 | MVVM を徹底し、コマンド経由で初期化する場合。 |
| Behavior 化 | 複数画面へ横展開しやすく重複コードを削減できる。 | 画面差分要件を吸収する拡張ポイント設計が必要。 | 同一ルールのソート挙動を複数 DataGrid へ適用する場合。 |

---

## 注意点

- `SortDescriptions` のクリアだけでは、ヘッダーの矢印の表示と並び順が食い違う。
- `SortMemberPath` が空の列は、標準の並び替えでも並ばず、矢印も付かない（[DefaultSort](https://github.com/dotnet/wpf/blob/v10.0.13/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DataGrid.cs#L7145-L7209)）。
  そのため、解除の対象にもならない。
  `DataGridTemplateColumn` では `SortMemberPath` を設定する。
- この記事の `TriStateSort` は、Shift なしのクリックでは列の条件をすべて外し、Shift を押しながらのクリックでは対象列の条件だけを外す。
  要件が違う場合は、この分岐を変える。

---

## まとめ

WPF `DataGrid` のソート状態は `ICollectionView.SortDescriptions` と `DataGridColumn.SortDirection` の 2 か所に分かれており、コードから一方だけを触ると食い違う。
初期化ではその両方を戻す必要がある。

分岐点は、初期化を起こす操作と、同じ挙動を何画面に広げるかにある。
ボタンから即時に解除するなら明示クリア、ヘッダークリックで完結させるなら `Sorting` イベント、MVVM 徹底なら `ICollectionView`、複数画面へ広げるなら Behavior 化を選ぶ。

## 関連記事

- [WPF DataGrid の並び替えを実装する方法](/ja/articles/wpf-datagrid-sorting/)
- [WPF DataGrid の行のダブルクリックをコマンドにつなぐ: MouseDoubleClick は列ヘッダーでも発火する](/ja/articles/wpf-datagrid-row-doubleclick-command-mvvm/)：列ヘッダーのダブルクリックで、並び替えと同時に直前に選んでいた行の詳細が開く書き方を実測した記事
- [WPF DataGrid でセル編集中と表示時でコントロールを切り替える方法](/ja/articles/wpf-datagrid-cell-editing-template/)
- [DataGrid（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/datagrid.html)：見出しの 3 回目のクリックでも並べ替えが解除されないことと、コードから並べ替える前に編集を終える方法を実測したページ
