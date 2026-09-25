using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「DataGrid」（apps/wpf-standard-control-demo/datagrid.md と日本語版）の記述を実測する。
///
/// 並べ替えの状態の持ち方とセルのテンプレートの切り替えは、記事 wpf-datagrid-sorting /
/// wpf-datagrid-sort-reset / wpf-datagrid-cell-editing-template で実測済み。
/// 列見出しのクリックは、DataGridColumnHeader のクリック処理（OnClick）を呼んで再現する。
/// キー操作は表示したウィンドウでキー入力イベントを送る。
/// </summary>
internal sealed class DataGridDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-datagrid";

    public string ImageDirectory => DemoProbe.ImageDirectory("datagrid");

    public IReadOnlyList<string> Verifies =>
    [
        "DataGrid の継承関係と、各プロパティの既定値（仮想化・選択・列幅・グリッド線・ヘッダー・行の詳細・クリップボードなど）",
        "AutoGenerateColumns で作られる列の順序（宣言順かアルファベット順か）と、読み取り専用のプロパティの列、デモアプリのデータ（IDataErrorInfo を実装）で作られる列",
        "列の数より大きい FrozenColumnCount（デモアプリのスライダーの最大値）を設定したときの結果",
        "RowValidationRules が既定（空）のときと DataErrorValidationRule を加えたときに、IDataErrorInfo.Error を返す行が行の編集後に検証エラーになるか",
        "DataGrid の IsReadOnly が CanUserAddRows と列の IsReadOnly（列で False を指定した場合を含む）に与える影響",
        "CanUserAddRows の新規行が、List・配列・引数なしのコンストラクターを持たない型でどうなるかと、Delete キーでの行の削除",
        "F2・Esc・Enter によるセルの編集の開始・取り消し・確定と、列の種類ごとの編集用の要素",
        "列見出しを 3 回クリックしたときの並べ替えの向きの移り変わり",
        "セルの編集中に並べ替えを加えたときの例外と、CommitEdit() / CancelEdit() / 行単位の CommitEdit・CancelEdit の後に並べ替えられるか",
        "AlternatingRowBackground だけを設定したときの AlternationCount と行の背景",
        "1,000 行のときに作られる DataGridRow の数と、グループ化したときの数（IsVirtualizingWhenGrouping が既定 / True）・GroupStyle がないときのグループの見出し",
        "FrozenColumnCount を設定して横にスクロールしたときの列の位置",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "DataGrid: type and defaults",
            ["item", "value"],
            await DefaultsAsync(),
            "datagrid-defaults.svg");

        await context.SaveTableAsync(
            "DataGrid: columns, read-only, adding and deleting rows",
            ["case", "measured"],
            await ColumnsAndRowsAsync(),
            "datagrid-columns-rows.svg");

        await context.SaveTableAsync(
            "DataGrid: editing, sorting, alternation, virtualization, frozen columns (4 x 100 in 200)",
            ["case", "measured"],
            await BehaviorAsync(),
            "datagrid-behavior.svg");
    }

    public sealed class Person : INotifyPropertyChanged
    {
        private string _name = "";

        public int Id { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public int Age { get; set; }

        public bool Active { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>プロパティをアルファベット順ではない順に宣言し、1 つを読み取り専用にした型。</summary>
    public sealed class Declared
    {
        public string Zeta { get; set; } = "z";

        public string Alpha => "a";

        public string Mid { get; set; } = "m";
    }

    /// <summary>デモアプリの SampleItem と同じ形の型（IDataErrorInfo の Error で行の誤りを返す）。</summary>
    public sealed class SampleItem : IDataErrorInfo
    {
        public string Name { get; set; } = "";

        public string Value { get; set; } = "";

        public string Error => string.IsNullOrEmpty(Name) ? "Name cannot be empty." : null!;

        public string this[string columnName] => null!;
    }

    private static ObservableCollection<SampleItem> SampleItems() =>
    [
        new SampleItem { Name = "Item 1", Value = "Value A" },
        new SampleItem { Name = "Item 2", Value = "Value B" },
        new SampleItem { Name = "", Value = "Error Row" },
    ];

    /// <summary>引数なしのコンストラクターを持たない型。</summary>
    public sealed class NoDefaultConstructor(int id)
    {
        public int Id { get; set; } = id;
    }

    private static ObservableCollection<Person> People(int count = 3) =>
        new(Enumerable.Range(1, count).Select(i => new Person { Id = i, Name = $"Name {i}", Age = 20 + i }));

    private static DataGrid NewGrid(object source, double width = 360, double height = 200) =>
        new() { ItemsSource = (System.Collections.IEnumerable)source, Width = width, Height = height };

    private static async Task Settle(FrameworkElement element) =>
        await Capture.SettleAsync(Window.GetWindow(element)!);

    private static DataGridCell Cell(DataGrid grid, int row, int column)
    {
        var dataRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(row);
        var presenter = Descendants(dataRow).OfType<DataGridCellsPresenter>().First();
        return (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
    }

    private static async Task<List<IReadOnlyList<string>>> DefaultsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        var chain = new List<string>();
        for (Type? type = typeof(DataGrid).BaseType; type is not null && type != typeof(Control); type = type.BaseType)
        {
            chain.Add(type.Name);
        }

        rows.Add(["base types", string.Join(" > ", chain)]);

        DataGrid grid = NewGrid(People());
        await ShowAsync(grid, async () =>
        {
            rows.Add(["AutoGenerateColumns / IsReadOnly", $"{grid.AutoGenerateColumns} / {grid.IsReadOnly}"]);
            rows.Add(["SelectionMode / SelectionUnit", $"{grid.SelectionMode} / {grid.SelectionUnit}"]);
            rows.Add(["ColumnWidth / MinColumnWidth / MaxColumnWidth",
                $"{grid.ColumnWidth} / {D(grid.MinColumnWidth)} / {D(grid.MaxColumnWidth)}"]);
            rows.Add(["GridLinesVisibility / HeadersVisibility", $"{grid.GridLinesVisibility} / {grid.HeadersVisibility}"]);
            rows.Add(["RowDetailsVisibilityMode / AreRowDetailsFrozen", $"{grid.RowDetailsVisibilityMode} / {grid.AreRowDetailsFrozen}"]);
            rows.Add(["CanUserAddRows / CanUserDeleteRows", $"{grid.CanUserAddRows} / {grid.CanUserDeleteRows}"]);
            rows.Add(["CanUserReorderColumns / CanUserResizeColumns / CanUserResizeRows / CanUserSortColumns",
                $"{grid.CanUserReorderColumns} / {grid.CanUserResizeColumns} / {grid.CanUserResizeRows} / {grid.CanUserSortColumns}"]);
            rows.Add(["EnableRowVirtualization / EnableColumnVirtualization / VirtualizingPanel.IsVirtualizing",
                $"{grid.EnableRowVirtualization} / {grid.EnableColumnVirtualization} / {VirtualizingPanel.GetIsVirtualizing(grid)}"]);
            rows.Add(["ClipboardCopyMode / FrozenColumnCount / AlternationCount",
                $"{grid.ClipboardCopyMode} / {grid.FrozenColumnCount} / {grid.AlternationCount}"]);
            await Task.CompletedTask;
        });

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> ColumnsAndRowsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            DataGrid grid = NewGrid(new List<Declared> { new() });
            await ShowAsync(grid, async () =>
            {
                rows.Add(["auto columns, declared Zeta, Alpha (get-only), Mid",
                    string.Join(", ", grid.Columns.Select(c => $"{c.Header} ({(c.IsReadOnly ? "read-only" : "editable")})"))]);
                await Task.CompletedTask;
            });
        }

        {
            // デモアプリのデータで自動生成したときの列。
            DataGrid grid = NewGrid(SampleItems());
            await ShowAsync(grid, async () =>
            {
                rows.Add(["auto columns, demo SampleItem (IDataErrorInfo)",
                    string.Join(", ", grid.Columns.Select(c => $"{c.Header} ({(c.IsReadOnly ? "read-only" : "editable")})"))]);
                await Task.CompletedTask;
            });
        }

        {
            DataGrid grid = NewGrid(SampleItems());
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridTextColumn { Header = "Frozen", Binding = new Binding(nameof(SampleItem.Name)) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Scrollable", Binding = new Binding(nameof(SampleItem.Value)) });
            string thrown = Throws(() => grid.FrozenColumnCount = 3);
            string shown = "shown";
            try
            {
                await ShowAsync(grid, () => Task.CompletedTask);
            }
            catch (Exception ex)
            {
                shown = ex.GetType().Name + " when shown";
            }

            rows.Add(["2 columns, FrozenColumnCount = 3 (demo slider max)",
                $"{thrown}, {shown}; value {grid.FrozenColumnCount}"]);
        }

        foreach (bool withRule in new[] { false, true })
        {
            // デモアプリの RowValidationErrorTemplate 欄と同じデータ。3 行目は Name が空で Error を返す。
            DataGrid grid = NewGrid(SampleItems());
            grid.RowValidationErrorTemplate = new ControlTemplate { VisualTree = new FrameworkElementFactory(typeof(TextBlock)) };
            if (withRule)
            {
                grid.RowValidationRules.Add(new DataErrorValidationRule { ValidationStep = ValidationStep.UpdatedValue });
            }

            await ShowAsync(grid, async () =>
            {
                var errorRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(2);
                bool atStart = Validation.GetHasError(errorRow);
                grid.CurrentCell = new DataGridCellInfo(grid.Items[2], grid.Columns[1]);
                grid.BeginEdit();
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                await Settle(grid);
                rows.Add([$"demo data, row 3 Error, RowValidationRules {(withRule ? "+ DataErrorValidationRule" : "empty")}: HasError start / edited",
                    $"{atStart} / {Validation.GetHasError(errorRow)}"]);
            });
        }

        {
            DataGrid grid = NewGrid(People());
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Person.Name)), IsReadOnly = false });
            grid.IsReadOnly = true;
            await ShowAsync(grid, async () =>
            {
                DataGridColumn column = grid.Columns[0];
                rows.Add(["DataGrid IsReadOnly=True: CanUserAddRows",
                    WpfProbe.ValueAndSource(grid, DataGrid.CanUserAddRowsProperty)]);
                rows.Add(["  column IsReadOnly=False set: IsReadOnly / BeginEdit()",
                    $"{column.IsReadOnly} / {BeginEditOn(grid)}"]);
                await Task.CompletedTask;
            });
        }

        foreach ((string label, object source) in new (string, object)[]
        {
            ("List<Person>", People().ToList()),
            ("ObservableCollection<Person>", People()),
            ("Person[] (array)", People().ToArray()),
            ("List<T> without parameterless ctor", new List<NoDefaultConstructor> { new(1), new(2) }),
        })
        {
            DataGrid grid = NewGrid(source);
            string thrown = "no exception";
            try
            {
                await ShowAsync(grid, async () =>
                {
                    bool placeholder = grid.Items.Contains(CollectionView.NewItemPlaceholder);
                    rows.Add([$"new-item row / CanUserAddRows, ItemsSource {label}",
                        $"{(placeholder ? "shown" : "not shown")} / {grid.CanUserAddRows}"]);
                    await Task.CompletedTask;
                });
            }
            catch (Exception ex)
            {
                thrown = ex.GetType().Name;
            }

            if (thrown != "no exception")
            {
                rows.Add([$"  {label}: exception", thrown]);
            }
        }

        {
            ObservableCollection<Person> people = People();
            DataGrid grid = NewGrid(people);
            await ShowAsync(grid, async () =>
            {
                grid.SelectedIndex = 1;
                DataGridCell cell = Cell(grid, 1, 0);
                cell.Focus();
                await Settle(grid);
                PressKey(cell, Key.Delete);
                await Settle(grid);
                rows.Add(["row 2 selected, Delete key: items left",
                    $"{people.Count} ({string.Join(", ", people.Select(p => p.Name))})"]);
            }, activate: true);
        }

        return rows;
    }

    private static string BeginEditOn(DataGrid grid)
    {
        grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[0]);
        return grid.BeginEdit().ToString();
    }

    private static async Task<List<IReadOnlyList<string>>> BehaviorAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            ObservableCollection<Person> people = People();
            DataGrid grid = NewGrid(people);
            await ShowAsync(grid, async () =>
            {
                DataGridCell cell = Cell(grid, 0, 1);
                cell.Focus();
                grid.CurrentCell = new DataGridCellInfo(cell);
                await Settle(grid);

                PressKey(cell, Key.F2);
                await Settle(grid);
                string editor = Descendants(cell).OfType<Control>().FirstOrDefault()?.GetType().Name ?? "(none)";
                rows.Add(["Name cell, F2: IsEditing / editor", $"{cell.IsEditing} / {editor}"]);

                Descendants(cell).OfType<TextBox>().First().Text = "Changed";
                PressKey(Descendants(cell).OfType<TextBox>().First(), Key.Escape);
                await Settle(grid);
                rows.Add(["  typed \"Changed\", Esc: IsEditing / source", $"{cell.IsEditing} / \"{people[0].Name}\""]);

                PressKey(cell, Key.F2);
                await Settle(grid);
                Descendants(cell).OfType<TextBox>().First().Text = "Committed";
                PressKey(Descendants(cell).OfType<TextBox>().First(), Key.Enter);
                await Settle(grid);
                rows.Add(["  F2, typed \"Committed\", Enter: source", $"\"{people[0].Name}\""]);
            }, activate: true);
        }

        {
            // 列の種類ごとの編集用の要素。
            ObservableCollection<Person> people = People();
            var grid = NewGrid(people, 420);
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Person.Name)) });
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Active", Binding = new Binding(nameof(Person.Active)) });
            grid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = "Age", SelectedItemBinding = new Binding(nameof(Person.Age)), ItemsSource = new[] { 21, 22, 23 },
            });
            await ShowAsync(grid, async () =>
            {
                var editors = new List<string>();
                for (int column = 0; column < 3; column++)
                {
                    grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[column]);
                    grid.BeginEdit();
                    await Settle(grid);
                    DataGridCell cell = Cell(grid, 0, column);
                    editors.Add($"{grid.Columns[column].GetType().Name.Replace("DataGrid", "").Replace("Column", "")}: {cell.Content?.GetType().Name}");
                    grid.CancelEdit();
                }

                rows.Add(["editor by column type", string.Join(", ", editors)]);
            });
        }

        {
            // 列見出しのクリックを 3 回。
            DataGrid grid = NewGrid(People());
            await ShowAsync(grid, async () =>
            {
                var header = Descendants(grid).OfType<DataGridColumnHeader>().First(h => h.Column == grid.Columns[1]);
                MethodInfo onClick = typeof(DataGridColumnHeader).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var directions = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    onClick.Invoke(header, null);
                    await Settle(grid);
                    directions.Add(grid.Columns[1].SortDirection?.ToString() ?? "none");
                }

                rows.Add(["Name header clicked 3 times: SortDirection", string.Join(" -> ", directions)]);
            });
        }

        // セルの編集中に並べ替えを加える。編集を終えるメソッドごとに、並べ替えられるかを確かめる。
        foreach ((string label, Action<DataGrid>? finish) in new (string, Action<DataGrid>?)[]
        {
            ("editing a cell, Items.SortDescriptions.Add(...)", null),
            ("  after CommitEdit()", g => g.CommitEdit()),
            ("  after CancelEdit()", g => g.CancelEdit()),
            ("  after CommitEdit(DataGridEditingUnit.Row, true)", g => g.CommitEdit(DataGridEditingUnit.Row, true)),
            ("  after CancelEdit(DataGridEditingUnit.Row)", g => g.CancelEdit(DataGridEditingUnit.Row)),
        })
        {
            ObservableCollection<Person> people = People();
            DataGrid grid = NewGrid(people);
            await ShowAsync(grid, async () =>
            {
                BeginEditOn(grid);
                await Settle(grid);
                finish?.Invoke(grid);
                await Settle(grid);
                string thrown = Throws(() => grid.Items.SortDescriptions.Add(new SortDescription(nameof(Person.Age), ListSortDirection.Descending)));
                grid.CancelEdit(DataGridEditingUnit.Row);
                rows.Add([label, thrown]);
            });
        }

        {
            DataGrid grid = NewGrid(People(4));
            grid.AlternatingRowBackground = Brushes.LightGray;
            await ShowAsync(grid, async () =>
            {
                var backgrounds = Enumerable.Range(0, 4)
                    .Select(i => ((DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(i)).Background?.ToString() ?? "null");
                rows.Add(["only AlternatingRowBackground set: AlternationCount / rows",
                    $"{grid.AlternationCount} / {string.Join(", ", backgrounds)}"]);
                await Task.CompletedTask;
            });
        }

        foreach ((string label, bool grouped, bool groupStyle, bool virtualizeGroups) in new[]
        {
            ("1,000 rows", false, false, false),
            ("1,000 rows grouped, with GroupStyle", true, true, false),
            ("1,000 rows grouped, no GroupStyle", true, false, false),
            ("1,000 rows grouped, GroupStyle, IsVirtualizingWhenGrouping=True", true, true, true),
        })
        {
            var people = new ObservableCollection<Person>(Enumerable.Range(1, 1000).Select(i => new Person { Id = i, Name = $"N{i}", Age = i % 10 }));
            var view = new ListCollectionView(people);
            if (grouped)
            {
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Person.Age)));
            }

            DataGrid grid = NewGrid(view, 360, 200);
            grid.CanUserAddRows = false;
            if (groupStyle)
            {
                grid.GroupStyle.Add(new GroupStyle());
            }

            if (virtualizeGroups)
            {
                VirtualizingPanel.SetIsVirtualizingWhenGrouping(grid, true);
            }

            await ShowAsync(grid, async () =>
            {
                rows.Add([$"{label}: DataGridRows / GroupItems",
                    $"{Descendants(grid).OfType<DataGridRow>().Count()} / {Descendants(grid).OfType<GroupItem>().Count()}"]);
                await Task.CompletedTask;
            });
        }

        foreach (int frozen in new[] { 0, 1 })
        {
            // 4 列を幅 100 にして、幅 200 の DataGrid から横にはみ出させる。
            DataGrid grid = NewGrid(People(), 200, 150);
            grid.ColumnWidth = new DataGridLength(100);
            grid.FrozenColumnCount = frozen;
            await ShowAsync(grid, async () =>
            {
                var viewer = Descendants(grid).OfType<ScrollViewer>().First();
                DataGridCell first = Cell(grid, 0, 0);
                DataGridCell second = Cell(grid, 0, 1);
                string before = $"{D(Bounds(first, grid).X)}, {D(Bounds(second, grid).X)}";
                viewer.ScrollToHorizontalOffset(60);
                await Settle(grid);
                rows.Add([$"FrozenColumnCount={frozen}, scrolled 60: x of columns 1, 2 before / after",
                    $"{before} / {D(Bounds(first, grid).X)}, {D(Bounds(second, grid).X)}"]);
            });
        }

        return rows;
    }
}
