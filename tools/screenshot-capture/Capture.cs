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

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

    /// <summary>GW_HWNDPREV。Z オーダーで 1 つ上のウィンドウ。</summary>
    private const uint GwHwndPrev = 3;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int length);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    /// <summary>DWMWA_CLOAKED。別の仮想デスクトップにあるなど、DWM が隠しているウィンドウ。</summary>
    private const int DwmwaCloaked = 14;

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
    /// ウィンドウの枠の範囲を、画面に表示されているとおりに PNG として保存する。
    /// <see cref="SaveWindow"/>（PrintWindow）はウィンドウを 1 つずつしか描かないため、
    /// ほかのウィンドウとの重なりそのものを見せる図ではこちらを使う。重なっている別のウィンドウも写る。
    /// 画面が消灯していると中身が写らないため、クライアント領域が一色なら止める。
    /// 別のウィンドウの写り込みは撮影の前後で確かめるが、その間だけ現れたウィンドウは検出できないので、PNG は目で確かめてからコミットする。
    /// </summary>
    public static void SaveScreenArea(Window window, string path, IReadOnlyList<Window> allowed)
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

        // DWM の矩形は物理ピクセルで、CopyFromScreen はプロセスの DPI 対応で座標が変わりうる。
        // 座標系が一致することを確かめた表示倍率 100% のときだけ撮る。GetDpi はプロセスの DPI 対応によってはシステムの DPI を返すため、
        // 撮るウィンドウは主モニターに置く前提である。
        DpiScale dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        if (dpi.DpiScaleX != 1.0 || dpi.DpiScaleY != 1.0)
        {
            throw new InvalidOperationException($"画面の撮影は表示倍率 100% でだけ行う（現在 {dpi.PixelsPerDip * 100}%）。表だけを測るなら SCREENSHOT_SKIP_WINDOW_CAPTURE=1 を付けて実行する。");
        }

        // 撮る範囲に、撮ってよいウィンドウ（allowed）以外が重なっていないことを、撮影の前と後の両方で確かめる。
        // 通知やほかのアプリの Topmost のウィンドウが写り込んだ図をリポジトリに残さないため。
        var allowedHandles = allowed.Select(w => new WindowInteropHelper(w).Handle).ToHashSet();
        EnsureNothingElseAbove(hwnd, bounds, allowedHandles);

        using var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(image))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(width, height));
        }

        // 写した後にも同じ確認をして、写している間も重なったままのウィンドウを保存の前に止める。
        // ただし、1 回目の確認の後に現れて 2 回目の確認の前に消えたウィンドウ（短い通知など）は検出できない。
        // この確認が防ぐのは「確かめた時点で重なっているもの」までで、保存した PNG はコミットの前に必ず目で確かめる。
        EnsureNothingElseAbove(hwnd, bounds, allowedHandles);

        EnsureContentRendered(image, ClientAreaIn(hwnd, bounds, image.Size));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        image.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// <paramref name="bottom"/> より Z オーダーで上にある、表示中（非表示でも、DWM で隠された状態でもない）のトップレベルのウィンドウを
    /// すべてたどり、<paramref name="allowed"/> 以外で <paramref name="bounds"/> と重なるものがあれば例外にする。
    /// 画面のうち bottom の範囲に見えるのは、bottom 自身とそれより上のウィンドウだけなので、標本点ではなく列挙で確かめる。
    /// （DWM の枠の角の丸みの外側の数ピクセルは、bottom より下が見える。）
    /// </summary>
    private static void EnsureNothingElseAbove(IntPtr bottom, RECT bounds, HashSet<IntPtr> allowed)
    {
        // EnumWindows でトップレベルのウィンドウを列挙し、bottom に着いたら止める。列挙が Z オーダーの上から順になることは
        // ドキュメントには無く、この環境で確かめた挙動である。そのため、bottom より後に列挙されたウィンドウも、
        // GW_HWNDPREV でたどって bottom より上にあれば調べる対象に入れる（順序が違っても見逃さない側に倒す）。
        // EnumWindows は列挙の時点で一覧を作る。GW_HWNDPREV でたどる部分は、同じハンドルに戻ったら止める。
        var above = new List<IntPtr>();
        bool reachedBottom = false;
        var after = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            if (h == bottom)
            {
                reachedBottom = true;
            }
            else if (reachedBottom)
            {
                after.Add(h);
            }
            else
            {
                above.Add(h);
            }

            return true;
        }, IntPtr.Zero);

        // bottom より後に列挙されたものでも、実際に bottom より上にあれば調べる（GW_HWNDPREV で bottom から上をたどって確かめる）。
        var actuallyAbove = new HashSet<IntPtr>();
        for (IntPtr h = GetWindow(bottom, GwHwndPrev); h != IntPtr.Zero; h = GetWindow(h, GwHwndPrev))
        {
            // たどる途中で Z オーダーが変わって同じハンドルに戻ったら止める（巡回で終わらなくならないため）。
            if (!actuallyAbove.Add(h))
            {
                break;
            }
        }

        above.AddRange(after.Where(actuallyAbove.Contains));

        if (!reachedBottom)
        {
            throw new InvalidOperationException("撮るウィンドウがトップレベルの列挙に見つからない。撮影を止めた。");
        }

        foreach (IntPtr h in above)
        {
            if (allowed.Contains(h) || !IsWindowVisible(h))
            {
                continue;
            }

            if (DwmGetWindowAttribute(h, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                continue;
            }

            RECT other = GetCaptureBounds(h);
            bool intersects = other.Left < bounds.Right && bounds.Left < other.Right && other.Top < bounds.Bottom && bounds.Top < other.Bottom;
            if (intersects)
            {
                var name = new System.Text.StringBuilder(256);
                GetClassName(h, name, name.Capacity);
                throw new InvalidOperationException($"撮る範囲に、撮ってよいウィンドウ以外（クラス {name}）が重なっている。撮影を止めた。");
            }
        }
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
