using System.Diagnostics;
using System.Text;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先」の表。
///
/// 3 つのハンドラ（<c>Application.DispatcherUnhandledException</c>・
/// <c>AppDomain.CurrentDomain.UnhandledException</c>・<c>TaskScheduler.UnobservedTaskException</c>）を
/// 登録した検証用アプリを、例外の経路だけを変えて起動する。
/// 例外を投げたスレッド、呼ばれたハンドラ、プロセスが動き続けたかを記録し、経路ごとの期待値と照合する。
/// 未処理例外はプロセスを終わらせるため、1 つの経路ごとに別プロセスで測る。
/// </summary>
internal sealed class UnhandledExceptionRoutesScene : IScene
{
    /// <summary>これを超えて生き残ったプロセスは、終了処理が止まったものとして扱う。</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public IReadOnlyList<string> Verifies =>
    [
        "例外の経路ごとに、例外を投げたスレッド・3 つのハンドラのどれが呼ばれたか・プロセスが動き続けるかを別プロセスで測り、期待値と照合する",
        "Click ハンドラと DispatcherTimer.Tick の例外は DispatcherUnhandledException に届き、Handled = true でプロセスが動き続けること",
        "Handled を立てないと AppDomain.UnhandledException が IsTerminating = true で呼ばれ、プロセスが終了すること",
        "new Thread の例外は DispatcherUnhandledException に届かず、AppDomain.UnhandledException の後にプロセスが終了すること",
        "2 つ目の UI スレッドの例外は Application.DispatcherUnhandledException に届かず、そのスレッドの Dispatcher.UnhandledException で Handled = true にすればプロセスが動き続けること",
        "待たれない Task の例外は、Task.Run でも UI スレッドで投げた async Task でも、GC が走らない限りどのハンドラにも届かないこと（GC を TryStartNoGCRegion で抑止して確かめる）",
        "待たれない Task の例外は、GC.Collect と GC.WaitForPendingFinalizers の後に UnobservedTaskException だけを呼び、プロセスは動き続けること",
        "System.Timers.Timer の同期の Elapsed の例外はどのハンドラにも届かず、async の Elapsed の例外はプロセスを終了させること",
        "UI スレッドで開始した async void の Click では、await した Task.Run の例外も ConfigureAwait(false) の後にスレッドプールで投げた例外も DispatcherUnhandledException に届くこと",
        "async void の Click から async Task のメソッドを await すると、そのメソッドの中で投げた例外も DispatcherUnhandledException に届くこと",
        "await を try / catch で囲むと、Task.Run の例外は catch で受けられ、どのハンドラも呼ばれないこと",
        "スレッドプールで開始した async void の例外はプロセスを終了させること",
    ];

    public string Slug => "wpf-unhandled-exception-dispatcher-task-thread";

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>
    /// 経路ごとの期待値。<c>Thrown</c> は例外を投げたスレッド（<c>ui</c> / <c>ui2</c> / <c>pool</c> / <c>thread</c>）。
    /// <c>Receiver</c> は呼ばれるハンドラ（<c>dispatcher</c> / <c>thread-dispatcher</c> / <c>appdomain</c> /
    /// <c>dispatcher+appdomain</c> / <c>unobserved</c> / <c>caught</c> / 空文字はどれも呼ばれない）。
    /// </summary>
    private readonly record struct Expected(string Thrown, string Receiver, bool Alive);

