---
layout: article-ja
title: "WPFのDataGridのソートを初期化する方法"
date: 2026-06-29
category: WPF
excerpt: "WPF DataGrid のソート状態を初期化する代表的な方法を整理し、単一列ソートと複数列ソートの両方で使える実装例を示す。"
---

WPF の `DataGrid` は便利なソート機能を持っていますが、要件によっては「初期状態に戻す（ソートを解除する）」動作を明示的に実装したいことがあります。  
本記事では、`DataGrid` のソート初期化を実現する代表的な方法を整理します。

## 概要

本記事では、以下の方法で `DataGrid` のソート初期化を扱います。  

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

## 問題

WPF `DataGrid` では、業務要件として「現在の並び替え状態を初期状態へ戻す」操作が必要になる場合があります。  
このとき、標準操作だけでは意図したタイミングでの初期化を統一しづらいケースがあります。

## 原因・背景

まず前提として、WPF `DataGrid` で `Shift + 列ヘッダークリック` は、標準では**複数列ソートの追加**です。  
つまり、既存ソートに列を積み増すための操作であり、ソート解除のショートカットではありません。

- 単独クリック: その列の昇順/降順を切り替え
- Shift+クリック: 複数列ソートとして追加

そのため、「初期状態に戻す」にはコードによる制御が必要です。

## 解決方法

以下の方針で要件に応じて初期化方法を選択します。  

- 明示的にソート条件と表示状態をクリアする
- `Sorting` イベントを処理して列単位の未ソート状態を制御する
- MVVM 構成では `ICollectionView` 側でソート状態を管理する
- 共通化が必要な場合は Behavior として再利用する

## 実装例

### コードで明示的にソートをクリアする

最もシンプルなのは、`SortDescriptions` と列ヘッダー矢印の両方をクリアする方法です。

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

### ポイント

- `SortDescriptions.Clear()` だけでは見た目の矢印が残る場合がある
- `column.SortDirection = null` を併用して UI 整合性を保つ

### 3回目のクリックで自動的に初期化する（カスタム挙動）

「昇順 → 降順 → 未ソート」の3状態にしたい場合は、`Sorting` イベントを使って制御します。

### XAML

```xml
<DataGrid x:Name="MyDataGrid"
          Sorting="DataGrid_Sorting" />
```

この設定により、列ヘッダークリック時の既定ソート処理へ独自ロジックを差し込める。

### C\#

```csharp
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
{
    if (sender is not DataGrid dataGrid) return;

    if (e.Column.SortDirection == ListSortDirection.Descending)
    {
        e.Handled = true; // 標準処理をキャンセル

        // 対象列の SortDescription のみ削除
        var target = dataGrid.Items.SortDescriptions
            .FirstOrDefault(sd => sd.PropertyName == e.Column.SortMemberPath);

        if (!string.IsNullOrEmpty(target.PropertyName))
        {
            dataGrid.Items.SortDescriptions.Remove(target);
        }

        e.Column.SortDirection = null;
        dataGrid.Items.Refresh();
    }
}
```

この実装では、降順状態でさらにクリックされた場合のみ対象列のソートを解除し、複数列ソート時でも他列の条件は維持される。

### CollectionView を使って ViewModel から初期化する

MVVM 構成では、`DataGrid` を直接操作せず `ICollectionView` を使うと管理しやすくなります。

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

```xml
<DataGrid ItemsSource="{Binding ItemsView}" />
```

ビューは `ItemsView` を表示するだけに限定されるため、ソート初期化の責務を ViewModel 側に集約できる。

### Behaviorクラスの作成

同じカスタムソート挙動を複数画面で使うなら、Behavior 化が有効です。  
以下は `Microsoft.Xaml.Behaviors.Wpf` を利用する例です。

```csharp
using Microsoft.Xaml.Behaviors;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

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

    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        if (e.Column.SortDirection == ListSortDirection.Descending)
        {
            e.Handled = true;

            var sd = grid.Items.SortDescriptions
                .FirstOrDefault(x => x.PropertyName == e.Column.SortMemberPath);

            if (!string.IsNullOrEmpty(sd.PropertyName))
            {
                grid.Items.SortDescriptions.Remove(sd);
            }

            e.Column.SortDirection = null;
            grid.Items.Refresh();
        }
    }
}
```

Behavior 化により、同じ三状態ソート解除ロジックを画面ごとに重複実装せず適用できる。

```xml
<Window
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

## 注意点

- `SortDescriptions` のクリアだけではヘッダー矢印表示と不整合になる場合があります。  
- 複数列ソートを併用する場合は、対象列のみを解除するか全解除するかを要件で明確化する必要があります。  
- 再利用目的で Behavior 化する場合は、画面ごとのソート仕様差分を吸収できる設計にしておくと保守しやすくなります。

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
|---|---|---|---|
| 明示クリア（`SortDescriptions.Clear` + `SortDirection = null`） | 実装が単純で導入が速い。 | DataGrid 参照が必要で MVVM 純度は下がる。 | 画面単位で即時に全解除したい場合。 |
| `Sorting` イベントで三状態制御 | UX を「昇順→降順→未ソート」に統一できる。 | イベント処理が複雑化しやすく列単位要件の整理が必要。 | ヘッダークリックだけで完結する操作性を重視する場合。 |
| `ICollectionView` で ViewModel 管理 | テストしやすく UI 依存を最小化できる。 | View と ViewModel の責務分離設計が前提となる。 | MVVM を徹底し、コマンド経由で初期化する場合。 |
| Behavior 化 | 複数画面へ横展開しやすく重複コードを削減できる。 | 画面差分要件を吸収する拡張ポイント設計が必要。 | 同一ルールのソート挙動を複数 DataGrid へ適用する場合。 |

## まとめ

WPF `DataGrid` のソート初期化は、用途に応じて実装方法を選べます。

- 標準操作の理解（Shift+クリックは複数列ソート）
- 明示クリア（`SortDescriptions.Clear` + `SortDirection = null`）
- 3状態遷移（`Sorting` イベント）
- MVVM 対応（`ICollectionView`）
- 再利用性（Behavior 化）

要件が単純ならヘルパーメソッド、画面横断で使うなら Behavior 化、MVVM 徹底なら `CollectionView` 中心で設計するのが実践的です。

## 関連記事

- [WPF DataGrid の並び替えを実装する方法](/ja/articles/wpf-datagrid-sorting/)
- [WPF DataGridTemplateColumn で表示と編集のテンプレートを分離する方法](/ja/articles/wpf-datagrid-cell-editing-template/)
