using System.Diagnostics;
using System.Text;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で Task.Result を呼ぶと画面が固まる原因と、ConfigureAwait(false) で直らない条件」の表。
///
/// Click ハンドラ（UI スレッド）から、非同期メソッドの完了を呼び出し方だけ変えて待つ。
/// UI スレッドが止まった時間、await の後の処理がどのスレッドで走ったか、待っている間に UI スレッドへ積んだ処理が走ったかを記録する。
/// 固まった UI スレッドは二度と戻らないため、呼び出し方ごとに別プロセスで測り、固まったプロセスは外から終了させる。
/// </summary>
internal sealed class TaskResultDeadlockScene : IScene
{
    /// <summary>
    /// 固まったかどうかは検証用アプリの監視スレッドが判定し、<c>hung</c> を記録した時点で終了させる。
    /// これは、監視スレッドが判定を書けないまま止まった場合の上限である（起動にかかる時間を含めて数える）。
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public IReadOnlyList<string> Verifies =>
    [
        "Click ハンドラの中では SynchronizationContext.Current が DispatcherSynchronizationContext であること",
        "UI スレッドで async メソッドの Task に .Result・.GetAwaiter().GetResult()・.Wait() を呼ぶと、いずれも戻らないこと",
        "固まったとき、内側の Task.Delay は完了しているのに await の後の処理は走っておらず、UI スレッドへ Normal と Render の優先度で積んだ処理も Pending のままであること",
        "Wait(TimeSpan) はタイムアウトで false を返し、その後で await の後の処理が UI スレッドで走ること（記録の順序で確かめる）",
        "呼び出し先のすべての await に ConfigureAwait(false) を付けると .Result が戻り、await の後の処理はスレッドプールで走ること。UI スレッドは Task.Delay の合計の時間だけ止まり、待っている間に積んだ処理は戻った後に走ること",
        "最初に中断する await にだけ ConfigureAwait(false) を付け、後続の await に付けなくても .Result が戻ること。2 つの await の間で SynchronizationContext.Current が null であること",
        "最初の await が完了済みの Task だと ConfigureAwait(false) を付けても UI スレッドに留まり、付いていない次の await で固まること。次の await にも付けていれば戻ること",
        "外側の await にだけ ConfigureAwait(false) を付けても、内側のメソッドの await が UI スレッドを捕まえて固まり、外側の await の後には到達しないこと",
        "Task.Run の中では SynchronizationContext.Current が null で、Task.Run で包んで .Result を呼ぶと戻ること（await の後はスレッドプール）",
        "Task.Run で包んでも、その中で Dispatcher.Invoke を呼ぶと Invoke から戻らずに固まること",
        "async void の Click で await すると UI スレッドは止まらず、待っている間に積んだ処理がすぐに走り、await の後は UI スレッドで走ること",
    ];

    public string Slug => "wpf-task-result-deadlock-synchronizationcontext";

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>
    /// 呼び出し方ごとの期待値。<c>Continued</c> は await の後の処理が走ったスレッド
    /// （<c>ui</c> / <c>pool</c> / 空文字は走らない）。<c>UiWork</c> は待っている間に積んだ UI の処理がいつ走ったか
    /// （<c>during</c> / <c>after</c> / <c>pending</c>）。
    /// </summary>
    private readonly record struct Expected(bool Hung, string Continued, string UiWork);