    /// <summary>表に載せる経路。<c>Key</c> は検証用アプリへ渡す引数。</summary>
    private static readonly (string Key, Loc Label, Expected Expected)[] Routes =
    [
        ("ui-handled", T("Click handler (Handled = true)", "Click ハンドラ（Handled = true）"), new("ui", "dispatcher", true)),
        ("ui-unhandled", T("Click handler (Handled not set)", "Click ハンドラ（Handled を立てない）"), new("ui", "dispatcher+appdomain", false)),
        ("dispatcher-timer", "DispatcherTimer.Tick", new("ui", "dispatcher", true)),
        ("thread", "new Thread(...)", new("thread", "appdomain", false)),
        ("second-ui-thread", T("2nd UI thread (Dispatcher.Run)", "2 つ目の UI スレッド（Dispatcher.Run）"), new("ui2", "appdomain", false)),
        ("second-ui-thread-handled", T("+ its own Dispatcher.UnhandledException", "+ そのスレッドの Dispatcher.UnhandledException"), new("ui2", "thread-dispatcher", true)),
        ("task-run", T("Task.Run(...), not awaited (no GC)", "Task.Run(...) を待たない（GC なし）"), new("pool", "", true)),
        ("task-run-gc", T("+ GC.Collect()", "+ GC.Collect()"), new("pool", "unobserved", true)),
        ("async-task-discarded", T("async Task called from Click, not awaited (no GC)", "Click から呼んだ async Task を待たない（GC なし）"), new("ui", "", true)),
        ("timers-timer", T("System.Timers.Timer.Elapsed (sync)", "System.Timers.Timer.Elapsed（同期）"), new("pool", "", true)),
        ("timers-timer-async", T("System.Timers.Timer.Elapsed (async)", "System.Timers.Timer.Elapsed（async）"), new("pool", "appdomain", false)),
        ("async-void", T("async void Click, await Task.Run", "async void の Click で await Task.Run"), new("pool", "dispatcher", true)),
        ("async-void-configureawait", T("async void Click, after ConfigureAwait(false)", "async void の Click で ConfigureAwait(false) の後"), new("pool", "dispatcher", true)),
        ("async-void-await-chain", T("async void Click, await async Task", "async void の Click で async Task を await"), new("ui", "dispatcher", true)),
        ("async-void-caught", T("async void Click, try / catch around await", "async void の Click で await を try / catch"), new("pool", "caught", true)),
        ("async-void-threadpool", T("async void started on the thread pool", "スレッドプールで開始した async void"), new("pool", "appdomain", false)),
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "unhandled-exception-scene");
        Directory.CreateDirectory(workspace);
        string executable = await BuildProbeAsync(workspace);

