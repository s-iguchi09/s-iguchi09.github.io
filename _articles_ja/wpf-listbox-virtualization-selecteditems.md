---
layout: article-ja
title: "WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法"
date: 2026-04-24
category: WPF
excerpt: "ListBox の仮想化有効時に選択状態が維持されない理由と、IsSelected を各アイテムに持たせて MVVM で安定させる解決方法を解説します。Shift 範囲選択への対応も含めます。"
---

## 概要

WPF の `ListBox` は、大量データを表示するとき `VirtualizingStackPanel` による UI 仮想化が有効になる。
仮想化が有効だと、画面外にあるアイテムのコンテナ(`ListBoxItem`)は破棄され、必要になった時点で再生成される。
このとき、選択状態の管理をコンテナに依存していると、スクロール後に「以前選択した項目が `SelectedItems` に残っていない」ように見えることがある。
最も安全なのは、選択状態をコンテナではなくデータ側に持たせる方法である。
各アイテムの ViewModel に `IsSelected` を持たせ、`ItemContainerStyle` で `ListBoxItem.IsSelected` を TwoWay バインドする。

## 前提・対象環境

- フレームワーク / 言語: .NET 6 以降 / C# 10
- 対象コントロール: WPF `ListBox`(`System.Windows.Controls`)
- アーキテクチャ: MVVM(各アイテム ViewModel が `IsSelected` を公開する)

以降の例では、UI 仮想化が有効な状態(`ListBox` の既定)で、1 万件規模のコレクションを `ListBox` にバインドすることを前提とする。
`SelectionMode` は複数選択を扱う `Extended` を用いる。

## 原因・背景

`ListBox` の UI 仮想化では、スクロールに応じてコンテナが作り直される。
選択状態を次のように扱っている場合、仮想化の影響を受けやすくなる。

- `ListBoxItem` を直接参照して選択を管理している
- Visual Tree からコンテナをたどって `SelectedItems` を構築している
- 再生成されたコンテナに対して選択状態を復元していない

失われているのはデータそのものではなく、コンテナ依存の選択同期である。
`VirtualizationMode="Recycling"` ではコンテナが使い回されるため、再利用されたコンテナに前の選択状態が残る、あるいは復元されないといった不整合がさらに起きやすくなる。

## 解決策: 各アイテムに IsSelected を持たせる

複数選択を MVVM で安定して扱うには、各アイテム ViewModel に `IsSelected` を持たせる方法が定番である。
選択状態がデータ側にあれば、コンテナが破棄・再生成されても値は保持される。

### ViewModel の例

各行を表す ViewModel に、変更通知付きの `IsSelected` を実装する。

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class RowItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public RowItemViewModel(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

`IsSelected` の変更通知がないと、`ListBoxItem.IsSelected` からの TwoWay バインドが初期表示や再生成時に同期しないため、`INotifyPropertyChanged` の実装は必須である。

### 画面全体の ViewModel の例

リスト全体を保持し、選択済みアイテムをデータ側から取得できるようにする。

```csharp
using System.Collections.ObjectModel;
using System.Linq;

public class MainViewModel
{
    public ObservableCollection<RowItemViewModel> Items { get; } = new();

    public MainViewModel()
    {
        for (int i = 1; i <= 10000; i++)
        {
            Items.Add(new RowItemViewModel(i, $"Row {i}"));
        }
    }

    public RowItemViewModel[] GetSelectedItems()
        => Items.Where(x => x.IsSelected).ToArray();
}
```

`GetSelectedItems` はコンテナではなくデータを走査するため、スクロール位置や仮想化の状態に関わらず、常に正しい選択集合を返す。

### XAML の例

`ItemContainerStyle` で `ListBoxItem.IsSelected` を各アイテムの `IsSelected` に TwoWay バインドする。

```xml
<ListBox ItemsSource="{Binding Items}"
         SelectionMode="Extended"
         ScrollViewer.CanContentScroll="True"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Id}" Width="80"/>
                <TextBlock Text="{Binding Name}"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>

    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="IsSelected"
                    Value="{Binding IsSelected, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        </Style>
    </ListBox.ItemContainerStyle>
</ListBox>
```

コンテナが再生成されても、バインドが `IsSelected` の値を読み直して選択状態を復元する。

## Shift 範囲選択への対応

本手法は `SelectionMode="Extended"` を維持するため、`Shift` による範囲選択や `Ctrl` による追加選択は WPF の標準動作に任せられる。
`Shift` による範囲選択が行われると、選択された範囲の各アイテムの `IsSelected` が `true` に設定される。
後続のスクロールでコンテナが仮想化されても選択状態はデータ側に残るため、選択が消えたように見える問題を回避できる。

## 注意点

### 1. SelectedItems を直接 TwoWay バインドしない

`SelectedItems` はコレクションだが、WPF の標準コントロールではそのまま素直に TwoWay バインドできない(依存関係プロパティではなく読み取り専用のコレクションであるため)。
複数選択を MVVM で扱う場合は、`IsSelected` パターンを採用するのが実装・保守の両面で現実的である。

### 2. CanContentScroll を false にしない

`ScrollViewer.CanContentScroll="False"` にすると、スクロール単位がアイテム単位からピクセル単位に変わり、仮想化が無効化されてすべてのアイテムが描画されやすくなる。
大量件数のリストでは、通常は `True` を維持する。

### 3. コンテナ依存のロジックを避ける

`ItemContainerGenerator.ContainerFromIndex` や Visual Tree の走査に依存すると、仮想化とコンテナの再利用の影響を受けやすくなる。
選択状態はデータ側で管理する。

### 4. 選択集合はコンテナから読み取らない

`ListBox.SelectedItems` は、仮想化で実体化されていないアイテムも含めて選択を反映する。
一方、コンテナを列挙して選択を集計するコードは、画面外のアイテムを取りこぼす。
選択集合は上記 `GetSelectedItems` のように `IsSelected` から求める。

## まとめ

WPF の `ListBox` で仮想化を有効にした場合、選択状態をコンテナに依存して管理していると、スクロール後に `SelectedItems` が消えたように見えることがある。
これを避けるには、次の構成が有効である。

- 各アイテム ViewModel に `IsSelected` を持たせる
- `ListBoxItem.IsSelected` を `IsSelected` に TwoWay バインドする
- `SelectionMode="Extended"` のまま標準の Shift / Ctrl 選択を使う
- 仮想化を維持するため `CanContentScroll="True"` を使う

数千〜数万件規模のリストで複数選択を扱う場合にこの構成が適する。
選択が少数かつ仮想化が不要な小さいリストであれば、標準の `SelectedItems` をそのまま使う方が簡潔である。
