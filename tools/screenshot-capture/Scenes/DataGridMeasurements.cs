using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// DataGrid の並び替え状態と、編集時のテンプレート切り替えを実測する部品。
/// </summary>
internal static class DataGridMeasurements
{
    private sealed class Row
    {
        public required string Name { get; init; }

        public required int Score { get; init; }
    }

    private static ObservableCollection<Row> Sample() =>
    [
        new Row { Name = "carol", Score = 20 },
        new Row { Name = "alice", Score = 30 },
        new Row { Name = "bob", Score = 10 },
    ];

    private static DataGrid BuildGrid(out DataGridTextColumn nameColumn)
    {
        nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Row.Name)),
        };

        var grid = new DataGrid
        {
            ItemsSource = Sample(),
            AutoGenerateColumns = false,
            // 新規行のプレースホルダーが Items に混ざると測定の邪魔になる。
            CanUserAddRows = false,
            Height = 140,
        };

        grid.Columns.Add(nameColumn);
        grid.Columns.Add(new DataGridTextColumn { Header = "Score", Binding = new Binding(nameof(Row.Score)) });
        return grid;
    }

    /// <summary>
    /// 並び替えの状態が 2 か所に分かれていることを測る。
    ///
    /// 記事の要点は「<c>SortDescriptions</c> を消しただけではヘッダーの矢印が残る」ことなので、
    /// ビューの並び順と列の <c>SortDirection</c> を別々に出す。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> SortStateAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(await MeasureAsync("initial", (_, _) => { }));

        rows.Add(await MeasureAsync("SortDescriptions.Add only", (grid, _) =>
        {
            grid.Items.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
            grid.Items.Refresh();
        }));

        rows.Add(await MeasureAsync("+ column.SortDirection", (grid, column) =>
        {
            grid.Items.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
            grid.Items.Refresh();
            column.SortDirection = ListSortDirection.Ascending;
        }));

        rows.Add(await MeasureAsync("then SortDescriptions.Clear() only", (grid, column) =>
        {
            grid.Items.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
            grid.Items.Refresh();
            column.SortDirection = ListSortDirection.Ascending;

            grid.Items.SortDescriptions.Clear();
            grid.Items.Refresh();
        }));

        rows.Add(await MeasureAsync("+ clear column.SortDirection", (grid, column) =>
        {
            grid.Items.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
            grid.Items.Refresh();
            column.SortDirection = ListSortDirection.Ascending;

            grid.Items.SortDescriptions.Clear();
            grid.Items.Refresh();
            column.SortDirection = null;
        }));

        // 複数列ソートの確認には、1 つ目の条件（Score）が同点の行が要る。同点が無いと 2 つ目の条件（Name）が
        // 効いているかどうかが並びに現れない。ほかの行と共有するデータは変えず、この 2 行でだけ同点の行を足す。
        rows.Add(await MeasureAsync("one SortDescription (Score desc), with a tie", (grid, _) =>
        {
            AddTie(grid);
            grid.Items.SortDescriptions.Add(new SortDescription(nameof(Row.Score), ListSortDirection.Descending));
            grid.Items.Refresh();
        }));

        rows.Add(await MeasureAsync("two SortDescriptions (Score desc, Name asc), with a tie", (grid, _) =>
        {
            AddTie(grid);
            grid.Items.SortDescriptions.Add(new SortDescription(nameof(Row.Score), ListSortDirection.Descending));
            grid.Items.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
            grid.Items.Refresh();
        }));

        // 記事の ViewModel の ClearSort と同じく、ItemsSource に渡した ICollectionView の SortDescriptions を消す。
        rows.Add(await MeasureAsync("ItemsSource = ICollectionView; view.SortDescriptions.Clear()", (grid, column) =>
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
            grid.ItemsSource = view;
            view.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
            column.SortDirection = ListSortDirection.Ascending;
            view.SortDescriptions.Clear();
        }));

        // ここまでの行はすべてコードから直接操作している。
        // 列ヘッダー経由の標準ソートでは 2 か所が同時に変わるため、その対照を置く。
        rows.Add(await MeasureAsync("column header click (standard sort)", (grid, column) =>
            ClickColumnHeader(grid, column)));

        return rows;
    }

    /// <summary>
    /// 列ヘッダーのクリックで走る標準のソート処理を実行する。
    ///
    /// マウスイベントを組み立てて投げても、<c>ButtonBase</c> は実際のデバイスの
    /// ボタン状態を見るためクリックとして扱われない。そこで、ヘッダーがクリック時に
    /// 呼ぶ <c>OnClick</c> をそのまま呼び、その先の <c>DataGrid</c> 側のソート処理を走らせる。
    /// 入力の伝搬だけを飛ばしており、並び替えの処理自体は標準のものである。
    /// </summary>
    private static void ClickColumnHeader(DataGrid grid, DataGridColumn column)
    {
        grid.UpdateLayout();

        DataGridColumnHeader header = Descendants(grid)
            .OfType<DataGridColumnHeader>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Column, column))
            ?? throw new InvalidOperationException("列ヘッダーが生成されていない。");

        MethodInfo onClick = typeof(DataGridColumnHeader).GetMethod(
            "OnClick", BindingFlags.Instance | BindingFlags.NonPublic, binder: null, Type.EmptyTypes, modifiers: null)
            ?? throw new InvalidOperationException("DataGridColumnHeader.OnClick が見つからない。");

        onClick.Invoke(header, null);
        grid.UpdateLayout();
    }

    /// <summary>carol と同じ Score の行を、名前の順と元の並びが食い違う位置（末尾）に足す。</summary>
    private static void AddTie(DataGrid grid)
        => ((ObservableCollection<Row>)grid.ItemsSource).Add(new Row { Name = "anna", Score = 20 });

    private static async Task<IReadOnlyList<string>> MeasureAsync(
        string label, Action<DataGrid, DataGridTextColumn> operate)
    {
        DataGrid grid = BuildGrid(out DataGridTextColumn nameColumn);

        var host = new Grid();
        host.Children.Add(grid);

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                label,
                host,
                _ =>
                [
                    grid.Items.SortDescriptions.Count.ToString(),
                    nameColumn.SortDirection?.ToString() ?? "null",
                    string.Join(", ", grid.Items.OfType<Row>().Select(row => row.Name)),
                ],
                Act: _ =>
                {
                    operate(grid, nameColumn);
                    grid.UpdateLayout();
                    return Task.CompletedTask;
                }),
        ]);

        return measured[0];
    }

    /// <summary>
    /// 表示中と編集中で、セルに置かれる要素が入れ替わることを測る。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> EditingTemplateAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(await MeasureCellAsync("display (not editing)", beginEdit: false));
        rows.Add(await MeasureCellAsync("editing (BeginEdit)", beginEdit: true));

        return rows;
    }

    private static async Task<IReadOnlyList<string>> MeasureCellAsync(string label, bool beginEdit)
    {
        var items = Sample();

        // 表示用は TextBlock、編集用は ComboBox にして、入れ替わりが型で分かるようにする。
        var display = new DataTemplate();
        var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
        displayFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(Row.Name)));
        display.VisualTree = displayFactory;

        var editing = new DataTemplate();
        var editingFactory = new FrameworkElementFactory(typeof(ComboBox));
        editingFactory.SetValue(ComboBox.IsEditableProperty, true);
        editingFactory.SetBinding(ComboBox.TextProperty, new Binding(nameof(Row.Name)));
        editing.VisualTree = editingFactory;

        var column = new DataGridTemplateColumn
        {
            Header = "Name",
            CellTemplate = display,
            CellEditingTemplate = editing,
        };

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            // 新規行のプレースホルダーが Items に混ざると測定の邪魔になる。
            CanUserAddRows = false,
            Height = 140,
        };
        grid.Columns.Add(column);

        var host = new Grid();
        host.Children.Add(grid);

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                label,
                host,
                _ =>
                {
                    string[] found = Descendants(grid)
                        .Where(element => element is TextBlock or ComboBox)
                        .Select(element => element.GetType().Name)
                        .Distinct()
                        .ToArray();

                    // セルが編集モードに入っているかも併せて出す。
                    DataGridCell? cell = Descendants(grid).OfType<DataGridCell>().FirstOrDefault();

                    return
                    [
                        found.Contains("ComboBox") ? "ComboBox" : "TextBlock",
                        cell is null ? "(no cell)" : WpfProbe.Describe(cell.IsEditing),
                    ];
                },
                Act: _ =>
                {
                    grid.UpdateLayout();

                    if (beginEdit)
                    {
                        grid.CurrentCell = new DataGridCellInfo(items[0], column);
                        grid.BeginEdit();
                        grid.UpdateLayout();
                    }

                    return Task.CompletedTask;
                }),
        ]);

        return measured[0];
    }

    /// <summary>
    /// 列の作り方を変えて、並び替えの可否がどう決まるかを測る。
    ///
    /// 記事は「SortMemberPath が解決できる限り標準で並び替え可能」と書いている。
    /// SortMemberPath を明示しない場合に何が入るのか、
    /// テンプレート列のように Binding を持たない場合はどうなるのかを確かめる。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> SortabilityAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(await MeasureColumnAsync("DataGridTextColumn, Binding only", () =>
            new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Row.Name)) }));

        rows.Add(await MeasureColumnAsync("+ explicit SortMemberPath=Score", () =>
            new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(Row.Name)),
                SortMemberPath = nameof(Row.Score),
            }));

        rows.Add(await MeasureColumnAsync("+ CanUserSort=False", () =>
            new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(Row.Name)),
                CanUserSort = false,
            }));

        rows.Add(await MeasureColumnAsync("DataGridTemplateColumn, no SortMemberPath", () =>
            new DataGridTemplateColumn { Header = "Name", CellTemplate = new DataTemplate() }));

        rows.Add(await MeasureColumnAsync("DataGridTemplateColumn + SortMemberPath=Name", () =>
            new DataGridTemplateColumn { Header = "Name", CellTemplate = new DataTemplate(), SortMemberPath = nameof(Row.Name) }));

        return rows;
    }

    private static async Task<IReadOnlyList<string>> MeasureColumnAsync(string label, Func<DataGridColumn> build)
    {
        DataGridColumn column = build();
        var grid = new DataGrid
        {
            ItemsSource = Sample(),
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            Height = 140,
        };
        grid.Columns.Add(column);

        var host = new Grid();
        host.Children.Add(grid);

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                label,
                host,
                _ =>
                [
                    column.SortMemberPath.Length == 0 ? "(empty)" : column.SortMemberPath,
                    WpfProbe.Describe(column.CanUserSort),
                    string.Join(", ", grid.Items.OfType<Row>().Select(row => row.Name)),
                ],
                Act: _ =>
                {
                    // どの列も、列ヘッダーのクリックで走る標準の並べ替えを実行する。
                    // 並べ替えられない列は、ここで何も起きないことが結果として表に出る。
                    ClickColumnHeader(grid, column);
                    grid.UpdateLayout();
                    return Task.CompletedTask;
                }),
        ]);

        return measured[0];
    }

    private sealed class ByNameLength : System.Collections.IComparer
    {
        public int Compare(object? x, object? y) => ((Row)x!).Name.Length.CompareTo(((Row)y!).Name.Length);
    }

    /// <summary>
    /// ListCollectionView の CustomSort と SortDescriptions の関係、Items.Refresh と選択の関係を測る。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> CustomSortAndRefreshAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        ObservableCollection<Row> source = Sample();
        var view = (ListCollectionView)CollectionViewSource.GetDefaultView(source);
        string Order() => string.Join(", ", view.OfType<Row>().Select(row => row.Name));

        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
        rows.Add(["SortDescriptions: Name asc", view.SortDescriptions.Count.ToString(), "null", Order()]);

        view.CustomSort = new ByNameLength();
        rows.Add(["then CustomSort = by name length", view.SortDescriptions.Count.ToString(), "set", Order()]);

        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Descending));
        rows.Add(["then SortDescriptions.Add(Name desc)", view.SortDescriptions.Count.ToString(), view.CustomSort is null ? "null" : "set", Order()]);

        // 選択と現在セルを決めてから Items.Refresh を呼ぶ。
        view.SortDescriptions.Clear();
        var grid = new DataGrid { ItemsSource = source, AutoGenerateColumns = true, CanUserAddRows = false, Height = 140 };
        string selection = "";
        rows.AddRange(await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "Items.Refresh() with a row selected",
                grid,
                _ => [ "-", "-", selection ],
                Act: _ =>
                {
                    Row target = source[2];
                    grid.SelectedItem = target;
                    grid.CurrentCell = new DataGridCellInfo(target, grid.Columns[0]);
                    grid.Items.Refresh();
                    grid.UpdateLayout();
                    selection = $"SelectedItem {((Row)grid.SelectedItem).Name}, CurrentCell {((Row)grid.CurrentCell.Item).Name}";
                    return Task.CompletedTask;
                }),
        ]));

        return rows;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
