using System.Diagnostics;
using System.Text;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// アプリ紹介ページ「WPF MessageBox Sample Creator」（apps/wpf-messagebox-sample-creator.md と日本語版）の記述を実測する。
///
/// ツールは .NET 8 用と .NET 10 用のビルドを配布しており、選択肢は実行しているランタイムの列挙型から作られる。
/// このツール自身は .NET 10 で動くため、.NET 8 でも測れるよう、両方を対象にした一時プロジェクトを作って実行する。
///
/// 既定ボタンは、実際に MessageBox を表示し、ダイアログのボタンのうち BS_DEFPUSHBUTTON を持つものを読む。
/// ボタンの文字は OS の言語で変わるため、コントロール ID（IDOK・IDYES など）で判定する。
/// 入力は一切送らない。ダイアログは先頭のボタンへ BM_CLICK を送って閉じる（YesNo は Esc で閉じられないため）。
/// </summary>
internal sealed class MessageBoxSampleCreatorScene : IScene
{
    private static readonly string[] Targets = ["net8.0-windows", "net10.0-windows"];

    public string Slug => "wpf-messagebox-sample-creator";

    public string ImageDirectory => Path.Combine("images", "wpf-messagebox-sample-creator", "verification");

    public IReadOnlyList<string> Verifies =>
    [
        ".NET 8 と .NET 10 での MessageBoxButton・MessageBoxResult・MessageBoxOptions のメンバー（ツールの選択肢は Enum.GetValues から作られる）",
        "Enum.GetValues(typeof(MessageBoxImage)) を文字列にしたときに並ぶ名前（同じ値の別名がどう表示されるか）",
        "すべてのボタン構成とすべての MessageBoxResult の組み合わせで MessageBox を表示したときに、既定になるボタン（.NET 8 と .NET 10）",
        "表示されていないボタンを既定の結果に指定したときに、先頭のボタンが既定になるか",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        var enums = new List<IReadOnlyList<string>>();
        var defaults = new List<IReadOnlyList<string>>();

        foreach (string target in Targets)
        {
            Dictionary<string, List<string>> result = await RunProbeAsync(target);
            string runtime = result["runtime"][0];

            enums.Add([$"{runtime}: MessageBoxButton", string.Join(", ", result["button"])]);
            enums.Add([$"{runtime}: MessageBoxResult", string.Join(", ", result["result"])]);
            enums.Add([$"{runtime}: MessageBoxOptions", string.Join(", ", result["options"])]);
            enums.Add([$"{runtime}: Enum.GetValues(MessageBoxImage) as text", string.Join(", ", result["image"])]);

            int combinations = 0, firstWhenAbsent = 0, absent = 0;
            foreach (string line in result["default"])
            {
                // button \t shown buttons \t result=default ...
                string[] parts = line.Split('\t');
                string button = parts[0];
                string[] shown = parts[1].Split(',');
                var others = new List<string>();
                foreach (string pair in parts.Skip(2))
                {
                    string[] kv = pair.Split('=');
                    combinations++;
                    if (!shown.Contains(kv[0]))
                    {
                        absent++;
                        if (kv[1] == shown[0])
                        {
                            firstWhenAbsent++;
                        }
                        else
                        {
                            others.Add($"{kv[0]} gave {kv[1]}");
                        }
                    }
                    else if (kv[0] != "None")
                    {
                        others.Add($"{kv[0]} → {kv[1]}");
                    }
                }

                defaults.Add([$"{runtime}: {button} ({string.Join(", ", shown)})", string.Join("; ", others)]);
            }

            defaults.Add(
            [
                $"{runtime}: None or a result not in the set → first button",
                $"{firstWhenAbsent} of {absent} (of {combinations} dialogs shown)",
            ]);
        }

        await context.SaveTableAsync(
            "MessageBox enums on .NET 8 and .NET 10",
            ["case", "measured"],
            enums,
            "messagebox-enums.svg");

        await context.SaveTableAsync(
            "MessageBox default button for each button set and defaultResult",
            ["button set (buttons shown)", "defaultResult → default button"],
            defaults,
            "messagebox-default-button.svg");
    }

