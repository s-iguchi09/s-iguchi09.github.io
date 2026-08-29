using System.Diagnostics;
using System.Text;
using System.Windows;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと
/// ShutdownMode・フォアグラウンドスレッドの扱い」の図。
///
/// 条件だけを変えた検証用アプリを実際に起動し、<c>Application.Exit</c> の発生・
/// <c>Run()</c> の戻り・プロセスの生存時間を測る。
/// 「プロセスが終了するか」は動かさないと分からないため、撮影のたびに測り直す。
/// </summary>
internal sealed class ShutdownLifetimeScene : IScene
{
    /// <summary>これを超えて生き残ったプロセスは、終了しないものとして扱う。</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(9);

    public IReadOnlyList<string> Verifies =>
    [
        "条件ごとに Application.Exit の発生・Run() の戻り・プロセスの生存時間を測る",
        "ウィンドウを生成しない場合と閉じないウィンドウがある場合に Application.Exit が発生しないこと",
        "既定の new Thread がプロセスを引き止め、IsBackground=true では引き止めないこと",
        "2 つ目の UI スレッドは InvokeShutdown を呼ばない限りプロセスを残すこと",
    ];

    public string Slug => "wpf-application-not-exiting-shutdownmode-threads";

    /// <summary>表に載せる条件。<c>Key</c> は検証用アプリへ渡す引数。</summary>
    private static readonly (string Key, string Label)[] Conditions =
    [
        ("control", "visible window only"),
        ("no-window", "no window at all"),
        ("unclosed-window", "Window created, never closed"),
        ("foreground-thread", "new Thread(...)"),
        ("background-thread", "+ IsBackground = true"),
        ("task-run", "Task.Run(...)"),
        ("second-ui-thread", "2nd UI thread + Dispatcher.Run()"),
        ("second-ui-thread-shutdown", "+ InvokeShutdown() on Exit"),
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "shutdown-lifetime-scene");
        Directory.CreateDirectory(workspace);
        string executable = await BuildProbeAsync(workspace);

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string key, string label) in Conditions)
        {
            Measurement result = await MeasureAsync(executable, workspace, key);
            rows.Add([label, result.ExitRaised, result.RunReturned, result.Lifetime]);
        }

        await context.SaveTableAsync(
            "process lifetime by condition",
            ["", "Application.Exit", "Run() returns", "process ends"],
            rows,
            "shutdown-lifetime-matrix.svg");
    }

    private readonly record struct Measurement(string ExitRaised, string RunReturned, string Lifetime);

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

        string output = await build.StandardOutput.ReadToEndAsync();
        output += await build.StandardError.ReadToEndAsync();
        await build.WaitForExitAsync();

        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException("検証用アプリのビルドに失敗した:\n" + output);
        }

        return Path.Combine(workspace, "bin", "Release", "net10.0-windows", "ShutdownProbe.exe");
    }

    /// <summary>
    /// 条件を 1 つ実行し、各イベントの発生とプロセスの生存時間を測る。
    /// </summary>
    private static async Task<Measurement> MeasureAsync(string executable, string workspace, string condition)
    {
        string log = Path.Combine(workspace, $"log-{condition}.txt");

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(new ProcessStartInfo(executable, $"{condition} \"{log}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = false,
        }) ?? throw new InvalidOperationException("検証用アプリを起動できない。");

        using var cancellation = new CancellationTokenSource(Timeout);
        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            process.Kill(entireProcessTree: true);
        }

        stopwatch.Stop();

        string content = File.Exists(log) ? await File.ReadAllTextAsync(log) : string.Empty;
        return new Measurement(
            content.Contains("Application.Exit", StringComparison.Ordinal) ? "raised" : "never",
            content.Contains("Run() returned", StringComparison.Ordinal) ? "returns" : "never",
            timedOut ? "never" : $"{stopwatch.Elapsed.TotalSeconds:F1} s");
    }

    private const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net10.0-windows</TargetFramework>
            <UseWPF>true</UseWPF>
            <Nullable>disable</Nullable>
            <LangVersion>latest</LangVersion>
            <AssemblyName>ShutdownProbe</AssemblyName>
            <StartupObject>ShutdownProbe.Program</StartupObject>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
          </PropertyGroup>
          <ItemGroup><Compile Include="Program.cs" /></ItemGroup>
        </Project>
        """;

    /// <summary>
    /// 検証用アプリの本体。記事に載せた構成をそのまま実行できる形にしたものである。
    /// ウィンドウは起動 2 秒後に自動で閉じ、バックグラウンド処理は 6 秒のスリープに置き換える。
    /// </summary>
    private const string ProbeSource = """
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Threading;
        using System.Windows;
        using System.Windows.Threading;

        namespace ShutdownProbe;

        internal static class Program
        {
            private static string _log;
            private static readonly Stopwatch Clock = Stopwatch.StartNew();

            private static void Record(string label)
                => File.AppendAllText(_log, $"{label}\t{Clock.Elapsed.TotalMilliseconds:F0}\n");

            [STAThread]
            private static int Main(string[] args)
            {
                string condition = args.Length > 0 ? args[0] : "control";
                _log = args.Length > 1 ? args[1] : "shutdown-probe.txt";
                File.WriteAllText(_log, "");

                var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
                app.Exit += (_, _) => Record("Application.Exit");

                Dispatcher second = null;

                switch (condition)
                {
                    case "control":
                        ShowAndAutoClose();
                        break;
                    case "no-window":
                        break;
                    case "unclosed-window":
                        _ = new Window { Title = "kept", Width = 200, Height = 150 };
                        ShowAndAutoClose();
                        break;
                    case "foreground-thread":
                        new Thread(() => Thread.Sleep(6000)) { IsBackground = false }.Start();
                        ShowAndAutoClose();
                        break;
                    case "background-thread":
                        new Thread(() => Thread.Sleep(6000)) { IsBackground = true }.Start();
                        ShowAndAutoClose();
                        break;
                    case "task-run":
                        System.Threading.Tasks.Task.Run(() => Thread.Sleep(6000));
                        ShowAndAutoClose();
                        break;
                    case "second-ui-thread":
                        second = StartSecondUiThread();
                        ShowAndAutoClose();
                        break;
                    case "second-ui-thread-shutdown":
                        second = StartSecondUiThread();
                        app.Exit += (_, _) => second?.InvokeShutdown();
                        ShowAndAutoClose();
                        break;
                }

                int code = app.Run();
                Record("Run() returned");
                return code;
            }

            private static void ShowAndAutoClose()
            {
                var window = new Window { Title = "main", Width = 240, Height = 160, Left = 40, Top = 40 };
                window.Show();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) => { timer.Stop(); window.Close(); };
                timer.Start();
            }

            private static Dispatcher StartSecondUiThread()
            {
                var ready = new ManualResetEventSlim();
                Dispatcher dispatcher = null;

                var thread = new Thread(() =>
                {
                    dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = false;
                thread.Start();

                ready.Wait();
                return dispatcher;
            }
        }
        """;
}
