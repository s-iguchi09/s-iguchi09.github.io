---
layout: article-ja
title: "WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法"
date: 2026-04-24
category: WPF
excerpt: "ListBox の仮想化有効時に選択状態が維持されない理由と、IsSelected を各アイテムに持たせて MVVM で安定させる解決方法を解説します。Shift 範囲選択への対応も含めます。"
---

## 概要

WPF の `ListBox` は、大量データを表示するときに `VirtualizingStackPanel` による UI 仮想化が有効になります。仮想化が有効だと、画面外にあるアイテムのコンテナ（`ListBoxItem`）は破棄され、必要になったタイミングで再生成されます。

このとき、選択状態の管理をコンテナに依存していると、スクロール後に「以前選択した項目が `SelectedItems` に残っていない」ように見えることがあります。

結論としては、**選択状態はコンテナではなくデータ側に持たせる**のが最も安全です。各アイテムの ViewModel に `IsSelected` を持たせ、`ItemContainerStyle` で `ListBoxItem.IsSelected` を TwoWay バインドします。

## なぜ問題が起きるのか

`ListBox` の UI 仮想化では、スクロールに応じてコンテナが作り直されます。選択状態を次のように扱っている場合、仮想化の影響を受けやすくなります。

- `ListBoxItem` を直接参照して選択を管理している
- Visual Tree からコンテナをたどって `SelectedItems` を構築している
- 再生成されたコンテナに対して選択状態を復元していない

つまり、失われているのはデータそのものではなく、**コンテナ依存の選択同期**です。

## 解決策: 各アイテムに IsSelected を持たせる

複数選択を MVVM で安定して扱うには、各アイテム ViewModel に `IsSelected` を持たせる方法が定番です。

### ViewModel の例

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

### 画面全体の ViewModel の例

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

### XAML の例

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

## Shift 範囲選択への対応

本手法は `SelectionMode="Extended"` を維持するため、`Shift` による範囲選択や `Ctrl` による追加選択は WPF の標準動作に任せられます。

`Shift` による範囲選択が行われると、選択された範囲の各アイテムの `IsSelected` が `true` に設定されます。後続のスクロールでコンテナが仮想化されても選択状態はデータ側に残るため、選択が消えたように見える問題を回避できます。

## よくある注意点

### 1. SelectedItems を直接 TwoWay バインドしようとしない

`SelectedItems` はコレクションですが、WPF の標準コントロールではそのまま素直に TwoWay バインドするのが難しいです。複数選択を MVVM で扱う場合、`IsSelected` パターンを採用するのが実装・保守の両面で現実的です。

### 2. CanContentScroll=false にしない

`ScrollViewer.CanContentScroll="False"` にすると、仮想化が壊れてすべてのアイテムが描画されやすくなります。大量件数のリストでは、通常は `True` を維持します。

### 3. コンテナ依存のロジックを避ける

`ItemContainerGenerator.ContainerFromIndex` や Visual Tree の走査に依存すると、仮想化と再利用の影響を受けやすくなります。選択状態はデータ側で管理します。

## まとめ

WPF の `ListBox` で仮想化を有効にした場合、選択状態をコンテナに依存して管理していると、スクロール後に `SelectedItems` が消えたように見えることがあります。

これを避けるには、次の構成が有効です。

- 各アイテム ViewModel に `IsSelected` を持たせる
- `ListBoxItem.IsSelected` を `IsSelected` に TwoWay バインドする
- `SelectionMode="Extended"` のまま標準の Shift / Ctrl 選択を使う
- 仮想化を維持するため `CanContentScroll="True"` を使う

この方法により、Shift 範囲選択を含めて、仮想化環境でも選択状態を安定して保持できます。
