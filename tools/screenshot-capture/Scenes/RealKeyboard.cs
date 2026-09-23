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

    /// <summary>INPUT 構造体。共用体の大きさを MOUSEINPUT（最大のメンバー）に合わせるため、明示的に配置する。</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyboardInput Keyboard;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    /// <summary>仮想キーを 1 回押して離す。</summary>
    public static async Task PressAsync(Window window, ushort virtualKey)
    {
        if (GetForegroundWindow() != new WindowInteropHelper(window).Handle)
        {
            throw new InvalidOperationException("前面のウィンドウが計測用のウィンドウではない。キーを送らない。");
        }

        var inputs = new[]
        {
            new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { VirtualKey = virtualKey } },
            new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = KeyEventKeyUp } },
        };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new InvalidOperationException(
                $"SendInput が入力を受け付けなかった（画面のロック中など）。Win32 エラー {Marshal.GetLastWin32Error()}。");
        }

        await Capture.SettleAsync(window, 100);
    }
}