        Loc called = T("called", "呼ばれる");
        Loc none = "—";

        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string key, Loc label, Expected expected) in Routes)
        {
            Measurement result = await MeasureAsync(executable, workspace, key);

            // 表は実行のたびに測り直す。本文は表の値を前提に書いているため、期待と食い違ったら止める。
            if (result.Thrown != expected.Thrown || result.Receiver != expected.Receiver || result.Alive != expected.Alive)
            {
                throw new InvalidOperationException(
                    $"{key}: 期待 {expected} に対して、実測は thrown={result.Thrown}, receiver={result.Receiver}, alive={result.Alive}。");
            }

            rows.Add([
                label,
                result.Thrown switch
                {
                    "ui" => T("UI", "UI"),
                    "ui2" => T("2nd UI", "2 つ目の UI"),
                    "pool" => T("thread pool", "スレッドプール"),
                    _ => T("dedicated", "専用スレッド"),
                },
                result.Receiver switch
                {
                    "dispatcher" or "dispatcher+appdomain" => called,
                    "thread-dispatcher" => T("— (its own Dispatcher.UnhandledException is called)", "—（そのスレッドの Dispatcher.UnhandledException が呼ばれる）"),
                    "caught" => T("— (caught by catch)", "—（catch で受ける）"),
                    _ => none,
                },
                result.Receiver is "appdomain" or "dispatcher+appdomain"
                    ? T("called (IsTerminating = true)", "呼ばれる（IsTerminating = true）")
                    : none,
                result.Receiver == "unobserved" ? called : none,
                result.Alive ? T("keeps running", "動き続ける") : T("terminated", "終了する"),
            ]);
        }

        await context.SaveTableAsync(
            "which handler receives the exception",
            [
                T("where the exception is thrown", "例外が発生する場所"),
                T("throwing thread", "投げたスレッド"),
                "DispatcherUnhandledException",
                "AppDomain.UnhandledException",
                "UnobservedTaskException",
                T("process", "プロセス"),
            ],
            rows,
            "unhandled-exception-routes.svg");
    }

    private readonly record struct Measurement(string Thrown, string Receiver, bool Alive);

    /// <summary>
    /// 検証用アプリを一時ディレクトリへ書き出してビルドし、実行ファイルのパスを返す。
    /// </summary>
    private static async Task<string> BuildProbeAsync(string workspace)
    {
        await File.WriteAllTextAsync(Path.Combine(workspace, "probe.csproj"), ProjectFile, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(workspace, "Program.cs"), ProbeSource, new UTF8Encoding(false));

        using var build = Process.Start(new ProcessStartInfo("dotnet", "build -c Release -v q --nologo")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("dotnet build を起動できない。");

        // 片方を読み切るまでもう片方を読まないと、バッファが埋まった側で build が止まる。
        Task<string> stdout = build.StandardOutput.ReadToEndAsync();
        Task<string> stderr = build.StandardError.ReadToEndAsync();
        await build.WaitForExitAsync();
        string output = await stdout + await stderr;

        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException("検証用アプリのビルドに失敗した:\n" + output);
        }

        return Path.Combine(workspace, "bin", "Release", "net10.0-windows", "ExceptionProbe.exe");
    }

    /// <summary>
    /// 経路を 1 つ実行し、例外を投げたスレッド・呼ばれたハンドラ・プロセスが動き続けたかを記録する。
    /// </summary>
    private static async Task<Measurement> MeasureAsync(string executable, string workspace, string route)
    {
        string log = Path.Combine(workspace, $"log-{route}.txt");
        File.Delete(log);

        using var process = Process.Start(new ProcessStartInfo(executable, $"{route} \"{log}\"")
        {
            UseShellExecute = false,
            // 未処理例外の内容は標準エラーに出る。表には使わないので捨てる。
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("検証用アプリを起動できない。");
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        using var cancellation = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Kill は終了を待たない。残ったプロセスが次の計測に影響しないよう、終わるまで待つ。
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new InvalidOperationException($"{route}: 検証用アプリが {Timeout.TotalSeconds} 秒で終わらなかった。");
        }

        await stderr;

        string[] lines = File.Exists(log) ? await File.ReadAllLinesAsync(log) : [];
        if (!lines.Contains("started"))
        {
            throw new InvalidOperationException($"{route}: 検証用アプリが経路を実行する前に終了した（終了コード {process.ExitCode}）。");
        }

        if (lines.Contains("nogc-lost"))
        {
            throw new InvalidOperationException($"{route}: GC の抑止が計測の途中で外れた。");
        }

        string[] thrown = lines.Where(line => line.StartsWith("thrown ", StringComparison.Ordinal)).ToArray();
        if (thrown.Length != 1)
        {
            throw new InvalidOperationException($"{route}: 例外がちょうど 1 回投げられていない（{thrown.Length} 回）。");
        }

        bool alive = lines.Contains("alive");

        // 「動き続ける」は、例外の後も UI スレッドが回り続け、自分で Shutdown して 0 で終わったことを指す。
        if (alive != (process.ExitCode == 0))
        {
            throw new InvalidOperationException($"{route}: alive={alive} と終了コード {process.ExitCode} が食い違う。");
        }

        // 判定は、経路を始めてから alive を記録するまでの観測期間に限る。
        // 終了処理の途中で GC が走ると、待たれない Task の UnobservedTaskException が alive の後に呼ばれることがあるため。
        IEnumerable<string> observed = alive ? lines.TakeWhile(line => line != "alive") : lines;

        // ハンドラは記録した順に並べる。Handled を立てない経路では Dispatcher の後に AppDomain が呼ばれる。
        string receiver = string.Join("+", observed
            .Select(line => line switch
            {
                "Dispatcher" => "dispatcher",
                "ThreadDispatcher" => "thread-dispatcher",
                "AppDomain terminating" => "appdomain",
                "AppDomain" => "appdomain-nonterminating",
                "Unobserved" => "unobserved",
                "caught" => "caught",
                _ => null,
            })
            .OfType<string>());

        return new Measurement(thrown[0]["thrown ".Length..], receiver, alive);
    }

    private const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net10.0-windows</TargetFramework>
            <UseWPF>true</UseWPF>
            <Nullable>disable</Nullable>
            <LangVersion>latest</LangVersion>
            <AssemblyName>ExceptionProbe</AssemblyName>
            <StartupObject>ExceptionProbe.Program</StartupObject>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
          </PropertyGroup>
          <ItemGroup><Compile Include="Program.cs" /></ItemGroup>
        </Project>
        """;

    /// <summary>
    /// 検証用アプリの本体。記事の実装例と同じく <c>Startup</c> で 3 つのハンドラを登録し、
    /// 引数で指定した経路で例外を 1 回だけ発生させる。
    /// Click は <c>ButtonAutomationPeer</c> の Invoke で非同期に発生させ、UI スレッドの Dispatcher の処理の中で
    /// Click ハンドラが実行されるようにしている。
    /// 経路を始めてから 2 秒後にまだ UI スレッドが動いていれば <c>alive</c> を記録して自分で終了する。
    /// </summary>
    private const string ProbeSource = """
        using System;
        using System.IO;
        using System.Runtime.InteropServices;
        using System.Threading;
        using System.Threading.Tasks;
        using System.Windows;
        using System.Windows.Automation.Peers;
        using System.Windows.Automation.Provider;
        using System.Windows.Controls;
        using System.Windows.Threading;

        namespace ExceptionProbe;

        internal static class Program
        {
            private static readonly object Gate = new();
            private static string _log;
            private static string _route;
            private static Dispatcher _main;

            // ローカル変数だけで持つと、発火する前に GC で回収されて Elapsed が呼ばれないことがある。
            private static System.Timers.Timer _timer;

            [DllImport("kernel32.dll")]
            private static extern uint SetErrorMode(uint mode);

            private static void Record(string label)
            {
                lock (Gate)
                {
                    File.AppendAllText(_log, label + "\n");
                }
            }

            /// <summary>例外を投げる。投げたスレッドの種類を先に記録する。</summary>
            private static void Fail(string where)
            {
                string thread = _main.CheckAccess() ? "ui"
                    : Dispatcher.FromThread(Thread.CurrentThread) != null ? "ui2"
                    : Thread.CurrentThread.IsThreadPoolThread ? "pool"
                    : "thread";
                Record("thrown " + thread);
                throw new InvalidOperationException(where);
            }

            [STAThread]
            private static int Main(string[] args)
            {
                // クラッシュしたときに Windows エラー報告のダイアログで止まらないようにする。
                SetErrorMode(0x0001 | 0x0002);

                _route = args[0];
                _log = args[1];

                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                _main = app.Dispatcher;

                // 記事の実装例（OnStartup）と同じく、起動時に 3 つのハンドラを登録する。
                app.Startup += (_, _) =>
                {
                    app.DispatcherUnhandledException += (_, e) =>
                    {
                        Record("Dispatcher");
                        e.Handled = _route != "ui-unhandled";
                    };

                    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                        Record(e.IsTerminating ? "AppDomain terminating" : "AppDomain");

                    TaskScheduler.UnobservedTaskException += (_, e) =>
                    {
                        Record("Unobserved");
                        e.SetObserved();
                    };

                    Start();
                };

                return app.Run();
            }

            private static void Start()
            {
                var button = new Button { Content = "Run" };
                var window = new Window { Title = "probe", Width = 200, Height = 120, Left = 40, Top = 40, Content = button };
                window.Show();
                Record("started");

                // 待たれない Task の例外は、GC がファイナライザーを走らせるまでどこにも通知されない。
                // 2 秒の間に GC が自然に走ると結果が揺れるため、Task の経路では GC を抑止して測る。
                bool suppressGc = _route is "task-run" or "task-run-gc" or "async-task-discarded";
                if (suppressGc)
                {
                    try
                    {
                        if (!GC.TryStartNoGCRegion(32 * 1024 * 1024))
                        {
                            Record("nogc-lost");
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // 要求サイズが ephemeral セグメントを超える環境では、開始できずに例外になる。
                        Record("nogc-lost");
                    }
                }

                switch (_route)
                {
                    case "ui-handled":
                    case "ui-unhandled":
                        button.Click += (_, _) => Fail("click");
                        Click(button);
                        break;
                    case "dispatcher-timer":
                        var tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                        tick.Tick += (_, _) =>
                        {
                            tick.Stop();
                            Fail("dispatcher timer");
                        };
                        tick.Start();
                        break;
                    case "thread":
                        new Thread(() => Fail("thread")) { IsBackground = true }.Start();
                        break;
                    case "second-ui-thread":
                    case "second-ui-thread-handled":
                        StartSecondUiThread(handle: _route == "second-ui-thread-handled");
                        break;
                    case "task-run":
                    case "task-run-gc":
                        button.Click += (_, _) => Task.Run(() => Fail("task"));
                        Click(button);
                        break;
                    case "async-task-discarded":
                        button.Click += (_, _) => _ = SaveAsync();
                        Click(button);
                        break;
                    case "timers-timer":
                        _timer = new System.Timers.Timer(100) { AutoReset = false };
                        _timer.Elapsed += (_, _) => Fail("timer");
                        _timer.Start();
                        break;
                    case "timers-timer-async":
                        _timer = new System.Timers.Timer(100) { AutoReset = false };
                        _timer.Elapsed += async (_, _) =>
                        {
                            await Task.Delay(100);
                            Fail("async timer");
                        };
                        _timer.Start();
                        break;
                    case "async-void":
                        button.Click += OnClickAsync;
                        Click(button);
                        break;
                    case "async-void-configureawait":
                        button.Click += OnClickConfigureAwaitAsync;
                        Click(button);
                        break;
                    case "async-void-await-chain":
                        button.Click += OnClickAwaitChainAsync;
                        Click(button);
                        break;
                    case "async-void-caught":
                        button.Click += OnClickCaughtAsync;
                        Click(button);
                        break;
                    case "async-void-threadpool":
                        Task.Run(() => FireAndForget());
                        break;
                }

                var check = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                check.Tick += (_, _) =>
                {
                    check.Stop();
                    if (suppressGc)
                    {
                        if (System.Runtime.GCSettings.LatencyMode != System.Runtime.GCLatencyMode.NoGCRegion)
                        {
                            Record("nogc-lost");
                        }
                        else
                        {
                            try
                            {
                                GC.EndNoGCRegion();
                            }
                            catch (InvalidOperationException)
                            {
                                // 割り当てが上限を超えたなどで、抑止が途中で終わっていた。
                                Record("nogc-lost");
                            }
                        }
                    }

                    if (_route == "task-run-gc")
                    {
                        // 待たれずに失敗した Task は、ファイナライザーで初めて UnobservedTaskException を発生させる。
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }

                    Record("alive");
                    Application.Current.Shutdown();
                };
                check.Start();
            }

            /// <summary>ButtonAutomationPeer の Invoke で、非同期に Click を発生させる。</summary>
            private static void Click(Button button)
            {
                var peer = new ButtonAutomationPeer(button);
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            }

            /// <summary>UI スレッドで再開してから投げる async Task。呼び出し側は待たない。</summary>
            private static async Task SaveAsync()
            {
                await Task.Delay(100);
                Fail("async Task");
            }

            private static async void OnClickAsync(object sender, RoutedEventArgs e)
            {
                await Task.Run(() => Fail("async void"));
            }

            private static async void OnClickConfigureAwaitAsync(object sender, RoutedEventArgs e)
            {
                await Task.Delay(100).ConfigureAwait(false);
                Fail("after ConfigureAwait(false)");
            }

            private static async void OnClickAwaitChainAsync(object sender, RoutedEventArgs e)
            {
                await SaveAsync();
            }

            private static async void OnClickCaughtAsync(object sender, RoutedEventArgs e)
            {
                try
                {
                    await Task.Run(() => Fail("caught"));
                }
                catch (InvalidOperationException)
                {
                    Record("caught");
                }
            }

            private static async void FireAndForget()
            {
                await Task.Delay(100);
                Fail("async void on the thread pool");
            }

            private static void StartSecondUiThread(bool handle)
            {
                var thread = new Thread(() =>
                {
                    if (handle)
                    {
                        // Application.DispatcherUnhandledException はメインの Dispatcher だけを見る。
                        // 別の UI スレッドは、そのスレッドの Dispatcher に登録する。
                        Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
                        {
                            Record("ThreadDispatcher");
                            e.Handled = true;
                        };
                    }

                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Fail("second UI thread")));
                    Dispatcher.Run();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }
        }
        """;
}
