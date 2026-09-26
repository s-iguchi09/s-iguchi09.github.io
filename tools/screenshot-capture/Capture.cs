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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

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

    /// <summary>Loaded を待つ上限。これを超えたら中断する。</summary>
    private static readonly TimeSpan LoadedTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// ウィンドウを表示し、レンダリングが落ち着くまで待ってから返す。
    /// </summary>
    /// <exception cref="TimeoutException">
    /// <see cref="FrameworkElement.Loaded"/> が <see cref="LoadedTimeout"/> 以内に発火しない場合。
    /// 待ち続けてバッチ全体が止まらないように上限を設けている。
    /// </exception>
    public static async Task ShowAndSettleAsync(Window window, int extraDelayMs = 400)
    {
        var loaded = new TaskCompletionSource();
        window.Loaded += (_, _) => loaded.TrySetResult();

        window.Show();
        ApplyNeutralChrome(window);
        window.Activate();

        Task completed = await Task.WhenAny(loaded.Task, Task.Delay(LoadedTimeout));
        if (completed != loaded.Task)
        {
            throw new TimeoutException(
                $"ウィンドウ '{window.Title}' の Loaded が {LoadedTimeout.TotalSeconds} 秒以内に発火しなかった。");
        }

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
    /// <paramref name="requireContentRendered"/> が真なら、クライアント領域が一色のときに保存せず例外にする
    /// （画面の消灯中などに空白の図で上書きしないため）。中身が一色の図を意図して撮るときだけ偽にする。
    /// </summary>
    public static void SaveWindow(Window window, string path, bool requireContentRendered = true)
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
        if (requireContentRendered)
        {
            EnsureContentRendered(cropped, ClientAreaIn(hwnd, bounds, cropped.Size));
        }

        cropped.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// クライアント領域の位置を、切り出した画像の座標で返す。
    /// タイトルバーと枠の大きさは表示スケールで変わるため、固定値ではなく実際の座標から求める。
    /// </summary>
    private static Rectangle ClientAreaIn(IntPtr hwnd, RECT bounds, System.Drawing.Size imageSize)
    {
        var origin = new POINT();
        if (!GetClientRect(hwnd, out RECT client) || !ClientToScreen(hwnd, ref origin))
        {
            throw new InvalidOperationException("クライアント領域の位置を取得できない。");
        }

        var area = new Rectangle(origin.X - bounds.Left, origin.Y - bounds.Top, client.Right - client.Left, client.Bottom - client.Top);
        area.Intersect(new Rectangle(System.Drawing.Point.Empty, imageSize));
        return area;
    }

    /// <summary>
    /// 画面がロックされているか消灯していると、PrintWindow はタイトルバーだけを描き、中身を一色で返す。
    /// そのまま保存すると空白の図で既存の図を上書きするため、クライアント領域が一色なら止める。
    /// </summary>
    private static void EnsureContentRendered(Bitmap image, Rectangle client)
    {
        // クライアント領域の縁は背景だけのことが多いため、内側を見る。
        // 1 ピクセル幅の線だけの図も見落とさないよう、間引かずにすべての画素を調べる。
        const int margin = 2;
        client.Inflate(-margin, -margin);
        if (client.Width <= 0 || client.Height <= 0)
        {
            return;
        }

        Color first = image.GetPixel(client.Left, client.Top);
        for (int y = client.Top; y < client.Bottom; y++)
        {
            for (int x = client.Left; x < client.Right; x++)
            {
                if (image.GetPixel(x, y) != first)
                {
                    return;
                }
            }
        }

        throw new InvalidOperationException(
            "ウィンドウのクライアント領域が一色で撮れた。画面がロックされているか消灯している可能性がある。画面を表示した状態で撮り直す。" +
            "一色の図を意図して撮る場合は requireContentRendered: false を指定する。");
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
