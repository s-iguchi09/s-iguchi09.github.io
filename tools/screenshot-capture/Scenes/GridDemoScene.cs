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
            ["case", "measured"],
            Placement(),
            "grid-placement.svg");

        await context.SaveTableAsync(
            "Grid: column widths and row heights",
            ["case", "sizes"],
            Sizing(),
            "grid-sizing.svg");

        await context.SaveTableAsync(
            "Grid columns 1* | 2* with contents 60 and 30 wide, by container",
            ["container", "sizes", "scroll bar"],
            Unbounded(),
            "grid-unbounded.svg");

        await context.SaveTableAsync(
            "Grid: hit testing and ZIndex",
            ["case", "element hit"],
            HitTesting(),
            "grid-hit-testing.svg");

        await context.SaveTableAsync(
            "Grid: SharedSizeGroup (contents 50 and 120 wide) and ShowGridLines",
            ["case", "measured"],
            SharedSizeAndGridLines(),
            "grid-shared-size.svg");

        await context.SaveTableAsync(
            "Grid: measure calls, and first-layout time ratio (3 runs, each the median of 15 alternating trials)",
            ["case", "measured"],
            LayoutCost(),
            "grid-layout-cost.svg");
    }

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

    private static List<IReadOnlyList<string>> Placement()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["default of Grid.Row / Grid.Column (metadata)",
            $"{Grid.RowProperty.DefaultMetadata.DefaultValue} / {Grid.ColumnProperty.DefaultMetadata.DefaultValue}"]);

        // 2 x 2 の Grid に、行・列を指定しない子を 2 つ置く。
        {
            Grid grid = NewGrid(2, 2);
            var a = new Border();
            var b = new Border();
            grid.Children.Add(a);
            grid.Children.Add(b);
            Layout(grid, 300, 200);
            rows.Add(["2x2, two children without Grid.Row/Column",
                $"{Format(Bounds(a, grid))} | {Format(Bounds(b, grid))}"]);
        }

        // デモアプリの「Column:2 x Row:2」で Column=2 を選んだ状態と同じ。
        {
            Grid grid = NewGrid(2, 2);
            var child = new Border();
            Grid.SetColumn(child, 2);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add(["2x2, Grid.Column=2 (out of range)", Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(2, 2);
            var child = new Border();
            Grid.SetColumn(child, 5);
            Grid.SetRow(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add(["2x2, Grid.Column=5, Grid.Row=5", Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(3, 2);
            var child = new Border();
            Grid.SetColumnSpan(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add(["3x2, Grid.ColumnSpan=5", Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(3, 2);
            var child = new Border();
            Grid.SetColumn(child, 1);
            Grid.SetColumnSpan(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add(["3x2, Grid.Column=1, ColumnSpan=5", Format(Bounds(child, grid))]);
        }

        {
            Grid grid = NewGrid(3, 2);
            var child = new Border();
            Grid.SetRowSpan(child, 5);
            grid.Children.Add(child);
            Layout(grid, 300, 200);
            rows.Add(["3x2, Grid.RowSpan=5", Format(Bounds(child, grid))]);
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
            rows.Add(["no definitions, child A + child B (Column=1, 50x30)",
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

    private static List<IReadOnlyList<string>> Sizing()
    {
        var rows = new List<IReadOnlyList<string>>();

        // 各列には幅 60 の子を置く。Auto 列はこの幅になるはずである。
        Grid Mixed() => SizingGrid(
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = new GridLength(80) },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        rows.Add(["Auto | 80 | 1* | 2*  (Grid width 440, children 60 wide)", Widths(Layout(Mixed(), 440, 100))]);

        Grid resized = Layout(Mixed(), 440, 100);
        Layout(resized, 740, 100);
        rows.Add(["same Grid resized to 740", Widths(resized)]);

        rows.Add(["1* (MinWidth 200) | 1*  (width 300)", Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        rows.Add(["1* (MaxWidth 50) | 1*  (width 300)", Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 50 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        rows.Add(["Auto (MaxWidth 30) | 1*  (width 300)", Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = GridLength.Auto, MaxWidth = 30 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        // デモアプリの初期値（Width=25, MinWidth=30, MaxWidth=100）と同じ指定。
        rows.Add(["1* | 25 (MinWidth 30, MaxWidth 100) | 1*  (width 300)", Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(25), MinWidth = 30, MaxWidth = 100 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        rows.Add(["1* (MinWidth 100, MaxWidth 50) | 1*  (width 300)", Widths(Layout(SizingGrid(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 100, MaxWidth = 50 },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }), 300, 100))]);

        // 行も同じ規則で決まるか。デモアプリの初期値（Height=20, MinHeight=10, MaxHeight=100）を含める。
        foreach ((string label, RowDefinition definition) in new (string, RowDefinition)[]
        {
            ("rows 1* | 20 (MinHeight 10, MaxHeight 100) | 1*", new RowDefinition { Height = new GridLength(20), MinHeight = 10, MaxHeight = 100 }),
            ("rows 1* | 5 (MinHeight 10) | 1*", new RowDefinition { Height = new GridLength(5), MinHeight = 10 }),
            ("rows 1* | Auto (MaxHeight 15), content 40 | 1*", new RowDefinition { Height = GridLength.Auto, MaxHeight = 15 }),
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
            rows.Add([$"{label}  (height 120)",
                string.Join(" / ", grid.RowDefinitions.Select(r => D(r.ActualHeight)))]);
        }

        return rows;
    }

    private static List<IReadOnlyList<string>> Unbounded()
    {
        var rows = new List<IReadOnlyList<string>>();

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
            rows.Add(["StackPanel Horizontal, width 400", $"columns {Widths(grid)}", "-"]);
        }

        {
            Grid grid = StarGrid();
            var panel = new StackPanel();
            panel.Children.Add(grid);
            Layout(panel, 400, 100);
            rows.Add(["StackPanel Vertical, width 400", $"columns {Widths(grid)}", "-"]);
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
            rows.Add([$"ScrollViewer {width} wide, horizontal {visibility}",
                $"columns {Widths(grid)}",
                $"horizontal {viewer.ComputedHorizontalScrollBarVisibility}"]);
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
            rows.Add(["ScrollViewer 300 high; rows 1* | 1* (contents 20)",
                $"rows {string.Join(" / ", grid.RowDefinitions.Select(r => D(r.ActualHeight)))}, " +
                $"scrollable {D(viewer.ScrollableHeight)}",
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
            rows.Add(["ScrollViewer 120 high, vertical Auto; rows Auto x3 (100 each)",
                $"Grid {D(grid.ActualHeight)}, scrollable {D(viewer.ScrollableHeight)}",
                $"vertical {viewer.ComputedVerticalScrollBarVisibility}"]);
        }

        return rows;
    }

    private static List<IReadOnlyList<string>> HitTesting()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (Brush? background in new Brush?[] { null, Brushes.Transparent })
        {
            // 背後の要素と、空き領域だけの Grid を重ねる。
            var root = new Grid();
            root.Children.Add(new Border { Name = "Behind", Background = Brushes.White });
            root.Children.Add(new Grid { Name = "Front", Background = background });
            Layout(root, 200, 100);
            rows.Add([$"front Grid Background={(background is null ? "null" : "Transparent")}, empty area",
                HitName(root, new Point(100, 50))]);
        }

        {
            var root = new Grid();
            var a = new Border { Name = "First", Background = Brushes.Red };
            var b = new Border { Name = "Second", Background = Brushes.Blue };
            root.Children.Add(a);
            root.Children.Add(b);
            Layout(root, 200, 100);
            rows.Add(["same cell, no ZIndex (First added first)", HitName(root, new Point(100, 50))]);

            Panel.SetZIndex(a, 1);
            Layout(root, 200, 100);
            rows.Add(["same cell, First ZIndex=1", HitName(root, new Point(100, 50))]);
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
            rows.Add([$"Inner (ZIndex=100) in nested Grid (ZIndex={nestedZIndex}) vs later sibling Outer",
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

    private static List<IReadOnlyList<string>> SharedSizeAndGridLines()
    {
        var rows = new List<IReadOnlyList<string>>();

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
            rows.Add([$"IsSharedSizeScope={scope}, shared column Width={label}",
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
            rows.Add([$"ShowGridLines={show}, one child", $"visual children: {string.Join(", ", visuals)}"]);
        }

        IEnumerable<string> lineProperties = typeof(Grid).GetProperties()
            .Where(p => p.Name.Contains("Line", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name);
        rows.Add(["public Grid properties named *Line*", string.Join(", ", lineProperties)]);

        return rows;
    }

    private static List<IReadOnlyList<string>> LayoutCost()
    {
        var rows = new List<IReadOnlyList<string>>();

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
            rows.Add([$"2x2 {label}", "measure calls " + string.Join(" / ", counters.Select(c => c.MeasureCount))]);
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
        foreach ((string label, Func<UIElement> build) in new (string, Func<UIElement>)[]
        {
            ("each row in a nested Grid (+50 Grids)", Nested),
            ("each TextBlock in 3 nested Grids (+1,500)", Wrapped),
            ("Auto columns instead of 1*", FlatAuto),
        })
        {
            var ratios = new List<string>();
            for (int run = 0; run < 3; run++)
            {
                (double flat, double other) = CompareLayoutTime(Flat, build, 800, 2000);
                ratios.Add($"x{other / flat:0.00}");
            }

            rows.Add([$"500 TextBlocks, vs flat Grid: {label}", string.Join(", ", ratios)]);
        }

        return rows;
    }
}
