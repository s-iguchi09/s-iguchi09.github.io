using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「ToolTip」（apps/wpf-standard-control-demo/tooltip.md と日本語版）の記述を実測する。
///
/// ツールチップは実際のマウス（<see cref="RealMouse"/>）でボタンの上にカーソルを置いて開く。
/// 開いた ToolTip は、ToolTip.Opened のクラスハンドラーで受け取る。
/// 時間は環境で揺れるため、本文には設定値との対応を書き、ミリ秒の値は図に持たせる。
/// </summary>
internal sealed class ToolTipDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-tooltip";

    public string ImageDirectory => DemoProbe.ImageDirectory("tooltip");

    public IReadOnlyList<string> Verifies =>
    [
        "ToolTipService の InitialShowDelay / ShowDuration / BetweenShowDelay / Placement / ShowsToolTipOnKeyboardFocus / ShowOnDisabled / HasDropShadow の既定値",
        "実際のマウスでボタンに乗せてからツールチップが開くまでの時間（InitialShowDelay 500 と 2000）",
        "ShowDuration=1000 で、乗せたまま 2.5 秒待ったときにツールチップが開いているか",
        "文字列の ToolTip を 2 回開いたとき、同じ ToolTip のインスタンスが使われるか",
        "実際のキーボードの Tab でフォーカスを移したとき、ShowsToolTipOnKeyboardFocus が True / False / 未設定のボタンでツールチップが開くか",
        "無効なボタンに乗せたとき、ShowOnDisabled が False / True でツールチップが開くか",
        "Placement=Bottom と Mouse で、ボタンとカーソルに対するツールチップの位置、HorizontalOffset 50 を加えたときの位置",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ToolTip: defaults, timing, reuse, keyboard, disabled elements and placement (real mouse)",
            ["case", "measured"],
            await MeasureAsync(),
            "tooltip-behavior.svg");
    }

    private static ToolTip? s_lastOpened;
    private static bool s_registered;

    private static void Register()
    {
        if (s_registered)
        {
            return;
        }

        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent,
            new RoutedEventHandler((sender, _) => s_lastOpened = (ToolTip)sender));
        s_registered = true;
    }

    /// <summary>カーソルを要素に乗せ、ツールチップが開くまでの時間を返す。開かなければ null。</summary>
    private static async Task<double?> HoverUntilOpenAsync(FrameworkElement element, int timeoutMs = 4000)
    {
        s_lastOpened = null;
        var clock = Stopwatch.StartNew();
        await RealMouse.MoveToAsync(element);
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            if (s_lastOpened is { IsOpen: true })
            {
                return clock.Elapsed.TotalMilliseconds;
            }

            await Task.Delay(10);
        }

        return null;
    }

    private static Point ScreenDip(Visual element)
    {
        Point device = element.PointToScreen(new Point(0, 0));
        return PresentationSource.FromVisual(element)!.CompositionTarget!.TransformFromDevice.Transform(device);
    }

    private static string Ms(double? value) => value is { } v ? $"opened after {Math.Round(v / 50) * 50} ms" : "did not open";

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        Register();
        var rows = new List<IReadOnlyList<string>>();

        var probe = new Button();
        rows.Add(["defaults: InitialShowDelay / ShowDuration / BetweenShowDelay",
            $"{ToolTipService.GetInitialShowDelay(probe)} / {ToolTipService.GetShowDuration(probe)} / {ToolTipService.GetBetweenShowDelay(probe)}"]);
        rows.Add(["defaults: Placement / ShowsToolTipOnKeyboardFocus / ShowOnDisabled / HasDropShadow",
            $"{ToolTipService.GetPlacement(probe)} / {WpfProbe.Describe(ToolTipService.GetShowsToolTipOnKeyboardFocus(probe))} / {ToolTipService.GetShowOnDisabled(probe)} / {ToolTipService.GetHasDropShadow(probe)}"]);

        var blank = new Border { Width = 160, Height = 60, Background = Brushes.White };
        var fast = new Button { Content = "delay 500", Width = 160, Height = 30, ToolTip = "fast" };
        ToolTipService.SetInitialShowDelay(fast, 500);
        var slow = new Button { Content = "delay 2000", Width = 160, Height = 30, ToolTip = "slow" };
        ToolTipService.SetInitialShowDelay(slow, 2000);
        var shortShow = new Button { Content = "duration 1000", Width = 160, Height = 30, ToolTip = "short" };
        ToolTipService.SetShowDuration(shortShow, 1000);
        var reused = new Button { Content = "string tip", Width = 160, Height = 30, ToolTip = "same text" };
        var disabled = new Button { Content = "disabled", Width = 160, Height = 30, ToolTip = "disabled tip", IsEnabled = false };
        var disabledShown = new Button { Content = "disabled, ShowOnDisabled", Width = 160, Height = 30, ToolTip = "shown", IsEnabled = false };
        ToolTipService.SetShowOnDisabled(disabledShown, true);
        var bottom = new Button { Content = "Bottom", Width = 160, Height = 30, ToolTip = "Bottom" };
        ToolTipService.SetPlacement(bottom, PlacementMode.Bottom);
        var bottomOffset = new Button { Content = "Bottom +50", Width = 160, Height = 30, ToolTip = "Bottom +50" };
        ToolTipService.SetPlacement(bottomOffset, PlacementMode.Bottom);
        ToolTipService.SetHorizontalOffset(bottomOffset, 50);
        var mouse = new Button { Content = "Mouse", Width = 160, Height = 30, ToolTip = "Mouse" };
        // 時間を測らないボタンは、待ち時間を短くするため InitialShowDelay を 300 にする。
        foreach (Button b in new[] { shortShow, reused, disabled, disabledShown, bottom, bottomOffset, mouse })
        {
            ToolTipService.SetInitialShowDelay(b, 300);
        }

        var panel = new StackPanel { Children = { blank, fast, slow, shortShow, reused, disabled, disabledShown, bottom, bottomOffset, mouse } };
        await ShowAsync(panel, async () =>
        {
            Window window = Window.GetWindow(panel)!;
            window.Topmost = true;
            window.Left = 400;
            window.Top = 200;
            await Capture.SettleAsync(window);

            async Task Rest()
            {
                await RealMouse.MoveToAsync(blank);
                await Task.Delay(1200);
            }

            using (RealMouse.Preserve())
            {
                await Rest();
                string a = Ms(await HoverUntilOpenAsync(fast));
                await Rest();
                string b = Ms(await HoverUntilOpenAsync(slow));
                rows.Add(["real mouse over a button: InitialShowDelay 500 / 2000", $"{a} / {b}"]);

                await Rest();
                await HoverUntilOpenAsync(shortShow);
                ToolTip? shown = s_lastOpened;
                await Task.Delay(2500);
                rows.Add(["ShowDuration=1000, pointer kept on the button: open 2.5 s later", WpfProbe.Describe(shown?.IsOpen)]);

                await Rest();
                await HoverUntilOpenAsync(reused);
                ToolTip? first = s_lastOpened;
                await Rest();
                await HoverUntilOpenAsync(reused);
                ToolTip? second = s_lastOpened;
                rows.Add(["string ToolTip opened twice: same ToolTip instance", $"{first is not null && ReferenceEquals(first, second)}"]);

                await Rest();
                string off = Ms(await HoverUntilOpenAsync(disabled, 2000));
                await Rest();
                string on = Ms(await HoverUntilOpenAsync(disabledShown, 2000));
                rows.Add(["disabled button: ShowOnDisabled False / True", $"{off} / {on}"]);

                foreach ((string label, Button target) in new[] { ("Placement=Bottom", bottom), ("Bottom, HorizontalOffset 50", bottomOffset), ("Placement=Mouse (default)", mouse) })
                {
                    await Rest();
                    await HoverUntilOpenAsync(target);
                    if (s_lastOpened is { } tip)
                    {
                        Point t = ScreenDip(tip);
                        Point o = ScreenDip(target);
                        Point center = new(o.X + target.ActualWidth / 2, o.Y + target.ActualHeight / 2);
                        rows.Add([$"{label}: tooltip from the button's top-left / from the pointer",
                            $"({D(t.X - o.X)}, {D(t.Y - o.Y)}) / ({D(t.X - center.X)}, {D(t.Y - center.Y)})"]);
                    }
                }

                await Rest();
            }
        });

        foreach (bool? showOnFocus in new bool?[] { true, false, null })
        {
            var before = new Button { Content = "before" };
            var target = new Button { Content = "target", ToolTip = "focus tip" };
            if (showOnFocus is { } value)
            {
                ToolTipService.SetShowsToolTipOnKeyboardFocus(target, value);
            }
            var focusPanel = new StackPanel { Width = 160, Children = { before, target } };
            await ShowAsync(focusPanel, async () =>
            {
                Window window = await FrontAsync(focusPanel);
                using (RealMouse.Preserve())
                {
                    // ホバーでツールチップが開かないよう、カーソルを target ではなく before の上に置いてから Tab を押す。
                    await RealMouse.MoveToAsync(before);
                    await FocusAsync(before);
                    s_lastOpened = null;

                    // 実際のキーボード入力の Tab（VK_TAB = 0x09）でフォーカスを移す。
                    await RealKeyboard.PressAsync(window, 0x09);
                    await Capture.SettleAsync(window, 1500);
                    bool focused = target.IsKeyboardFocused;
                    rows.Add([$"real Tab (mouse on the other button), OnKeyboardFocus={WpfProbe.Describe(showOnFocus)}: focused / mouse over / opened",
                        $"{focused} / {target.IsMouseOver} / {s_lastOpened is { IsOpen: true }}"]);
                    if (s_lastOpened is { } open)
                    {
                        open.IsOpen = false;
                    }
                }
            }, activate: true);
        }

        return rows;
    }
}
