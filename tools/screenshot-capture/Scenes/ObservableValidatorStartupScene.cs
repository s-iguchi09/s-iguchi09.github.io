using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Mvvm;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「CommunityToolkit.Mvvm の ObservableValidator で、起動直後に検証エラーが出ない理由」の図と表。
///
/// 名前の TextBox（メッセージを出す ErrorTemplate 付き）だけのビューに、検証の書き方が違うビューモデルを渡し、
/// 起動直後・21 文字を入力した後・入力を消して空にした後の Validation.HasError を読む。
/// 続けて保存ボタンを足したビューで、起動直後の保存ボタンの IsEnabled、保存ボタンを押したときに保存まで進んだ回数、正しい名前を入力した後の IsEnabled を読む。
/// 検証の規則はどの書き方でも「必須」と「20 文字まで」で同じにしている。
/// Prism は MIT ライセンスの最後の版（8.1.97）を使う（csproj の注記を参照）。
/// </summary>
internal sealed partial class ObservableValidatorStartupScene : IScene
{
    private const string LongName = "abcdefghijklmnopqrstu";
    private const string ValidName = "taro";
    private const string RequiredMessage = "Name is required.";
    private const string LengthMessage = "Name must be 20 characters or fewer.";

    public IReadOnlyList<string> Verifies =>
    [
        "CommunityToolkit.Mvvm 8.4.0 の ObservableValidator で、setter が SetProperty(ref field, value, true) を呼ぶ書き方と、[ObservableProperty] に [NotifyDataErrorInfo] を付けた書き方は、起動直後（名前は空）には Validation.HasError が False で、21 文字を入力すると True になり、入力を消して空にしても True であること",
        "[ObservableProperty] に検証属性（[Required]・[MaxLength]）だけを付け、[NotifyDataErrorInfo] を付けない書き方は、起動直後・21 文字の入力後・空にした後のどれでも Validation.HasError が False のままであること",
        "コンストラクターで ValidateAllProperties() を呼ぶと、起動直後から Validation.HasError が True になり、必須のメッセージが ErrorTemplate に描かれること",
        "Prism.Core 8.1.97 の BindableBase と ErrorsContainer で setter から検証する書き方も、起動直後は False で、コンストラクターで検証すると起動直後から True になること",
        "保存ボタンの CanExecute を「HasErrors が False」にし、ErrorsChanged で NotifyCanExecuteChanged を呼んでも、コンストラクターで検証しなければ起動直後のボタンは有効で、押すと空の名前のまま保存まで進むこと。コンストラクターで ValidateAllProperties() を呼ぶと（ErrorsChanged の購読より前に呼んでも）起動直後のボタンは無効になること",
        "CanExecute も ErrorsChanged の購読も持たず、保存処理の先頭で ValidateAllProperties() を呼んで HasErrors なら戻る書き方は、起動直後にはエラーを出さず、保存ボタンを押した時点で保存まで進まずに必須のメッセージを出すこと",
        "保存の表のどの書き方でも、続けて正しい名前（taro）を入力すると保存ボタンの IsEnabled が True になること（コンストラクターで検証した書き方では、無効から有効に戻る）",
        "図は、[NotifyDataErrorInfo] だけの書き方とコンストラクターで ValidateAllProperties() を呼ぶ書き方の起動直後、および保存処理の先頭で検証する書き方で保存ボタンを押した後に撮っていること。保存ボタンは上から有効・無効・有効であること",
    ];

    public string Slug => "wpf-observablevalidator-no-error-on-startup";

    public async Task CaptureAsync(SceneContext context)
    {
        await ShootFigureAsync(context);
        await SaveDisplayMatrixAsync(context);
        await SaveSaveMatrixAsync(context);
    }

    // ------------------------------------------------------------ 図