    /// <summary>
    /// 一時プロジェクトを指定の TFM でビルドして実行し、「キー \t 値」の行をキーごとにまとめて返す。
    /// </summary>
    private static async Task<Dictionary<string, List<string>>> RunProbeAsync(string targetFramework)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "messagebox-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            // BOM を付ける。付けないと、コンパイラが既定のコードページで読み、日本語コメントが壊れる。
            var utf8 = new UTF8Encoding(true);
            await File.WriteAllTextAsync(Path.Combine(workspace, "Probe.cs"), ProbeSource, utf8);
            await File.WriteAllTextAsync(
                Path.Combine(workspace, "probe.csproj"),
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{targetFramework}</TargetFramework>
                    <UseWPF>true</UseWPF>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <!-- 既定の RollForward（Minor）のまま、TFM と同じメジャー版のランタイムで動かす。 -->
                  </PropertyGroup>
                </Project>
                """,
                utf8);

            (int exitCode, string output) = await RunAsync("dotnet", "run -c Release -v q --nologo", workspace);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"{targetFramework} のビルドまたは実行が失敗した。{Environment.NewLine}{output}");
            }

            var result = new Dictionary<string, List<string>>();
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                int tab = line.IndexOf('\t');
                if (tab < 0)
                {
                    continue;
                }

                string key = line[..tab];
                if (!result.TryGetValue(key, out List<string>? list))
                {
                    result[key] = list = [];
                }

                list.Add(line[(tab + 1)..]);
            }

            foreach (string key in new[] { "runtime", "button", "result", "options", "image", "default" })
            {
                if (!result.ContainsKey(key))
                {
                    throw new InvalidOperationException($"{targetFramework} の出力に {key} が無い。{Environment.NewLine}{output}");
                }
            }

            // 列挙の値は 1 行にまとめて出しているため、カンマで分けておく。
            foreach (string key in new[] { "button", "result", "options", "image" })
            {
                result[key] = result[key][0].Split(',').ToList();
            }

            return result;
        }
        finally
        {
            try { Directory.Delete(workspace, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{fileName} を起動できない。");

        // 両方の読み取りを先に始める。片方だけ読むと、もう片方のパイプが埋まって子プロセスが止まる。
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        // ダイアログが閉じられないなど、一時プロジェクトが終わらない場合に呼び出し元まで止まらないよう、時間を区切る。
        // ビルドと 90 回ほどのダイアログ表示は 1〜2 分で終わるため、10 分を上限にする。
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} {arguments} が 10 分以内に終わらなかったため止めた。");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        return (process.ExitCode, stdout + stderr);
    }

    /// <summary>
    /// 一時プロジェクトで実行するコード。ツール本体と同じく MessageBox.Show の 5 引数版（所有者なし）を呼ぶ。
    /// </summary>
    private const string ProbeSource = """
        using System.Runtime.InteropServices;
        using System.Text;
        using System.Windows;

        static class Probe
        {
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string title);
            [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr lParam);
            delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
            [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
            [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr h);
            [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
            [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);

            // MessageBox のボタンのコントロール ID。文字は OS の言語で変わるため、ID で見分ける。
            static readonly Dictionary<int, string> Ids = new()
            {
                [1] = "OK", [2] = "Cancel", [3] = "Abort", [4] = "Retry", [5] = "Ignore",
                [6] = "Yes", [7] = "No", [10] = "TryAgain", [11] = "Continue",
            };

            [STAThread]
            static void Main()
            {
                Console.OutputEncoding = new UTF8Encoding(false);
                Console.WriteLine($"runtime\t.NET {Environment.Version}");
                Console.WriteLine("button\t" + string.Join(",", Enum.GetNames<MessageBoxButton>()));
                Console.WriteLine("result\t" + string.Join(",", Enum.GetNames<MessageBoxResult>()));
                Console.WriteLine("options\t" + string.Join(",", Enum.GetNames<MessageBoxOptions>()));
                // ツールが ObjectDataProvider で Enum.GetValues をバインドした場合に ComboBox に出る文字列。
                Console.WriteLine("image\t" + string.Join(",", Enum.GetValues(typeof(MessageBoxImage)).Cast<object>().Select(v => v.ToString())));

                foreach (var button in Enum.GetValues<MessageBoxButton>())
                {
                    string shown = "";
                    var line = new StringBuilder();
                    foreach (var result in Enum.GetValues<MessageBoxResult>())
                    {
                        string caption = $"messagebox-probe {button} {result}";
                        string found = "?";
                        var watcher = new Thread(() =>
                        {
                            IntPtr dialog = IntPtr.Zero;
                            for (int i = 0; i < 200 && dialog == IntPtr.Zero; i++) { Thread.Sleep(25); dialog = FindWindow("#32770", caption); }
                            // ダイアログを見つけられないと、メインスレッドの MessageBox.Show を閉じる手段が無い。
                            // 待ち続けないよう、失敗の終了コードでプロセスごと終わらせる（呼び出し元が失敗として扱う）。
                            if (dialog == IntPtr.Zero) { Console.Error.WriteLine($"dialog not found: {caption}"); Environment.Exit(2); }
                            Thread.Sleep(100);
                            var buttons = new List<(IntPtr Handle, string Name, bool IsDefault)>();
                            EnumChildWindows(dialog, (h, _) =>
                            {
                                var cls = new StringBuilder(64);
                                GetClassName(h, cls, 64);
                                if (cls.ToString() == "Button")
                                {
                                    int id = GetDlgCtrlID(h);
                                    bool isDefault = (GetWindowLong(h, -16) & 0xF) == 1; // BS_DEFPUSHBUTTON
                                    buttons.Add((h, Ids.TryGetValue(id, out string? name) ? name : "id" + id, isDefault));
                                }
                                return true;
                            }, IntPtr.Zero);
                            // OK だけのダイアログでは、OK ボタンのコントロール ID が 2（IDCANCEL）だった。
                            // ボタンが 1 つのときは OK と読み替える。
                            if (buttons.Count == 1 && buttons[0].Name == "Cancel")
                            {
                                buttons[0] = (buttons[0].Handle, "OK", buttons[0].IsDefault);
                            }
                            if (buttons.Count == 0) { Console.Error.WriteLine($"no buttons in the dialog: {caption}"); Environment.Exit(3); }
                            shown = string.Join(",", buttons.Select(b => b.Name));
                            var defaults = buttons.Where(b => b.IsDefault).Select(b => b.Name).ToList();
                            found = defaults.Count == 1 ? defaults[0] : "defaults:" + string.Join("+", defaults);
                            for (int i = 0; i < 40 && IsWindow(dialog); i++) { PostMessage(buttons[0].Handle, 0x00F5 /* BM_CLICK */, IntPtr.Zero, IntPtr.Zero); Thread.Sleep(50); }
                            if (IsWindow(dialog)) { Console.Error.WriteLine($"the dialog did not close: {caption}"); Environment.Exit(4); }
                        }) { IsBackground = true };
                        watcher.Start();
                        MessageBox.Show("probe", caption, button, MessageBoxImage.None, result);
                        watcher.Join();
                        line.Append($"\t{result}={found}");
                    }
                    Console.WriteLine($"default\t{button}\t{shown}{line}");
                }
            }
        }
        """;
}
