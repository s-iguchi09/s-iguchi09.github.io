using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF DataGrid の並び替えを実装する方法」の図。
/// コードから SortDescriptions だけを差し替えたときに、
/// 行の並びとヘッダーのソートグリフが食い違う様子を実際に取得する。
/// </summary>
internal sealed class DataGridSortingScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "列の作り方ごとに SortMemberPath と CanUserSort に何が入るかを確かめる",
        "SortMemberPath を明示しない DataGridTextColumn では Binding のパスが入ること",
        "Binding を持たないテンプレート列では SortMemberPath が空になり、並び替えられないこと",
    ];

    public string Slug => "wpf-datagrid-sorting";

    public async Task CaptureAsync(SceneContext context)
    {
        DataGrid outOfSync = BuildGrid();
        DataGrid inSync = BuildGrid();

        Window window = DemoLayout.BuildPanelWindow(
            "DataGrid Sorting",
            [
                new DemoLayout.Panel("SortDescriptions only", outOfSync),
                new DemoLayout.Panel("SortDescriptions + SortDirection", inSync),
            ]);

        await context.ShootAsync(window, "datagrid-sort-glyph-sync.png", _ =>
        {
            // どちらも「Name の昇順で表示していた」状態から始める。
            ApplyInitialNameSort(outOfSync);
            ApplyInitialNameSort(inSync);

            // 片方は SortDescriptions だけを Price の降順へ差し替える。
            // 行は Price 降順になるが、グリフは Name に付いたまま残る。
            SortByPrice(outOfSync);

            // もう片方はグリフも合わせて更新する。
            SortByPrice(inSync);
            SyncGlyphs(inSync);

            return Task.CompletedTask;
        });

        await context.SaveTableAsync(
            "sortability by how the column is declared",
            ["column", "SortMemberPath", "CanUserSort", "order after sorting"],
            await DataGridMeasurements.SortabilityAsync(),
            "datagrid-sortability.svg");
    }

    private static DataGrid BuildGrid()
    {
        var grid = new DataGrid
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

        return grid;
    }

    private static void ApplyInitialNameSort(DataGrid grid)
    {
        grid.Items.SortDescriptions.Clear();
        grid.Items.SortDescriptions.Add(
            new SortDescription(nameof(Product.Name), ListSortDirection.Ascending));
        grid.Columns[0].SortDirection = ListSortDirection.Ascending;
        grid.Items.Refresh();
    }

    private static void SortByPrice(DataGrid grid)
    {
        grid.Items.SortDescriptions.Clear();
        grid.Items.SortDescriptions.Add(
            new SortDescription(nameof(Product.Price), ListSortDirection.Descending));
        grid.Items.Refresh();
    }

    private static void SyncGlyphs(DataGrid grid)
    {
        foreach (DataGridColumn column in grid.Columns)
        {
            column.SortDirection = null;
        }

        grid.Columns[1].SortDirection = ListSortDirection.Descending;
    }
}
