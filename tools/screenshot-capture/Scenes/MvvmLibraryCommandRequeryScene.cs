using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Commands;
using Prism.Mvvm;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「CommunityToolkit.Mvvm と Prism のコマンドは InvalidateRequerySuggested で再評価されない」の図と表。
///
/// 名前の TextBox と保存ボタンを並べたビュー（XAML のバインドだけで動かす）に、書き方の違うビューモデルを渡し、
/// 名前をキーで入力した後・CommandManager.InvalidateRequerySuggested を呼んだ後・フォーカスを別の TextBox へ移した後の
/// 保存ボタンの IsEnabled を読む。
/// CanExecute は「名前が空でない」で、どの書き方でも同じにしている。
/// Prism は MIT ライセンスの最後の版（8.1.97）を使う（csproj の注記を参照）。
/// </summary>
internal sealed partial class MvvmLibraryCommandRequeryScene : IScene
{
    private const string TypedName = "taro";

    public IReadOnlyList<string> Verifies =>
    [
        "CommunityToolkit.Mvvm 8.4.0 の RelayCommand は、CanExecute が依存するプロパティを SetProperty で変えただけでは、名前をキーで入力しても、CommandManager.InvalidateRequerySuggested を呼んでも、フォーカスを別の TextBox へ移しても、ボタンの IsEnabled が False のままであること",
        "Prism.Core 8.1.97 の DelegateCommand も、SetProperty だけでは同じ 3 つの操作のどれでも IsEnabled が False のままであること",
        "Toolkit の setter で NotifyCanExecuteChanged を呼ぶ書き方と [NotifyCanExecuteChangedFor] を付けた書き方、Prism の setter で RaiseCanExecuteChanged を呼ぶ書き方と ObservesProperty を付けた書き方は、名前を入力した時点で IsEnabled が True になること",
        "CanExecuteChanged を CommandManager.RequerySuggested へ転送した自作のコマンドは、何も呼ばなくても名前をキーで入力した時点で IsEnabled が True になること（対照）",
        "同じ自作のコマンドで、名前をキー入力ではなくビューモデルのプロパティへの代入で変えると IsEnabled は False のままで、InvalidateRequerySuggested を呼んだ後に True になること（InvalidateRequerySuggested が届く対照）",
        "CommunityToolkit.Mvvm と Prism.Core の、実行時に読み込まれたアセンブリが PresentationCore・PresentationFramework・WindowsBase のどれも参照していないこと（CommandManager は PresentationCore にある）",
        "図は、Toolkit の RelayCommand と Prism の DelegateCommand（どちらも SetProperty だけ）と、転送した自作のコマンドの 3 つに同じ名前をキーで入力し、InvalidateRequerySuggested を呼び、キャレットを写さないためにキーボードフォーカスを外した後に撮っていること",
    ];

    public string Slug => "wpf-mvvm-library-command-requery";

    public async Task CaptureAsync(SceneContext context)
    {
        await ShootFigureAsync(context);
        await SaveMatrixAsync(context);
        await SaveReferencesAsync(context);
    }

    // ------------------------------------------------------------ ライブラリが参照するアセンブリ

