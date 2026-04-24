---
layout: article-ja
title: "WPF DataGrid の並び替えを実装する方法"
date: 2026-04-20
category: WPF
excerpt: "DataGrid のソート処理の基本と、実務で使いやすい実装パターンを解説します。"
---

## 概要

WPF の `DataGrid` コントロールは、列ヘッダーをクリックするだけで行を昇順・降順に並び替える機能を標準で備えています。`CanUserSortColumns` が `true`（デフォルト）の状態で `SortMemberPath` を適切に設定すると、ユーザーは追加コードなしにソートを操作できます。本記事では基本的な使い方から、コードで制御する方法、カスタムソートロジックの実装まで順を追って解説します。

## デフォルトのソート設定

`DataGridTextColumn` の `SortMemberPath` にバインド先のプロパティ名を指定するだけで並び替えが有効になります。

```xml
<DataGrid ItemsSource="{Binding Products}"
          AutoGenerateColumns="False"
          CanUserSortColumns="True">
  <DataGrid.Columns>
    <DataGridTextColumn Header="商品名"  Binding="{Binding Name}"  SortMemberPath="Name" />
    <DataGridTextColumn Header="価格"   Binding="{Binding Price}" SortMemberPath="Price" />
  </DataGrid.Columns>
</DataGrid>
```

列ヘッダーを 1 回クリックで昇順、もう 1 回で降順、さらに 1 回でソート解除になります。

## コードでソートを制御する

`DataGrid.Items.SortDescriptions` を直接操作することで、ユーザー操作を介さずにプログラムからソートをかけることができます。

```csharp
using System.ComponentModel;

dataGrid.Items.SortDescriptions.Clear();
dataGrid.Items.SortDescriptions.Add(
    new SortDescription(nameof(Product.Price), ListSortDirection.Descending));
dataGrid.Items.Refresh();
```

列ヘッダーに表示されるソートグリフも同期させると UI の整合性が保たれます。

```csharp
foreach (var col in dataGrid.Columns)
    col.SortDirection = null;

var priceCol = dataGrid.Columns.First(c => c.SortMemberPath == nameof(Product.Price));
priceCol.SortDirection = ListSortDirection.Descending;
```

## ICollectionView によるカスタムソート

大文字小文字を区別しない文字列ソートや、複数キーの組み合わせソートなど、標準の `SortDescription` では表現できないケースでは `ICollectionView.CustomSort` を使います。

```csharp
var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
view.CustomSort = Comparer<Product>.Create((a, b) =>
    StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
```

`CustomSort` は `SortDescriptions` より優先されるため、両方を切り替える場合は先に `SortDescriptions.Clear()` を呼び出してください。

## まとめ

| シナリオ | 推奨アプローチ |
|---|---|
| 単純な列ソート | `CanUserSortColumns="True"`（デフォルト） |
| コードからのソート | `SortDescriptions` ＋ `SortDirection` 更新 |
| カスタムソートロジック | `ICollectionView.CustomSort` |

業務アプリの多くはデフォルト機能で対応できます。`CustomSort` は `SortDescription` では表現できない特殊な並び替えが必要なときだけ使用するのが運用上の目安です。