    /// <summary>表に載せる呼び出し方。<c>Key</c> は検証用アプリへ渡す引数。</summary>
    private static readonly (string Key, Loc Label, Expected Expected)[] Routes =
    [
        ("result", "LoadAsync().Result", new(true, "", "pending")),
        ("getresult", "LoadAsync().GetAwaiter().GetResult()", new(true, "", "pending")),
        ("wait", "LoadAsync().Wait()", new(true, "", "pending")),
        ("wait-timeout", "LoadAsync().Wait(TimeSpan.FromSeconds(1))", new(false, "ui", "after")),
        ("configureawait-all", "LoadAllConfigureAwaitAsync().Result", new(false, "pool", "after")),
        ("configureawait-first-suspend", "LoadFirstSuspendConfigureAwaitAsync().Result", new(false, "pool", "after")),
        ("configureawait-completed", "LoadCachedFirstAsync().Result", new(true, "", "pending")),
        ("configureawait-completed-all", "LoadCachedFirstAllConfigureAwaitAsync().Result", new(false, "pool", "after")),
        ("configureawait-outer", "LoadOuterAsync().Result", new(true, "", "pending")),
        ("task-run", "Task.Run(() => LoadAsync()).Result", new(false, "pool", "after")),
        ("task-run-invoke", "Task.Run(() => LoadWithInvokeAsync()).Result", new(true, "pool", "pending")),
        ("await", T("await LoadAsync() in async void Click", "async void の Click で await LoadAsync()"), new(false, "ui", "during")),
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "task-result-deadlock-scene");
        Directory.CreateDirectory(workspace);
        string executable = await BuildProbeAsync(workspace);

        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string key, Loc label, Expected expected) in Routes)
        {
            Measurement result = await MeasureAsync(executable, workspace, key);

            // 表は実行のたびに測り直す。本文は表の値を前提に書いているため、期待と食い違ったら止める。
            if (result.Hung != expected.Hung || result.Continued != expected.Continued || result.UiWork != expected.UiWork)
            {
                throw new InvalidOperationException(
                    $"{key}: 期待 {expected} に対して、実測は hung={result.Hung}, continued={result.Continued}, uiWork={result.UiWork}。");
            }

            CheckDuration(key, result);

            rows.Add([
                label,
                result.Hung
                    ? T("stays blocked", "止まったまま")
                    : key switch
                    {
                        "wait-timeout" => T($"blocked {result.BlockedMs:N0} ms, Wait returns false", $"{result.BlockedMs:N0} ms 止まり、Wait は false を返す"),
                        "await" => T($"not blocked (queued work ran after {result.BlockedMs:N0} ms)", $"止まらない（積んだ処理は {result.BlockedMs:N0} ms 後に走る）"),
                        _ => T($"blocked {result.BlockedMs:N0} ms, then returns", $"{result.BlockedMs:N0} ms 止まって戻る"),
                    },
                result.Continued switch
                {
                    "ui" when key == "wait-timeout" => T("UI (after Wait returned)", "UI（Wait が戻った後）"),
                    "ui" => "UI",
                    "pool" when key == "task-run-invoke" => T("thread pool (then never returns from Dispatcher.Invoke)", "スレッドプール（その後 Dispatcher.Invoke から戻らない）"),
                    "pool" => T("thread pool", "スレッドプール"),
                    _ => T("does not run (Task.Delay already completed)", "走らない（Task.Delay は完了済み）"),
                },
                result.UiWork switch
                {
                    "during" => T("runs while waiting", "待っている間に走る"),
                    "after" => T("runs after the Click returns", "Click が戻った後に走る"),
                    _ => T("stays Pending", "Pending のまま"),
                },
            ]);
        }

        await context.SaveTableAsync(
            "blocking on an async method from the UI thread",
            [
                T("how the Click handler waits", "Click ハンドラでの待ち方"),
                T("UI thread", "UI スレッド"),
                T("code after the inner await", "内側の await の後"),
                T("UI work queued during the wait", "待っている間に UI スレッドへ積んだ処理"),
            ],
            rows,
            "task-result-deadlock-matrix.svg");
    }

    private readonly record struct Measurement(bool Hung, long BlockedMs, string Continued, string UiWork);

    /// <summary>
    /// 本文は「戻る待ち方では UI スレッドが Task.Delay(300) の合計の時間だけ止まる（first-suspend は 2 回で 600 ms）」
    /// 「Wait(TimeSpan) はタイムアウトの 1 秒止まる」「await では止まらない」と書いている。時間がその範囲を外れたら止める。
    /// </summary>
    private static void CheckDuration(string key, Measurement result)
    {
        if (result.Hung)
        {
            return;
        }

        (long min, long max) = key switch
        {
            "wait-timeout" => (950L, 1600L),
            "await" => (0L, 250L),
            "configureawait-first-suspend" => (550L, 1000L),
            _ => (280L, 600L),
        };

        if (result.BlockedMs < min || result.BlockedMs > max)
        {
            throw new InvalidOperationException($"{key}: UI スレッドが止まった時間 {result.BlockedMs} ms が想定の {min}〜{max} ms を外れた。");
        }
    }

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

        return Path.Combine(workspace, "bin", "Release", "net10.0-windows", "DeadlockProbe.exe");
    }

    /// <summary>
    /// 呼び出し方を 1 つ実行し、UI スレッドが止まった時間、await の後の処理が走ったスレッド、
    /// 待っている間に積んだ UI の処理がいつ走ったかを記録する。
    /// </summary>
    private static async Task<Measurement> MeasureAsync(string executable, string workspace, string route)
    {
        string log = Path.Combine(workspace, $"log-{route}.txt");
        File.Delete(log);

        using var process = Process.Start(new ProcessStartInfo(executable, $"{route} \"{log}\"")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("検証用アプリを起動できない。");
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        // 監視スレッドが hung を書いたら、その時点で終了させる。固まった UI スレッドは戻らない。
        bool exited = false;
        var limit = Stopwatch.StartNew();
        while (true)
        {
            if (process.HasExited)
            {
                exited = true;
                break;
            }

            bool timedOut = limit.Elapsed > Timeout;
            if (ReadLines(log).Contains("hung") || timedOut)
            {
                // Kill は終了を待たない。次の計測に影響しないよう、終わるまで待つ。
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                if (timedOut && !ReadLines(log).Contains("hung"))
                {
                    throw new InvalidOperationException($"{route}: 監視スレッドが判定を書かないまま {Timeout.TotalSeconds} 秒が過ぎた。");
                }

                break;
            }

            await Task.Delay(100);
        }

        string errors = await stderr;
        string[] lines = ReadLines(log);
        string? Value(string key) => lines.FirstOrDefault(line => line.StartsWith(key + " ", StringComparison.Ordinal))?[(key.Length + 1)..];
        int IndexOf(string prefix) => Array.FindIndex(lines, line => line.StartsWith(prefix, StringComparison.Ordinal));

        if (!lines.Contains("started"))
        {
            throw new InvalidOperationException($"{route}: 検証用アプリが計測を始める前に終了した。\n{errors}");
        }

        if (Value("context") != "DispatcherSynchronizationContext")
        {
            throw new InvalidOperationException($"{route}: Click ハンドラの SynchronizationContext が {Value("context")} だった。");
        }

        bool hung = lines.Contains("hung");
        if (hung == exited)
        {
            throw new InvalidOperationException($"{route}: 監視スレッドの判定（hung={hung}）とプロセスの終了（exited={exited}）が食い違う。\n{errors}");
        }

        if (exited && (process.ExitCode != 0 || !lines.Contains("alive")))
        {
            throw new InvalidOperationException($"{route}: Click が戻った後に UI スレッドが回り続けていない（終了コード {process.ExitCode}）。\n{errors}");
        }

        if (hung)
        {
            // 固まった時点で、待っていた Task.Delay は完了している。止まっているのは、その後の処理の実行である。
            if (Value("delay") != "RanToCompletion")
            {
                throw new InvalidOperationException($"{route}: 固まった時点の Task.Delay が {Value("delay")} だった。");
            }

            if (Value("ui-work-status") != "Pending" || Value("render-work-status") != "Pending")
            {
                throw new InvalidOperationException(
                    $"{route}: 固まった時点の UI の処理が Normal={Value("ui-work-status")}, Render={Value("render-work-status")} だった。");
            }
        }
        else if (!lines.Contains("ui-work") || !lines.Contains("render-work"))
        {
            throw new InvalidOperationException($"{route}: Click が戻った後も UI の処理が走っていない。");
        }

        switch (route)
        {
            case "wait-timeout":
                if (Value("wait-returned") != "False")
                {
                    throw new InvalidOperationException($"{route}: Wait(TimeSpan) が {Value("wait-returned")} を返した。");
                }

                // UI スレッドを放した後で、await の後の処理が UI スレッドで走ったこと。
                if (IndexOf("returned ") < 0 || IndexOf("continued ") < IndexOf("returned "))
                {
                    throw new InvalidOperationException($"{route}: await の後の処理が Wait の戻る前に走った。");
                }

                break;

            case "configureawait-completed" or "configureawait-completed-all":
                if (Value("after-completed-await") != "ui")
                {
                    throw new InvalidOperationException($"{route}: 完了済みの Task を await した後のスレッドが {Value("after-completed-await")} だった。");
                }

                break;

            case "configureawait-first-suspend":
                // 1 つ目の await の後には、2 つ目の await が取り込む UI スレッドのコンテキストが無いこと。
                if (Value("between-context") != "null")
                {
                    throw new InvalidOperationException($"{route}: 2 つの await の間の SynchronizationContext が {Value("between-context")} だった。");
                }

                break;

            case "configureawait-outer":
                if (IndexOf("outer-continued ") >= 0)
                {
                    throw new InvalidOperationException($"{route}: 外側の await の後に到達した。");
                }

                break;

            case "task-run" or "task-run-invoke":
                if (Value("pool-context") != "null")
                {
                    throw new InvalidOperationException($"{route}: Task.Run の中の SynchronizationContext が {Value("pool-context")} だった。");
                }

                if (route == "task-run-invoke" && (!lines.Contains("invoking") || lines.Contains("invoked")))
                {
                    throw new InvalidOperationException($"{route}: Dispatcher.Invoke の中で止まっていない。");
                }

                break;
        }

        string uiWork = hung ? "pending" : Value("ui-work-before-return") == "True" ? "during" : "after";

        // await の行は Click ハンドラが await で抜けるため、止まった時間ではなく積んだ処理が走るまでの時間を載せる。
        string blocked = route == "await" ? Value("ui-work-ms") ?? "" : Value("returned") ?? "";

        return new Measurement(
            hung,
            hung ? 0 : long.Parse(blocked),
            Value("continued") ?? "",
            uiWork);
    }

    private static string[] ReadLines(string path)
    {
        // 検証用アプリが追記している最中にも読む。書き込みを妨げないよう、書き込みの共有を許して開く。
        if (!File.Exists(path))
        {
            return [];
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net10.0-windows</TargetFramework>
            <UseWPF>true</UseWPF>
            <Nullable>disable</Nullable>
            <LangVersion>latest</LangVersion>
            <AssemblyName>DeadlockProbe</AssemblyName>
            <StartupObject>DeadlockProbe.Program</StartupObject>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
          </PropertyGroup>
          <ItemGroup><Compile Include="Program.cs" /></ItemGroup>
        </Project>
        """;

    /// <summary>
    /// 検証用アプリの本体。ウィンドウを表示し、<c>ButtonAutomationPeer</c> の Invoke で Click を発生させる。
    /// Click ハンドラは UI スレッドの Dispatcher の処理の中で実行されるので、利用者がボタンを押したときと同じ条件になる。
    ///
    /// Click ハンドラは最初に、UI スレッドへ処理を 2 つ積む（<c>BeginInvoke</c>。優先度は Normal と、
    /// レイアウトと描画が使う Render）。UI スレッドが待っている間にこれが走るかどうかで、UI スレッドが処理できる状態かを判定する。
    /// 監視スレッドは 2.5 秒後に Click ハンドラが戻っていなければ、その時点の状態と <c>hung</c> を記録する。
    /// Click が戻ったら 1 秒だけ UI スレッドを回してから終了する。
    /// </summary>
    private const string ProbeSource = """
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        using System.Windows;
        using System.Windows.Automation.Peers;
        using System.Windows.Automation.Provider;
        using System.Windows.Controls;
        using System.Windows.Threading;

        namespace DeadlockProbe;

        internal static class Program
        {
            private static readonly object Gate = new();
            private static string _log;
            private static string _route;
            private static Dispatcher _main;
            private static Label _status;
            private static Stopwatch _stopwatch;

            // 監視スレッドから読む状態。
            private static Task _delay;
            private static DispatcherOperation _uiWork;
            private static DispatcherOperation _renderWork;
            private static bool _returned;

            private static void Record(string label)
            {
                lock (Gate)
                {
                    // シーン側が読んでいる最中にも書けるよう、読み取りの共有を許して開く。
                    using var stream = new FileStream(_log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream);
                    writer.Write(label + "\n");
                }
            }

            private static string ThreadKind() =>
                _main.CheckAccess() ? "ui" : Thread.CurrentThread.IsThreadPoolThread ? "pool" : "other";

            [STAThread]
            private static int Main(string[] args)
            {
                _route = args[0];
                _log = args[1];

                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                _main = app.Dispatcher;

                app.Startup += (_, _) =>
                {
                    var button = new Button { Content = "Load" };
                    _status = new Label();
                    var panel = new StackPanel();
                    panel.Children.Add(button);
                    panel.Children.Add(_status);
                    var window = new Window
                    {
                        Content = panel,
                        Width = 240,
                        Height = 120,
                        ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    };

                    if (_route == "await")
                    {
                        button.Click += OnClickAwait;
                    }
                    else
                    {
                        button.Click += OnClick;
                    }

                    window.Show();
                    _main.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        var peer = new ButtonAutomationPeer(button);
                        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                    }));
                };

                return app.Run();
            }

            // ---- 待たれる側の非同期メソッド（記事のコード例と同じ名前） ----

            /// <summary>ConfigureAwait(false) を付けない。</summary>
            private static async Task<int> LoadAsync()
            {
                _delay = Task.Delay(300);
                await _delay;
                Record("continued " + ThreadKind());
                return 42;
            }

            /// <summary>すべての await に ConfigureAwait(false) を付ける。</summary>
            private static async Task<int> LoadAllConfigureAwaitAsync()
            {
                _delay = Task.Delay(300);
                await _delay.ConfigureAwait(false);
                Record("continued " + ThreadKind());
                return 42;
            }

            /// <summary>最初に中断する await にだけ ConfigureAwait(false) を付け、2 つ目には付けない。</summary>
            private static async Task<int> LoadFirstSuspendConfigureAwaitAsync()
            {
                await Task.Delay(300).ConfigureAwait(false);
                Record("between-context " + (SynchronizationContext.Current?.GetType().Name ?? "null"));
                _delay = Task.Delay(300);
                await _delay;
                Record("continued " + ThreadKind());
                return 42;
            }

            /// <summary>キャッシュに当たると同期的に完了する Task。</summary>
            private static Task<int> GetCachedAsync() => Task.FromResult(42);

            /// <summary>最初の await（完了済みの Task）にだけ ConfigureAwait(false) を付ける。</summary>
            private static async Task<int> LoadCachedFirstAsync()
            {
                int cached = await GetCachedAsync().ConfigureAwait(false);
                Record("after-completed-await " + ThreadKind());
                _delay = Task.Delay(300);
                await _delay;
                Record("continued " + ThreadKind());
                return cached;
            }

            /// <summary>最初の await は完了済みの Task で、2 つ目の await にも ConfigureAwait(false) を付ける。</summary>
            private static async Task<int> LoadCachedFirstAllConfigureAwaitAsync()
            {
                int cached = await GetCachedAsync().ConfigureAwait(false);
                Record("after-completed-await " + ThreadKind());
                _delay = Task.Delay(300);
                await _delay.ConfigureAwait(false);
                Record("continued " + ThreadKind());
                return cached;
            }

            /// <summary>外側の await にだけ ConfigureAwait(false) を付け、内側は LoadAsync のまま。</summary>
            private static async Task<int> LoadOuterAsync()
            {
                int value = await LoadAsync().ConfigureAwait(false);
                Record("outer-continued " + ThreadKind());
                return value;
            }

            /// <summary>スレッドプールで待ち、途中で画面を更新するために Dispatcher.Invoke を呼ぶ。</summary>
            private static async Task<int> LoadWithInvokeAsync()
            {
                _delay = Task.Delay(300);
                await _delay;
                Record("continued " + ThreadKind());
                Record("invoking");
                _main.Invoke(() => _status.Content = "loaded");
                Record("invoked");
                return 42;
            }

            // ---- Click ハンドラ ----

            private static void Begin()
            {
                Record("started");
                Record("context " + (SynchronizationContext.Current?.GetType().Name ?? "null"));
                _stopwatch = Stopwatch.StartNew();

                // UI スレッドが待っている間にこれが走れば、UI スレッドは処理できる状態である。
                _uiWork = _main.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    Record("ui-work-ms " + _stopwatch.ElapsedMilliseconds);
                    Record("ui-work");
                }));
                _renderWork = _main.BeginInvoke(DispatcherPriority.Render, new Action(() => Record("render-work")));

                var watchdog = new Thread(() =>
                {
                    Thread.Sleep(2500);
                    lock (Gate)
                    {
                        if (!_returned)
                        {
                            Record("delay " + (_delay?.Status.ToString() ?? "none"));
                            Record("ui-work-status " + _uiWork.Status);
                            Record("render-work-status " + _renderWork.Status);
                            Record("hung");
                        }
                    }
                })
                { IsBackground = true };
                watchdog.Start();
            }

            private static void Returned()
            {
                lock (Gate)
                {
                    _returned = true;
                    Record("returned " + _stopwatch.ElapsedMilliseconds);
                    Record("ui-work-before-return " + (_uiWork.Status == DispatcherOperationStatus.Completed));
                }

                _ = FinishAsync();
            }

            private static async Task FinishAsync()
            {
                await Task.Delay(1000);
                Record("alive");
                Application.Current.Shutdown(0);
            }

            private static void OnClick(object sender, RoutedEventArgs e)
            {
                Begin();
                switch (_route)
                {
                    case "result":
                        _ = LoadAsync().Result;
                        break;
                    case "getresult":
                        _ = LoadAsync().GetAwaiter().GetResult();
                        break;
                    case "wait":
                        LoadAsync().Wait();
                        break;
                    case "wait-timeout":
                        Record("wait-returned " + LoadAsync().Wait(TimeSpan.FromSeconds(1)));
                        break;
                    case "configureawait-all":
                        _ = LoadAllConfigureAwaitAsync().Result;
                        break;
                    case "configureawait-first-suspend":
                        _ = LoadFirstSuspendConfigureAwaitAsync().Result;
                        break;
                    case "configureawait-completed":
                        _ = LoadCachedFirstAsync().Result;
                        break;
                    case "configureawait-completed-all":
                        _ = LoadCachedFirstAllConfigureAwaitAsync().Result;
                        break;
                    case "configureawait-outer":
                        _ = LoadOuterAsync().Result;
                        break;
                    case "task-run":
                        _ = Task.Run(() =>
                        {
                            Record("pool-context " + (SynchronizationContext.Current?.GetType().Name ?? "null"));
                            return LoadAsync();
                        }).Result;
                        break;
                    case "task-run-invoke":
                        _ = Task.Run(() =>
                        {
                            Record("pool-context " + (SynchronizationContext.Current?.GetType().Name ?? "null"));
                            return LoadWithInvokeAsync();
                        }).Result;
                        break;
                    default:
                        throw new ArgumentException(_route);
                }

                Returned();
            }

            private static async void OnClickAwait(object sender, RoutedEventArgs e)
            {
                Begin();
                await LoadAsync();
                Returned();
            }
        }
        """;
}
