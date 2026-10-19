using System.ComponentModel.DataAnnotations;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Commands;
using Prism.Mvvm;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 付録サンプルの第 4・5・10 章を、自前の基底クラスではなく MVVM ライブラリで書いた場合の検証。
///
/// 保守の現場では CommunityToolkit.Mvvm や Prism のコマンド・基底クラスを使うことが多い。
/// サンプルのビュー（XAML）はそのままにして、ビューモデルだけをライブラリで書き直し、各章のシーンと同じ手順で測る。
/// 記事に値が無いため、公式文書の記述から予想した値と比べる。
/// Prism は MIT ライセンスの最後の版（8.1.97）を使う。9 以降はライセンスが変わり、Prism.Core 9 には AsyncDelegateCommand がある。
/// </summary>
internal sealed partial class BookALibrariesScene : IScene
{
    private const string ToolkitCommandDoc = "Toolkit の文書: CanExecute の変化は NotifyCanExecuteChanged を呼ぶまで反映されない";
    private const string PrismCommandDoc = "Prism.Core は WPF に依存せず、CommandManager を使わない（RaiseCanExecuteChanged か ObservesProperty で通知する）";
    private const string ToolkitAsyncDoc = "Toolkit の文書（AsyncRelayCommandOptions）";

    public IReadOnlyList<string> Verifies =>
    [
        "第 4 章のビューで、CommunityToolkit.Mvvm の RelayCommand は CanExecute が依存するプロパティを変えても、NotifyCanExecuteChanged を呼ばなければボタンの IsEnabled が変わらず、CommandManager.InvalidateRequerySuggested でも変わらないこと。[NotifyCanExecuteChangedFor] を付けると変わること",
        "第 4 章のビューで、Prism の DelegateCommand は RaiseCanExecuteChanged を呼ばなければボタンの IsEnabled が変わらず、CommandManager.InvalidateRequerySuggested でも変わらないこと。ObservesProperty を付けると変わること",
        "第 5 章のビューで、CommunityToolkit.Mvvm の ObservableValidator は SetProperty(..., validate: true) だけでは起動直後にエラーが出ず、コンストラクターで ValidateAllProperties を呼ぶと出ること。属性のメッセージが ErrorTemplate に出て、正しい値で消えること",
        "第 10 章のビューで、CommunityToolkit.Mvvm の AsyncRelayCommand は既定では取り込みの例外が DispatcherUnhandledException に届き、FlowExceptionsToTaskScheduler を付けると DispatcherUnhandledException・AppDomain.UnhandledException に届かず（GC を呼ばない 1 秒の間は UnobservedTaskException にも届かず）、ExecutionTask が Faulted になること。Prism の DelegateCommand に async のラムダを渡すと DispatcherUnhandledException に届くこと",
    ];

    public string Slug => "book-a-libraries";

    public string ImageDirectory => Path.Combine("images", "book-a", "libraries");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>();

        // ---- 第 4 章: CanExecute の変化がボタンに反映されるか（値は保存ボタンの IsEnabled の「入力前 → 入力後」）。
        checks.Add(new("第 4 章、Toolkit の RelayCommand、setter は SetProperty だけ", ToolkitCommandDoc, "False → False",
            await BookACh04CanExecuteScene.TypeAsync(new Ch04.CanExecuteNotUpdating.FixedView { DataContext = new ToolkitPlainSaveViewModel() }, requery: false)));
        checks.Add(new("第 4 章、同じ書き方で、入力の後に CommandManager.InvalidateRequerySuggested()", ToolkitCommandDoc, "False → False",
            await BookACh04CanExecuteScene.TypeAsync(new Ch04.CanExecuteNotUpdating.FixedView { DataContext = new ToolkitPlainSaveViewModel() }, requery: true)));
        checks.Add(new("第 4 章、Toolkit の [ObservableProperty] に [NotifyCanExecuteChangedFor]", ToolkitCommandDoc, "False → True",
            await BookACh04CanExecuteScene.TypeAsync(new Ch04.CanExecuteNotUpdating.FixedView { DataContext = new ToolkitNotifyingSaveViewModel() }, requery: false)));
        checks.Add(new("第 4 章、Prism の DelegateCommand、setter は SetProperty だけ", PrismCommandDoc, "False → False",
            await BookACh04CanExecuteScene.TypeAsync(new Ch04.CanExecuteNotUpdating.FixedView { DataContext = new PrismSaveViewModel(observes: false) }, requery: false)));
        checks.Add(new("第 4 章、同じ書き方で、入力の後に CommandManager.InvalidateRequerySuggested()", PrismCommandDoc, "False → False",
            await BookACh04CanExecuteScene.TypeAsync(new Ch04.CanExecuteNotUpdating.FixedView { DataContext = new PrismSaveViewModel(observes: false) }, requery: true)));
        checks.Add(new("第 4 章、Prism の DelegateCommand に ObservesProperty(() => Name)", PrismCommandDoc, "False → True",
            await BookACh04CanExecuteScene.TypeAsync(new Ch04.CanExecuteNotUpdating.FixedView { DataContext = new PrismSaveViewModel(observes: true) }, requery: false)));

