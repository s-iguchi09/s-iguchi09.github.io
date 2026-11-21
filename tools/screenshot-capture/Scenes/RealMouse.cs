using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 実際のマウスを動かし、ボタンを押す・離す。
///
/// ClickMode の Release と Hover は、マウスのキャプチャと実際のボタンの状態・カーソル位置を読むため、
/// ルーティングイベントを発生させるだけでは再現できない。そのため OS のマウス入力を使う。
/// カーソルを動かす前と、ボタンを押す直前に、カーソルの下が計測用のウィンドウであることを確かめる（ほかのアプリをクリックしないため）。
/// 押す直前にも確かめるのは、移動後に待つ間に別のウィンドウ（通知など）が前面に出ることがあるためである。
/// ポップアップ（サブメニューなど）の中の要素は、その要素を表示している HWND（計測用のウィンドウから開いたポップアップ）を確かめる。
/// カーソルの位置は <see cref="Preserve"/> で元に戻す。
/// </summary>
internal static class RealMouse
{
    private const uint InputMouse = 0;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint AncestorRoot = 2;

    /// <summary>押したまま例外で抜けたときに、<see cref="Preserve"/> の破棄で離すための状態。</summary>
    private static bool s_leftDown;

    /// <summary><see cref="DoubleClickAsync"/> で右ボタンを離せずに抜けたときに、<see cref="Preserve"/> の破棄で離すための状態。</summary>
    private static bool s_rightDown;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int size);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    /// <summary>破棄するとカーソルを元の位置へ戻す（ボタンを押したままなら離す）。</summary>
    public static IDisposable Preserve()
    {
        GetCursorPos(out NativePoint original);
        return new Restore(original);
    }

    /// <summary>
    /// 要素の中心（または <paramref name="offset"/> の位置）へカーソルを動かす。
    /// ポップアップの中の要素は <see cref="Window.GetWindow"/> で窓を得られないため、<paramref name="window"/> に計測用のウィンドウを渡す。
    /// </summary>
    public static async Task MoveToAsync(FrameworkElement element, Point? offset = null, Window? window = null)
    {
        Point screen = element.PointToScreen(offset ?? new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        var point = new NativePoint { X = (int)Math.Round(screen.X), Y = (int)Math.Round(screen.Y) };

        window ??= Window.GetWindow(element)
            ?? throw new InvalidOperationException("ポップアップの中の要素には、計測用のウィンドウを渡す。");
        EnsureOver(HandleOf(element), point);
        SetCursorPos(point.X, point.Y);
        Send(MouseEventMove);
        await Capture.SettleAsync(window, 100);
    }

    /// <summary>ボタンを押す。<paramref name="target"/> を渡すと、カーソルの下がその要素を表示している HWND であることを確かめる（既定は計測用のウィンドウ）。</summary>
    public static async Task LeftDownAsync(Window window, FrameworkElement? target = null)
    {
        GetCursorPos(out NativePoint point);
        EnsureOver(target is null ? new WindowInteropHelper(window).Handle : HandleOf(target), point);
        Send(MouseEventLeftDown);
        s_leftDown = true;
        await Capture.SettleAsync(window, 100);
    }

    /// <summary>
    /// ボタンを離す。押したままにしないよう、カーソルの下を確かめずに必ず離す
    /// （押す直前の確認で、押した先は計測用のウィンドウであることが分かっている）。
    /// </summary>
    public static async Task LeftUpAsync(Window window)
    {
        Send(MouseEventLeftUp);
        s_leftDown = false;
        await Capture.SettleAsync(window, 100);
    }

    /// <summary>
    /// 要素の中心へカーソルを動かし、左ボタンを押して離す。
    /// ポップアップの中の要素も押せるよう、<see cref="MoveToAsync"/> には常に <paramref name="window"/> を渡す。
    /// </summary>
    public static async Task ClickAsync(Window window, FrameworkElement target)
    {
        await MoveToAsync(target, null, window);
        await LeftDownAsync(window, target);
        await LeftUpAsync(window);
    }

    /// <summary>
    /// 要素の中心（または <paramref name="offset"/> の位置）へカーソルを動かし、<paramref name="button"/> で指定したボタン（左か右）の押す・離すを 2 回続けて送る。
    /// <paramref name="interval"/> を省くと 4 つの入力を 1 回の SendInput で送るため、間隔はダブルクリックの時間（GetDoubleClickTime）より十分に短い。
    /// <paramref name="interval"/> を渡すと、1 回目のクリックと 2 回目のクリックを、その時間だけ空けて送る。
    /// </summary>
    public static async Task DoubleClickAsync(
        Window window, FrameworkElement target, Point? offset = null, System.Windows.Input.MouseButton button = System.Windows.Input.MouseButton.Left,
        TimeSpan? interval = null)
    {
        (uint down, uint up) = button switch
        {
            System.Windows.Input.MouseButton.Left => (MouseEventLeftDown, MouseEventLeftUp),
            System.Windows.Input.MouseButton.Right => (MouseEventRightDown, MouseEventRightUp),
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };
        await MoveToAsync(target, offset, window);
        GetCursorPos(out NativePoint point);
        EnsureOver(HandleOf(target), point);
        if (interval is TimeSpan gap)
        {
            // 人が操作するときのように、1 回目のクリックと 2 回目のクリックの間を空ける。
            // 間隔がダブルクリックの時間を超えると 2 回のクリックになるため、超える指定は受け付けない。
            if (gap.TotalMilliseconds >= GetDoubleClickTime())
            {
                throw new ArgumentOutOfRangeException(nameof(interval), $"間隔 {gap.TotalMilliseconds} ms がダブルクリックの時間 {GetDoubleClickTime()} ms 以上。");
            }

            SendClick(down, up, button);
            await Task.Delay(gap);
            GetCursorPos(out NativePoint second);
            EnsureOver(HandleOf(target), second);
            SendClick(down, up, button);
            await Capture.SettleAsync(window, 100);
            return;
        }

        var inputs = new[] { down, up, down, up }
            .Select(flags => new Input { Type = InputMouse, Mouse = new MouseInput { Flags = flags } })
            .ToArray();
        uint accepted = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (accepted != inputs.Length)
        {
            int error = Marshal.GetLastWin32Error();

            // 押す入力だけが受け付けられた（受け付けた数が奇数の）ときは、押したまま残らないよう離す。
            // 離す入力も失敗したときは、押したままの状態を残して Preserve の破棄でもう一度離させ、
            // 受け付けた件数を含む元の例外はそのまま投げる。
            if (accepted % 2 == 1)
            {
                try
                {
                    Send(up);
                }
                catch (InvalidOperationException)
                {
                    if (button == System.Windows.Input.MouseButton.Left)
                    {
                        s_leftDown = true;
                    }
                    else
                    {
                        s_rightDown = true;
                    }
                }
            }

            throw new InvalidOperationException(
                $"SendInput が入力を {accepted} 件しか受け付けなかった（画面のロック中など）。Win32 エラー {error}。");
        }

        await Capture.SettleAsync(window, 100);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>
    /// 押す・離すを 1 回の SendInput で送る。押す入力だけが受け付けられたときは、押したまま残らないよう離し、
    /// 離せなければ押したままの状態を残して <see cref="Preserve"/> の破棄でもう一度離させる。
    /// </summary>
    private static void SendClick(uint down, uint up, System.Windows.Input.MouseButton button)
    {
        var inputs = new[] { down, up }
            .Select(flags => new Input { Type = InputMouse, Mouse = new MouseInput { Flags = flags } })
            .ToArray();
        uint accepted = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (accepted == inputs.Length)
        {
            return;
        }

        int error = Marshal.GetLastWin32Error();
        if (accepted == 1)
        {
            try
            {
                Send(up);
            }
            catch (InvalidOperationException)
            {
                if (button == System.Windows.Input.MouseButton.Left)
                {
                    s_leftDown = true;
                }
                else
                {
                    s_rightDown = true;
                }
            }
        }

        throw new InvalidOperationException(
            $"SendInput が入力を {accepted} 件しか受け付けなかった（画面のロック中など）。Win32 エラー {error}。");
    }

    /// <summary>要素を表示しているトップレベルの HWND（ポップアップの中ならポップアップの HWND）。</summary>
    private static IntPtr HandleOf(FrameworkElement element) =>
        (PresentationSource.FromVisual(element) as HwndSource)?.Handle
            ?? throw new InvalidOperationException("表示していない要素の上にはカーソルを動かせない。");

    /// <summary>画面上の点の下にあるトップレベルのウィンドウが、<paramref name="expected"/> でなければ例外にする。</summary>
    private static void EnsureOver(IntPtr expected, NativePoint point)
    {
        IntPtr actual = GetAncestor(WindowFromPoint(point), AncestorRoot);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"カーソルの位置 ({point.X}, {point.Y}) に計測用のウィンドウが無い（ほかのウィンドウが前面にある）。前面のウィンドウ: {Describe(actual)}");
        }
    }

    /// <summary>ウィンドウのクラス名とプロセス名（ガードで止まったときに、何が前面にあったかを示すため）。<see cref="RealKeyboard"/> も使う。</summary>
    internal static string Describe(IntPtr hwnd)
    {
        var name = new System.Text.StringBuilder(256);
        GetClassName(hwnd, name, name.Capacity);
        GetWindowThreadProcessId(hwnd, out uint processId);
        string process;
        try
        {
            process = System.Diagnostics.Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            process = "(終了済み)";
        }

        return $"クラス {name}、プロセス {process}";
    }

    private static void Send(uint flags)
    {
        var inputs = new[] { new Input { Type = InputMouse, Mouse = new MouseInput { Flags = flags } } };
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) != 1)
        {
            throw new InvalidOperationException(
                $"SendInput が入力を受け付けなかった（画面のロック中など）。Win32 エラー {Marshal.GetLastWin32Error()}。");
        }
    }

    private sealed class Restore(NativePoint original) : IDisposable
    {
        /// <summary>
        /// 押したままのボタンを離してから、カーソルを元へ戻す。
        /// 離せなかったときは、押したままカーソルを動かすとドラッグになるので戻さず、標準エラーに書く
        /// （using の中で起きた例外を隠さないよう、ここでは例外を投げない）。
        /// </summary>
        public void Dispose()
        {
            bool released = Release(ref s_leftDown, MouseEventLeftUp, "左")
                & Release(ref s_rightDown, MouseEventRightUp, "右");
            if (released)
            {
                SetCursorPos(original.X, original.Y);
            }
        }

        private static bool Release(ref bool down, uint upFlag, string name)
        {
            if (!down)
            {
                return true;
            }

            try
            {
                Send(upFlag);
                down = false;
                return true;
            }
            catch (InvalidOperationException)
            {
                Console.Error.WriteLine($"マウスの{name}ボタンを離せなかった。押されたまま残っている可能性があるため、カーソルを元へ戻さない。");
                return false;
            }
        }
    }
}
