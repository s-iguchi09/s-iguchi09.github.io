using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Grid」（apps/wpf-standard-control-demo/grid.md と日本語版）の記述を実測する。
/// </summary>
internal sealed class GridDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-grid";

    public string ImageDirectory => DemoProbe.ImageDirectory("grid");

    public IReadOnlyList<string> Verifies =>
    [
        "Grid.Row / Grid.Column の既定値と、指定しない子要素が配置されるセル",
        "定義数を超える Grid.Column / Grid.Row / Grid.ColumnSpan / Grid.RowSpan を与えた場合の配置と、負の値・0 を与えた場合の例外",
        "行・列の定義が無い Grid で子要素が配置される範囲",
        "Auto・固定値・比率（1* と 2*）の列幅と、Grid の幅を変えたときの比率列の追従、MinWidth / MaxWidth の効き方（Width や MaxWidth と矛盾した場合に MinWidth が優先されること）と、行の Height / MinHeight / MaxHeight も同じ規則で決まること",
        "幅や高さが無限に与えられる場所（横向き StackPanel、横スクロールを許した ScrollViewer）での比率列・比率行の幅と高さ、縦向き StackPanel と横スクロール無効の ScrollViewer では有限の幅になること",
        "ScrollViewer 内で Auto 行が可視領域を超えたときのスクロール範囲とスクロールバーの表示",
        "Background が null と Transparent のときの空き領域のヒットテスト結果",
        "Panel.ZIndex が同じ親の子の間でだけ前後関係を決め、入れ子の Grid の子には効かないこと",
        "IsSharedSizeScope の有無による SharedSizeGroup の列幅の同期と、比率列で共有した場合の幅",
        "ShowGridLines が追加するビジュアルの型と、線の色や種類を変える公開プロパティが無いこと",
        "列の種類（固定・Auto・比率）による子要素の Measure 回数と、入れ子の Grid と平坦な Grid の初回レイアウト時間の比",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Grid: placement (Grid 300 x 200)",
            [T("case", "条件"), T("measured", "計測値")],
            Placement(),
            "grid-placement.svg");

        await context.SaveTableAsync(
            "Grid: column widths and row heights",
            [T("case", "条件"), T("sizes", "大きさ")],
            Sizing(),
            "grid-sizing.svg");

        await context.SaveTableAsync(
            "Grid columns 1* | 2* with contents 60 and 30 wide, by container",
            [T("container", "入れ物"), T("sizes", "大きさ"), T("scroll bar", "スクロールバー")],
            Unbounded(),
            "grid-unbounded.svg");

        await context.SaveTableAsync(
            "Grid: hit testing and ZIndex",
            [T("case", "条件"), T("element hit", "当たった要素")],
            HitTesting(),
            "grid-hit-testing.svg");

        await context.SaveTableAsync(
            "Grid: SharedSizeGroup (contents 50 and 120 wide) and ShowGridLines",
            [T("case", "条件"), T("measured", "計測値")],
            SharedSizeAndGridLines(),
            "grid-shared-size.svg");

        await context.SaveTableAsync(
            "Grid: measure calls, and first-layout time ratio (3 runs, each the median of 15 alternating trials)",
            [T("case", "条件"), T("measured", "計測値")],
            LayoutCost(),
            "grid-layout-cost.svg");
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>列の幅の一覧。</summary>
    private static Loc Columns(string widths) => T($"columns {widths}", $"列 {widths}");

    private static Grid NewGrid(int columns, int rows)
    {
        var grid = new Grid();
        for (int i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (int i = 0; i < rows; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
        }

        return grid;
    }

    private static List<IReadOnlyList<Loc>> Placement()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        rows.Add([T("default of Grid.Row / Grid.Column (metadata)", "Grid.Row / Grid.Column の既定値（メタデータ）"),
            $"{Grid.RowProperty.DefaultMetadata.DefaultValue} / {Grid.ColumnProperty.DefaultMetadata.DefaultValue}"]);

        // 2 x 2 の Grid に、行・列を指定しない子を 2 つ置く。
        {
            Grid grid = NewGrid(2, 2);
            var a = new Border();
            var b = new Border();
            grid.Children.Add(a);
            grid.Children.Add(b);
            Layout(grid, 300, 200);
            rows.Add([T("2x2, two children without Grid.Row/Column", "2x2、Grid.Row/Column を指定しない子が 2 つ"),
                $"{Format(Bounds(a, grid))} | {Format(Bounds(b, grid))}"]);
        }

        // デモアプリの「Column:2 x Row:2」で Column=2 を選んだ状態と同じ。
        {
            Grid grid = NewGrid(2, 2);
            var child = new Border();
            Grid.SetColumn(child, 2);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add([T("2x2, Grid.Column=2 (out of range)", "2x2、Grid.Column=2（範囲外）"), Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(2, 2);
            var child = new Border();
            Grid.SetColumn(child, 5);
            Grid.SetRow(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add([T("2x2, Grid.Column=5, Grid.Row=5", "2x2、Grid.Column=5、Grid.Row=5"), Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(3, 2);
            var child = new Border();
            Grid.SetColumnSpan(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add([T("3x2, Grid.ColumnSpan=5", "3x2、Grid.ColumnSpan=5"), Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(3, 2);
            var child = new Border();
            Grid.SetColumn(child, 1);
            Grid.SetColumnSpan(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add([T("3x2, Grid.Column=1, ColumnSpan=5", "3x2、Grid.Column=1、ColumnSpan=5"), Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(3, 2);
            var child = new Border();
            Grid.SetRowSpan(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add([T("3x2, Grid.RowSpan=5", "3x2、Grid.RowSpan=5"), Format(Bounds(child, grid))]);
        }

        rows.Add(["Grid.SetColumn(child, -1)", Throws(() => Grid.SetColumn(new Border(), -1))]);
        rows.Add(["Grid.SetColumnSpan(child, 0)", Throws(() => Grid.SetColumnSpan(new Border(), 0))]);

        {
            var grid = new Grid();
            var a = new Border();
            var b = new Border { Width = 50, Height = 30 };
            Grid.SetColumn(b, 1);
            grid.Children.Add(a);
            grid.Children.Add(b);
            Layout(grid, 300, 200);
            rows.Add([T("no definitions, child A + child B (Column=1, 50x30)", "定義なし、子 A と子 B（Column=1、50x30）"),
                $"{Format(Bounds(a, grid))} | {Format(Bounds(b, grid))}"]);
        }

        return rows;
    }

    private static string Widths(Grid grid) =>
        string.Join(" / ", grid.ColumnDefinitions.Select(c => D(c.ActualWidth)));

    private static Grid SizingGrid(params ColumnDefinition[] columns)
    {
        var grid = new Grid();
        for (int i = 0; i < columns.Length; i++)
        {
            grid.ColumnDefinitions.Add(columns[i]);
            var child = new Border { Width = 60, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetColumn(child, i);
            grid.Children.Add(child);
        }

        return grid;
    }

    private static List<IReadOnlyList<Loc>> Sizing()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // 各列には幅 60 の子を置く。Auto 列はこの幅になるはずである。
        Grid Mixed() => SizingGrid(
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = new GridLength(80) },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        rows.Add([T("Auto | 80 | 1* | 2*  (Grid width 440, children 60 wide)", "Auto | 80 | 1* | 2*（Grid の幅 440、子の幅 60）"), Widths(Layout(Mixed(), 440, 100))]);

        Grid resized = Layout(Mixed(), 440, 100);
        Layout(resized, 740, 100);
        rows.Add([T("same Grid resized to 740", "同じ Grid を幅 740 に広げる"), Widths(resized)]);

        rows.Add([T("1* (MinWidth 200) | 1*  (width 300)", "1*（MinWidth 200） | 1*（Grid の幅 300）"), Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        rows.Add([T("1* (MaxWidth 50) | 1*  (width 300)", "1*（MaxWidth 50） | 1*（Grid の幅 300）"), Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 50 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        rows.Add([T("Auto (MaxWidth 30) | 1*  (width 300)", "Auto（MaxWidth 30） | 1*（Grid の幅 300）"), Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = GridLength.Auto, MaxWidth = 30 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        // デモアプリの初期値（Width=25, MinWidth=30, MaxWidth=100）と同じ指定。
        rows.Add([T("1* | 25 (MinWidth 30, MaxWidth 100) | 1*  (width 300)", "1* | 25（MinWidth 30、MaxWidth 100） | 1*（Grid の幅 300）"), Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(25), MinWidth = 30, MaxWidth = 100 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        rows.Add([T("1* (MinWidth 100, MaxWidth 50) | 1*  (width 300)", "1*（MinWidth 100、MaxWidth 50） | 1*（Grid の幅 300）"), Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 100, MaxWidth = 50 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        // 行も同じ規則で決まるか。デモアプリの初期値（Height=20, MinHeight=10, MaxHeight=100）を含める。
        foreach ((Loc label, RowDefinition definition) in new (Loc, RowDefinition)[]
        {
            (T("rows 1* | 20 (MinHeight 10, MaxHeight 100) | 1*", "行 1* | 20（MinHeight 10、MaxHeight 100） | 1*"), new RowDefinition { Height = new GridLength(20), MinHeight = 10, MaxHeight = 100 }),
            (T("rows 1* | 5 (MinHeight 10) | 1*", "行 1* | 5（MinHeight 10） | 1*"), new RowDefinition { Height = new GridLength(5), MinHeight = 10 }),
            (T("rows 1* | Auto (MaxHeight 15), content 40 | 1*", "行 1* | Auto（MaxHeight 15）、内容 40 | 1*"), new RowDefinition { Height = GridLength.Auto, MaxHeight = 15 }),
        })
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(definition);
            grid.RowDefinitions.Add(new RowDefinition());
            var child = new Border { Height = 40, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetRow(child, 1);
            grid.Children.Add(child);
            Layout(grid, 100, 120);
            rows.Add([T($"{label.En}  (height 120)", $"{label.Ja}（Grid の高さ 120）"),
                string.Join(" / ", grid.RowDefinitions.Select(r => D(r.ActualHeight)))]);
        }

        return rows;
    }

    private static List<IReadOnlyList<Loc>> Unbounded()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // 比率が保たれるかを見分けるため、1* 列の中身を 2* 列より広くしておく。
        Grid StarGrid()
        {
            Grid grid = SizingGrid(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            ((Border)grid.Children[1]).Width = 30;
            return grid;
        }

        {
            Grid grid = StarGrid();
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(grid);
            Layout(panel, 400, 100);
            rows.Add([T("StackPanel Horizontal, width 400", "横向きの StackPanel、幅 400"), Columns(Widths(grid)), "-"]);
        }

        {
            Grid grid = StarGrid();
            var panel = new StackPanel();
            panel.Children.Add(grid);
            Layout(panel, 400, 100);
            rows.Add([T("StackPanel Vertical, width 400", "縦向きの StackPanel、幅 400"), Columns(Widths(grid)), "-"]);
        }

        foreach ((ScrollBarVisibility visibility, double width) in new[]
        {
            (ScrollBarVisibility.Disabled, 400.0),
            (ScrollBarVisibility.Auto, 400.0),
            // 中身の合計（90）より狭い場合。横スクロールの可否で差が出るかを見る。
            (ScrollBarVisibility.Disabled, 60.0),
            (ScrollBarVisibility.Auto, 60.0),
        })
        {
            Grid grid = StarGrid();
            var viewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = visibility,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = grid,
            };
            Layout(viewer, width, 100);
            rows.Add([T($"ScrollViewer {width} wide, horizontal {visibility}", $"幅 {width} の ScrollViewer、横 {visibility}"),
                Columns(Widths(grid)),
                T($"horizontal {viewer.ComputedHorizontalScrollBarVisibility}", $"横 {viewer.ComputedHorizontalScrollBarVisibility}")]);
        }

        {
            var grid = new Grid();
            for (int i = 0; i < 2; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                var child = new Border { Height = 20 };
                Grid.SetRow(child, i);
                grid.Children.Add(child);
            }

            var viewer = new ScrollViewer { Content = grid };
            Layout(viewer, 300, 300);
            rows.Add([T("ScrollViewer 300 high; rows 1* | 1* (contents 20)", "高さ 300 の ScrollViewer、行 1* | 1*（内容 20）"),
                T($"rows {string.Join(" / ", grid.RowDefinitions.Select(r => D(r.ActualHeight)))}, scrollable {D(viewer.ScrollableHeight)}",
                  $"行 {string.Join(" / ", grid.RowDefinitions.Select(r => D(r.ActualHeight)))}、スクロールできる量 {D(viewer.ScrollableHeight)}"),
                "-"]);
        }

        {
            var grid = new Grid();
            for (int i = 0; i < 3; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var child = new Border { Height = 100 };
                Grid.SetRow(child, i);
                grid.Children.Add(child);
            }

            var viewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = grid };
            Layout(viewer, 300, 120);
            rows.Add([T("ScrollViewer 120 high, vertical Auto; rows Auto x3 (100 each)", "高さ 120 の ScrollViewer、縦 Auto、行 Auto x3（それぞれ 100）"),
                T($"Grid {D(grid.ActualHeight)}, scrollable {D(viewer.ScrollableHeight)}", $"Grid {D(grid.ActualHeight)}、スクロールできる量 {D(viewer.ScrollableHeight)}"),
                T($"vertical {viewer.ComputedVerticalScrollBarVisibility}", $"縦 {viewer.ComputedVerticalScrollBarVisibility}")]);
        }

        return rows;
    }

    private static List<IReadOnlyList<Loc>> HitTesting()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        foreach (Brush? background in new Brush?[] { null, Brushes.Transparent })
        {
            // 背後の要素と、空き領域だけの Grid を重ねる。
            var root = new Grid();
            root.Children.Add(new Border { Name = "Behind", Background = Brushes.White });
            root.Children.Add(new Grid { Name = "Front", Background = background });
            Layout(root, 200, 100);
            rows.Add([T($"front Grid Background={(background is null ? "null" : "Transparent")}, empty area", $"手前の Grid の Background={(background is null ? "null" : "Transparent")}、何も無い所"),
                HitName(root, new Point(100, 50))]);
        }

        {
            var root = new Grid();
            var a = new Border { Name = "First", Background = Brushes.Red };
            var b = new Border { Name = "Second", Background = Brushes.Blue };
            root.Children.Add(a);
            root.Children.Add(b);
            Layout(root, 200, 100);
            rows.Add([T("same cell, no ZIndex (First added first)", "同じセル、ZIndex なし（First を先に追加）"), HitName(root, new Point(100, 50))]);

            Panel.SetZIndex(a, 1);
            Layout(root, 200, 100);
            rows.Add([T("same cell, First ZIndex=1", "同じセル、First の ZIndex=1"), HitName(root, new Point(100, 50))]);
        }

        foreach (int nestedZIndex in new[] { 0, 1 })
        {
            // 入れ子の Grid の子に大きな ZIndex を付けても、外側の兄弟より前に出るかを見る。
            var root = new Grid();
            var nested = new Grid();
            var inner = new Border { Name = "Inner", Background = Brushes.Red };
            Panel.SetZIndex(inner, 100);
            nested.Children.Add(inner);
            Panel.SetZIndex(nested, nestedZIndex);
            root.Children.Add(nested);
            root.Children.Add(new Border { Name = "Outer", Background = Brushes.Blue });
            Layout(root, 200, 100);
            rows.Add([T($"Inner (ZIndex=100) in nested Grid (ZIndex={nestedZIndex}) vs later sibling Outer", $"入れ子の Grid（ZIndex={nestedZIndex}）の中の Inner（ZIndex=100）と、後から足した兄弟の Outer"),
                HitName(root, new Point(100, 50))]);
        }

        return rows;
    }

    private static Grid SharedRow(double labelWidth, GridLength width)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width, SharedSizeGroup = "Label" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new Border { Width = labelWidth, Height = 20 });
        return grid;
    }

    private static List<IReadOnlyList<Loc>> SharedSizeAndGridLines()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        foreach ((bool scope, GridLength width, string label) in new[]
        {
            (false, GridLength.Auto, "Auto"),
            (true, GridLength.Auto, "Auto"),
            (true, new GridLength(1, GridUnitType.Star), "1*"),
        })
        {
            var panel = new StackPanel();
            Grid.SetIsSharedSizeScope(panel, scope);
            Grid first = SharedRow(50, width);
            Grid second = SharedRow(120, width);
            panel.Children.Add(first);
            panel.Children.Add(second);
            Layout(panel, 400, 100);
            rows.Add([T($"IsSharedSizeScope={scope}, shared column Width={label}", $"IsSharedSizeScope={scope}、共有する列の Width={label}"),
                $"{D(first.ColumnDefinitions[0].ActualWidth)} / {D(second.ColumnDefinitions[0].ActualWidth)}"]);
        }

        foreach (bool show in new[] { false, true })
        {
            Grid grid = NewGrid(2, 2);
            grid.Children.Add(new Border());
            grid.ShowGridLines = show;
            Layout(grid, 200, 100);
            var visuals = Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(grid))
                .Select(i => VisualTreeHelper.GetChild(grid, i).GetType().Name);
            rows.Add([T($"ShowGridLines={show}, one child", $"ShowGridLines={show}、子は 1 つ"), T($"visual children: {string.Join(", ", visuals)}", $"visual の子: {string.Join(", ", visuals)}")]);
        }

        IEnumerable<string> lineProperties = typeof(Grid).GetProperties()
            .Where(p => p.Name.Contains("Line", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name);
        rows.Add([T("public Grid properties named *Line*", "名前に Line を含む Grid の public プロパティ"), string.Join(", ", lineProperties)]);

        return rows;
    }

    private static List<IReadOnlyList<Loc>> LayoutCost()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // 2 x 2 の Grid で、各セルの子が初回レイアウトで何回測られるかを数える。
        // Auto 列 x 比率行のセルと、比率列 x Auto 行のセルが同居すると、
        // 互いのサイズに依存するため測り直しが起きる。
        foreach ((string label, GridLength columnA, GridLength columnB, GridLength rowA, GridLength rowB) in new[]
        {
            ("columns 100,100 / rows 50,50",
                new GridLength(100), new GridLength(100), new GridLength(50), new GridLength(50)),
            ("columns Auto,Auto / rows Auto,Auto",
                GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto),
            ("columns 1*,1* / rows 1*,1*",
                new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star),
                new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star)),
            ("columns Auto,1* / rows 1*,Auto",
                GridLength.Auto, new GridLength(1, GridUnitType.Star),
                new GridLength(1, GridUnitType.Star), GridLength.Auto),
        })
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = columnA });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = columnB });
            grid.RowDefinitions.Add(new RowDefinition { Height = rowA });
            grid.RowDefinitions.Add(new RowDefinition { Height = rowB });
            var counters = new List<MeasureCounter>();
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    var counter = new MeasureCounter();
                    Grid.SetRow(counter, r);
                    Grid.SetColumn(counter, c);
                    grid.Children.Add(counter);
                    counters.Add(counter);
                }
            }

            Layout(grid, 300, 200);
            string calls = string.Join(" / ", counters.Select(c => c.MeasureCount));
            rows.Add([T($"2x2 {label}", $"2x2 {label.Replace("columns ", "列 ").Replace("rows ", "行 ")}"), T("measure calls " + calls, "測定の回数 " + calls)]);
        }

        const int rowCount = 50;
        const int columnCount = 10;

        UIElement Flat() => FlatGrid(new GridLength(1, GridUnitType.Star));

        UIElement FlatAuto() => FlatGrid(GridLength.Auto);

        UIElement FlatGrid(GridLength columnWidth)
        {
            var grid = new Grid();
            for (int c = 0; c < columnCount; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = columnWidth });
            }

            for (int r = 0; r < rowCount; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int c = 0; c < columnCount; c++)
                {
                    var text = new TextBlock { Text = $"R{r}C{c}" };
                    Grid.SetRow(text, r);
                    Grid.SetColumn(text, c);
                    grid.Children.Add(text);
                }
            }

            return grid;
        }

        UIElement Nested()
        {
            var outer = new Grid();
            for (int r = 0; r < rowCount; r++)
            {
                outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var inner = new Grid();
                for (int c = 0; c < columnCount; c++)
                {
                    inner.ColumnDefinitions.Add(new ColumnDefinition());
                    var text = new TextBlock { Text = $"R{r}C{c}" };
                    Grid.SetColumn(text, c);
                    inner.Children.Add(text);
                }

                Grid.SetRow(inner, r);
                outer.Children.Add(inner);
            }

            return outer;
        }

        // 各 TextBlock を 3 重の Grid で包む。要素数は平坦な場合の 4 倍になる。
        UIElement Wrapped()
        {
            var grid = (Grid)Flat();
            foreach (TextBlock text in grid.Children.OfType<TextBlock>().ToList())
            {
                int row = Grid.GetRow(text);
                int column = Grid.GetColumn(text);
                grid.Children.Remove(text);

                UIElement wrapped = text;
                for (int depth = 0; depth < 3; depth++)
                {
                    var wrapper = new Grid();
                    wrapper.Children.Add(wrapped);
                    wrapped = wrapper;
                }

                Grid.SetRow(wrapped, row);
                Grid.SetColumn(wrapped, column);
                grid.Children.Add(wrapped);
            }

            return grid;
        }

        // どれも「平坦な Grid」と交互に測り、平坦な Grid に対する比を出す。
        // 1 回の比は実行ごとに 1 割ほど揺れるため、3 回繰り返して範囲を示す。
        foreach ((Loc label, Func<UIElement> build) in new (Loc, Func<UIElement>)[]
        {
            (T("each row in a nested Grid (+50 Grids)", "行ごとに入れ子の Grid（+50 個）"), Nested),
            (T("each TextBlock in 3 nested Grids (+1,500)", "TextBlock ごとに 3 重の入れ子の Grid（+1,500 個）"), Wrapped),
            (T("Auto columns instead of 1*", "1* の代わりに Auto の列"), FlatAuto),
        })
        {
            var ratios = new List<string>();
            for (int run = 0; run < 3; run++)
            {
                (double flat, double other) = CompareLayoutTime(Flat, build, 800, 2000);
                ratios.Add($"x{other / flat:0.00}");
            }

            rows.Add([T($"500 TextBlocks, vs flat Grid: {label.En}", $"TextBlock 500 個、平坦な Grid との比: {label.Ja}"), string.Join(", ", ratios)]);
        }

        return rows;
    }
}
