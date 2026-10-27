using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Commands;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で CommunityToolkit.Mvvm の AsyncRelayCommand が投げた例外の届き先と FlowExceptionsToTaskScheduler」の図と表。
///
/// 取り込みのボタンと、エラーのメッセージを出す TextBlock だけのビューに、コマンドの書き方が違うビューモデルを渡し、
/// ボタンを UI オートメーションの Invoke で押して、DispatcherUnhandledException と TaskScheduler.UnobservedTaskException が呼ばれた回数、
/// ImportCommand.ExecutionTask.Status、画面のメッセージ、ボタンの IsEnabled を読む。
/// UnobservedTaskException は、GC を呼ばずに 1 秒待った後、GC の後、2 回目を押して GC した後の 3 回読む。
/// 取り込みはどの書き方でも Task.Run の中で InvalidOperationException を投げる。
/// 計測は DispatcherUnhandledException を Handled にするので、プロセスは落ちない。
/// Prism は MIT ライセンスの最後の版（8.1.97）を使う（csproj の注記を参照）。Prism 8 に AsyncDelegateCommand のような非同期専用のコマンドの型は無い。
/// </summary>
internal sealed partial class AsyncRelayCommandExceptionScene : IScene
{
    private const string FailureMessage = "The import file was not found.";

    public IReadOnlyList<string> Verifies =>
    [
        "CommunityToolkit.Mvvm 8.4.0 の [RelayCommand] を async Task のメソッドに付けた既定のコマンドは、ボタンから実行して取り込みが例外を投げると、DispatcherUnhandledException が 1 回呼ばれ、UnobservedTaskException は GC の前後とも、2 回目を押して GC した後も呼ばれないこと。ExecutionTask.Status は Faulted になること",
        "[RelayCommand(FlowExceptionsToTaskScheduler = true)] は、DispatcherUnhandledException が呼ばれず、UnobservedTaskException は 1 回目の後には GC を呼んでも呼ばれず、2 回目を押して GC した後に 1 回呼ばれること。ExecutionTask.Status は Faulted になること",
        "Prism.Core 8.1.97 の DelegateCommand に async のラムダを渡すと、DispatcherUnhandledException が 1 回呼ばれ、UnobservedTaskException は呼ばれないこと",
        "既定のコマンドでも、メソッドの中で await を try / catch すれば、DispatcherUnhandledException も UnobservedTaskException も呼ばれず、メッセージが画面に出て、ExecutionTask.Status は RanToCompletion になること",
        "既定のコマンドを別のコマンドから ExecuteAsync で await し、呼び出し側で try / catch すると、DispatcherUnhandledException も UnobservedTaskException も呼ばれず、メッセージが画面に出ること。ImportCommand.ExecutionTask.Status は Faulted になること",
        "どの書き方でも、例外の後のボタンの IsEnabled は True であること",
        "Toolkit の 4 つの書き方のどれでも、1 回目の ExecutionTask は 1 回目の後の GC では回収されず、2 回目を押して ExecutionTask が差し替わった後の GC で回収されること（弱参照の IsAlive で確かめた）",
        "ExecutionTask.Status にバインドした行は、押す前（ExecutionTask が null）には FallbackValue の ExecutionTask: (not run) を出し、実行が終わると Faulted または RanToCompletion に変わること",
        "図は、既定・FlowExceptionsToTaskScheduler・メソッドの中で try / catch の 3 つのビューでボタンを押した後に撮っていること。ExecutionTask の行は上から Faulted・Faulted・RanToCompletion で、メッセージは下のビューにだけ出ており、ボタンはどれも有効で、DispatcherUnhandledException は（既定のビューの分の）1 回だけ呼ばれたこと",
    ];

    public string Slug => "wpf-asyncrelaycommand-exception-handling";

    public async Task CaptureAsync(SceneContext context)
    {
        await ShootFigureAsync(context);
        await SaveRouteMatrixAsync(context);
    }

    // ------------------------------------------------------------ 図

