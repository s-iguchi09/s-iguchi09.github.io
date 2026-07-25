using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPFのDataGridのソートを初期化する方法」の図。
/// 「昇順 → 降順 → 未ソート」の 3 状態を、実際の DataGrid で並べて取得する。
/// </summary>
internal sealed class DataGridSortResetScene : IScene
{
    public string Slug => "wpf-datagrid-sort-reset";

    public async Task CaptureAsync(SceneContext context)
    {
        DataGrid ascending = BuildGrid();
        DataGrid descending = BuildGrid();
        DataGrid cleared = BuildGrid();

        Window window = DemoLayout.BuildPanelWindow(
            "DataGrid Sort States",
            [
                new DemoLayout.Panel("ListSortDirection.Ascending", ascending),
                new DemoLayout.Panel("ListSortDirection.Descending", descending),
                new DemoLayout.Panel("SortDirection = null", cleared),
            ]);

        await context.ShootAsync(window, "datagrid-sort-three-states.png", _ =>
        {
            Sort(ascending, ListSortDirection.Ascending);
            Sort(descending, ListSortDirection.Descending);
            ClearSort(cleared);
            return Task.CompletedTask;
        });
    }

    private static DataGrid BuildGrid() => new()
    {
        ItemsSource = SampleData.Products(),
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        CanUserResizeColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        HorizontalAlignment = HorizontalAlignment.Left,
        Columns =
        {
            new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(Product.Name)),
                SortMemberPath = nameof(Product.Name),
                Width = 110,
            },
            new DataGridTextColumn
            {
                Header = "Price",
                Binding = new Binding(nameof(Product.Price)),
                SortMemberPath = nameof(Product.Price),
                Width = 80,
            },
        },
    };

    private static void Sort(DataGrid grid, ListSortDirection direction)
    {
        grid.Items.SortDescriptions.Clear();
        grid.Items.SortDescriptions.Add(new SortDescription(nameof(Product.Name), direction));
        grid.Columns[0].SortDirection = direction;
        grid.Items.Refresh();
    }

    /// <summary>
    /// 記事の ClearDataGridSort と同じ手順。
    /// SortDescriptions を消すだけではヘッダーの矢印が残るため、SortDirection も null にする。
    /// </summary>
    private static void ClearSort(DataGrid grid)
    {
        grid.Items.SortDescriptions.Clear();

        foreach (DataGridColumn column in grid.Columns)
        {
            column.SortDirection = null;
        }

        grid.Items.Refresh();
    }
}
