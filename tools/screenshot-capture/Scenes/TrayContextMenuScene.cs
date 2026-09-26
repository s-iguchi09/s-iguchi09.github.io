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
        "別のウィンドウが前面のとき、このプロセスから自分のウィンドウへ SetForegroundWindow を呼んだ戻り値と、前面が切り替わったか",
        "自分のウィンドウが既に前面のときの SetForegroundWindow の戻り値（前面を取れない状況では測れないと記録する）",
    ];

    public string Slug => "wpf-tray-contextmenu-close-on-focus-loss";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

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

            IntPtr before = GetForegroundWindow();
            if (before != own)
            {
                bool result = SetForegroundWindow(own);
                await Capture.SettleAsync(window);
                bool switched = GetForegroundWindow() == own;
                rows.Add(["another window in front: SetForegroundWindow(own)", $"returns {WpfProbe.Describe(result)}, foreground switched {WpfProbe.Describe(switched)}"]);
            }

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