    private static async Task ShootFigureAsync(SceneContext context)
    {
        FrameworkElement[] views =
        [
            BuildStatusView(new ToolkitImportViewModel()),
            BuildStatusView(new ToolkitFlowingImportViewModel()),
            BuildStatusView(new ToolkitCatchingImportViewModel()),
        ];

        Window window = DemoLayout.BuildPanelWindow(
            "AsyncRelayCommand exceptions",
            [
                new DemoLayout.Panel("[RelayCommand]  (default)", views[0]),
                new DemoLayout.Panel("[RelayCommand(FlowExceptionsToTaskScheduler = true)]", views[1]),
                new DemoLayout.Panel("[RelayCommand] + try / catch in ImportAsync", views[2]),
            ],
            Orientation.Vertical);

        await WithHandlersAsync(async counts =>
        {
            await context.ShootAsync(window, "asyncrelaycommand-after-failure.png", async _ =>
            {
                await Capture.SettleAsync(window);
                string[] initial = views.Select(view => ((TextBlock)view.FindName("statusText")).Text).Distinct().ToArray();
                if (initial is not ["ExecutionTask: (not run)"])
                {
                    throw new InvalidOperationException($"押す前の ExecutionTask の行が想定と異なる（{string.Join(", ", initial)}）。");
                }

                foreach (FrameworkElement view in views)
                {
                    Click(ImportButton(view));
                }

                await Task.Delay(1000);
                await Capture.SettleAsync(window, 100);

                string[] statuses = views.Select(view => ((TextBlock)view.FindName("statusText")).Text).ToArray();
                if (statuses is not ["ExecutionTask: Faulted", "ExecutionTask: Faulted", "ExecutionTask: RanToCompletion"])
                {
                    throw new InvalidOperationException($"図の ExecutionTask が想定と異なる（{string.Join(", ", statuses)}）。");
                }

                string[] messages = views.Select(ErrorText).ToArray();
                if (messages is not ["", "", FailureMessage])
                {
                    throw new InvalidOperationException($"図のメッセージが想定と異なる（{string.Join(", ", messages)}）。");
                }

                if (views.Any(view => !ImportButton(view).IsEnabled))
                {
                    throw new InvalidOperationException("図のボタンが無効になっている。");
                }

                // 上の既定のビューの例外だけが DispatcherUnhandledException に届く。
                if (counts.Dispatcher != 1)
                {
                    throw new InvalidOperationException($"図の DispatcherUnhandledException の回数が想定と異なる（{counts.Dispatcher}）。");
                }
            });
        });
    }

    // ------------------------------------------------------------ 表: 例外がどこに届くか

