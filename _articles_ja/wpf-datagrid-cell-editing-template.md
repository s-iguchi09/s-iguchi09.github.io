---
layout: article-ja
title: "WPF DataGrid でセル編集中と表示時でコントロールを切り替える方法"
date: 2026-06-09
category: WPF
excerpt: "DataGridTemplateColumn の CellTemplate と CellEditingTemplate を使い分け、表示時と編集中で最適な UI を構成する方法を解説します。"
---

## 概要

WPF の `DataGrid` でセルの表示状態と編集状態に異なるコントロールを使う場合は、`DataGridTemplateColumn` の `CellTemplate` と `CellEditingTemplate` を分離して定義する方法が基本となる。
本記事では、基本実装、実務で多い応用パターン、`CellEditingTemplate` を採用する判断基準を整理する。

## 前提・対象環境

- フレームワーク／言語: .NET 8 / C# 12
- 対象コントロール・機能: WPF `DataGrid`
- アーキテクチャ: MVVM
- その他制約: 一覧表示時の視認性と編集時の入力効率を両立する構成を想定

## 問題

`DataGridTextColumn` のような標準列のみを使うと、表示時と編集中で同一の UI になりやすい。
その結果、一覧画面としては入力欄が多く見えすぎる、あるいは編集時の入力補助が不足するという問題が発生する。

## 原因・背景

一覧表示で必要な要件は「軽量で読みやすい表示」であり、編集時に必要な要件は「入力制約と操作性」である。
この要件差を単一コントロールで満たそうとすると、どちらかの体験が犠牲になる。
`DataGrid` は編集開始時にテンプレートを切り替える仕組みを持つため、表示用と編集用を分離する設計が適している。

## 解決方法

`DataGridTemplateColumn` を使い、通常時は `CellTemplate`、編集時は `CellEditingTemplate` を定義する。
この構成では `DataGrid` が編集開始と終了に応じてテンプレートを自動で切り替えるため、表示と編集の責務を明確に分離できる。
表示は軽量な `TextBlock`、編集は `TextBox` や `ComboBox`、`DatePicker` など入力向けコントロールを使い分ける。

## 実装例

まず、最小構成として表示時 `TextBlock`、編集時 `TextBox` を定義する。
この実装は表示と編集の切り替えを最も明確に示す。

```xml
<DataGrid ItemsSource="{Binding Items}">
  <DataGrid.Columns>
    <DataGridTemplateColumn Header="名前">
      <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
          <TextBlock Text="{Binding Name}" />
        </DataTemplate>
      </DataGridTemplateColumn.CellTemplate>
      <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
          <TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        </DataTemplate>
      </DataGridTemplateColumn.CellEditingTemplate>
    </DataGridTemplateColumn>
  </DataGrid.Columns>
</DataGrid>
```

編集開始時は `CellEditingTemplate` が適用され、編集終了後は `CellTemplate` に戻る。
切り替え処理をイベントで手動実装する必要はない。

次に、値制約が必要な列では表示 `TextBlock` と編集 `ComboBox` の構成を使う。
この構成は入力候補を限定し、誤入力を抑制できる。

```xml
<DataGridTemplateColumn Header="種別">
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding Category}" />
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
  <DataGridTemplateColumn.CellEditingTemplate>
    <DataTemplate>
      <ComboBox
        ItemsSource="{Binding DataContext.Categories, RelativeSource={RelativeSource AncestorType=DataGrid}}"
        SelectedItem="{Binding Category, Mode=TwoWay}" />
    </DataTemplate>
  </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

`ComboBox` の選択肢は `DataGrid` の `DataContext` から参照し、行アイテムと編集候補を分離して管理する。
選択肢の追加やローカライズが必要な業務画面でも拡張しやすい。

表示と編集を単一テンプレートで切り替える必要がある場合は、`DataGridCell.IsEditing` を `DataTrigger` で参照する方法も利用できる。
ただし、フォーカス制御と可読性の観点で複雑化しやすい。

```xml
<DataTemplate>
  <Grid>
    <TextBlock x:Name="display" Text="{Binding Name}" />
    <TextBox x:Name="editor" Text="{Binding Name, Mode=TwoWay}" Visibility="Collapsed" />
    <DataTemplate.Triggers>
      <DataTrigger
        Binding="{Binding RelativeSource={RelativeSource AncestorType=DataGridCell}, Path=IsEditing}"
        Value="True">
        <Setter TargetName="display" Property="Visibility" Value="Collapsed" />
        <Setter TargetName="editor" Property="Visibility" Value="Visible" />
      </DataTrigger>
    </DataTemplate.Triggers>
  </Grid>
</DataTemplate>
```

この方法は一部の特殊要件で有効だが、基本方針は `CellTemplate` と `CellEditingTemplate` の分離を優先する。
保守性、再利用性、デバッグ容易性の点で差が出る。

## 注意点

- 単純なテキスト編集のみで十分な列は `DataGridTextColumn` を優先し、過剰なテンプレート化を避ける。
- 編集用コントロールに重い UI を常時配置すると仮想化時の描画負荷が増えるため、編集時のみ表示する構成を維持する。
- 単一テンプレート切り替えは柔軟だが、キーボード移動や初期フォーカスの調整コストが高くなる。

## 代替案・比較

- `CellTemplate` / `CellEditingTemplate` 分離: 実装が明確で保守しやすく、表示と編集で異なる UI を作る要件に最適。
- `DataTrigger` による単一テンプレート切り替え: 柔軟性は高いが、XAML が複雑化しやすく運用負荷が上がる。
- 標準列（`DataGridTextColumn` / `DataGridCheckBoxColumn`）: 記述量は少ないが、表示と編集で UI を大きく変える要件には不向き。

## まとめ

`CellEditingTemplate` を利用する主目的は、表示時の見やすさと編集時の入力しやすさを分離することである。
表示が中心の列では軽量な表示 UI を維持し、編集時のみ入力制約や補助 UI を有効化すると、UX と性能を両立しやすい。
判断基準として、表示と編集で UI を変えたい場合、入力制約が必要な場合、編集 UI が重い場合は `DataGridTemplateColumn` の採用が適する。
一方で、単純テキスト編集だけで完結する列は標準列を選択するほうが実装効率が高い。

## 関連記事

- [WPF DataGrid の並び替えを実装する方法](/ja/articles/wpf-datagrid-sorting/)
- [WPF DatePicker で表示形式をカスタマイズする方法](/ja/articles/wpf-datepicker-custom-format/)
- [WPF ComboBox の ItemsSource 設計パターン](/ja/articles/wpf-combobox-itemssource-patterns/)