        // ---- 第 5 章: ObservableValidator のエラーが表示されるか（値は HasError・Errors の数・装飾（adorner）の数、またはメッセージ）。
        const string ValidatorDoc = "Toolkit の文書: SetProperty の validate: true は設定時に検証する。ValidateAllProperties はすべてを検証する";
        await BookACh05ValidationScene.ShowViewAsync(new Ch05.ValidationNotDisplayed.FixedView { DataContext = new ToolkitAccountViewModel(validateOnStart: false) }, async view =>
        {
            TextBox box = BookACh05ValidationScene.Box(view);
            checks.Add(new("第 5 章、Toolkit の ObservableValidator、validate: true だけ、起動直後（名前は空）", ValidatorDoc, "HasError False、Errors 0、装飾 0",
                BookACh05ValidationScene.Stages(box)));
            await FocusAsync(box);
            TypeLetters(box, "abcdefghijklmnopqrstu");
            await BookACh05ValidationScene.Settle(view);
            checks.Add(new("第 5 章、同じ書き方で、21 文字を入力", ValidatorDoc, "名前は 20 文字以内にしてください。",
                BookACh05ValidationScene.Message(box)));
        });
        await BookACh05ValidationScene.ShowViewAsync(new Ch05.ValidationNotDisplayed.FixedView { DataContext = new ToolkitAccountViewModel(validateOnStart: true) }, async view =>
        {
            TextBox box = BookACh05ValidationScene.Box(view);
            checks.Add(new("第 5 章、コンストラクターで ValidateAllProperties()、起動直後（名前は空）", ValidatorDoc, "HasError True、Errors 1、装飾 1",
                BookACh05ValidationScene.Stages(box)));
            checks.Add(new("第 5 章、同じ書き方で、起動直後のメッセージ", ValidatorDoc, "名前は必須です。", BookACh05ValidationScene.Message(box)));
            await FocusAsync(box);
            TypeLetters(box, "abcdefghijklmnopqrstu");
            await BookACh05ValidationScene.Settle(view);
            checks.Add(new("第 5 章、同じ書き方で、21 文字を入力", ValidatorDoc, "名前は 20 文字以内にしてください。", BookACh05ValidationScene.Message(box)));
            box.SelectAll();
            TypeLetters(box, "taro");
            await BookACh05ValidationScene.Settle(view);
            checks.Add(new("第 5 章、同じ書き方で、正しい名前（taro）を入力", ValidatorDoc, "HasError False、Errors 0、装飾 0", BookACh05ValidationScene.Stages(box)));
        });

        // ---- 第 10 章: 取り込みの例外がどこに届くか（計測は DispatcherUnhandledException を Handled にするので、プロセスは落ちない）。
        const string None = "DispatcherUnhandledException —、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし）";
        const string ToDispatcher = "DispatcherUnhandledException 呼ばれる（1 回）、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし）";
        checks.Add(new("第 10 章、Toolkit の AsyncRelayCommand（既定）で、try / catch なし", ToolkitAsyncDoc + ": 既定は例外を呼び出し元のコンテキストで投げ直す", ToDispatcher,
            await RunImportAsync(new ToolkitImportViewModel(AsyncRelayCommandOptions.None))));
        var flowing = new ToolkitImportViewModel(AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        checks.Add(new("第 10 章、Toolkit の AsyncRelayCommand に FlowExceptionsToTaskScheduler", ToolkitAsyncDoc + ": 呼び出し元では投げず、UnobservedTaskException に流す（GC までは届かない）", None,
            await RunImportAsync(flowing)));
        checks.Add(new("第 10 章、同じ書き方で、実行後の ExecutionTask の状態", ToolkitAsyncDoc + ": 失敗は ExecutionTask から読める", "Faulted",
            flowing.StartImportCommand.ExecutionTask?.Status.ToString() ?? "（なし）"));
        checks.Add(new("第 10 章、Prism の DelegateCommand に async のラムダ、try / catch なし", "async のラムダは async void になり、例外は UI スレッドの同期コンテキストに投げ直される", ToDispatcher,
            await RunImportAsync(new PrismImportViewModel())));

        BookAReport.WriteLibraryReport(
            "libraries.md", Slug, "MVVM ライブラリで書いた場合の第 4・5・10 章",
            "付録サンプルのビュー（XAML）はそのままにして、ビューモデルだけを CommunityToolkit.Mvvm 8.4.0 と Prism.Core 8.1.97 で書き直し、各章のシーンと同じ手順で測った。" +
            "記事に値が無いので、公式文書などから予想した値と比べる。第 4 章の値は保存ボタンの IsEnabled の「入力前 → 入力後」、第 5 章は HasError・Errors の数・装飾（adorner）の数かメッセージ。" +
            "第 10 章は計測のために DispatcherUnhandledException を Handled にしている。実際のアプリでハンドラーが無ければ、そこに届いた例外でプロセスが終わる。",
            checks);
    }

