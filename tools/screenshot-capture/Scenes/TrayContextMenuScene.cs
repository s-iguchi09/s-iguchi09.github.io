using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF のタスクトレイの ContextMenu がフォーカスを失っても閉じない問題」の実測。
///
/// トレイアイコンへの実際のクリックと、別プロセスとの間の前面の移り変わりは自動化できないため、
/// ここで測るのは記事の表にある 2 点だけである。
/// 1. ContextMenu.StaysOpen の既定値（メタデータと、new した直後の値）。
/// 2. このプロセスから SetForegroundWindow を呼んだときの戻り値と、前面が実際に切り替わったか。
///    前面に別のウィンドウがある状態と、自分のウィンドウが既に前面の状態では結果が違う。
///    後者は、前面を取れない状況（画面の消灯中など）では測れないため、そのときは測れなかったと記録する。
/// </summary>
internal sealed class TrayContextMenuScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "ContextMenu.StaysOpen の既定値（依存関係プロパティのメタデータと、new した直後の値）",
        "Popup.StaysOpen の既定値（比較のため）",
        "別のプロセスが前面のとき（補助の PowerShell のフォームに前面を渡す）、このプロセスから自分のウィンドウへ SetForegroundWindow を呼んだ戻り値と、前面が切り替わったか",
        "自分のウィンドウが既に前面のときの SetForegroundWindow の戻り値（前面を取れない状況では測れないと記録する）",
    ];

    public string Slug => "wpf-tray-contextmenu-close-on-focus-loss";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// 前面を取る別のプロセス。起動したプロセスが前面にあれば、起動されたプロセスは前面を取れる。
    /// 小さなフォームを最前面に出して待つだけの PowerShell を使う。
    /// </summary>
    private const string ForegroundHelper =
        "Add-Type -AssemblyName System.Windows.Forms; " +
        "$f = New-Object System.Windows.Forms.Form; $f.Text = 'foreground helper'; $f.Width = 240; $f.Height = 120; $f.TopMost = $true; " +
        "$f.Add_Shown({ $f.Activate() }); [System.Windows.Forms.Application]::Run($f)";

    /// <summary>
    /// 別のプロセスに前面を渡してから、このプロセスの自分のウィンドウへ SetForegroundWindow を呼ぶ。
    /// 補助のプロセスが前面を取れなかったときは、測れなかったと返す。
    /// </summary>
    private static async Task<string> WhileAnotherProcessInFrontAsync(Window window, IntPtr own)
    {
        using var helper = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "powershell.exe", $"-NoProfile -NonInteractive -Command \"{ForegroundHelper}\"")
        {
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("補助のプロセスを起動できない。");
        try
        {
            bool helperInFront = false;
            for (int i = 0; i < 100 && !helperInFront; i++)
            {
                await Task.Delay(100);
                GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
                helperInFront = pid == helper.Id;
            }

            if (!helperInFront)
            {
                return "not measured in this run (the helper process could not take the foreground)";
            }

            bool result = SetForegroundWindow(own);
            await Capture.SettleAsync(window);
            return $"returns {WpfProbe.Describe(result)}, foreground switched {WpfProbe.Describe(GetForegroundWindow() == own)}";
        }
        finally
        {
            if (!helper.HasExited)
            {
                // Kill は終了を待たない。TopMost の補助フォームが残って次の撮影に写らないよう、終わるまで待つ。
                helper.Kill(entireProcessTree: true);
                helper.WaitForExit();
            }
        }
    }

    public async Task CaptureAsync(SceneContext context)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "ContextMenu.StaysOpen metadata default", WpfProbe.Describe(ContextMenu.StaysOpenProperty.GetMetadata(typeof(ContextMenu)).DefaultValue) },
            new[] { "new ContextMenu().StaysOpen", WpfProbe.Describe(new ContextMenu().StaysOpen) },
            new[] { "Popup.StaysOpen metadata default", WpfProbe.Describe(Popup.StaysOpenProperty.GetMetadata(typeof(Popup)).DefaultValue) },
        };

        var window = new Window { Title = "SetForegroundWindow probe", Width = 240, Height = 100, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            await Capture.ShowAndSettleAsync(window);
            IntPtr own = new WindowInteropHelper(window).Handle;

            // 画面が使えれば、表示した直後の自分のウィンドウは前面にある。消灯中などで前面を取れなければ測れない。
            if (GetForegroundWindow() == own)
            {
                bool result = SetForegroundWindow(own);
                await Capture.SettleAsync(window);
                rows.Add(["own window already in front: SetForegroundWindow(own)", $"returns {WpfProbe.Describe(result)}, still in front {WpfProbe.Describe(GetForegroundWindow() == own)}"]);
            }
            else
            {
                rows.Add(["own window already in front: SetForegroundWindow(own)", "not measured in this run (the window could not take the foreground)"]);
            }

            rows.Add(["another process in front: SetForegroundWindow(own)", await WhileAnotherProcessInFrontAsync(window, own)]);
        }
        finally
        {
            window.Close();
        }

        await context.SaveTableAsync(
            "ContextMenu.StaysOpen and SetForegroundWindow from this process",
            ["case", "measured"],
            rows,
            "tray-contextmenu-facts.svg");
    }
}
