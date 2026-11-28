using System.Diagnostics;
using System.Text;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の ShutdownMode と MainWindow: ログイン画面を閉じるとアプリが終了する原因」の表。
///
/// 起動の構成だけを変えた検証用アプリを実際に起動し、ログイン画面を閉じた直後に終了の処理が始まっているか、
/// メイン画面が生成・表示されるか、<c>Application.Exit</c> が何をきっかけに起きるか、プロセスが終わるかを測る。
/// 終了の判定は <c>Application</c> の内部で行われるため、動かさないと分からない。撮影のたびに測り直す。
/// </summary>
internal sealed class LoginStartupExitScene : IScene
{
    /// <summary>これを超えて生き残ったプロセスは、終了しないものとして扱う。</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public IReadOnlyList<string> Verifies =>
    [
        "OnLastWindowClose のまま OnStartup でログイン画面を ShowDialog すると、ShowDialog が戻った時点で Application の終了処理が始まっており、続く Show() は例外を出さずに IsVisible が false のまま戻ること",
        "OnMainWindowClose を起動時に設定すると、最初に生成したログイン画面が MainWindow になり、ログイン画面を閉じた時点で終了処理が始まること",
        "StartupUri を残したままログイン画面を出すと、ログイン画面を閉じた時点で終了処理が始まり、StartupUri のメイン画面は生成されるが表示されない（Loaded が発生しない）こと",
        "メイン画面を先に生成しておくと表示は続くが、ログインを取り消したときに Shutdown を呼ばないとプロセスが残り、呼べば終わること",
        "ログイン中は OnExplicitShutdown にし、メイン画面を MainWindow に代入してから OnMainWindowClose に切り替えると、メイン画面を閉じたときに終了すること",
        "MainWindow だったログイン画面を閉じると MainWindow が null に戻り、代入しなくても次に生成したメイン画面が MainWindow になること",
        "代入を省き、メイン画面の前に別の Window を生成すると、その Window が MainWindow になり、メイン画面を閉じても終了しないこと。代入すれば終了すること",
        "ログイン後に OnLastWindowClose に切り替えると、ウィンドウがメイン画面だけなら閉じたときに終了するが、表示していない別の Window が残っている間はメイン画面を閉じても終了しないこと",
        "ログイン後に OnMainWindowClose へ切り替えないと、メイン画面を閉じても終了しないこと",
        "ShowDialog が戻った後に ShutdownMode を設定すると InvalidOperationException が投げられ、OnStartup の残りが実行されずに終了すること",
        "OnExplicitShutdown の構成で、取り消しと × で閉じた場合（WM_CLOSE）のどちらも ShowDialog が False を返し、Shutdown を呼べばプロセスが終わること",
    ];

