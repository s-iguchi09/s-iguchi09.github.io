using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 付録サンプルのうち、UI スレッドが固まる・プロセスが終わらない挙動を、別プロセスで動かして測る。
///
/// 親（各章のシーン）は <see cref="RunAsync"/> で自分自身の実行ファイルを <see cref="Flag"/> 付きで起動し、
/// 子は <see cref="Run"/> でシナリオを実行して、結果を「キー=値」の行で標準出力に書く。
/// 固まったまま戻らない子は監視スレッドが判定を書いて Environment.Exit で終わらせ、それでも終わらなければ親が強制終了する。
/// 記事の元の計測（TaskResultDeadlockScene・ShutdownLifetimeScene）も、同じ理由で別プロセスで測っている。
/// </summary>
internal static class BookAChildProcess
{
    public const string Flag = "--book-a-child";

    // ---------------------------------------------------------------- 親の側

    /// <summary>子を起動し、標準出力の「キー=値」と、起動から終了までの時間を返す。</summary>
    public static async Task<(Dictionary<string, string> Values, TimeSpan Elapsed, bool Exited)> RunAsync(string scenario, TimeSpan limit)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!, $"{Flag} {scenario}")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var values = new Dictionary<string, string>();
        Stopwatch watch = Stopwatch.StartNew();
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("子プロセスを起動できない。");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(limit);
        bool exited = true;
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            exited = false;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        TimeSpan elapsed = watch.Elapsed;
        foreach (string line in (await output).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equals = line.IndexOf('=');
            if (equals > 0)
            {
                values[line[..equals]] = line[(equals + 1)..];
            }
        }

        return (values, elapsed, exited);
    }

    // ---------------------------------------------------------------- 子の側

    public static int Run(string scenario) => scenario switch
    {
        "ch11-repro" => Ch11(new Ch11.TaskResultDeadlock.ReproView { DataContext = new Ch11.TaskResultDeadlock.ReproLoadViewModel() },
            vm => ((Ch11.TaskResultDeadlock.ReproLoadViewModel)vm).Status),
        "ch11-fixed" => Ch11(new Ch11.TaskResultDeadlock.FixedView { DataContext = new Ch11.TaskResultDeadlock.LoadViewModel() },
            vm => ((Ch11.TaskResultDeadlock.LoadViewModel)vm).Status),
        "ch12-repro" => Ch12(new Ch12.ProcessNotExiting.ReproView { DataContext = new Ch12.ProcessNotExiting.ReproMonitorViewModel() }),
        "ch12-fixed" => Ch12(new Ch12.ProcessNotExiting.FixedView { DataContext = new Ch12.ProcessNotExiting.MonitorViewModel() }),
        _ => throw new ArgumentException($"未知のシナリオ: {scenario}", nameof(scenario)),
    };

    private static void Write(string key, string value)
    {
        Console.Out.WriteLine($"{key}={value}");
        Console.Out.Flush();
    }

    /// <summary>
    /// 第 11 章。「読み込む」のコマンドを UI スレッドで実行し、200 ms 後に別の処理を UI スレッドへ積む。
    /// 2.5 秒後に、積んだ処理が走ったか（UI スレッドが止まっていないか）と、読み込みの結果が出たかを書いて終わる。
    /// </summary>
    private static int Ch11(UserControl view, Func<object, string> readStatus)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new Window { Content = view, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false };
        object vm = view.DataContext;
        app.Startup += (_, _) =>
        {
            window.Show();
            Dispatcher dispatcher = app.Dispatcher;
            var button = (Button)view.FindName("loadButton");
            Stopwatch watch = Stopwatch.StartNew();

            // ボタンを押したときと同じく、バインドしたコマンドを UI スレッドで実行する。
            dispatcher.BeginInvoke(() => button.Command.Execute(null));

            new Thread(() =>
            {
                Thread.Sleep(200);
                string statusWhenMarkerRan = "";
                long markerMs = -1;
                DispatcherOperation marker = dispatcher.BeginInvoke(() =>
                {
                    markerMs = watch.ElapsedMilliseconds;
                    statusWhenMarkerRan = readStatus(vm);
                });
                Thread.Sleep(2300);
                Write("marker", marker.Status.ToString());
                Write("markerMs", markerMs.ToString());
                Write("statusWhenMarkerRan", statusWhenMarkerRan);
                Write("status", readStatus(vm));
                Environment.Exit(0);
            }) { IsBackground = true }.Start();
        };

        app.Run();
        return 0;
    }

    /// <summary>
    /// 第 12 章。記事の計測と同じく、起動直後に監視を始め、起動から 2 秒後にウィンドウを閉じる（ShutdownMode は既定の OnLastWindowClose）。
    /// Application.Exit の発生と Run の戻りを書く。プロセスがいつ終わるかは親が測る。
    /// </summary>
    private static int Ch12(UserControl view)
    {
        var app = new Application();
        var window = new Window { Content = view, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false };
        app.Exit += (_, _) => Write("exit", "発生する");
        app.Startup += (_, _) =>
        {
            window.Show();
            var button = (Button)view.FindName("startButton");
            button.Command.Execute(null);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.Close();
            };
            timer.Start();
        };

        app.Run();
        Write("runReturned", "戻る");
        return 0;
    }
}
