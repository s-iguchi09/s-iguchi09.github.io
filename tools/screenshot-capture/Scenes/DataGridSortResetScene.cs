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
    public IReadOnlyList<string> Verifies =>
    [
        "並び替えの状態が ICollectionView の SortDescriptions と列の SortDirection に分かれていること",
        "SortDescriptions を消しただけでは列の SortDirection が残ること（ヘッダーの矢印が消えない）",
        "SortDescriptions を足しただけでは列の SortDirection が付かないこと",
        "SortDescriptions を 2 つ足すと複数列ソートになること（1 つ目の条件が同点の行で、2 つ目の条件が効くこと）",
        "ItemsSource に渡した ICollectionView の SortDescriptions を消しても、列の SortDirection が残ること",
        "コードから一方だけを操作すると 2 か所が食い違うのに対し、列ヘッダークリックで走る標準の並び替えでは両方が同時に更新されること",
        "ListCollectionView.CustomSort を設定したビューに、記事の ClearSort（SortDescriptions.Clear と Refresh）を呼んでも CustomSort が残るか",
    ];

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

        await context.SaveTableAsync(
            "sort state after each operation",
            ["operation", "SortDescriptions", "column.SortDirection", "order"],
            await DataGridMeasurements.SortStateAsync(),
            "datagrid-sort-state.svg");

        await context.SaveTableAsync(
            "ListCollectionView with CustomSort, then the article's ClearSort",
            ["step", "CustomSort", "SortDescriptions", "order (Price)"],
            CustomSortThenClear(),
            "datagrid-sort-customsort.svg");
    }

    /// <summary>
    /// 記事の ViewModel の ClearSort は SortDescriptions だけを消す。CustomSort を設定したビューで、それが残るかを測る。
    /// </summary>
    private static List<IReadOnlyList<string>> CustomSortThenClear()
    {
        var view = new ListCollectionView(SampleData.Products().ToList());
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(Row("initial"));

        view.CustomSort = new ComparerAdapter(Comparer<Product>.Create((a, b) => b.Price.CompareTo(a.Price)));
        rows.Add(Row("CustomSort = Price descending"));

        // 記事の ClearSort と同じ手順。
        view.SortDescriptions.Clear();
        view.Refresh();
        rows.Add(Row("SortDescriptions.Clear() + Refresh()"));

        view.CustomSort = null;
        view.Refresh();
        rows.Add(Row("CustomSort = null + Refresh()"));

        return rows;

        IReadOnlyList<string> Row(string step) =>
        [
            step,
            view.CustomSort is null ? "null" : "set",
            view.SortDescriptions.Count.ToString(),
            string.Join(", ", view.Cast<Product>().Select(p => p.Price)),
        ];
    }

    /// <summary>CustomSort は非ジェネリックの IComparer を受け取るため、型付きの比較子を包む。</summary>
    private sealed class ComparerAdapter(IComparer<Product> inner) : System.Collections.IComparer
    {
        public int Compare(object? x, object? y) => inner.Compare((Product)x!, (Product)y!);
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