    public string Slug => "wpf-login-window-startup-app-exits";

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>表に載せる構成。<c>Key</c> は検証用アプリへ渡す引数。</summary>
    private static readonly (string Key, Loc Label)[] Conditions =
    [
        ("naive", T("OnLastWindowClose (default), Show after login", "OnLastWindowClose（既定）、ログイン後に Show")),
        ("main-window-close", T("OnMainWindowClose from startup", "起動時から OnMainWindowClose")),
        ("startup-uri", T("StartupUri kept, login in OnStartup", "StartupUri を残し、OnStartup でログイン")),
        ("main-first", T("main window created before login", "ログイン前にメイン画面を生成")),
        ("main-first-cancel", T("main window created before login, canceled, no Shutdown()", "ログイン前にメイン画面を生成、取り消し、Shutdown() なし")),
        ("main-first-cancel-shutdown", T("main window created before login, canceled → Shutdown()", "ログイン前にメイン画面を生成、取り消し → Shutdown()")),
        ("explicit", T("OnExplicitShutdown → OnMainWindowClose (recommended)", "OnExplicitShutdown → OnMainWindowClose（推奨）")),
        ("explicit-cancel", T("recommended, canceled → Shutdown()", "推奨形、取り消し → Shutdown()")),
        ("explicit-close-button", T("recommended, login closed with × (WM_CLOSE) → Shutdown()", "推奨形、× で閉じる（WM_CLOSE）→ Shutdown()")),
        ("explicit-no-assign", T("recommended without assigning MainWindow", "推奨形から MainWindow の代入を省く")),
        ("explicit-extra-window", T("no MainWindow assignment, another Window created before main", "代入を省き、メイン画面の前に別の Window を生成")),
        ("explicit-extra-window-assigned", T("MainWindow assigned, another Window created before main", "代入し、メイン画面の前に別の Window を生成")),
        ("explicit-last-window", T("OnExplicitShutdown → OnLastWindowClose", "OnExplicitShutdown → OnLastWindowClose")),
        ("explicit-last-window-extra", T("OnExplicitShutdown → OnLastWindowClose, another Window created", "OnExplicitShutdown → OnLastWindowClose、別の Window を生成")),
        ("explicit-no-restore", T("OnExplicitShutdown kept after login", "ログイン後も OnExplicitShutdown のまま")),
        ("late-explicit", T("OnExplicitShutdown set after ShowDialog returns", "ShowDialog が戻った後に OnExplicitShutdown を設定")),
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "login-startup-exit-scene");
        Directory.CreateDirectory(workspace);
        string executable = await BuildProbeAsync(workspace);

        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string key, Loc label) in Conditions)
        {
            Measurement m = await MeasureAsync(executable, workspace, key);
            rows.Add([
                label,
                m.MainWindowAtLogin,
                m.DialogResult,
                m.MainWindowAfterLogin,
                m.ShuttingDownAfterLogin ? T("started", "始まっている") : T("not started", "始まっていない"),
                m.VisibleAfterShow ?? "—",
                m.MainWindowAfterShow ?? "—",
                !m.MainCreated ? T("never created", "生成されない")
                    : !m.MainShown ? T("never shown", "表示されない")
                    : m.MainStillOpen ? T("open after 1.5 s", "1.5 秒後も開いている") : T("closed within 1.5 s", "1.5 秒以内に閉じる"),
                m.ExitTrigger switch
                {
                    ExitTrigger.LoginClosed => T("login window closed", "ログイン画面を閉じたこと"),
                    ExitTrigger.MainClosed => T("main window closed", "メイン画面を閉じたこと"),
                    ExitTrigger.ShutdownCall => "Shutdown()",
                    _ => T("not raised", "発生しない"),
                },
                m.ProcessEnded ? T("ends", "終わる") : T("keeps running", "終わらない"),
                m.Exception ?? "—",
            ]);
        }

        await context.SaveTableAsync(
            "startup configuration and shutdown",
            [
                "",
                T("MainWindow during login", "ログイン中の MainWindow"),
                T("ShowDialog() result", "ShowDialog() の戻り値"),
                T("MainWindow after login closes", "ログイン画面を閉じた後の MainWindow"),
                T("Shutdown right after ShowDialog()", "ShowDialog() 直後の終了処理"),
                T("IsVisible after Show()", "Show() 直後の IsVisible"),
                T("MainWindow after Show()", "Show() 後の MainWindow"),
                T("Main window", "メイン画面"),
                T("Application.Exit triggered by", "Application.Exit のきっかけ"),
                T("Process", "プロセス"),
                T("Unhandled exception", "未処理の例外"),
            ],
            rows,
            "login-startup-exit-matrix.svg");
    }

    private enum ExitTrigger { NotRaised, LoginClosed, MainClosed, ShutdownCall }

    private readonly record struct Measurement(
        string MainWindowAtLogin,
        string DialogResult,
        string MainWindowAfterLogin,
        bool ShuttingDownAfterLogin,
        string? VisibleAfterShow,
        string? MainWindowAfterShow,
        bool MainCreated,
        bool MainShown,
        bool MainStillOpen,
        ExitTrigger ExitTrigger,
        bool ProcessEnded,
        string? Exception);

    /// <summary>
    /// 検証用アプリを一時ディレクトリへ書き出してビルドし、実行ファイルのパスを返す。
    /// </summary>
    private static async Task<string> BuildProbeAsync(string workspace)
    {
        var utf8 = new UTF8Encoding(false);
        foreach ((string name, string content) in ProbeFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(workspace, name), content, utf8);
        }

        using var build = Process.Start(new ProcessStartInfo("dotnet", "build -c Release -v q --nologo")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("dotnet build を起動できない。");

        Task<string> stdout = build.StandardOutput.ReadToEndAsync();
        Task<string> stderr = build.StandardError.ReadToEndAsync();
        await build.WaitForExitAsync();
        string output = await stdout + await stderr;

        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException("検証用アプリのビルドに失敗した:\n" + output);
        }

        return Path.Combine(workspace, "bin", "Release", "net10.0-windows", "LoginStartupProbe.exe");
    }

    /// <summary>
    /// 構成を 1 つ実行し、ログの各行から結果を読み取る。
    /// </summary>
    private static async Task<Measurement> MeasureAsync(string executable, string workspace, string condition)
    {
        string log = Path.Combine(workspace, $"log-{condition}.txt");
        if (File.Exists(log))
        {
            File.Delete(log);
        }

        using var process = Process.Start(new ProcessStartInfo(executable, $"{condition} \"{log}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = false,
        }) ?? throw new InvalidOperationException("検証用アプリを起動できない。");

        using var cancellation = new CancellationTokenSource(Timeout);
        bool ended = true;
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            ended = false;
            // Kill は終了を待たない。残ったプロセスが次の計測に影響しないよう、終わるまで待つ。
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        string[] lines = File.Exists(log) ? await File.ReadAllLinesAsync(log) : [];
        Console.WriteLine($"[{condition}] " + string.Join(" | ", lines));

        string? Value(string key) =>
            lines.Select(l => l.Split('\t')).FirstOrDefault(p => p[0] == key) is { Length: > 1 } p ? p[1] : null;
        bool Has(string key) => lines.Any(l => l.Split('\t')[0] == key);
        int Order(string key) => Array.FindIndex(lines, l => l.Split('\t')[0] == key);

        if (!Has("login closed"))
        {
            throw new InvalidOperationException($"{condition}: ログイン画面が閉じられていない。検証用アプリが想定どおりに動いていない。");
        }

        bool shuttingDown = Value("shutting down after login") == "True";

        // Exit のきっかけは、ShowDialog 直後の終了処理の有無と、Exit より前に記録された出来事で決める。
        int exit = Order("Application.Exit");
        ExitTrigger trigger;
        if (exit < 0)
        {
            trigger = ExitTrigger.NotRaised;
        }
        else if (shuttingDown)
        {
            // ShowDialog の直後に終了処理が始まっていれば、その後の Shutdown() は何もしない
            // （Application.CriticalShutdown は IsShuttingDown のとき何もせずに戻る）。こちらを優先する。
            trigger = ExitTrigger.LoginClosed;
        }
        else if (Has("Shutdown() called") && Order("Shutdown() called") < exit)
        {
            trigger = ExitTrigger.ShutdownCall;
        }
        else if (Has("main closing") && Order("main closing") < exit)
        {
            trigger = ExitTrigger.MainClosed;
        }
        else
        {
            throw new InvalidOperationException($"{condition}: Application.Exit のきっかけが記録に無い。想定外の経路である。");
        }

        return new Measurement(
            Value("MainWindow at login") ?? "?",
            Value("login closed") ?? "?",
            Value("MainWindow after login") ?? "?",
            shuttingDown,
            Value("after Show")?.ToLowerInvariant(),
            Value("MainWindow after Show"),
            Has("main created"),
            Has("main loaded"),
            Value("main after 1.5 s") == "open",
            trigger,
            ended,
            Value("unhandled"));
    }

    private const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net10.0-windows</TargetFramework>
            <UseWPF>true</UseWPF>
            <Nullable>enable</Nullable>
            <LangVersion>latest</LangVersion>
            <AssemblyName>LoginStartupProbe</AssemblyName>
            <RootNamespace>LoginStartupProbe</RootNamespace>
            <StartupObject>LoginStartupProbe.Program</StartupObject>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>
    /// 記事に載せたビューモデル。閉じる要求はビューモデルの <c>CloseRequested</c> で伝える。
    /// </summary>
    private const string ViewModelsSource = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel;
        using System.Runtime.CompilerServices;
        using System.Windows.Input;

        namespace LoginStartupProbe;

        public abstract class ObservableObject : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                return true;
            }
        }

        public sealed class RelayCommand(Action execute) : ICommand
        {
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => execute();
        }

        public interface ICloseRequest
        {
            event EventHandler<bool>? CloseRequested;
        }

        public sealed class LoginViewModel : ObservableObject, ICloseRequest
        {
            private string _userName = "";

            public LoginViewModel()
            {
                LoginCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
                CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
            }

            public string UserName
            {
                get => _userName;
                set => SetProperty(ref _userName, value);
            }

            public ICommand LoginCommand { get; }

            public ICommand CancelCommand { get; }

            public event EventHandler<bool>? CloseRequested;
        }

        public sealed class MainViewModel
        {
            public MainViewModel(string userName) => Greeting = $"Signed in as {userName}";

            public string Greeting { get; }
        }
        """;

    private const string LoginWindowXaml = """
        <Window x:Class="LoginStartupProbe.LoginWindow"
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                Title="login" Width="260" Height="150" Left="40" Top="40"
                WindowStartupLocation="Manual">
          <StackPanel Margin="12">
            <TextBox Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}" />
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
              <Button Content="Login" Command="{Binding LoginCommand}" Width="70" />
              <Button Content="Cancel" Command="{Binding CancelCommand}" Width="70" Margin="8,0,0,0" />
            </StackPanel>
          </StackPanel>
        </Window>
        """;

    private const string LoginWindowCode = """
        namespace LoginStartupProbe;

        public partial class LoginWindow : System.Windows.Window
        {
            public LoginWindow() => InitializeComponent();
        }
        """;

    private const string MainWindowXaml = """
        <Window x:Class="LoginStartupProbe.MainWindow"
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                Title="main" Width="300" Height="160" Left="320" Top="40"
                WindowStartupLocation="Manual">
          <TextBlock Margin="12" Text="{Binding Greeting}" />
        </Window>
        """;

    /// <summary>
    /// メイン画面のコードビハインド。記事では <c>InitializeComponent</c> だけだが、ここでは
    /// StartupUri の構成でメイン画面が生成されたかを観測するため、生成の記録を 1 行足している（観測のための例外）。
    /// </summary>
    private const string MainWindowCode = """
        namespace LoginStartupProbe;

        public partial class MainWindow : System.Windows.Window
        {
            public MainWindow()
            {
                InitializeComponent();
                Program.Record("main created");
            }
        }
        """;

    /// <summary>
    /// 検証用アプリの本体。<c>OnStartup</c> の中身を構成ごとに切り替える。
    /// 利用者の操作は、ログイン画面の表示から 0.8 秒後にビューモデルのコマンドを実行するか、
    /// ウィンドウへ WM_CLOSE を送って代える。メイン画面は表示から 1.5 秒後に開いているかを記録し、閉じる。
    /// 終了処理が始まったかは、<c>Application</c> の内部の静的プロパティ <c>IsShuttingDown</c> をリフレクションで読む。
    /// </summary>
    private const string ProgramSource = """
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Linq;
        using System.Reflection;
        using System.Runtime.InteropServices;
        using System.Windows;
        using System.Windows.Interop;
        using System.Windows.Threading;

        namespace LoginStartupProbe;

        internal static class Program
        {
            public static string Log = "login-startup-probe.txt";
            private static readonly Stopwatch Clock = Stopwatch.StartNew();

            [DllImport("user32.dll")]
            private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            private const uint WM_CLOSE = 0x0010;

            private static readonly PropertyInfo IsShuttingDownProperty =
                typeof(Application).GetProperty("IsShuttingDown", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMemberException("Application.IsShuttingDown が見つからない。");

            public static bool IsShuttingDown => (bool)IsShuttingDownProperty.GetValue(null)!;

            public static void Record(string label, string value = "")
                => File.AppendAllText(Log, $"{label}\t{value}\t{Clock.Elapsed.TotalMilliseconds:F0}\n");

            [STAThread]
            private static int Main(string[] args)
            {
                string condition = args.Length > 0 ? args[0] : "naive";
                Log = args.Length > 1 ? args[1] : Log;
                File.WriteAllText(Log, "");

                var app = new ProbeApp(condition);
                if (condition == "startup-uri")
                {
                    app.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
                }

                app.Exit += (_, _) => Record("Application.Exit");
                app.DispatcherUnhandledException += (_, e) =>
                {
                    Record("unhandled", e.Exception.GetType().Name);
                    Record("unhandled message", e.Exception.Message);
                    e.Handled = true;
                };

                // ログイン画面とメイン画面が表示されたら、利用者の操作の代わりと観測を始める。
                EventManager.RegisterClassHandler(typeof(LoginWindow), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler((s, _) => OnLoginLoaded((LoginWindow)s, condition)));
                EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler((s, _) => OnMainLoaded((MainWindow)s)));

                int code = app.Run();
                Record("Run() returned");
                return code;
            }

            private static void OnLoginLoaded(LoginWindow login, string condition)
            {
                Record("MainWindow at login", Application.Current.MainWindow?.GetType().Name ?? "null");
                var viewModel = (LoginViewModel)login.DataContext;
                viewModel.UserName = "alice";
                After(0.8, () =>
                {
                    if (condition.EndsWith("-close-button"))
                        PostMessage(new WindowInteropHelper(login).Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    else if (condition.Contains("-cancel"))
                        viewModel.CancelCommand.Execute(null);
                    else
                        viewModel.LoginCommand.Execute(null);
                });
            }

            private static void OnMainLoaded(MainWindow main)
            {
                Record("main loaded");
                main.Closing += (_, _) => Record("main closing");
                After(1.5, () =>
                {
                    bool open = main.IsVisible && Application.Current?.Windows.OfType<MainWindow>().Contains(main) == true;
                    Record("main after 1.5 s", open ? "open" : "gone");
                    if (open) main.Close();
                });
            }

            private static void After(double seconds, Action action)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                timer.Tick += (_, _) => { timer.Stop(); action(); };
                timer.Start();
            }
        }

        /// <summary>
        /// 記事の App。構成ごとに OnStartup の中身だけが違う。
        /// </summary>
        internal sealed class ProbeApp(string condition) : Application
        {
            protected override void OnStartup(StartupEventArgs e)
            {
                base.OnStartup(e);
                switch (condition)
                {
                    case "naive":
                    case "startup-uri":
                        Naive();
                        break;
                    case "main-window-close":
                        ShutdownMode = ShutdownMode.OnMainWindowClose;
                        Naive();
                        break;
                    case "main-first":
                    case "main-first-cancel":
                    case "main-first-cancel-shutdown":
                        MainFirst();
                        break;
                    case "late-explicit":
                        LateExplicit();
                        break;
                    default:
                        Explicit();
                        break;
                }
            }

            /// <summary>Shutdown() を呼んだことを記録してから呼ぶ。</summary>
            private void StopApp()
            {
                Program.Record("Shutdown() called");
                Shutdown();
            }

            /// <summary>ログイン画面を出し、閉じた結果を返す。</summary>
            private static bool? ShowLogin(out string userName)
            {
                var viewModel = new LoginViewModel();
                var login = new LoginWindow { DataContext = viewModel };

                void OnCloseRequested(object? sender, bool result) => login.DialogResult = result;
                viewModel.CloseRequested += OnCloseRequested;
                try
                {
                    bool? result = login.ShowDialog();
                    Program.Record("login closed", result?.ToString() ?? "null");
                    Program.Record("MainWindow after login", Current.MainWindow?.GetType().Name ?? "null");
                    Program.Record("shutting down after login", Program.IsShuttingDown.ToString());
                    userName = viewModel.UserName;
                    return result;
                }
                finally
                {
                    viewModel.CloseRequested -= OnCloseRequested;
                }
            }

            private void ShowMain(MainWindow main)
            {
                main.Show();
                Program.Record("after Show", main.IsVisible.ToString());
                Program.Record("MainWindow after Show", MainWindow?.GetType().Name ?? "null");
            }

            /// <summary>よく書かれる形。ShutdownMode を変えずに、ログイン後にメイン画面を出す。</summary>
            private void Naive()
            {
                if (ShowLogin(out string userName) != true)
                {
                    StopApp();
                    return;
                }

                if (condition == "startup-uri") return; // メイン画面は StartupUri に任せる

                ShowMain(new MainWindow { DataContext = new MainViewModel(userName) });
            }

            /// <summary>メイン画面を先に生成しておき、ログイン後に表示する。</summary>
            private void MainFirst()
            {
                var main = new MainWindow();
                if (ShowLogin(out string userName) != true)
                {
                    // main-first-cancel は Shutdown を呼ばない形。生成済みのメイン画面がプロセスを引き止める。
                    if (condition == "main-first-cancel-shutdown") StopApp();
                    return;
                }

                main.DataContext = new MainViewModel(userName);
                ShowMain(main);
            }

            /// <summary>記事の推奨形と、そこから 1 か所ずつ変えた形。</summary>
            private void Explicit()
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                if (ShowLogin(out string userName) != true)
                {
                    StopApp();
                    return;
                }

                if (condition.StartsWith("explicit-extra-window") || condition == "explicit-last-window-extra")
                {
                    // ログインとメイン画面の間で、別のウィンドウを生成する変更が入った形（表示はしない）。
                    _ = new Window { Title = "extra" };
                }

                var main = new MainWindow { DataContext = new MainViewModel(userName) };
                if (condition is not ("explicit-no-assign" or "explicit-extra-window")) MainWindow = main;

                ShutdownMode = condition switch
                {
                    "explicit-no-restore" => ShutdownMode.OnExplicitShutdown,
                    "explicit-last-window" or "explicit-last-window-extra" => ShutdownMode.OnLastWindowClose,
                    _ => ShutdownMode.OnMainWindowClose,
                };
                ShowMain(main);
            }

            /// <summary>ログイン画面が閉じた後に OnExplicitShutdown へ変える形。変えるのが遅い。</summary>
            private void LateExplicit()
            {
                if (ShowLogin(out string userName) != true)
                {
                    StopApp();
                    return;
                }

                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var main = new MainWindow { DataContext = new MainViewModel(userName) };
                MainWindow = main;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                ShowMain(main);
            }
        }
        """;

    private static readonly (string Name, string Content)[] ProbeFiles =
    [
        ("probe.csproj", ProjectFile),
        ("ViewModels.cs", ViewModelsSource),
        ("LoginWindow.xaml", LoginWindowXaml),
        ("LoginWindow.xaml.cs", LoginWindowCode),
        ("MainWindow.xaml", MainWindowXaml),
        ("MainWindow.xaml.cs", MainWindowCode),
        ("Program.cs", ProgramSource),
    ];
}
