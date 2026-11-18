using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF マスター詳細で SelectedItem に直接バインドすると編集を取り消せない: 編集用コピーと IEditableObject」の計測。
///
/// 一覧（ListBox / DataGrid）と詳細欄を並べた記事と同じ XAML を表示し、
/// 詳細欄の TextBox に文字を打ってから、実際のマウス（<see cref="RealMouse"/>）で Cancel・Save・一覧の 2 行目をクリックする。
/// 各時点のソースの値、一覧に表示されている文字、詳細欄の文字、選択の状態（ビューモデル・コントロール・ICollectionView）を読む。
/// 文字の入力は <see cref="DemoProbe.TypeInto"/>（InputManager 経由）で行う。
/// </summary>
internal sealed class MasterDetailEditCancelScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "詳細欄を SelectedCustomer.Name に直接バインドすると、UpdateSourceTrigger=PropertyChanged では入力の直後に、既定のトリガー（LostFocus）では Cancel のコマンドの実行時までに、ソースが入力した値に変わっていて、Cancel の後の一覧の表示も入力した値であること（UpdateSourceTrigger=PropertyChanged では、入力の直後の一覧の表示も入力した値であること）",
        "詳細欄を編集用のコピー（Editing）にバインドすると、入力の直後も Cancel の実行時もソースは元の値のままで、Cancel の後は一覧の表示と詳細欄が元の値になり、Save をクリックするとソースと一覧の表示が入力した値になること",
        "IEditableObject を実装したアイテムに既定のトリガーで直接バインドし、選んだときに BeginEdit を呼ぶと、入力の直後のソースは元の値のままで、Cancel の実行時には入力した値に変わっているが、CancelEdit の後は一覧の表示と詳細欄が元の値に戻ること",
        "編集中に一覧の 2 行目をクリックすると、何も止めなければ選択が移り、編集用のコピーでは入力した値が捨てられ、IEditableObject では入力した値が 1 行目に残ること",
        "詳細欄が既定のトリガーのまま、SelectedCustomer のセッターで選択の変更を拒むと（PropertyChanged をその場で発生させても、既定の優先度（Normal）の Dispatcher.BeginInvoke で遅らせても）、ビューモデルと SelectedItem は 1 行目のままだが、SelectedIndex と画面の選択は 2 行目になり、ListBox・DataGrid のどちらでも食い違うこと",
        "詳細欄が既定のトリガーのまま、IsSynchronizedWithCurrentItem=True にして ICollectionView.CurrentChanging で e.Cancel = true にすると、CurrentItem とビューモデルは 1 行目のままだが、SelectedItem・SelectedIndex・画面の選択は 2 行目になり、ListBox・DataGrid のどちらでも食い違うこと",
        "一覧の IsEnabled を変更の有無にバインドし、詳細欄を UpdateSourceTrigger=PropertyChanged にすると、2 行目の上へカーソルを動かしてボタンを押す直前の一覧の IsEnabled が False で、2 行目をクリックしても選択はどこも 1 行目のままで、入力した値が詳細欄に残ること（ListBox・DataGrid）",
        "同じ構成で詳細欄が既定のトリガー（LostFocus）だと、2 行目の上へカーソルを動かしてボタンを押す直前の一覧の IsEnabled は True のままで、2 行目のクリックで選択が移り、入力した値が捨てられること",
        "選択の変更をセッターで拒んだ後、または CurrentChanging で取り消した後に Save をクリックすると、画面で選ばれている 2 行目ではなく 1 行目が入力した値に更新されること（ListBox・DataGrid）",
        "一覧を無効にした状態で Cancel をクリックすると一覧の IsEnabled が True に戻り、その後 2 行目をクリックすると選択と詳細欄が 2 行目に移ること",
    ];

    public string Slug => "wpf-master-detail-edit-cancel-selecteditem";

    public async Task CaptureAsync(SceneContext context)
    {
        await ShootDirectTypingAsync(context);
        await ShootRejectedSelectionAsync(context);

        await context.SaveTableAsync(
            "Clicking Cancel after typing \"edited\" in the Name box",
            [
                T("detail form bound to", "詳細欄のバインド先"),
                T("source while typing", "入力中のソース"),
                T("source when Cancel runs", "Cancel の実行時のソース"),
                T("list after Cancel", "Cancel 後の一覧の表示"),
                T("detail form after Cancel", "Cancel 後の詳細欄"),
            ],
            await CancelCasesAsync(),
            "master-detail-cancel.svg");

        await context.SaveTableAsync(
            "Clicking Save after typing \"edited\" in the Name box",
            [
                T("detail form bound to", "詳細欄のバインド先"),
                T("source after Save", "Save 後のソース"),
                T("list after Save", "Save 後の一覧の表示"),
            ],
            await SaveCasesAsync(),
            "master-detail-save.svg");

        await context.SaveTableAsync(
            "Clicking the second row after typing \"edited\" in the Name box of the first row",
            [
                T("list", "一覧"),
                T("how the change is stopped", "止め方"),
                T("SelectedCustomer (view model)", "SelectedCustomer（ビューモデル）"),
                "SelectedItem / SelectedIndex",
                T("row highlighted on screen", "画面で選ばれている行"),
                "CurrentItem",
                T("detail form", "詳細欄"),
                T("first row in the list", "一覧の 1 行目"),
                T("list IsEnabled just before the mouse press", "ボタンを押す直前の一覧の IsEnabled"),
            ],
            await SwitchCasesAsync(),
            "master-detail-switch.svg");

        await context.SaveTableAsync(
            "Clicking Save after the change of selection was stopped",
            [
                T("list", "一覧"),
                T("how the change is stopped", "止め方"),
                T("row highlighted on screen", "画面で選ばれている行"),
                T("first row after Save", "Save 後の 1 行目"),
                T("second row after Save", "Save 後の 2 行目"),
            ],
            await SaveAfterStoppedCasesAsync(),
            "master-detail-save-after-stopped.svg");
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // ---------------------------------------------------------------- 記事の XAML

    /// <summary>
    /// 記事の XAML。{DetailPath} は詳細欄のバインドのパス（SelectedCustomer または Editing）、{DetailTrigger} は詳細欄のバインドに足す設定、
    /// {List} は一覧（ListBox または DataGrid）。
    /// </summary>
    private const string PageXaml = """
        <DockPanel Width="380" Margin="8">
          <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button x:Name="save" Content="Save" Command="{Binding SaveCommand}" Padding="12,2" Margin="0,0,8,0" />
            <Button x:Name="cancel" Content="Cancel" Command="{Binding CancelCommand}" Padding="12,2" />
          </StackPanel>
          {List}
          <StackPanel Margin="8,0,0,0">
            <TextBlock Text="Name" />
            <TextBox x:Name="name" Text="{Binding {DetailPath}.Name{DetailTrigger}}" />
            <TextBlock Text="City" Margin="0,8,0,0" />
            <TextBox x:Name="city" Text="{Binding {DetailPath}.City{DetailTrigger}}" />
          </StackPanel>
        </DockPanel>
        """;

    /// <summary>一覧の ListBox。{Selection} は選択のバインド、{IsEnabled} は IsEnabled のバインド。</summary>
    private const string ListBoxXaml = """
        <ListBox x:Name="list" Width="140" Height="90" ItemsSource="{Binding Customers}" DisplayMemberPath="Name"{Selection}{IsEnabled} />
        """;

    /// <summary>一覧の DataGrid。{Selection} は選択のバインド、{IsEnabled} は IsEnabled のバインド。</summary>
    private const string DataGridXaml = """
        <DataGrid x:Name="list" Width="140" Height="90" ItemsSource="{Binding Customers}" IsReadOnly="True"
                  AutoGenerateColumns="False" HeadersVisibility="None"{Selection}{IsEnabled}>
          <DataGrid.Columns>
            <DataGridTextColumn Binding="{Binding Name}" Width="*" />
          </DataGrid.Columns>
        </DataGrid>
        """;

    private const string SelectedItemBinding = " SelectedItem=\"{Binding SelectedCustomer}\"";

    private const string SynchronizedWithCurrentItem = " IsSynchronizedWithCurrentItem=\"True\"";

    private const string IsEnabledBinding = " IsEnabled=\"{Binding CanChangeSelection}\"";

    private const string PropertyChangedTrigger = ", UpdateSourceTrigger=PropertyChanged";

    // ---------------------------------------------------------------- 記事のモデルとビューモデル

    /// <summary>ビューモデルの最小の基底。</summary>
    private abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>CanExecute を持たない最小のコマンド。</summary>
    private sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }

    /// <summary>一覧に並ぶ顧客。</summary>
    private class Customer : ObservableObject
    {
        private string _name = "";
        private string _city = "";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }

        public Customer Clone() => new() { Name = Name, City = City };

        public void CopyFrom(Customer other)
        {
            Name = other.Name;
            City = other.City;
        }

        public bool HasSameValues(Customer other) => Name == other.Name && City == other.City;
    }

    /// <summary>IEditableObject を実装した顧客。BeginEdit で値を控え、CancelEdit で戻す。</summary>
    private sealed class EditableCustomer : Customer, IEditableObject
    {
        private Customer? _backup;

        public void BeginEdit() => _backup ??= Clone();

        public void EndEdit() => _backup = null;

        public void CancelEdit()
        {
            if (_backup is not null)
            {
                CopyFrom(_backup);
                _backup = null;
            }
        }
    }

    private enum Mode
    {
        /// <summary>詳細欄を SelectedCustomer に直接バインドする。</summary>
        Direct,

        /// <summary>詳細欄を編集用のコピー（Editing）にバインドする。</summary>
        Copy,

        /// <summary>詳細欄を SelectedCustomer に直接バインドし、IEditableObject で取り消す。</summary>
        Editable,
    }

    private enum Guard
    {
        None,

        /// <summary>SelectedCustomer のセッターで拒み、その場で PropertyChanged を発生させる。</summary>
        RejectInSetter,

        /// <summary>SelectedCustomer のセッターで拒み、Dispatcher.BeginInvoke で PropertyChanged を遅らせる。</summary>
        RejectDeferred,

        /// <summary>IsSynchronizedWithCurrentItem=True にし、ICollectionView.CurrentChanging で e.Cancel = true にする。</summary>
        CurrentChanging,

        /// <summary>一覧の IsEnabled を CanChangeSelection（変更が無いこと）にバインドする。</summary>
        DisableList,
    }

    /// <summary>
    /// 記事のビューモデル。<see cref="Mode"/> と <see cref="Guard"/> の組み合わせごとに、記事の各節のコードと同じ処理をする。
    /// 選択の変更を拒む止め方では、破棄してよいかの確認（記事では IDialogService）に「破棄しない」と答えたものとして扱う。
    /// </summary>
    private sealed class CustomersViewModel : ObservableObject
    {
        private readonly Mode _mode;
        private readonly Guard _guard;
        private Customer? _selectedCustomer;
        private Customer? _editing;

        public CustomersViewModel(Mode mode, Guard guard, IEnumerable<Customer> customers)
        {
            _mode = mode;
            _guard = guard;
            Customers = new ObservableCollection<Customer>(customers);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            if (guard == Guard.CurrentChanging)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(Customers);
                view.CurrentChanging += (_, e) =>
                {
                    if (IsDirty && e.IsCancelable && !ConfirmDiscard())
                    {
                        e.Cancel = true;
                    }
                };
                view.CurrentChanged += (_, _) => Select(view.CurrentItem as Customer);
                Select(view.CurrentItem as Customer);
            }
            else
            {
                SelectedCustomer = Customers[0];
            }
        }

        public ObservableCollection<Customer> Customers { get; }

        public ICommand SaveCommand { get; }

        public ICommand CancelCommand { get; }

        /// <summary>破棄の確認が呼ばれた回数。</summary>
        public int Confirmations { get; private set; }

        /// <summary>Save のコマンドが実行された回数。</summary>
        public int Saves { get; private set; }

        /// <summary>Cancel のコマンドが実行された回数。</summary>
        public int Cancels { get; private set; }

        /// <summary>Cancel のコマンドが実行された時点のソース（SelectedCustomer）の Name。</summary>
        public string? NameWhenCancelRan { get; private set; }

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (value == _selectedCustomer)
                {
                    return;
                }

                if (_guard is Guard.RejectInSetter or Guard.RejectDeferred && IsDirty && !ConfirmDiscard())
                {
                    if (_guard == Guard.RejectInSetter)
                    {
                        OnPropertyChanged();
                    }
                    else
                    {
                        // 優先度を指定しない BeginInvoke は DispatcherPriority.Normal で実行される。
                        Dispatcher.CurrentDispatcher.BeginInvoke(() => OnPropertyChanged(nameof(SelectedCustomer)));
                    }

                    return;
                }

                Select(value);
            }
        }

        /// <summary>詳細欄のバインド先（編集用のコピー）。</summary>
        public Customer? Editing
        {
            get => _editing;
            private set
            {
                Customer? old = _editing;
                if (SetProperty(ref _editing, value))
                {
                    if (old is not null)
                    {
                        old.PropertyChanged -= OnEditingChanged;
                    }

                    if (value is not null)
                    {
                        value.PropertyChanged += OnEditingChanged;
                    }

                    OnDirtyChanged();
                }
            }
        }

        public bool IsDirty => _mode == Mode.Copy
            && _selectedCustomer is not null && _editing is not null && !_editing.HasSameValues(_selectedCustomer);

        public bool CanChangeSelection => !IsDirty;

        /// <summary>破棄してよいかの確認。計測では「破棄しない」と答える。</summary>
        private bool ConfirmDiscard()
        {
            Confirmations++;
            return false;
        }

        private void OnEditingChanged(object? sender, PropertyChangedEventArgs e) => OnDirtyChanged();

        private void OnDirtyChanged()
        {
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(CanChangeSelection));
        }

        private void Select(Customer? value)
        {
            _selectedCustomer = value;
            OnPropertyChanged(nameof(SelectedCustomer));
            switch (_mode)
            {
                case Mode.Copy:
                    Editing = value?.Clone();
                    break;
                case Mode.Editable:
                    (value as IEditableObject)?.BeginEdit();
                    break;
            }
        }

        private void Save()
        {
            Saves++;
            switch (_mode)
            {
                case Mode.Copy when _selectedCustomer is not null && _editing is not null:
                    _selectedCustomer.CopyFrom(_editing);
                    OnDirtyChanged();
                    break;
                case Mode.Editable when _selectedCustomer is IEditableObject editable:
                    editable.EndEdit();
                    editable.BeginEdit();
                    break;
            }
        }

        private void Cancel()
        {
            Cancels++;
            NameWhenCancelRan = _selectedCustomer?.Name;
            switch (_mode)
            {
                case Mode.Copy:
                    Editing = _selectedCustomer?.Clone();
                    break;
                case Mode.Editable when _selectedCustomer is IEditableObject editable:
                    editable.CancelEdit();
                    editable.BeginEdit();
                    break;
            }
        }
    }

    private static Customer[] NewCustomers(Mode mode) => mode == Mode.Editable
        ? [new EditableCustomer { Name = "alpha", City = "Tokyo" }, new EditableCustomer { Name = "beta", City = "Osaka" }]
        : [new Customer { Name = "alpha", City = "Tokyo" }, new Customer { Name = "beta", City = "Osaka" }];

    private sealed record Page(DockPanel Root, Selector List, TextBox Name, CustomersViewModel ViewModel);

    private static Page Build(Mode mode, Guard guard, bool dataGrid = false, bool propertyChangedTrigger = false)
    {
        string selection = guard == Guard.CurrentChanging ? SynchronizedWithCurrentItem : SelectedItemBinding;
        string list = (dataGrid ? DataGridXaml : ListBoxXaml)
            .Replace("{Selection}", selection)
            .Replace("{IsEnabled}", guard == Guard.DisableList ? IsEnabledBinding : "");
        string xaml = PageXaml
            .Replace("{DetailTrigger}", propertyChangedTrigger ? PropertyChangedTrigger : "")
            .Replace("{DetailPath}", mode == Mode.Copy ? "Editing" : "SelectedCustomer")
            .Replace("{List}", list);
        var root = SceneContext.LoadXaml<DockPanel>(xaml);
        var vm = new CustomersViewModel(mode, guard, NewCustomers(mode));
        root.DataContext = vm;
        return new Page(root, (Selector)root.FindName("list"), (TextBox)root.FindName("name"), vm);
    }

    // ---------------------------------------------------------------- 読み取り

    private static string Quote(string? text) => text is null ? "null" : $"\"{text}\"";

    private static string NameOf(object? item) => item is Customer c ? Quote(c.Name) : "null";

    /// <summary>一覧の <paramref name="index"/> 行目に表示されている文字。</summary>
    private static string ShownText(Selector list, int index)
    {
        var container = (DependencyObject)list.ItemContainerGenerator.ContainerFromIndex(index);
        TextBlock block = Descendants(container).OfType<TextBlock>().First(b => b.Text.Length > 0);
        return Quote(block.Text);
    }

    /// <summary>画面で選ばれている行（コンテナの IsSelected が true の行）。</summary>
    private static string SelectedOnScreen(Selector list)
    {
        var rows = Enumerable.Range(0, list.Items.Count)
            .Where(i => list.ItemContainerGenerator.ContainerFromIndex(i) is DependencyObject c && Selector.GetIsSelected(c))
            .Select(i => Quote(((Customer)list.Items[i]).Name))
            .ToList();
        return rows.Count == 0 ? "-" : string.Join(", ", rows);
    }

    private static async Task TypeInNameAsync(Page page, string letters)
    {
        await FocusAsync(page.Name);
        page.Name.SelectAll();
        TypeInto(page.Name, letters);
        await Capture.SettleAsync(Window.GetWindow(page.Root)!, 100);
    }

    private static async Task ClickAsync(Window window, FrameworkElement target)
    {
        using (RealMouse.Preserve())
        {
            await RealMouse.ClickAsync(window, target);
        }

        await Capture.SettleAsync(window, 200);
    }

    /// <summary>Save か Cancel のボタンを実際のマウスでクリックし、そのコマンドが 1 回だけ実行されたことを確かめる。</summary>
    private static async Task ClickButtonAsync(Window window, Page page, string name)
    {
        int Count() => name == "save" ? page.ViewModel.Saves : page.ViewModel.Cancels;
        int before = Count();
        await ClickAsync(window, (Button)page.Root.FindName(name));
        if (Count() != before + 1)
        {
            throw new InvalidOperationException($"{name} のクリックでコマンドが 1 回だけ実行されていない（{before} → {Count()}）。");
        }
    }

    /// <summary>選択の変更を拒む止め方で、破棄の確認（拒む処理）が実際に呼ばれたことを確かめる。呼ばれずに結果だけ残る計測を防ぐ。</summary>
    private static void EnsureGuardRan(Page page, Guard guard)
    {
        if (guard is Guard.RejectInSetter or Guard.RejectDeferred or Guard.CurrentChanging && page.ViewModel.Confirmations == 0)
        {
            throw new InvalidOperationException($"{guard} の拒む処理が呼ばれていない。");
        }
    }

    private static FrameworkElement SecondRow(Page page) =>
        (FrameworkElement)page.List.ItemContainerGenerator.ContainerFromIndex(1);

    // ---------------------------------------------------------------- 図

    /// <summary>「問題」の図。SelectedCustomer.Name に UpdateSourceTrigger=PropertyChanged で直接バインドし、打っている途中の一覧を撮る。</summary>
    private static async Task ShootDirectTypingAsync(SceneContext context)
    {
        Page page = Build(Mode.Direct, Guard.None, propertyChangedTrigger: true);
        var window = new Window { Title = "Customers", Content = page.Root, SizeToContent = SizeToContent.WidthAndHeight };
        await context.ShootAsync(window, "master-detail-direct-typing.png", async _ =>
        {
            await FrontCenteredAsync(page.Root);
            await TypeInNameAsync(page, "edited");
            if (ShownText(page.List, 0) != "\"edited\"")
            {
                throw new InvalidOperationException("入力中の値が一覧に出ていない。図の前提が崩れている。");
            }
        });
    }

    /// <summary>セッターで選択の変更を拒んだ後の図。画面では 2 行目が選ばれ、詳細欄には 1 行目を編集した値が残る。</summary>
    private static async Task ShootRejectedSelectionAsync(SceneContext context)
    {
        Page page = Build(Mode.Copy, Guard.RejectInSetter);
        var window = new Window { Title = "Customers", Content = page.Root, SizeToContent = SizeToContent.WidthAndHeight };
        await context.ShootAsync(window, "master-detail-rejected-selection.png", async w =>
        {
            await FrontCenteredAsync(page.Root);
            await TypeInNameAsync(page, "edited");
            await ClickAsync(w, SecondRow(page));
            EnsureGuardRan(page, Guard.RejectInSetter);
            if (SelectedOnScreen(page.List) != "\"beta\"" || page.ViewModel.SelectedCustomer?.Name != "alpha")
            {
                throw new InvalidOperationException("画面とビューモデルの選択が食い違っていない。図の前提が崩れている。");
            }

            // 撮る前に、カーソルが図に写らない位置（ウィンドウの外）へ戻っていることを Preserve の破棄で保証している。
        });
    }

    // ---------------------------------------------------------------- 第一の壁: Cancel と Save

    private static Loc Describe(Mode mode, bool propertyChangedTrigger) => (mode, propertyChangedTrigger) switch
    {
        (Mode.Direct, false) => T("SelectedCustomer.Name (default trigger)", "SelectedCustomer.Name（既定のトリガー）"),
        (Mode.Direct, true) => "SelectedCustomer.Name, UpdateSourceTrigger=PropertyChanged",
        (Mode.Copy, _) => T("Editing.Name (copy for editing)", "Editing.Name（編集用のコピー）"),
        (Mode.Editable, _) => T("SelectedCustomer.Name with IEditableObject", "SelectedCustomer.Name ＋ IEditableObject"),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static async Task<List<IReadOnlyList<Loc>>> CancelCasesAsync() =>
    [
        await CancelAsync(Mode.Direct, propertyChangedTrigger: false),
        await CancelAsync(Mode.Direct, propertyChangedTrigger: true),
        await CancelAsync(Mode.Copy, propertyChangedTrigger: false),
        await CancelAsync(Mode.Editable, propertyChangedTrigger: false),
    ];

    private static async Task<IReadOnlyList<Loc>> CancelAsync(Mode mode, bool propertyChangedTrigger)
    {
        Page page = Build(mode, Guard.None, propertyChangedTrigger: propertyChangedTrigger);
        Customer first = page.ViewModel.Customers[0];
        string typing = "";
        string list = "";
        string detail = "";
        await ShowAsync(page.Root, async () =>
        {
            Window window = await FrontCenteredAsync(page.Root);
            await TypeInNameAsync(page, "edited");
            typing = Quote(first.Name);
            await ClickButtonAsync(window, page, "cancel");
            list = ShownText(page.List, 0);
            detail = Quote(page.Name.Text);
        }, activate: true);

        return [Describe(mode, propertyChangedTrigger), typing, Quote(page.ViewModel.NameWhenCancelRan), list, detail];
    }

    private static async Task<List<IReadOnlyList<Loc>>> SaveCasesAsync() =>
    [
        await SaveAsync(Mode.Copy),
        await SaveAsync(Mode.Editable),
    ];

    private static async Task<IReadOnlyList<Loc>> SaveAsync(Mode mode)
    {
        Page page = Build(mode, Guard.None);
        Customer first = page.ViewModel.Customers[0];
        string list = "";
        await ShowAsync(page.Root, async () =>
        {
            Window window = await FrontCenteredAsync(page.Root);
            await TypeInNameAsync(page, "edited");
            await ClickButtonAsync(window, page, "save");
            list = ShownText(page.List, 0);
        }, activate: true);

        return [Describe(mode, propertyChangedTrigger: false), Quote(first.Name), list];
    }

    // ---------------------------------------------------------------- 第二の壁: 編集中の行の切り替え

    private static Loc Describe(Guard guard, Mode mode, bool propertyChangedTrigger, bool cancelFirst) => guard switch
    {
        Guard.None when mode == Mode.Copy => T("nothing (copy for editing)", "なし（編集用のコピー）"),
        Guard.None => T("nothing (IEditableObject)", "なし（IEditableObject）"),
        Guard.RejectInSetter => T("reject in the setter, raise PropertyChanged", "セッターで拒んで PropertyChanged"),
        Guard.RejectDeferred => T("reject in the setter, raise PropertyChanged via Dispatcher.BeginInvoke (Normal priority)", "セッターで拒んで Dispatcher.BeginInvoke（Normal の優先度）で PropertyChanged"),
        Guard.CurrentChanging => T("IsSynchronizedWithCurrentItem, e.Cancel = true in CurrentChanging", "IsSynchronizedWithCurrentItem ＋ CurrentChanging で e.Cancel = true"),
        Guard.DisableList when cancelFirst => T("IsEnabled=\"{Binding CanChangeSelection}\", detail with UpdateSourceTrigger=PropertyChanged, click Cancel first", "IsEnabled=\"{Binding CanChangeSelection}\"、詳細欄は UpdateSourceTrigger=PropertyChanged、先に Cancel をクリック"),
        Guard.DisableList when propertyChangedTrigger => T("IsEnabled=\"{Binding CanChangeSelection}\", detail with UpdateSourceTrigger=PropertyChanged", "IsEnabled=\"{Binding CanChangeSelection}\"、詳細欄は UpdateSourceTrigger=PropertyChanged"),
        Guard.DisableList => T("IsEnabled=\"{Binding CanChangeSelection}\", detail with the default trigger", "IsEnabled=\"{Binding CanChangeSelection}\"、詳細欄は既定のトリガー"),
        _ => throw new ArgumentOutOfRangeException(nameof(guard)),
    };

    private static async Task<List<IReadOnlyList<Loc>>> SwitchCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>
        {
            await SwitchAsync(Mode.Copy, Guard.None, dataGrid: false),
            await SwitchAsync(Mode.Editable, Guard.None, dataGrid: false),
        };
        foreach (bool dataGrid in new[] { false, true })
        {
            foreach (Guard guard in new[] { Guard.RejectInSetter, Guard.RejectDeferred, Guard.CurrentChanging })
            {
                rows.Add(await SwitchAsync(Mode.Copy, guard, dataGrid));
            }

            rows.Add(await SwitchAsync(Mode.Copy, Guard.DisableList, dataGrid, propertyChangedTrigger: true));
        }

        rows.Add(await SwitchAsync(Mode.Copy, Guard.DisableList, dataGrid: false, propertyChangedTrigger: false));
        rows.Add(await SwitchAsync(Mode.Copy, Guard.DisableList, dataGrid: false, propertyChangedTrigger: true, cancelFirst: true));
        return rows;
    }

    private static async Task<IReadOnlyList<Loc>> SwitchAsync(Mode mode, Guard guard, bool dataGrid, bool propertyChangedTrigger = false, bool cancelFirst = false)
    {
        Page page = Build(mode, guard, dataGrid, propertyChangedTrigger);
        IReadOnlyList<Loc>? row = null;
        Loc enabledBeforeClick = "";
        await ShowAsync(page.Root, async () =>
        {
            Window window = await FrontCenteredAsync(page.Root);
            await TypeInNameAsync(page, "edited");
            string afterTyping = page.List.IsEnabled ? "True" : "False";
            if (cancelFirst)
            {
                await ClickButtonAsync(window, page, "cancel");
            }

            using (RealMouse.Preserve())
            {
                // カーソルを 2 行目へ動かし終えてから、ボタンを押す直前の IsEnabled を読む。
                FrameworkElement second = SecondRow(page);
                await RealMouse.MoveToAsync(second, null, window);
                string beforePress = page.List.IsEnabled ? "True" : "False";
                enabledBeforeClick = cancelFirst
                    ? T($"{afterTyping} → {beforePress} (Cancel clicked in between)", $"{afterTyping} → {beforePress}（間に Cancel をクリック）")
                    : beforePress;
                await RealMouse.LeftDownAsync(window, second);
                await RealMouse.LeftUpAsync(window);
            }

            await Capture.SettleAsync(window, 200);
            EnsureGuardRan(page, guard);
            ICollectionView view = CollectionViewSource.GetDefaultView(page.ViewModel.Customers);
            row =
            [
                dataGrid ? "DataGrid" : "ListBox",
                Describe(guard, mode, propertyChangedTrigger, cancelFirst),
                NameOf(page.ViewModel.SelectedCustomer),
                $"{NameOf(page.List.SelectedItem)} / {page.List.SelectedIndex}",
                SelectedOnScreen(page.List),
                NameOf(view.CurrentItem),
                Quote(page.Name.Text),
                ShownText(page.List, 0),
                enabledBeforeClick,
            ];
        }, activate: true);

        return row ?? throw new InvalidOperationException("計測できなかった。");
    }

    private static async Task<List<IReadOnlyList<Loc>>> SaveAfterStoppedCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach (bool dataGrid in new[] { false, true })
        {
            foreach (Guard guard in new[] { Guard.RejectInSetter, Guard.CurrentChanging })
            {
                rows.Add(await SaveAfterStoppedAsync(guard, dataGrid));
            }
        }

        return rows;
    }

    /// <summary>1 行目に入力し、2 行目をクリックして選択の変更を止められた後に、Save をクリックする。</summary>
    private static async Task<IReadOnlyList<Loc>> SaveAfterStoppedAsync(Guard guard, bool dataGrid)
    {
        Page page = Build(Mode.Copy, guard, dataGrid);
        string highlighted = "";
        await ShowAsync(page.Root, async () =>
        {
            Window window = await FrontCenteredAsync(page.Root);
            await TypeInNameAsync(page, "edited");
            await ClickAsync(window, SecondRow(page));
            EnsureGuardRan(page, guard);
            highlighted = SelectedOnScreen(page.List);
            await ClickButtonAsync(window, page, "save");
        }, activate: true);

        return
        [
            dataGrid ? "DataGrid" : "ListBox",
            Describe(guard, Mode.Copy, propertyChangedTrigger: false, cancelFirst: false),
            highlighted,
            Quote(page.ViewModel.Customers[0].Name),
            Quote(page.ViewModel.Customers[1].Name),
        ];
    }
}
