using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using ScreenshotCapture.Scenes.DataGridSortReset;
using static ScreenshotCapture.Scenes.DemoProbe;

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
        "列ヘッダーは実際のマウスで 1 回ずつクリックし（クリックの間はダブルクリックの時間より長く空ける）、Shift は実際のキー入力で押したままにすること。途中の手順の後に、列の SortDirection が標準の並べ替えのとおりになっていなければ計測を止めること",
        "Category を降順にしてから Shift + クリックで Name を足し、Shift なしで Category をクリックすると、対象列と同じ PropertyName の最初の条件だけを外す以前のハンドラーでは Name の条件が残って Name の矢印が消え、記事の TriStateSort では条件も矢印も残らないこと",
        "同じ状態から Shift を押しながら Category をクリックすると、どちらのハンドラーでも Name の条件と矢印が残ること",
        "Category でグループ化（PropertyGroupDescription）して Category を降順にしてからもう一度クリックすると、以前のハンドラーではグループ化用の昇順の条件が外れて降順の条件と並びが残り、記事の TriStateSort ではグループ化用の条件だけが残ること",
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
            [T("operation", "操作"), "SortDescriptions", "column.SortDirection", T("order", "並び順")],
            await DataGridMeasurements.SortStateAsync(),
            "datagrid-sort-state.svg");

        await context.SaveTableAsync(
            "ListCollectionView with CustomSort, then the article's ClearSort",
            [T("step", "手順"), "CustomSort", "SortDescriptions", T("order (Price)", "並び順（Price）")],
            CustomSortThenClear(),
            "datagrid-sort-customsort.svg");

        var triState = new List<IReadOnlyList<Loc>>();
        foreach (Scenario scenario in Enum.GetValues<Scenario>())
        {
            foreach (bool improved in new[] { false, true })
            {
                triState.Add(await MeasureTriStateAsync(scenario, improved));
            }
        }

        await context.SaveTableAsync(
            "Clicking a descending column again with each Sorting handler",
            [
                T("operation", "操作"),
                T("handler", "ハンドラー"),
                "SortDescriptions",
                T("column arrows", "列の矢印"),
                T("order (Name)", "並び順（Name）"),
            ],
            triState,
            "datagrid-tristate-sort.svg");
    }

    private enum Scenario
    {
        /// <summary>Category を降順、Shift + クリックで Name を足し、Shift なしで Category をクリックする。</summary>
        PlainClickInMultiSort,

        /// <summary>同じ状態から、Shift を押しながら Category をクリックする。</summary>
        ShiftClickInMultiSort,

        /// <summary>Category でグループ化し、Category を降順にしてからもう一度クリックする。</summary>
        GroupedBySameProperty,
    }

    private const ushort VirtualKeyShift = 0x10;

    private static Loc Describe(Scenario scenario) => scenario switch
    {
        Scenario.PlainClickInMultiSort => T(
            "Category desc, Shift+click Name, then click Category (no Shift)",
            "Category を降順、Shift + クリックで Name を追加、Shift なしで Category をクリック"),
        Scenario.ShiftClickInMultiSort => T(
            "Category desc, Shift+click Name, then Shift+click Category",
            "Category を降順、Shift + クリックで Name を追加、Shift を押しながら Category をクリック"),
        _ => T(
            "grouped by Category, Category desc, then click Category",
            "Category でグループ化、Category を降順、Category をクリック"),
    };

    /// <summary>
    /// 3 列の DataGrid を表示し、実際のマウスで列ヘッダーを押して、降順の Category をもう一度クリックした後の状態を測る。
    /// <paramref name="improved"/> が真なら記事の TriStateSort を、偽なら以前の版のハンドラーを Sorting につなぐ。
    /// </summary>
    private static async Task<IReadOnlyList<Loc>> MeasureTriStateAsync(Scenario scenario, bool improved)
    {
        RealKeyboard.EnsureNoStuckKey();
        DataGrid grid = BuildGrid();
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Category",
            Binding = new Binding(nameof(Product.Category)),
            SortMemberPath = nameof(Product.Category),
            Width = 90,
        });
        grid.Sorting += (sender, e) =>
        {
            if (improved)
            {
                TriStateSort.OnSorting((DataGrid)sender, e);
            }
            else
            {
                FirstMatchSort.OnSorting((DataGrid)sender, e);
            }
        };

        if (scenario == Scenario.GroupedBySameProperty)
        {
            grid.Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Product.Category)));
        }

        var window = new Window
        {
            Title = "DataGrid Tri-State Sort",
            Content = grid,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        try
        {
            await Capture.ShowAndSettleAsync(window);
            DataGridColumn name = grid.Columns[0];
            DataGridColumn category = grid.Columns[2];

            using (RealMouse.Preserve())
            {
                await ClickHeaderAsync(window, grid, category, shift: false);
                Expect(category, ListSortDirection.Ascending);
                await ClickHeaderAsync(window, grid, category, shift: false);
                Expect(category, ListSortDirection.Descending);

                if (scenario != Scenario.GroupedBySameProperty)
                {
                    await ClickHeaderAsync(window, grid, name, shift: true);
                    Expect(category, ListSortDirection.Descending);
                    Expect(name, ListSortDirection.Ascending);
                }

                await ClickHeaderAsync(window, grid, category, shift: scenario == Scenario.ShiftClickInMultiSort);
            }

            Loc sorts = grid.Items.SortDescriptions.Count == 0
                ? T("none", "なし")
                : string.Join(", ", grid.Items.SortDescriptions.Select(sd =>
                    $"{sd.PropertyName} {(sd.Direction == ListSortDirection.Ascending ? "asc" : "desc")}"));
            string[] arrows = grid.Columns
                .Where(column => column.SortDirection is not null)
                .Select(column => $"{column.Header} {(column.SortDirection == ListSortDirection.Ascending ? "asc" : "desc")}")
                .ToArray();
            string order = string.Join(", ", grid.Items.Cast<Product>().Select(product => product.Name));

            return
            [
                Describe(scenario),
                improved ? T("TriStateSort (this article)", "TriStateSort（この記事）") : T("removes the first match (FirstOrDefault)", "最初に一致した条件を外す（FirstOrDefault）"),
                sorts,
                arrows.Length == 0 ? T("none", "なし") : string.Join(", ", arrows),
                order,
            ];
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>列ヘッダーを実際のマウスで 1 回クリックする。前のクリックと 1 続きにならないよう、先にダブルクリックの時間より長く待つ。</summary>
    private static async Task ClickHeaderAsync(Window window, DataGrid grid, DataGridColumn column, bool shift)
    {
        await Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 200);
        DataGridColumnHeader header = Descendants(grid)
            .OfType<DataGridColumnHeader>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Column, column))
            ?? throw new InvalidOperationException("列ヘッダーが生成されていない。");

        if (shift)
        {
            using (RealKeyboard.Hold(window, VirtualKeyShift))
            {
                await RealMouse.ClickAsync(window, header);
            }
        }
        else
        {
            await RealMouse.ClickAsync(window, header);
        }

        await Capture.SettleAsync(window, 200);
    }

    /// <summary>途中の手順の後の状態が標準の並べ替えのとおりでなければ、クリックが届いていないので止める。</summary>
    private static void Expect(DataGridColumn column, ListSortDirection direction)
    {
        if (column.SortDirection != direction)
        {
            throw new InvalidOperationException(
                $"{column.Header} の SortDirection が {direction} ではない（{column.SortDirection?.ToString() ?? "null"}）。クリックが届いていない。");
        }
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>
    /// 記事の ViewModel の ClearSort は SortDescriptions だけを消す。CustomSort を設定したビューで、それが残るかを測る。
    /// </summary>
    private static List<IReadOnlyList<Loc>> CustomSortThenClear()
    {
        var view = new ListCollectionView(SampleData.Products().ToList());
        var rows = new List<IReadOnlyList<Loc>>();

        rows.Add(Row(T("initial", "最初")));

        view.CustomSort = new ComparerAdapter(Comparer<Product>.Create((a, b) => b.Price.CompareTo(a.Price)));
        rows.Add(Row(T("CustomSort = Price descending", "CustomSort = Price の降順")));

        // 記事の ClearSort と同じ手順。
        view.SortDescriptions.Clear();
        view.Refresh();
        rows.Add(Row("SortDescriptions.Clear() + Refresh()"));

        view.CustomSort = null;
        view.Refresh();
        rows.Add(Row("CustomSort = null + Refresh()"));

        return rows;

        IReadOnlyList<Loc> Row(Loc step) =>
        [
            step,
            view.CustomSort is null ? "null" : T("set", "設定あり"),
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