    private static async Task ShootFigureAsync(SceneContext context)
    {
        FrameworkElement[] views =
        [
            BuildSaveView(new ToolkitGuardedSaveViewModel(validateOnStart: false)),
            BuildSaveView(new ToolkitGuardedSaveViewModel(validateOnStart: true)),
            BuildSaveView(new ToolkitValidateOnSaveViewModel()),
        ];

        Window window = DemoLayout.BuildPanelWindow(
            "ObservableValidator on startup",
            [
                new DemoLayout.Panel("[NotifyDataErrorInfo] only  (on startup)", views[0]),
                new DemoLayout.Panel("ValidateAllProperties() in the constructor  (on startup)", views[1]),
                new DemoLayout.Panel("ValidateAllProperties() in Save  (after clicking Save)", views[2]),
            ],
            Orientation.Vertical);

        await context.ShootAsync(window, "observablevalidator-startup.png", async _ =>
        {
            await Capture.SettleAsync(window);
            Click((Button)views[2].FindName("saveButton"));
            await Capture.SettleAsync(window, 100);

            bool[] errors = views.Select(view => Validation.GetHasError(NameBox(view))).ToArray();
            if (errors is not [false, true, true])
            {
                throw new InvalidOperationException($"図の状態が想定と異なる（{string.Join(", ", errors)}）。");
            }

            string[] messages = views.Skip(1).Select(view => Message(NameBox(view))).ToArray();
            if (messages.Any(message => message != RequiredMessage))
            {
                throw new InvalidOperationException($"図のメッセージが想定と異なる（{string.Join(", ", messages)}）。");
            }

            bool[] enabled = views.Select(view => ((Button)view.FindName("saveButton")).IsEnabled).ToArray();
            if (enabled is not [true, false, true])
            {
                throw new InvalidOperationException($"図の保存ボタンの状態が想定と異なる（{string.Join(", ", enabled)}）。");
            }
        });
    }

    // ------------------------------------------------------------ 表: エラーが表示されるか

