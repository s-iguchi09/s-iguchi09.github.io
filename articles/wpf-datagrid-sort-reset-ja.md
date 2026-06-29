---
title: "WPFのDataGridのソートを初期化する方法"
emoji: "🧭"
type: "tech"
topics: ["wpf", "datagrid", "csharp", "mvvm"]
published: false
---

WPF の `DataGrid` は便利なソート機能を持っていますが、要件によっては「初期状態に戻す（ソートを解除する）」動作を明示的に実装したいことがあります。  
本記事では、`DataGrid` のソート初期化を実現する代表的な方法を整理します。

## 対象トピック

- Shift+クリックの標準機能
- コードで明示的にソートをクリアする
- 3回目のクリックで自動的に初期化する（カスタム挙動）
- `CollectionView` を使って ViewModel から初期化する
- Behaviorクラスの作成

## Shift+クリックの標準機能

まず前提として、WPF `DataGrid` で `Shift + 列ヘッダークリック` は、標準では**複数列ソートの追加**です。  
つまり、既存ソートに列を積み増すための操作であり、ソート解除のショートカットではありません。

- 単独クリック: その列の昇順/降順を切り替え
- Shift+クリック: 複数列ソートとして追加

そのため、「初期状態に戻す」にはコードによる制御が必要です。

## コードで明示的にソートをクリアする

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

## 3回目のクリックで自動的に初期化する（カスタム挙動）

「昇順 → 降順 → 未ソート」の3状態にしたい場合は、`Sorting` イベントを使って制御します。

### XAML

```xml
<DataGrid x:Name="MyDataGrid"
          Sorting="DataGrid_Sorting" />
```

### C#

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

## CollectionView を使って ViewModel から初期化する

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

```xml
<DataGrid ItemsSource="{Binding ItemsView}" />
```

## Behaviorクラスの作成

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

## まとめ

WPF `DataGrid` のソート初期化は、用途に応じて実装方法を選べます。

- 標準操作の理解（Shift+クリックは複数列ソート）
- 明示クリア（`SortDescriptions.Clear` + `SortDirection = null`）
- 3状態遷移（`Sorting` イベント）
- MVVM 対応（`ICollectionView`）
- 再利用性（Behavior 化）

要件が単純ならヘルパーメソッド、画面横断で使うなら Behavior 化、MVVM 徹底なら `CollectionView` 中心で設計するのが実践的です。
