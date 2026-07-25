using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ScreenshotCapture;

/// <summary>
/// 実行中の WPF ウィンドウを PNG として保存するヘルパー。
/// タイトルバーを含む実際のウィンドウを記事の証跡として残すため、
/// <c>RenderTargetBitmap</c>（クライアント領域のみ）ではなく
/// Win32 の <c>PrintWindow</c> を使う。
/// </summary>
internal static class Capture
{
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>DWMWA_EXTENDED_FRAME_BOUNDS。影を除いた実際の枠を得る。</summary>
    private const int DwmwaExtendedFrameBounds = 9;

    // Windows 11 (build 22000 以降) のウィンドウ枠を明示的に指定する属性。
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaSystemBackdropType = 38;

    /// <summary>DWMSBT_NONE。Mica を無効化する。</summary>
    private const int DwmsbtNone = 1;

    /// <summary>
    /// タイトルバーと枠の色を固定する。
    /// 既定では Windows のアクセントカラーと Mica の背景が反映され、
    /// 撮影した環境によってスクリーンショットの色が変わってしまうため、
    /// 記事に載せる図では既定のニュートラルな配色へ揃える。
    /// COLORREF は 0x00BBGGRR の順であることに注意する。
    /// </summary>
    public static void ApplyNeutralChrome(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int backdrop = DwmsbtNone;
        DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));

        int caption = ToColorRef(0xFF, 0xFF, 0xFF);
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));

        int text = ToColorRef(0x1A, 0x1A, 0x1A);
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));

        int border = ToColorRef(0xD0, 0xD0, 0xD0);
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
    }

    private static int ToColorRef(byte r, byte g, byte b) => r | (g << 8) | (b << 16);

    /// <summary>
    /// ウィンドウを表示し、レンダリングが落ち着くまで待ってから返す。
    /// </summary>
    public static async Task ShowAndSettleAsync(Window window, int extraDelayMs = 400)
    {
        var loaded = new TaskCompletionSource();
        window.Loaded += (_, _) => loaded.TrySetResult();

        window.Show();
        ApplyNeutralChrome(window);
        window.Activate();

        await loaded.Task;
        await SettleAsync(window, extraDelayMs);
    }

    /// <summary>
    /// 表示済みのウィンドウについて、描画が落ち着くまで待つ。
    /// </summary>
    public static async Task SettleAsync(Window window, int extraDelayMs = 250)
    {
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(extraDelayMs);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// ウィンドウ全体（タイトルバー・枠を含む）を PNG として保存する。
    /// </summary>
    public static void SaveWindow(Window window, string path)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("ウィンドウハンドルが未生成のため取得できない。");
        }

        RECT bounds = GetCaptureBounds(hwnd);
        int width = bounds.Right - bounds.Left;
        int height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"ウィンドウ矩形が不正である: {width}x{height}");
        }

        // PrintWindow はウィンドウ矩形（影を含む GetWindowRect 基準）へ描画するため、
        // いったんそのサイズで受け取り、影を除いた枠だけを切り出す。
        GetWindowRect(hwnd, out RECT full);
        int fullWidth = full.Right - full.Left;
        int fullHeight = full.Bottom - full.Top;

        using var raw = new Bitmap(fullWidth, fullHeight, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(raw))
        {
            IntPtr hdc = g.GetHdc();
            try
            {
                if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
                {
                    throw new InvalidOperationException("PrintWindow に失敗した。");
                }
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        var crop = new Rectangle(
            bounds.Left - full.Left,
            bounds.Top - full.Top,
            width,
            height);
        crop.Intersect(new Rectangle(0, 0, fullWidth, fullHeight));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using Bitmap cropped = raw.Clone(crop, PixelFormat.Format32bppArgb);
        cropped.Save(path, ImageFormat.Png);
    }

    private static RECT GetCaptureBounds(IntPtr hwnd)
    {
        // DWM の拡張フレーム境界が取れる場合は、ドロップシャドウ分の余白を除いた矩形を使う。
        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out RECT frame, Marshal.SizeOf<RECT>()) == 0
            && frame.Right > frame.Left
            && frame.Bottom > frame.Top)
        {
            return frame;
        }

        GetWindowRect(hwnd, out RECT window);
        return window;
    }
}
