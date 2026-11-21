using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// OS のキーボード入力（SendInput）でキーを押して離す。
///
/// <see cref="DemoProbe.SendKey"/> は WPF の InputManager に直接入力を渡すため、
/// 「最後の入力がキーボードだったか」を OS の入力から判断する処理（キーボードフォーカスでのツールチップなど）には届かない。
/// 押す前に、前面のウィンドウが計測用のウィンドウであることを確かめる（ほかのアプリにキーを送らないため）。
/// </summary>
internal static class RealKeyboard
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>MOUSEINPUT。共用体の大きさを決める最大のメンバーとして定義する（キーボード入力だけを送るが、大きさを合わせるため）。</summary>
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

    /// <summary>INPUT の共用体。各メンバーを先頭に重ねる。</summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    /// <summary>
    /// INPUT 構造体。共用体の位置と全体の大きさはマーシャラーに計算させる
    /// （x64 では位置 8・40 バイト、x86 では位置 4・28 バイト。固定値にすると x86 で SendInput が失敗する）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    /// <summary>
    /// 仮想キー（Ctrl など）を押したままにする。破棄すると離す。
    /// 押したまま実際のマウスで操作する計測（Ctrl を押しながらのクリックなど）に使う。
    /// </summary>
    public static IDisposable Hold(Window window, ushort virtualKey)
    {
        EnsureNoStuckKey();
        IntPtr foreground = GetForegroundWindow();
        if (foreground != new WindowInteropHelper(window).Handle)
        {
            throw new InvalidOperationException(
                $"前面のウィンドウが計測用のウィンドウではない。キーを送らない。前面のウィンドウ: {RealMouse.Describe(foreground)}");
        }

        Send(virtualKey, 0);
        return new Release(virtualKey);
    }

    /// <summary><see cref="Hold"/> で押したキーを離せなかったときの仮想キー。離せなかった後の計測を止めるために残す。</summary>
    private static ushort? s_stuckKey;

    /// <summary>
    /// <see cref="Hold"/> で押したキーを離せずに残っていれば例外にする。
    /// 修飾キーが押されたままの状態で、修飾キーを押さない操作を測らないため、実際の入力を送る計測の前に呼ぶ。
    /// </summary>
    public static void EnsureNoStuckKey()
    {
        if (s_stuckKey is ushort key)
        {
            throw new InvalidOperationException($"仮想キー 0x{key:X2} を離せていない。押されたままの状態で計測を続けない。");
        }
    }

    /// <summary>キーボードの INPUT を 1 つ作る。<paramref name="flags"/> が 0 なら押す、KEYEVENTF_KEYUP なら離す。</summary>
    private static Input KeyInput(ushort virtualKey, uint flags = 0) =>
        new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = flags } } };

    private static void Send(ushort virtualKey, uint flags)
    {
        var inputs = new[] { KeyInput(virtualKey, flags) };
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) != 1)
        {
            throw new InvalidOperationException(
                $"SendInput が入力を受け付けなかった（画面のロック中など）。Win32 エラー {Marshal.GetLastWin32Error()}。");
        }
    }

    /// <summary>
    /// 押したキーを離す。using の中で起きた例外を隠さないよう、ここでは例外を投げずに数回やり直す。
    /// それでも離せなければ標準エラーに書く（キーが OS 上で押されたまま残っているため、手で離す必要がある）。
    /// </summary>
    private sealed class Release(ushort virtualKey) : IDisposable
    {
        public void Dispose()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Send(virtualKey, KeyEventKeyUp);
                    return;
                }
                catch (InvalidOperationException)
                {
                    Thread.Sleep(100);
                }
            }

            s_stuckKey = virtualKey;
            Console.Error.WriteLine($"仮想キー 0x{virtualKey:X2} を離せなかった。キーが押されたまま残っている可能性がある。");
        }
    }

    /// <summary>仮想キーを 1 回押して離す。<paramref name="modifiers"/> のキー（Ctrl など）は、その間押したままにする。</summary>
    public static async Task PressAsync(Window window, ushort virtualKey, params ushort[] modifiers)
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground != new WindowInteropHelper(window).Handle)
        {
            throw new InvalidOperationException(
                $"前面のウィンドウが計測用のウィンドウではない。キーを送らない。前面のウィンドウ: {RealMouse.Describe(foreground)}");
        }

        var inputs = new List<Input>();
        inputs.AddRange(modifiers.Select(m => KeyInput(m)));
        inputs.Add(KeyInput(virtualKey));
        inputs.Add(KeyInput(virtualKey, KeyEventKeyUp));
        // modifiers.Reverse() と書くと、System.Memory を参照する環境（tools/book-a-net48）では C# 14 が Span の Reverse（void）を選ぶ。
        inputs.AddRange(Enumerable.Reverse(modifiers).Select(m => KeyInput(m, KeyEventKeyUp)));
        if (SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>()) != inputs.Count)
        {
            int error = Marshal.GetLastWin32Error();

            // 途中までしか受け付けられなかったとき、修飾キーが押されたまま残らないよう、すべてのキーを離しておく。
            Input[] releases = modifiers.Append(virtualKey)
                .Select(k => KeyInput(k, KeyEventKeyUp))
                .ToArray();
            SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>());
            throw new InvalidOperationException(
                $"SendInput が入力を受け付けなかった（画面のロック中など）。Win32 エラー {error}。");
        }

        await Capture.SettleAsync(window, 100);
    }
}