    /// <summary>第 10 章のシーンと同じ手順で測り、計測が Handled にしているため常に同じになる「プロセス」の欄を除く。</summary>
    private static async Task<string> RunImportAsync(object viewModel)
    {
        string result = await BookACh10UnhandledExceptionScene.RunAsync(new Ch10.UnhandledExceptionRoutes.FixedView { DataContext = viewModel });
        const string Suffix = "、プロセス: 動き続ける";
        return result.EndsWith(Suffix, StringComparison.Ordinal) ? result.Substring(0, result.Length - Suffix.Length) : result;
    }

    // ------------------------------------------------------------ 第 4 章のビューモデル（ビューは Name・SaveCommand・LastSaved にバインドする）

    /// <summary>Toolkit の RelayCommand。setter は SetProperty だけで、コマンドに通知しない。</summary>
    internal sealed class ToolkitPlainSaveViewModel : ObservableObject
    {
        private string _name = "";
        private string _lastSaved = "（まだ保存していない）";

        public ToolkitPlainSaveViewModel() => SaveCommand = new RelayCommand(() => LastSaved = Name, () => !string.IsNullOrEmpty(Name));

        public RelayCommand SaveCommand { get; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string LastSaved
        {
            get => _lastSaved;
            private set => SetProperty(ref _lastSaved, value);
        }
    }

    /// <summary>Toolkit のソース生成。Name が変わると、生成された setter が SaveCommand.NotifyCanExecuteChanged を呼ぶ。</summary>
    internal sealed partial class ToolkitNotifyingSaveViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = "";

        [ObservableProperty]
        private string _lastSaved = "（まだ保存していない）";

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save() => LastSaved = Name;

        private bool CanSave() => !string.IsNullOrEmpty(Name);
    }

    /// <summary>Prism の DelegateCommand。<paramref name="observes"/> が真なら ObservesProperty で Name の変化を通知させる。</summary>
    internal sealed class PrismSaveViewModel : BindableBase
    {
        private string _name = "";
        private string _lastSaved = "（まだ保存していない）";

        public PrismSaveViewModel(bool observes)
        {
            var command = new DelegateCommand(() => LastSaved = Name, () => !string.IsNullOrEmpty(Name));
            SaveCommand = observes ? command.ObservesProperty(() => Name) : command;
        }

        public DelegateCommand SaveCommand { get; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string LastSaved
        {
            get => _lastSaved;
            private set => SetProperty(ref _lastSaved, value);
        }
    }

    // ------------------------------------------------------------ 第 5 章のビューモデル（ビューは Name にバインドする）

    /// <summary>Toolkit の ObservableValidator。検証の規則は属性で書き、メッセージは付録サンプルと同じにする。</summary>
    internal sealed class ToolkitAccountViewModel : ObservableValidator
    {
        private string _name = "";

        public ToolkitAccountViewModel(bool validateOnStart)
        {
            if (validateOnStart)
            {
                ValidateAllProperties();
            }
        }

        [Required(ErrorMessage = "名前は必須です。")]
        [MaxLength(20, ErrorMessage = "名前は 20 文字以内にしてください。")]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, validate: true);
        }
    }

    // ------------------------------------------------------------ 第 10 章のビューモデル（ビューは StartImportCommand・ErrorMessage にバインドする）

    /// <summary>Toolkit の AsyncRelayCommand で、付録サンプルと同じ取り込みを await する（try / catch は書かない）。</summary>
    internal sealed class ToolkitImportViewModel : ObservableObject
    {
        public ToolkitImportViewModel(AsyncRelayCommandOptions options) =>
            StartImportCommand = new AsyncRelayCommand(() => Task.Run(Ch10.UnhandledExceptionRoutes.Importer.Import), options);

        public AsyncRelayCommand StartImportCommand { get; }

        public string ErrorMessage => "";
    }

    /// <summary>Prism 8 の DelegateCommand に async のラムダを渡す（Prism 8 に非同期のコマンドは無い）。</summary>
    internal sealed class PrismImportViewModel : BindableBase
    {
        public PrismImportViewModel() =>
            StartImportCommand = new DelegateCommand(async () => await Task.Run(Ch10.UnhandledExceptionRoutes.Importer.Import));

        public DelegateCommand StartImportCommand { get; }

        public string ErrorMessage => "";
    }
}