    /// <summary>
    /// コマンドの型を含むアセンブリが、CommandManager を含む PresentationCore を参照しているかを読む。
    /// 参照していなければ、そのコマンドは CommandManager.RequerySuggested を静的なコードでは購読していない。
    /// 実際に購読していないことは、SaveMatrixAsync の実測（InvalidateRequerySuggested の後も False）で確かめる。
    /// </summary>
    private static async Task SaveReferencesAsync(SceneContext context)
    {
        (string Name, Type Type)[] types =
        [
            ("CommunityToolkit.Mvvm.Input.RelayCommand", typeof(RelayCommand)),
            ("Prism.Commands.DelegateCommand", typeof(DelegateCommand)),
        ];

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string name, Type type) in types)
        {
            System.Reflection.Assembly assembly = type.Assembly;
            string target = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>()
                .Select(attribute => attribute.FrameworkName)
                .FirstOrDefault() ?? "-";
            bool referencesWpf = assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name is "PresentationCore" or "PresentationFramework" or "WindowsBase");
            if (referencesWpf)
            {
                throw new InvalidOperationException($"{name} のアセンブリが WPF のアセンブリを参照している。記事の説明と合わない。");
            }

            rows.Add([name, assembly.GetName().Name + " " + assembly.GetName().Version, target, "False"]);
        }

        await context.SaveTableAsync(
            "Does the command's assembly reference WPF?",
            [
                Loc.Of("type", "型"),
                Loc.Of("assembly", "アセンブリ"),
                Loc.Of("TargetFramework of the loaded assembly", "読み込まれたアセンブリの TargetFramework"),
                Loc.Of("references PresentationCore / PresentationFramework / WindowsBase", "PresentationCore / PresentationFramework / WindowsBase を参照する"),
            ],
            rows,
            "library-command-references.svg");
    }

    // ------------------------------------------------------------ 図

    private static async Task ShootFigureAsync(SceneContext context)
    {
        FrameworkElement[] views =
        [
            BuildView(new ToolkitPlainViewModel()),
            BuildView(new PrismViewModel(PrismMode.SetPropertyOnly)),
            BuildView(new RequeryViewModel()),
        ];

        Window window = DemoLayout.BuildPanelWindow(
            "InvalidateRequerySuggested after typing",
            [
                new DemoLayout.Panel("CommunityToolkit.Mvvm RelayCommand  (SetProperty only)", views[0]),
                new DemoLayout.Panel("Prism DelegateCommand  (SetProperty only)", views[1]),
                new DemoLayout.Panel("own RelayCommand  (CanExecuteChanged => CommandManager.RequerySuggested)", views[2]),
            ],
            Orientation.Vertical);

        await context.ShootAsync(window, "library-command-requery.png", async _ =>
        {
            foreach (FrameworkElement view in views)
            {
                var box = (TextBox)view.FindName("nameBox");
                await FocusAsync(box);
                TypeInto(box, TypedName);
                await Capture.SettleAsync(window);
            }

            CommandManager.InvalidateRequerySuggested();
            await Capture.SettleAsync(window, 100);

            // 撮影に入力欄のキャレットが写らないよう、フォーカスを外す。
            Keyboard.ClearFocus();
            await Capture.SettleAsync(window);

            bool[] enabled = views.Select(view => ((Button)view.FindName("saveButton")).IsEnabled).ToArray();
            if (enabled is not [false, false, true])
            {
                throw new InvalidOperationException($"図の状態が想定と異なる（{string.Join(", ", enabled)}）。");
            }
        });
    }

    // ------------------------------------------------------------ 表

    private static async Task SaveMatrixAsync(SceneContext context)
    {
        // ByCode が真の行は、名前をキー入力ではなくビューモデルへの代入で変える（自作のコマンドだけ）。
        (Loc Label, Func<object> Create, bool ByCode, string Expected)[] cases =
        [
            (T("own RelayCommand, CanExecuteChanged => CommandManager.RequerySuggested (name typed)", "自作の RelayCommand、CanExecuteChanged を CommandManager.RequerySuggested へ転送（名前をキーで入力）"),
                () => new RequeryViewModel(), false, "False / True / True / True"),
            (T("same command, name assigned to the view model in code", "同じ自作のコマンド、名前をビューモデルへコードで代入"),
                () => new RequeryViewModel(), true, "False / False / True / True"),
            (T("Toolkit RelayCommand, setter calls SetProperty only", "Toolkit の RelayCommand、setter は SetProperty だけ"),
                () => new ToolkitPlainViewModel(), false, "False / False / False / False"),
            (T("Toolkit RelayCommand, setter calls NotifyCanExecuteChanged", "Toolkit の RelayCommand、setter で NotifyCanExecuteChanged"),
                () => new ToolkitNotifyingViewModel(), false, "False / True / True / True"),
            (T("Toolkit [ObservableProperty] + [NotifyCanExecuteChangedFor]", "Toolkit の [ObservableProperty] に [NotifyCanExecuteChangedFor]"),
                () => new ToolkitGeneratedViewModel(), false, "False / True / True / True"),
            (T("Prism DelegateCommand, setter calls SetProperty only", "Prism の DelegateCommand、setter は SetProperty だけ"),
                () => new PrismViewModel(PrismMode.SetPropertyOnly), false, "False / False / False / False"),
            (T("Prism DelegateCommand, setter calls RaiseCanExecuteChanged", "Prism の DelegateCommand、setter で RaiseCanExecuteChanged"),
                () => new PrismViewModel(PrismMode.RaiseInSetter), false, "False / True / True / True"),
            (T("Prism DelegateCommand + ObservesProperty(() => Name)", "Prism の DelegateCommand に ObservesProperty(() => Name)"),
                () => new PrismViewModel(PrismMode.ObservesProperty), false, "False / True / True / True"),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        var mismatches = new List<string>();
        foreach ((Loc label, Func<object> create, bool byCode, string expected) in cases)
        {
            string[] values = await MeasureAsync(create(), byCode);
            if (string.Join(" / ", values) != expected)
            {
                mismatches.Add($"{label.Ja}: {string.Join(" / ", values)}（想定 {expected}）");
            }

            rows.Add([label, .. values.Select(value => (Loc)value)]);
        }

        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException("想定と異なる結果が出た。" + Environment.NewLine + string.Join(Environment.NewLine, mismatches));
        }

        await context.SaveTableAsync(
            "Save button IsEnabled (CanExecute: Name is not empty)",
            [
                Loc.Of("command and view model", "コマンドとビューモデル"),
                Loc.Of("before typing", "入力前"),
                Loc.Of("after setting the name", "名前を入れた後"),
                Loc.Of("after InvalidateRequerySuggested", "InvalidateRequerySuggested の後"),
                Loc.Of("after moving focus", "フォーカスを移した後"),
            ],
            rows,
            "library-command-requery-matrix.svg");
    }

    /// <summary>
    /// ビューを表示し、名前の TextBox へ InputManager を通して文字を打ち込む（<paramref name="byCode"/> なら、ビューモデルのプロパティへ代入する）。
    /// 続けて InvalidateRequerySuggested を呼び、最後にキーボードフォーカスを 2 つ目の TextBox へ移す。
    /// 各段階の後に、保存ボタンの IsEnabled を読む。
    /// </summary>
    private static async Task<string[]> MeasureAsync(object viewModel, bool byCode)
    {
        FrameworkElement view = BuildView(viewModel);
        var values = new string[4];
        await ShowAsync(view, async () =>
        {
            Window window = Window.GetWindow(view)!;
            var nameBox = (TextBox)view.FindName("nameBox");
            var otherBox = (TextBox)view.FindName("otherBox");
            var save = (Button)view.FindName("saveButton");

            await FocusAsync(nameBox);
            values[0] = save.IsEnabled.ToString();

            if (byCode)
            {
                // キー入力を伴わずに条件だけを変える。WPF の入力による再問い合わせが起きない。
                if (viewModel is not RequeryViewModel requery)
                {
                    throw new ArgumentException("コードで代入する行は RequeryViewModel だけで測る。", nameof(viewModel));
                }

                requery.Name = TypedName;
            }
            else
            {
                TypeInto(nameBox, TypedName);
            }

            await Capture.SettleAsync(window, 100);
            values[1] = save.IsEnabled.ToString();

            CommandManager.InvalidateRequerySuggested();
            await Capture.SettleAsync(window, 100);
            values[2] = save.IsEnabled.ToString();

            await FocusAsync(otherBox);
            if (!otherBox.IsKeyboardFocused)
            {
                throw new InvalidOperationException("フォーカスを 2 つ目の TextBox へ移せない。");
            }

            await Capture.SettleAsync(window, 100);
            values[3] = save.IsEnabled.ToString();
        }, activate: true);
        return values;
    }

    /// <summary>記事に載せるビュー。コードビハインドは持たず、バインドだけで動かす。</summary>
    private static FrameworkElement BuildView(object viewModel)
    {
        var view = SceneContext.LoadXaml<StackPanel>(
            """
            <StackPanel Orientation="Horizontal" Margin="4">
              <TextBox x:Name="nameBox" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" Width="150" />
              <Button x:Name="saveButton" Content="Save" Command="{Binding SaveCommand}" Padding="14,4" Margin="12,0" />
              <TextBox x:Name="otherBox" Width="80" />
            </StackPanel>
            """);
        view.DataContext = viewModel;
        return view;
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // ------------------------------------------------------------ ビューモデル（記事のコード例と同じ）

    /// <summary>Toolkit の RelayCommand。setter は SetProperty だけで、コマンドに通知しない。</summary>
    private sealed class ToolkitPlainViewModel : ObservableObject
    {
        private string _name = "";

        public ToolkitPlainViewModel() => SaveCommand = new RelayCommand(Save, () => !string.IsNullOrEmpty(Name));

        public RelayCommand SaveCommand { get; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private void Save()
        {
        }
    }

    /// <summary>Toolkit の RelayCommand。setter で値が変わったら NotifyCanExecuteChanged を呼ぶ。</summary>
    private sealed class ToolkitNotifyingViewModel : ObservableObject
    {
        private string _name = "";

        public ToolkitNotifyingViewModel() => SaveCommand = new RelayCommand(Save, () => !string.IsNullOrEmpty(Name));

        public RelayCommand SaveCommand { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private void Save()
        {
        }
    }

    /// <summary>Toolkit のソース生成。生成された Name の setter が SaveCommand.NotifyCanExecuteChanged を呼ぶ。</summary>
    private sealed partial class ToolkitGeneratedViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = "";

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
        }

        private bool CanSave() => !string.IsNullOrEmpty(Name);
    }

    private enum PrismMode
    {
        SetPropertyOnly,
        RaiseInSetter,
        ObservesProperty,
    }

    /// <summary>Prism の DelegateCommand。<paramref name="mode"/> で通知の仕方を切り替える。</summary>
    private sealed class PrismViewModel : BindableBase
    {
        private readonly PrismMode _mode;
        private string _name = "";

        public PrismViewModel(PrismMode mode)
        {
            _mode = mode;
            var command = new DelegateCommand(Save, () => !string.IsNullOrEmpty(Name));
            SaveCommand = mode == PrismMode.ObservesProperty ? command.ObservesProperty(() => Name) : command;
        }

        public DelegateCommand SaveCommand { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value) && _mode == PrismMode.RaiseInSetter)
                {
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private void Save()
        {
        }
    }

    /// <summary>対照。CanExecuteChanged の購読を CommandManager.RequerySuggested へ転送する自作のコマンドを持つ。</summary>
    private sealed class RequeryViewModel : INotifyPropertyChanged
    {
        private string _name = "";

        public RequeryViewModel() => SaveCommand = new RequeryRelayCommand(() => { }, () => !string.IsNullOrEmpty(Name));

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand SaveCommand { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }

    private sealed class RequeryRelayCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter) => execute();
    }
}
