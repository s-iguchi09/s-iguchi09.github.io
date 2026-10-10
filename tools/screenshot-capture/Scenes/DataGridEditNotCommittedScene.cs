using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の DataGrid で編集中の値が保存ボタンで ViewModel に届かない原因と CommitEdit の使い方」の計測。
///
/// 記事と同じ XAML（Menu・ToolBar・ToolBar の外の Button・DataGrid）を表示し、
/// セルを編集中のまま実際のマウスとキーボード（<see cref="RealMouse"/> / <see cref="RealKeyboard"/>）で保存の操作をする。
/// 保存のコマンドが実行された時点のキーボードフォーカス、保存処理で呼んだ CommitEdit の戻り値、
/// ソースの値、IEditableObject.EndEdit の回数、ICollectionView.Refresh の結果、保存後の編集状態を読む。
/// 文字の入力は <see cref="DemoProbe.TypeInto"/>（InputManager 経由）で行う。
/// </summary>
internal sealed class DataGridEditNotCommittedScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "ToolBar の Button と Menu の MenuItem は Focusable が True で、属するフォーカス スコープがそれぞれ ToolBar と Menu であり、ToolBar の外の Button は Window であること",
        "セルを編集中のまま実際のマウスで ToolBar の Button・Menu の MenuItem をクリックすると、キーボードフォーカスは一度そのコントロールへ移るが、保存のコマンドの実行時にはセルの TextBox に戻っており、ソースは更新されず、セルと行は編集中のままであること",
        "実際のキーボードの Ctrl+S（Window の DataContext から {Binding SaveCommand} で解決する KeyBinding）では、フォーカスは DataGrid から出ず、ソースは更新されないこと",
        "ToolBar の外の Button をクリックすると、フォーカスがその Button へ移り、コマンドの実行時にはソースが更新され、IEditableObject.EndEdit が呼ばれ、セルと行の編集が終わっていること",
        "ToolBar に FocusManager.IsFocusScope=\"False\" を付けると、Button のクリックでフォーカスはその Button に移ったままになり、ソースが更新され、セルと行の編集が終わること",
        "Menu に FocusManager.IsFocusScope=\"False\" を付けると、MenuItem のクリックでフォーカスはその MenuItem に移ったままになり、ソースが更新され、セルと行の編集が終わること",
        "セルの編集中に保存処理で CommitEdit() を 1 回呼ぶと True が返りセルの編集は終わるが、行は編集中のままで、ソースは更新されず、EndEdit も呼ばれないこと（IEditableObject の有無によらない）",
        "セルの編集中に CommitEdit() を 2 回続けて呼ぶと、どちらも True が返り、ソースが更新され、セルと行の編集が終わること",
        "保存処理で CommitEdit(DataGridEditingUnit.Row, true) を呼ぶと、ToolBar の Button・Menu の MenuItem・Ctrl+S・ToolBar の外の Button のどれで保存しても True が返り、ソースが更新され、セルと行の編集が終わること（ToolBar の Button では IEditableObject の有無によらない）",
        "行が編集中のまま ICollectionView.Refresh を呼ぶと InvalidOperationException になり、行の編集を確定した後は成功すること",
        "Name 列に UpdateSourceTrigger=PropertyChanged を書くと、確定しなくてもソースは更新されるが、セルと行は編集中のままで、EndEdit は呼ばれず、Refresh は InvalidOperationException になること",
        "int の列に \"abc\" を入力して確定が False になった後、5 に直してもう一度保存すると True が返り、ソースが 5 になり、行の編集が終わること",
        "セルを編集していない状態で CommitEdit(DataGridEditingUnit.Row, true) を呼ぶと True が返ること",
        "int の列に \"abc\" を入力したまま CommitEdit(DataGridEditingUnit.Row, true) を呼ぶと False が返り、ソースは更新されず、セルと行は編集中のままであること",
        "セルに入力した後に実際の Esc を 2 回押すと、既定のバインドではソースは入力中も変わらず元の値のままで、UpdateSourceTrigger=PropertyChanged では IEditableObject を実装したアイテムだけが CancelEdit で元の値に戻り、INotifyPropertyChanged だけのアイテムは入力した値のまま残ること",
    ];

    public string Slug => "wpf-datagrid-edit-not-committed-on-save";

    public async Task CaptureAsync(SceneContext context)
    {
        await ShootProblemAsync(context);

        await context.SaveTableAsync(
            "Focus properties of the three Save controls",
            [T("control", "コントロール"), "Focusable", T("focus scope it belongs to", "属するフォーカス スコープ"), "IsFocusScope"],
            await FocusPropertiesAsync(),
            "datagrid-save-controls-focus.svg");

        await context.SaveTableAsync(
            "Saving while a DataGrid cell is being edited",
            [
                T("save input", "保存の操作"),
                T("called in the save handler", "保存処理で呼ぶもの"),
                T("where focus went when it left the DataGrid", "DataGrid から出たフォーカスの移り先"),
                T("keyboard focus when saving", "保存時のキーボードフォーカス"),
                T("return value", "戻り値"),
                T("source value", "ソースの値"),
                "EndEdit",
                "Refresh",
                T("after saving: cell / row editing", "保存後の編集中: セル / 行"),
            ],
            await SaveCasesAsync(),
            "datagrid-save-while-editing.svg");

        await context.SaveTableAsync(
            "Pressing Esc twice after typing in a cell",
            [
                T("binding of the column", "列のバインド"),
                T("item", "アイテム"),
                T("source while typing", "入力中のソース"),
                T("source after Esc twice", "Esc を 2 回押した後のソース"),
                "CancelEdit",
            ],
            await EscapeCasesAsync(),
            "datagrid-escape-after-typing.svg");
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // ---------------------------------------------------------------- 記事の XAML とデータ

    /// <summary>記事の XAML。{0} は Name 列のバインドに足す設定、{1} は ToolBar に、{2} は Menu に足す属性。</summary>
    private const string PageXaml = """
        <DockPanel Width="400">
          <Menu DockPanel.Dock="Top"{2}>
            <MenuItem x:Name="menuSave" Header="Save" Command="{Binding SaveCommand}" />
          </Menu>
          <ToolBar DockPanel.Dock="Top"{1}>
            <Button x:Name="toolBarSave" Content="Save" Command="{Binding SaveCommand}" />
          </ToolBar>
          <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="4">
            <Button x:Name="plainSave" Content="Save" Command="{Binding SaveCommand}" Padding="12,2" />
            <TextBlock Text="{Binding LastSaved}" Margin="8,0" VerticalAlignment="Center" />
          </StackPanel>
          <DataGrid x:Name="grid" ItemsSource="{Binding Items}"
                    AutoGenerateColumns="False" CanUserAddRows="False" Height="100">
            <DataGrid.Columns>
              <DataGridTextColumn Header="Name" Binding="{Binding Name{0}}" Width="*" />
              <DataGridTextColumn Header="Quantity" Binding="{Binding Quantity}" Width="90" />
            </DataGrid.Columns>
          </DataGrid>
        </DockPanel>
        """;

    /// <summary>INotifyPropertyChanged だけを実装するアイテム。</summary>
    private class Item : INotifyPropertyChanged
    {
        private string _name = "";
        private int _quantity;

        public string Name
        {
            get => _name;
            set { _name = value; OnChanged(nameof(Name)); }
        }

        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnChanged(nameof(Quantity)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>IEditableObject も実装するアイテム。BeginEdit で値を控え、CancelEdit で戻す。呼ばれた回数を数える。</summary>
    private sealed class EditableItem : Item, IEditableObject
    {
        private (string Name, int Quantity)? _backup;

        public int EndEdits { get; private set; }

        public int CancelEdits { get; private set; }

        public void BeginEdit() => _backup ??= (Name, Quantity);

        public void EndEdit()
        {
            EndEdits++;
            _backup = null;
        }

        public void CancelEdit()
        {
            CancelEdits++;
            if (_backup is { } backup)
            {
                Name = backup.Name;
                Quantity = backup.Quantity;
                _backup = null;
            }
        }
    }

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }

    /// <summary>記事の XAML の DataContext。SaveCommand は <see cref="OnSave"/> を呼ぶ。</summary>
    private sealed class Page : INotifyPropertyChanged
    {
        private string _lastSaved = "";

        public Page(Item first)
        {
            Items.Add(first);
            Items.Add(new Item { Name = "beta", Quantity = 2 });
            SaveCommand = new DelegateCommand(() =>
            {
                LastSaved = $"saved: Name = {Items[0].Name}";
                OnSave?.Invoke();
            });
        }

        public ObservableCollection<Item> Items { get; } = [];

        public ICommand SaveCommand { get; }

        public Action? OnSave { get; set; }

        public string LastSaved
        {
            get => _lastSaved;
            set { _lastSaved = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastSaved))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static Item NewFirst(bool editable) => editable
        ? new EditableItem { Name = "alpha", Quantity = 1 }
        : new Item { Name = "alpha", Quantity = 1 };

    private static (DockPanel Root, DataGrid Grid, Page Page) Build(Item first, string nameBinding = "", string toolBarAttributes = "", string menuAttributes = "")
    {
        var root = SceneContext.LoadXaml<DockPanel>(PageXaml.Replace("{0}", nameBinding).Replace("{1}", toolBarAttributes).Replace("{2}", menuAttributes));
        var page = new Page(first);
        root.DataContext = page;
        return (root, (DataGrid)root.FindName("grid"), page);
    }

    // ---------------------------------------------------------------- 操作

    /// <summary>1 行目の <paramref name="column"/> 列のセルを編集状態にし、<paramref name="letters"/> を打ち込む。</summary>
    private static async Task TypeInCellAsync(Window window, DataGrid grid, int column, string letters)
    {
        await FocusAsync(grid);
        grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[column]);
        if (!grid.BeginEdit())
        {
            throw new InvalidOperationException("セルを編集状態にできない。");
        }

        await Capture.SettleAsync(window, 100);
        if (Keyboard.FocusedElement is not TextBox box || CellOf(box) is not { IsEditing: true } cell || cell.Column != grid.Columns[column])
        {
            throw new InvalidOperationException($"1 行目の {grid.Columns[column].Header} 列の編集中のセルの TextBox にフォーカスが無い（{Keyboard.FocusedElement?.GetType().Name ?? "なし"}）。");
        }

        box.SelectAll();
        TypeInto(box, letters);
        await Capture.SettleAsync(window, 100);
    }

    /// <summary>要素を含む DataGridCell（無ければ null）。</summary>
    private static DataGridCell? CellOf(DependencyObject element)
    {
        for (DependencyObject? node = element; node is not null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is DataGridCell cell)
            {
                return cell;
            }
        }

        return null;
    }

    private static bool CellEditing(DataGrid grid) => Descendants(grid).OfType<DataGridCell>().Any(c => c.IsEditing);

    private static bool RowEditing(DataGrid grid) => Descendants(grid).OfType<DataGridRow>().Any(r => r.IsEditing);

    private static Loc DescribeFocus(DataGrid grid) => Keyboard.FocusedElement switch
    {
        null => T("none", "なし"),
        TextBox box when grid.IsAncestorOf(box) => T("TextBox in the cell", "セルの TextBox"),
        DataGrid element when element == grid => "DataGrid",
        Button button when button.Name == "toolBarSave" => T("Button in the ToolBar", "ToolBar の Button"),
        Button button when button.Name == "plainSave" => T("Button outside the ToolBar", "ToolBar の外の Button"),
        MenuItem item when item.Name == "menuSave" => T("MenuItem in the Menu", "Menu の MenuItem"),
        DependencyObject element when grid.IsAncestorOf(element) => T($"{element.GetType().Name} in the DataGrid", $"DataGrid の中の {element.GetType().Name}"),
        var element => element.GetType().Name,
    };

    private static string YesNo(bool value) => value ? "True" : "False";

    // ---------------------------------------------------------------- 保存のコントロールのフォーカスの設定

    /// <summary>記事の XAML の 3 つの Save について、Focusable と、属するフォーカス スコープを読む。</summary>
    private static async Task<List<IReadOnlyList<Loc>>> FocusPropertiesAsync()
    {
        (DockPanel root, DataGrid _, Page _) = Build(NewFirst(editable: true));
        var rows = new List<IReadOnlyList<Loc>>();
        await ShowAsync(root, () =>
        {
            foreach ((string name, Loc label) in new (string, Loc)[]
            {
                ("toolBarSave", T("Button in the ToolBar", "ToolBar の Button")),
                ("menuSave", T("MenuItem in the Menu", "Menu の MenuItem")),
                ("plainSave", T("Button outside the ToolBar", "ToolBar の外の Button")),
            })
            {
                var control = (FrameworkElement)root.FindName(name);
                DependencyObject scope = FocusManager.GetFocusScope(control);
                rows.Add([label, YesNo(control.Focusable), scope.GetType().Name, YesNo(FocusManager.GetIsFocusScope(control))]);
            }

            return Task.CompletedTask;
        });
        return rows;
    }

    // ---------------------------------------------------------------- 問題の図

    /// <summary>記事の「問題」の図。セルを編集中のまま ToolBar の Save をクリックし、保存された値と編集中のセルを並べて撮る。</summary>
    private static async Task ShootProblemAsync(SceneContext context)
    {
        (DockPanel root, DataGrid grid, Page _) = Build(NewFirst(editable: true));
        var window = new Window { Title = "DataGrid", Content = root, SizeToContent = SizeToContent.WidthAndHeight };
        await context.ShootAsync(window, "datagrid-toolbar-save-while-editing.png", async w =>
        {
            await FrontCenteredAsync(root);
            await TypeInCellAsync(w, grid, 0, "edited");
            using (RealMouse.Preserve())
            {
                await RealMouse.ClickAsync(w, (Button)root.FindName("toolBarSave"));
            }

            // 撮る前に、カーソルが図に写らない位置（ウィンドウの外）へ移っていることを Preserve の破棄で保証している。
            await Capture.SettleAsync(w, 200);
        });
    }

    // ---------------------------------------------------------------- 保存の操作ごとの結果

    private enum SaveInput
    {
        ToolBarButton,
        MenuItem,
        CtrlS,
        PlainButton,
    }

    private enum Commit
    {
        None,
        Cell,
        CellTwice,
        Row,
    }

    private static Loc Describe(SaveInput input) => input switch
    {
        SaveInput.ToolBarButton => T("click the Button in the ToolBar", "ToolBar の Button をクリック"),
        SaveInput.MenuItem => T("click the MenuItem in the Menu", "Menu の MenuItem をクリック"),
        SaveInput.CtrlS => T("Ctrl+S (KeyBinding on the Window)", "Ctrl+S（Window の KeyBinding）"),
        SaveInput.PlainButton => T("click a Button outside the ToolBar", "ToolBar の外の Button をクリック"),
        _ => throw new ArgumentOutOfRangeException(nameof(input)),
    };

    private static Loc Describe(Commit commit) => commit switch
    {
        Commit.None => T("nothing", "なし"),
        Commit.Cell => "CommitEdit()",
        Commit.CellTwice => T("CommitEdit() twice", "CommitEdit() を 2 回"),
        Commit.Row => "CommitEdit(DataGridEditingUnit.Row, true)",
        _ => throw new ArgumentOutOfRangeException(nameof(commit)),
    };

    private static async Task<List<IReadOnlyList<Loc>>> SaveCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach (SaveInput input in Enum.GetValues<SaveInput>())
        {
            rows.Add(await SaveAsync(Describe(input), input, Commit.None, editable: true));
        }

        rows.Add(await SaveAsync(
            T("click the Button in a ToolBar with FocusManager.IsFocusScope=\"False\"", "FocusManager.IsFocusScope=\"False\" の ToolBar の Button をクリック"),
            SaveInput.ToolBarButton, Commit.None, editable: true, toolBarAttributes: " FocusManager.IsFocusScope=\"False\""));
        rows.Add(await SaveAsync(
            T("click the MenuItem in a Menu with FocusManager.IsFocusScope=\"False\"", "FocusManager.IsFocusScope=\"False\" の Menu の MenuItem をクリック"),
            SaveInput.MenuItem, Commit.None, editable: true, menuAttributes: " FocusManager.IsFocusScope=\"False\""));

        Loc toolBar = Describe(SaveInput.ToolBarButton);
        rows.Add(await SaveAsync(toolBar, SaveInput.ToolBarButton, Commit.Cell, editable: true));
        rows.Add(await SaveAsync(toolBar, SaveInput.ToolBarButton, Commit.CellTwice, editable: true));
        foreach (SaveInput input in Enum.GetValues<SaveInput>())
        {
            rows.Add(await SaveAsync(Describe(input), input, Commit.Row, editable: true));
        }

        rows.Add(await SaveAsync(
            T("click the Button in the ToolBar (item without IEditableObject)", "ToolBar の Button をクリック（IEditableObject の無いアイテム）"),
            SaveInput.ToolBarButton, Commit.Cell, editable: false));
        rows.Add(await SaveAsync(
            T("click the Button in the ToolBar (item without IEditableObject)", "ToolBar の Button をクリック（IEditableObject の無いアイテム）"),
            SaveInput.ToolBarButton, Commit.Row, editable: false));
        rows.Add(await SaveAsync(
            T("click the Button in the ToolBar (Name column with UpdateSourceTrigger=PropertyChanged)", "ToolBar の Button をクリック（Name 列に UpdateSourceTrigger=PropertyChanged）"),
            SaveInput.ToolBarButton, Commit.None, editable: true, nameBinding: ", UpdateSourceTrigger=PropertyChanged"));
        rows.Add(await SaveAsync(
            T("click the Button in the ToolBar (\"abc\" typed into the int Quantity column)", "ToolBar の Button をクリック（int の Quantity 列に \"abc\" を入力）"),
            SaveInput.ToolBarButton, Commit.Row, editable: true, quantity: true));
        rows.Add(await SaveAsync(
            T("click the Button in the ToolBar (\"abc\" in Quantity, then corrected to 5 and clicked again)", "ToolBar の Button をクリック（Quantity に \"abc\"、5 に直してもう一度クリック）"),
            SaveInput.ToolBarButton, Commit.Row, editable: true, quantity: true, retryWith: '5'));
        rows.Add(await SaveAsync(
            T("click the Button in the ToolBar (no cell has been edited)", "ToolBar の Button をクリック（セルを編集していない）"),
            SaveInput.ToolBarButton, Commit.Row, editable: true, typeFirst: false));
        return rows;
    }

    private static async Task<IReadOnlyList<Loc>> SaveAsync(
        Loc label, SaveInput input, Commit commit, bool editable, string nameBinding = "", bool quantity = false, bool typeFirst = true,
        string toolBarAttributes = "", char? retryWith = null, string menuAttributes = "")
    {
        Item first = NewFirst(editable);
        (DockPanel root, DataGrid grid, Page page) = Build(first, nameBinding, toolBarAttributes, menuAttributes);
        var returns = new List<string>();
        IReadOnlyList<Loc>? row = null;
        Loc? focusLeft = null;
        bool armed = false;
        grid.IsKeyboardFocusWithinChanged += (_, e) =>
        {
            // 保存の操作の間に最初に出たときだけ、移った先を記録する。
            if (e.NewValue is false && focusLeft is null && armed)
            {
                focusLeft = DescribeFocus(grid);
            }
        };
        page.OnSave = () =>
        {
            Loc focus = DescribeFocus(grid);
            // 保存し直す行では、保存ごとの戻り値を矢印でつなぐ。
            returns.Add(commit switch
            {
                Commit.None => "-",
                Commit.Cell => YesNo(grid.CommitEdit()),
                Commit.CellTwice => $"{YesNo(grid.CommitEdit())}, {YesNo(grid.CommitEdit())}",
                Commit.Row => YesNo(grid.CommitEdit(DataGridEditingUnit.Row, true)),
                _ => throw new ArgumentOutOfRangeException(nameof(commit)),
            });
            Loc returned = string.Join(" → ", returns);
            string value = quantity ? first.Quantity.ToString() : $"\"{first.Name}\"";
            string endEdits = first is EditableItem e ? e.EndEdits.ToString() : "-";
            Loc refresh;
            try
            {
                CollectionViewSource.GetDefaultView(page.Items).Refresh();
                refresh = T("succeeds", "成功");
            }
            catch (Exception ex)
            {
                refresh = ex.GetType().Name;
            }

            row = [label, Describe(commit), focusLeft ?? T("did not leave", "出ない"), focus, returned, value, endEdits, refresh, $"{YesNo(CellEditing(grid))} / {YesNo(RowEditing(grid))}"];
        };

        await ShowAsync(root, async () =>
        {
            Window window = await FrontCenteredAsync(root);
            // 記事の XAML と同じく、Window の DataContext から {Binding SaveCommand} で解決させる。
            window.DataContext = page;
            var keyBinding = new KeyBinding { Key = Key.S, Modifiers = ModifierKeys.Control };
            BindingOperations.SetBinding(keyBinding, InputBinding.CommandProperty, new Binding(nameof(Page.SaveCommand)));
            window.InputBindings.Add(keyBinding);
            if (typeFirst)
            {
                await TypeInCellAsync(window, grid, quantity ? 1 : 0, quantity ? "abc" : "edited");
            }
            else
            {
                await FocusAsync(grid);
            }

            armed = true;
            using (RealMouse.Preserve())
            {
                switch (input)
                {
                    case SaveInput.ToolBarButton:
                        await RealMouse.ClickAsync(window, (Button)root.FindName("toolBarSave"));
                        if (retryWith is char digit)
                        {
                            await Capture.SettleAsync(window, 100);
                            TypeDigit(digit);
                            await RealMouse.ClickAsync(window, (Button)root.FindName("toolBarSave"));
                        }

                        break;
                    case SaveInput.MenuItem:
                        await RealMouse.ClickAsync(window, (MenuItem)root.FindName("menuSave"));
                        break;
                    case SaveInput.CtrlS:
                        await RealKeyboard.PressAsync(window, 0x53, 0x11); // VK_S + VK_CONTROL
                        break;
                    case SaveInput.PlainButton:
                        await RealMouse.ClickAsync(window, (Button)root.FindName("plainSave"));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(input));
                }
            }

            await Capture.SettleAsync(window, 100);
        }, activate: true);

        return row ?? throw new InvalidOperationException($"保存のコマンドが実行されなかった: {label.En}");
    }

    /// <summary>
    /// フォーカスのある TextBox の文字をすべて選び、数字 1 文字に置き換える。
    /// <see cref="DemoProbe.TypeLetters"/> と同じく、キーと文字を InputManager を通して送る（英小文字以外も打つため、ここで書く）。
    /// </summary>
    private static void TypeDigit(char digit)
    {
        if (digit is < '0' or > '9')
        {
            throw new ArgumentException("数字 1 文字だけを打てる。", nameof(digit));
        }

        if (Keyboard.FocusedElement is not TextBox box)
        {
            throw new InvalidOperationException($"数字を打つ TextBox にフォーカスが無い（{Keyboard.FocusedElement?.GetType().Name ?? "なし"}）。");
        }

        box.SelectAll();
        var key = (Key)((int)Key.D0 + (digit - '0'));
        SendKey(key);
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, digit.ToString()));
        SendKey(key, down: false);
        if (box.Text != digit.ToString())
        {
            throw new InvalidOperationException($"数字が TextBox に届いていない（Text = \"{box.Text}\"）。");
        }
    }

    // ---------------------------------------------------------------- Esc での取り消し

    private static async Task<List<IReadOnlyList<Loc>>> EscapeCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string binding, Loc bindingLabel) in new (string, Loc)[]
        {
            ("", "{Binding Name}"),
            (", UpdateSourceTrigger=PropertyChanged", "{Binding Name, UpdateSourceTrigger=PropertyChanged}"),
        })
        {
            foreach (bool editable in new[] { true, false })
            {
                Item first = NewFirst(editable);
                (DockPanel root, DataGrid grid, Page _) = Build(first, binding);
                string typing = "";
                await ShowAsync(root, async () =>
                {
                    Window window = await FrontCenteredAsync(root);
                    await TypeInCellAsync(window, grid, 0, "edited");
                    typing = $"\"{first.Name}\"";
                    await RealKeyboard.PressAsync(window, 0x1B); // VK_ESCAPE: セルの編集を取り消す
                    await RealKeyboard.PressAsync(window, 0x1B); // VK_ESCAPE: 行の編集を取り消す
                    await Capture.SettleAsync(window, 100);
                }, activate: true);

                rows.Add(
                [
                    bindingLabel,
                    editable ? T("implements IEditableObject", "IEditableObject を実装") : T("INotifyPropertyChanged only", "INotifyPropertyChanged のみ"),
                    typing,
                    $"\"{first.Name}\"",
                    first is EditableItem e ? e.CancelEdits.ToString() : "-",
                ]);
            }
        }

        return rows;
    }
}