    private static async Task SaveDisplayMatrixAsync(SceneContext context)
    {
        (Loc Label, Func<object> Create, string Expected)[] cases =
        [
            (T("Toolkit, setter calls SetProperty(ref _name, value, true)", "Toolkit、setter で SetProperty(ref _name, value, true)"),
                () => new ToolkitManualViewModel(), "False / True / True"),
            (T("Toolkit [ObservableProperty] + [Required] / [MaxLength] only", "Toolkit の [ObservableProperty] に [Required] / [MaxLength] だけ"),
                () => new ToolkitAttributesOnlyViewModel(), "False / False / False"),
            (T("Toolkit [ObservableProperty] + [NotifyDataErrorInfo]", "Toolkit の [ObservableProperty] に [NotifyDataErrorInfo]"),
                () => new ToolkitNotifyingViewModel(validateOnStart: false), "False / True / True"),
            (T("same, plus ValidateAllProperties() in the constructor", "同じ書き方で、コンストラクターで ValidateAllProperties()"),
                () => new ToolkitNotifyingViewModel(validateOnStart: true), "True / True / True"),
            (T("Prism BindableBase + ErrorsContainer, setter validates", "Prism の BindableBase と ErrorsContainer、setter で検証"),
                () => new PrismAccountViewModel(validateOnStart: false), "False / True / True"),
            (T("same, plus validation in the constructor", "同じ書き方で、コンストラクターでも検証"),
                () => new PrismAccountViewModel(validateOnStart: true), "True / True / True"),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        var mismatches = new List<string>();
        foreach ((Loc label, Func<object> create, string expected) in cases)
        {
            string[] values = await MeasureDisplayAsync(create());
            if (string.Join(" / ", values) != expected)
            {
                mismatches.Add($"{label.Ja}: {string.Join(" / ", values)}（想定 {expected}）");
            }

            rows.Add([label, .. values.Select(value => (Loc)value)]);
        }

        ThrowIfAny(mismatches);

        await context.SaveTableAsync(
            "Validation.HasError of the name TextBox (rules: required, up to 20 characters)",
            [
                Loc.Of("how the view model validates", "ビューモデルの検証の書き方"),
                Loc.Of("on startup (name is empty)", "起動直後（名前は空）"),
                Loc.Of("after typing 21 characters", "21 文字を入力した後"),
                Loc.Of("after clearing the input", "入力を消して空にした後"),
            ],
            rows,
            "observablevalidator-display-matrix.svg");
    }

    /// <summary>
    /// ビューを表示し、起動直後の HasError を読む。
    /// 続けて名前の TextBox へ InputManager を通して 21 文字を打ち込み、最後に全選択して Backspace で消す。各段階の後に HasError を読む。
    /// </summary>
    private static async Task<string[]> MeasureDisplayAsync(object viewModel)
    {
        FrameworkElement view = BuildNameView(viewModel);
        var values = new string[3];
        await ShowAsync(view, async () =>
        {
            Window window = Window.GetWindow(view)!;
            TextBox box = NameBox(view);
            await Capture.SettleAsync(window, 100);
            values[0] = Validation.GetHasError(box).ToString();
            if (values[0] == "True" && Message(box) != RequiredMessage)
            {
                throw new InvalidOperationException($"起動直後のメッセージが想定と異なる（{Message(box)}）。");
            }

            await FocusAsync(box);
            TypeInto(box, LongName);
            await Capture.SettleAsync(window, 100);
            values[1] = Validation.GetHasError(box).ToString();
            if (values[1] == "True" && Message(box) != LengthMessage)
            {
                throw new InvalidOperationException($"21 文字のメッセージが想定と異なる（{Message(box)}）。");
            }

            box.SelectAll();
            PressKey(box, System.Windows.Input.Key.Back);
            await Capture.SettleAsync(window, 100);
            if (box.Text.Length != 0)
            {
                throw new InvalidOperationException("入力を消せない。");
            }

            values[2] = Validation.GetHasError(box).ToString();
            if (values[2] == "True" && Message(box) != RequiredMessage)
            {
                throw new InvalidOperationException($"空にした後のメッセージが想定と異なる（{Message(box)}）。");
            }
        }, activate: true);
        return values;
    }

    // ------------------------------------------------------------ 表: 起動直後に保存できるか

    private static async Task SaveSaveMatrixAsync(SceneContext context)
    {
        (Loc Label, Func<ISaveCounter> Create, string Expected)[] cases =
        [
            (T("CanExecute: !HasErrors, [NotifyDataErrorInfo] only", "CanExecute は !HasErrors、[NotifyDataErrorInfo] だけ"),
                () => new ToolkitGuardedSaveViewModel(validateOnStart: false), "True / False / 1 / False / True"),
            (T("CanExecute: !HasErrors, plus ValidateAllProperties() in the constructor", "CanExecute は !HasErrors、コンストラクターで ValidateAllProperties()"),
                () => new ToolkitGuardedSaveViewModel(validateOnStart: true), "False / True / - / - / True"),
            (T("no CanExecute, ValidateAllProperties() at the start of Save", "CanExecute なし、Save の先頭で ValidateAllProperties()"),
                () => new ToolkitValidateOnSaveViewModel(), "True / False / 0 / True / True"),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        var mismatches = new List<string>();
        foreach ((Loc label, Func<ISaveCounter> create, string expected) in cases)
        {
            string[] values = await MeasureSaveAsync(create());
            if (string.Join(" / ", values) != expected)
            {
                mismatches.Add($"{label.Ja}: {string.Join(" / ", values)}（想定 {expected}）");
            }

            rows.Add([label, .. values.Select(value => value == "-" ? Loc.Of("- (cannot click)", "-（押せない）") : (Loc)value)]);
        }

        ThrowIfAny(mismatches);

        await context.SaveTableAsync(
            "Saving with the name still empty (Toolkit ObservableValidator)",
            [
                Loc.Of("how Save is guarded", "保存の止め方"),
                Loc.Of("Save button IsEnabled on startup", "起動直後の保存ボタンの IsEnabled"),
                Loc.Of("HasError on startup", "起動直後の HasError"),
                Loc.Of("times the save went through after clicking Save", "保存ボタンを押した後に保存まで進んだ回数"),
                Loc.Of("HasError after clicking Save", "保存ボタンを押した後の HasError"),
                Loc.Of("Save button IsEnabled after typing a valid name", "正しい名前を入力した後の保存ボタンの IsEnabled"),
            ],
            rows,
            "observablevalidator-save-matrix.svg");
    }

    /// <summary>
    /// 保存ボタン付きのビューを表示し、起動直後の IsEnabled と HasError を読む。
    /// ボタンが有効なら UI オートメーションの Invoke で押し（Click イベントからコマンドが実行される）、保存まで進んだ回数と HasError を読む。
    /// 無効なら押せないので、押した後の 2 列は「-」にする。
    /// 最後に、名前の TextBox へ InputManager を通して正しい名前を打ち込み、保存ボタンの IsEnabled を読む。
    /// </summary>
    private static async Task<string[]> MeasureSaveAsync(ISaveCounter viewModel)
    {
        FrameworkElement view = BuildSaveView(viewModel);
        var values = new string[5];
        await ShowAsync(view, async () =>
        {
            Window window = Window.GetWindow(view)!;
            TextBox box = NameBox(view);
            var save = (Button)view.FindName("saveButton");
            async Task TypeValidNameAsync()
            {
                await FocusAsync(box);
                TypeInto(box, ValidName);
                await Capture.SettleAsync(window, 100);
                values[4] = save.IsEnabled.ToString();
            }

            await Capture.SettleAsync(window, 100);
            values[0] = save.IsEnabled.ToString();
            values[1] = Validation.GetHasError(box).ToString();
            if (values[1] == "True" && Message(box) != RequiredMessage)
            {
                throw new InvalidOperationException($"起動直後のメッセージが想定と異なる（{Message(box)}）。");
            }

            if (!save.IsEnabled)
            {
                values[2] = "-";
                values[3] = "-";
                await TypeValidNameAsync();
                return;
            }

            Click(save);
            await Capture.SettleAsync(window, 100);
            values[2] = viewModel.SaveCount.ToString();
            values[3] = Validation.GetHasError(box).ToString();
            if (values[3] == "True" && Message(box) != RequiredMessage)
            {
                throw new InvalidOperationException($"保存の後のメッセージが想定と異なる（{Message(box)}）。");
            }

            await TypeValidNameAsync();
        }, activate: true);
        return values;
    }

    // ------------------------------------------------------------ ビュー（コードビハインドは持たず、バインドだけで動かす）

    private const string ErrorTemplateResources =
        """
        <StackPanel.Resources>
          <ControlTemplate x:Key="FieldErrorTemplate">
            <StackPanel>
              <Border BorderBrush="#D13438" BorderThickness="1">
                <AdornedElementPlaceholder x:Name="Adorned" />
              </Border>
              <TextBlock x:Name="errorMessage" Margin="2,2,0,0" FontSize="11" Foreground="#D13438"
                         Text="{Binding ElementName=Adorned, Path=AdornedElement.(Validation.Errors)/ErrorContent}" />
            </StackPanel>
          </ControlTemplate>
          <Style TargetType="TextBox">
            <Setter Property="Validation.ErrorTemplate" Value="{StaticResource FieldErrorTemplate}" />
            <Setter Property="Margin" Value="0,0,0,22" />
          </Style>
        </StackPanel.Resources>
        """;

    private static FrameworkElement BuildNameView(object viewModel) => BuildView(viewModel, withSaveButton: false);

    private static FrameworkElement BuildSaveView(object viewModel) => BuildView(viewModel, withSaveButton: true);

    /// <summary>記事の XAML。表示の表は保存ボタンを除いた同じビューで測る。</summary>
    private static FrameworkElement BuildView(object viewModel, bool withSaveButton)
    {
        string saveButton = withSaveButton
            ? """<Button x:Name="saveButton" Content="Save" Command="{Binding SaveCommand}" Padding="14,2" Margin="12,0,0,22" />"""
            : "";
        var view = SceneContext.LoadXaml<StackPanel>(
            $$"""
            <StackPanel Margin="8">
              {{ErrorTemplateResources}}
              <TextBlock Text="Name (required, up to 20 characters)" />
              <StackPanel Orientation="Horizontal">
                <TextBox x:Name="nameBox" Width="240" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
                {{saveButton}}
              </StackPanel>
            </StackPanel>
            """);
        view.DataContext = viewModel;
        return view;
    }

    private static TextBox NameBox(FrameworkElement view) => (TextBox)view.FindName("nameBox");

    /// <summary>ErrorTemplate に描かれたメッセージ。付録サンプルの第 5 章と同じく、errorMessage という名前の TextBlock を読む。</summary>
    private static string Message(TextBox box) => BookA.BookACh05ValidationScene.Message(box);

    /// <summary>ボタンを UI オートメーションの Invoke で押す。無効なボタンには ElementNotEnabledException が出る。</summary>
    private static void Click(Button button)
    {
        var provider = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!;
        provider.Invoke();
    }

    private static void ThrowIfAny(List<string> mismatches)
    {
        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException("想定と異なる結果が出た。" + Environment.NewLine + string.Join(Environment.NewLine, mismatches));
        }
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // ------------------------------------------------------------ ビューモデル（記事のコード例と同じ）
    // ValidateAllProperties のソース生成が型を参照するため、ObservableValidator の派生は internal にする。

    /// <summary>Toolkit の ObservableValidator。setter で SetProperty の validate 引数に true を渡す。</summary>
    internal sealed class ToolkitManualViewModel : ObservableValidator
    {
        private string _name = "";

        [Required(ErrorMessage = RequiredMessage)]
        [MaxLength(20, ErrorMessage = LengthMessage)]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }
    }

    /// <summary>Toolkit のソース生成。検証属性は生成されたプロパティへ写されるが、[NotifyDataErrorInfo] が無いので setter は検証しない。</summary>
    internal sealed partial class ToolkitAttributesOnlyViewModel : ObservableValidator
    {
        [ObservableProperty]
        [Required(ErrorMessage = RequiredMessage)]
        [MaxLength(20, ErrorMessage = LengthMessage)]
        private string _name = "";
    }

    /// <summary>Toolkit のソース生成に [NotifyDataErrorInfo] を付ける。<paramref name="validateOnStart"/> ならコンストラクターで全プロパティを検証する。</summary>
    internal sealed partial class ToolkitNotifyingViewModel : ObservableValidator
    {
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = RequiredMessage)]
        [MaxLength(20, ErrorMessage = LengthMessage)]
        private string _name = "";