    private static async Task SaveRouteMatrixAsync(SceneContext context)
    {
        (Loc Label, Func<IImportViewModel> Create, string Expected)[] cases =
        [
            (T("Toolkit [RelayCommand] on async Task (default)", "Toolkit、async Task に [RelayCommand]（既定）"),
                () => new ToolkitImportViewModel(), "1 / 0 / 0 / 0 / Faulted / (none) / True"),
            (T("Toolkit [RelayCommand(FlowExceptionsToTaskScheduler = true)]", "Toolkit、[RelayCommand(FlowExceptionsToTaskScheduler = true)]"),
                () => new ToolkitFlowingImportViewModel(), "0 / 0 / 0 / 1 / Faulted / (none) / True"),
            (T("Prism DelegateCommand with an async lambda", "Prism、DelegateCommand に async のラムダ"),
                () => new PrismImportViewModel(), "1 / 0 / 0 / 0 / - / (none) / True"),
            (T("Toolkit default, try / catch around the await in ImportAsync", "Toolkit の既定、ImportAsync の中で await を try / catch"),
                () => new ToolkitCatchingImportViewModel(), $"0 / 0 / 0 / 0 / RanToCompletion / {FailureMessage} / True"),
            (T("Toolkit default, another command awaits ImportCommand.ExecuteAsync in try / catch", "Toolkit の既定、別のコマンドから ImportCommand.ExecuteAsync を await して try / catch"),
                () => new ToolkitExecuteAsyncCallerViewModel(), $"0 / 0 / 0 / 0 / Faulted / {FailureMessage} / True"),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        var mismatches = new List<string>();
        foreach ((Loc label, Func<IImportViewModel> create, string expected) in cases)
        {
            string[] values = await MeasureRouteAsync(create());
            if (string.Join(" / ", values) != expected)
            {
                mismatches.Add($"{label.Ja}: {string.Join(" / ", values)}（想定 {expected}）");
            }

            rows.Add(
            [
                label,
                .. values.Select(value => value switch
                {
                    "-" => Loc.Of("- (no such property)", "-（プロパティが無い）"),
                    "(none)" => Loc.Of("(none)", "（なし）"),
                    _ => (Loc)value,
                }),
            ]);
        }

        ThrowIfAny(mismatches);

        await context.SaveTableAsync(
            "Where an exception thrown by the import command goes (DispatcherUnhandledException is marked Handled)",
            [
                Loc.Of("how the command is written", "コマンドの書き方"),
                Loc.Of("DispatcherUnhandledException (calls)", "DispatcherUnhandledException（回数）"),
                Loc.Of("UnobservedTaskException, 1 s without an explicit GC", "UnobservedTaskException（GC を明示的に呼ばずに 1 秒）"),
                Loc.Of("UnobservedTaskException after GC", "UnobservedTaskException（GC の後）"),
                Loc.Of("UnobservedTaskException after a 2nd click and GC", "UnobservedTaskException（2 回目を押して GC の後）"),
                Loc.Of("ImportCommand.ExecutionTask.Status", "ImportCommand.ExecutionTask.Status"),
                Loc.Of("message on screen", "画面のメッセージ"),
                Loc.Of("button IsEnabled after the failure", "失敗の後のボタンの IsEnabled"),
            ],
            rows,
            "asyncrelaycommand-exception-routes.svg");
    }

    /// <summary>
    /// ビューを表示し、ボタンを Invoke で押して 1 秒待ち（GC は呼ばない）、DispatcherUnhandledException と UnobservedTaskException の回数、
    /// ExecutionTask.Status、画面のメッセージ、ボタンの IsEnabled を読む。
    /// 続けて GC を呼んで UnobservedTaskException の回数を読み、最後にもう一度押して 1 秒待ち、GC を呼んで回数を読む（UnobservedTaskException の回数は 1 回目からの累計。
    /// ほかの列は 1 回目を押した後の値）。
    /// Toolkit の行では、1 回目の ExecutionTask を弱参照で持ち、1 回目の GC の後に残っていて、2 回目を押した後の GC で回収されたことを確かめる。
    /// ビューモデルはウィンドウの DataContext から参照されたまま GC を呼ぶ。
    /// </summary>
    private static async Task<string[]> MeasureRouteAsync(IImportViewModel viewModel)
    {
        FrameworkElement view = BuildView(viewModel);
        var values = new string[7];
        await WithHandlersAsync(async counts =>
        {
            await ShowAsync(view, async () =>
            {
                Window window = Window.GetWindow(view)!;
                Button button = ImportButton(view);
                await Capture.SettleAsync(window, 100);

                Click(button);
                await Task.Delay(1000);
                await Capture.SettleAsync(window, 100);
                values[0] = counts.Dispatcher.ToString();
                values[1] = counts.Unobserved.ToString();
                values[4] = StatusText(viewModel);
                values[5] = ErrorText(view) is { Length: > 0 } message ? message : "(none)";
                values[6] = button.IsEnabled.ToString();

                WeakReference? firstTask = WeakTask(viewModel);

                // 1 回目の Task を読んだときの一時的な参照が、この呼び出しのフレームに残ったまま GC が走らないよう、
                // 続きを Dispatcher に回してフレームを分ける。
                await Task.Yield();
                CollectGarbage();
                values[2] = counts.Unobserved.ToString();
                bool aliveAfterFirstGc = firstTask?.IsAlive ?? false;

                Click(button);
                await Task.Delay(1000);
                await Capture.SettleAsync(window, 100);
                CollectGarbage();
                values[3] = counts.Unobserved.ToString();

                // Toolkit の行では、1 回目の Task は ExecutionTask が参照している間は回収されず、
                // 2 回目で ExecutionTask が差し替わった後の GC で回収される。
                if (firstTask is not null && (!aliveAfterFirstGc || firstTask.IsAlive))
                {
                    throw new InvalidOperationException(
                        $"1 回目の Task の回収が想定と異なる（1 回目の GC の後 IsAlive={aliveAfterFirstGc}、2 回目の後の GC の後 IsAlive={firstTask.IsAlive}）。");
                }
            });
        });
        return values;
    }

    /// <summary>ExecutionTask.Status の文字列。Task への一時的な参照を呼び出し元に残さないよう、別のメソッドで読む。</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string StatusText(IImportViewModel viewModel) => viewModel.ImportTask?.Status.ToString() ?? "-";

    /// <summary>ExecutionTask を弱参照で持つ。強い参照を async メソッドの状態に残さないよう、別のメソッドで作る。</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference? WeakTask(IImportViewModel viewModel) =>
        viewModel.ImportTask is { } task ? new WeakReference(task) : null;

    // ------------------------------------------------------------ 計測の土台

    private sealed class Counts
    {
        private int _dispatcher;
        private int _unobserved;

        public int Dispatcher => Volatile.Read(ref _dispatcher);

        public int Unobserved => Volatile.Read(ref _unobserved);

        public void AddDispatcher() => Interlocked.Increment(ref _dispatcher);

        public void AddUnobserved() => Interlocked.Increment(ref _unobserved);
    }

    /// <summary>
    /// DispatcherUnhandledException と UnobservedTaskException を数えながら <paramref name="act"/> を実行する。
    /// 前の計測で残った Task が数に入らないよう、ハンドラーを付ける前に回収しきっておく。
    /// </summary>
    private static async Task WithHandlersAsync(Func<Counts, Task> act)
    {
        await DrainPreviousTasksAsync();

        var counts = new Counts();
        DispatcherUnhandledExceptionEventHandler onDispatcher = (_, e) =>
        {
            counts.AddDispatcher();
            e.Handled = true; // 計測ツールを落とさないため。
        };
        EventHandler<UnobservedTaskExceptionEventArgs> onUnobserved = (_, e) =>
        {
            counts.AddUnobserved();
            e.SetObserved();
        };
        Application.Current.DispatcherUnhandledException += onDispatcher;
        TaskScheduler.UnobservedTaskException += onUnobserved;
        try
        {
            await act(counts);
        }
        finally
        {
            Application.Current.DispatcherUnhandledException -= onDispatcher;
            TaskScheduler.UnobservedTaskException -= onUnobserved;
        }
    }

    /// <summary>
    /// 前の計測のウィンドウは、閉じた後もしばらく Dispatcher の待ち行列などから参照されていて、1 回の GC では回収されない
    /// （実際に、前の行の FlowExceptionsToTaskScheduler の Task が次の行の GC で UnobservedTaskException を出した）。
    /// Dispatcher を空にしてから GC を呼ぶことを、UnobservedTaskException が 3 回続けて出なくなるまで繰り返す。
    /// </summary>
    private static async Task DrainPreviousTasksAsync()
    {
        int late = 0;
        EventHandler<UnobservedTaskExceptionEventArgs> onLate = (_, e) =>
        {
            Interlocked.Increment(ref late);
            e.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += onLate;
        try
        {
            int quiet = 0;
            for (int round = 0; quiet < 3; round++)
            {
                if (round == 20)
                {
                    throw new InvalidOperationException("前の計測の Task を回収しきれない。");
                }

                int before = Volatile.Read(ref late);
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(200);
                CollectGarbage();
                quiet = Volatile.Read(ref late) == before ? quiet + 1 : 0;
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= onLate;
        }
    }

    /// <summary>UnobservedTaskException はファイナライザーから呼ばれるので、ファイナライザーの完了まで待つ。</summary>
    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // ------------------------------------------------------------ ビュー（コードビハインドは持たず、バインドだけで動かす）

    /// <summary>記事の XAML。表はこのビューで測る。</summary>
    private static FrameworkElement BuildView(IImportViewModel viewModel) => BuildView(viewModel, withStatus: false);

    /// <summary>図のビュー。記事の XAML に ExecutionTask.Status の行を足したもの。</summary>
    private static FrameworkElement BuildStatusView(IImportViewModel viewModel) => BuildView(viewModel, withStatus: true);

    private static FrameworkElement BuildView(IImportViewModel viewModel, bool withStatus)
    {
        string status = withStatus
            ? """
              <TextBlock x:Name="statusText" Margin="0,6,0,0"
                         Text="{Binding ImportCommand.ExecutionTask.Status,
                                StringFormat='ExecutionTask: {0}',
                                FallbackValue='ExecutionTask: (not run)'}" />
              """
            : "";
        var view = SceneContext.LoadXaml<StackPanel>(
            $$"""
            <StackPanel Margin="8" Width="360">
              <Button x:Name="importButton" Content="Import" Command="{Binding {{viewModel.ButtonCommandPath}}}"
                      HorizontalAlignment="Left" Padding="14,2" />
              {{status}}
              <TextBlock x:Name="errorText" Margin="0,6,0,0" Foreground="#D13438" Text="{Binding ErrorMessage}" />
            </StackPanel>
            """);
        view.DataContext = viewModel;
        return view;
    }

    private static Button ImportButton(FrameworkElement view) => (Button)view.FindName("importButton");

    private static string ErrorText(FrameworkElement view) => ((TextBlock)view.FindName("errorText")).Text;

    /// <summary>
    /// ボタンを UI オートメーションの Invoke で押す。Invoke は ButtonBase.OnClick を Dispatcher 経由で呼び、
    /// OnClick が Click イベントを発生させた後にコマンドを実行する。
    /// </summary>
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

    /// <summary>取り込みの失敗を再現する。ファイルが見つからなかったことにして、常に投げる。</summary>
    private static void ReadImportFile() => throw new InvalidOperationException(FailureMessage);

    /// <summary>計測用の口。ImportCommand の ExecutionTask を読む（Prism は持たないので null）。</summary>
    internal interface IImportViewModel
    {
        Task? ImportTask { get; }

        /// <summary>ボタンの Command に結び付けるプロパティ。</summary>
        string ButtonCommandPath => "ImportCommand";
    }

    /// <summary>記事の「現象」の節のビューモデル。async Task のメソッドに [RelayCommand] を付けただけ。</summary>
    internal sealed partial class ToolkitImportViewModel : ObservableObject, IImportViewModel
    {
        [ObservableProperty]
        private string _errorMessage = "";

        public Task? ImportTask => ImportCommand.ExecutionTask;

        [RelayCommand]
        private async Task ImportAsync()
        {
            await Task.Run(ReadImportFile);
        }
    }

    /// <summary>同じメソッドに FlowExceptionsToTaskScheduler を付ける。</summary>
    internal sealed partial class ToolkitFlowingImportViewModel : ObservableObject, IImportViewModel
    {
        [ObservableProperty]
        private string _errorMessage = "";

        public Task? ImportTask => ImportCommand.ExecutionTask;

        [RelayCommand(FlowExceptionsToTaskScheduler = true)]
        private async Task ImportAsync()
        {
            await Task.Run(ReadImportFile);
        }
    }

    /// <summary>Prism 8 の DelegateCommand に async のラムダを渡す。ラムダは async void になる。</summary>
    internal sealed class PrismImportViewModel : Prism.Mvvm.BindableBase, IImportViewModel
    {
        private string _errorMessage = "";

        public PrismImportViewModel()
        {
            ImportCommand = new DelegateCommand(async () => await Task.Run(ReadImportFile));
        }

        public DelegateCommand ImportCommand { get; }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public Task? ImportTask => null;
    }

    /// <summary>記事の実装例。既定のコマンドのまま、メソッドの中で await を try / catch する。</summary>
    internal sealed partial class ToolkitCatchingImportViewModel : ObservableObject, IImportViewModel
    {
        [ObservableProperty]
        private string _errorMessage = "";

        public Task? ImportTask => ImportCommand.ExecutionTask;

        [RelayCommand]
        private async Task ImportAsync()
        {
            ErrorMessage = "";
            try
            {
                await Task.Run(ReadImportFile);
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }

    /// <summary>
    /// 既定のコマンド（ImportCommand）を、別のコマンド（ImportAndReportCommand）から ExecuteAsync で await する。
    /// このビューモデルのビューは、ボタンを ImportAndReportCommand に結び付ける。
    /// </summary>
    internal sealed partial class ToolkitExecuteAsyncCallerViewModel : ObservableObject, IImportViewModel
    {
        [ObservableProperty]
        private string _errorMessage = "";

        public Task? ImportTask => ImportCommand.ExecutionTask;

        public string ButtonCommandPath => nameof(ImportAndReportCommand);

        [RelayCommand]
        private async Task ImportAsync()
        {
            await Task.Run(ReadImportFile);
        }

        [RelayCommand]
        private async Task ImportAndReportAsync()
        {
            ErrorMessage = "";
            try
            {
                await ImportCommand.ExecuteAsync(null);
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