        public ToolkitNotifyingViewModel(bool validateOnStart)
        {
            if (validateOnStart)
            {
                ValidateAllProperties();
            }
        }
    }

    /// <summary>保存まで進んだ回数を読むための計測用の口。</summary>
    internal interface ISaveCounter
    {
        int SaveCount { get; }
    }

    /// <summary>
    /// 記事の「問題」の節のビューモデル。CanExecute は !HasErrors で、ErrorsChanged でコマンドに知らせる。
    /// <paramref name="validateOnStart"/> なら、購読より前にコンストラクターで全プロパティを検証する（記事の差し替え版のコンストラクター）。
    /// </summary>
    internal sealed partial class ToolkitGuardedSaveViewModel : ObservableValidator, ISaveCounter
    {
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = RequiredMessage)]
        [MaxLength(20, ErrorMessage = LengthMessage)]
        private string _name = "";

        public ToolkitGuardedSaveViewModel(bool validateOnStart)
        {
            if (validateOnStart)
            {
                ValidateAllProperties();
            }

            // エラーが増減したら、CanExecute を問い合わせ直させる。
            ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
        }

        /// <summary>保存まで進んだ回数（計測用）。</summary>
        public int SaveCount { get; private set; }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save() => SaveCount++;

        private bool CanSave() => !HasErrors;
    }

    /// <summary>記事の「保存処理の先頭で検証する」ビューモデル。CanExecute も ErrorsChanged の購読も持たない。</summary>
    internal sealed partial class ToolkitValidateOnSaveViewModel : ObservableValidator, ISaveCounter
    {
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = RequiredMessage)]
        [MaxLength(20, ErrorMessage = LengthMessage)]
        private string _name = "";

        /// <summary>保存まで進んだ回数（計測用）。Save は呼ばれても、先頭の検証で戻れば増えない。</summary>
        public int SaveCount { get; private set; }

        [RelayCommand]
        private void Save()
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                return;
            }

            SaveCount++;
        }
    }

    /// <summary>Prism.Core の BindableBase と ErrorsContainer で INotifyDataErrorInfo を実装する。規則は Toolkit の行の [Required]（空白だけも不可）と [MaxLength(20)] に合わせる。</summary>
    internal sealed class PrismAccountViewModel : BindableBase, INotifyDataErrorInfo
    {
        private readonly ErrorsContainer<string> _errors;
        private string _name = "";

        public PrismAccountViewModel(bool validateOnStart)
        {
            _errors = new ErrorsContainer<string>(propertyName => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName)));
            if (validateOnStart)
            {
                ValidateName();
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errors.HasErrors;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    ValidateName();
                }
            }
        }

        public IEnumerable GetErrors(string? propertyName) => _errors.GetErrors(propertyName);

        private void ValidateName()
        {
            var messages = new List<string>();
            if (string.IsNullOrWhiteSpace(Name))
            {
                messages.Add(RequiredMessage);
            }
            else if (Name.Length > 20)
            {
                messages.Add(LengthMessage);
            }

            _errors.SetErrors(nameof(Name), messages);
        }
    }
}
