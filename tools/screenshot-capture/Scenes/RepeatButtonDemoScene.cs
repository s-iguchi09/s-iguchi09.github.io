using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「RepeatButton」（apps/wpf-standard-control-demo/repeatbutton.md と日本語版）の記述を実測する。
///
/// 押し続けたときの繰り返しは、実際のマウス（<see cref="RealMouse"/>）でボタンを押したまま待って確かめる。
/// クリックの時刻は環境で揺れるため、本文には Delay・Interval との対応だけを書き、ミリ秒の値は図に持たせる。
/// </summary>
internal sealed class RepeatButtonDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-repeatbutton";

    public string ImageDirectory => DemoProbe.ImageDirectory("repeatbutton");

    public IReadOnlyList<string> Verifies =>
    [
        "Delay と Interval の既定値と、それを決める SystemParameters.KeyboardDelay / KeyboardSpeed、ClickMode の既定値",
        "実際のマウスで押し続けたときのクリックの時刻（押した時点、Delay 後、以後の間隔）と、離したときのクリック、コマンドの実行回数",
        "押したままボタンの外へ出たときと戻ったときの繰り返し",
        "Space を押し続けたとき（OS のキーリピートなし）のクリックの回数",
        "Delay に負の値、Interval に 0 を設定したときの例外と、TextBox からのバインドで不正な値や空文字を渡したときの値",
        "縦・横の ScrollBar と Slider の既定のテンプレートの中の RepeatButton（とそのコマンド）",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "RepeatButton: defaults, holding the real mouse button, keyboard",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureRepeatAsync(),
            "repeatbutton-repeat.svg");

        await context.SaveTableAsync(
            "RepeatButton: invalid values and where WPF uses it",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureValuesAsync(),
            "repeatbutton-values.svg");
    }

    private sealed class CountingCommand : ICommand
    {
        public int Executed { get; set; }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executed++;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static string Ms(double value) => $"{Math.Round(value)} ms";

    private static async Task<List<IReadOnlyList<Loc>>> MeasureRepeatAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        var defaults = new RepeatButton();
        rows.Add([T("default Delay / Interval (KeyboardDelay / KeyboardSpeed on this machine)", "Delay / Interval の既定値（このマシンの KeyboardDelay / KeyboardSpeed）"),
            $"{defaults.Delay} / {defaults.Interval} ({SystemParameters.KeyboardDelay} / {SystemParameters.KeyboardSpeed})"]);
        rows.Add([T("default ClickMode", "ClickMode の既定値"), defaults.ClickMode.ToString()]);

        {
            var command = new CountingCommand();
            var blank = new Border { Width = 120, Height = 40, Background = Brushes.White };
            var button = new RepeatButton { Content = "Hold down", Delay = 300, Interval = 100, Width = 120, Height = 40, Command = command };
            var clock = new Stopwatch();
            var times = new List<double>();
            button.Click += (_, _) => times.Add(clock.Elapsed.TotalMilliseconds);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Children = { blank, button } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                window.Topmost = true;
                await Capture.SettleAsync(window);

                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(button);
                    clock.Start();
                    await RealMouse.LeftDownAsync(window);
                    await Task.Delay(1000 - 100);
                    int held = times.Count;
                    await RealMouse.LeftUpAsync(window);
                    await Task.Delay(300);

                    var gaps = times.Skip(1).Zip(times, (later, earlier) => later - earlier).ToList();
                    var steady = gaps.Skip(1).OrderBy(g => g).ToList();
                    rows.Add([T("Delay=300, Interval=100, mouse held for about 1 s: clicks while held / after release", "Delay=300、Interval=100、マウスを約 1 秒押し続ける: 押している間のクリック数 / 離した後"),
                        $"{held} / {times.Count - held}"]);
                    rows.Add([T("  first click after button down / second after the first", "  押してから 1 回目のクリックまで / 1 回目から 2 回目まで"),
                        $"{Ms(times[0])} / {Ms(gaps[0])}"]);
                    rows.Add([T("  gaps after that: min / median / max", "  その後の間隔: 最小 / 中央値 / 最大"),
                        $"{Ms(steady[0])} / {Ms(steady[steady.Count / 2])} / {Ms(steady[^1])}"]);
                    rows.Add([T("  Command executions / Click events", "  Command の実行回数 / Click イベントの回数"), $"{command.Executed} / {times.Count}"]);

                    // 押したまま外へ出て、また戻る。
                    times.Clear();
                    await RealMouse.MoveToAsync(button);
                    await RealMouse.LeftDownAsync(window);
                    await Task.Delay(500);
                    await RealMouse.MoveToAsync(blank);
                    int beforeLeaving = times.Count;
                    await Task.Delay(500);
                    int whileOutside = times.Count - beforeLeaving;
                    bool pressedOutside = button.IsPressed;
                    await RealMouse.MoveToAsync(button);
                    int beforeReturning = times.Count;
                    await Task.Delay(500);
                    int afterReturning = times.Count - beforeReturning;
                    await RealMouse.LeftUpAsync(window);
                    rows.Add([T("held, moved off for 0.5 s, moved back for 0.5 s: clicks outside (IsPressed) / after returning", "押したまま 0.5 秒外に出て、0.5 秒戻る: 外にいる間のクリック数（IsPressed） / 戻った後"),
                        $"{whileOutside} ({pressedOutside}) / {afterReturning}"]);
                }
            });
        }

        {
            var button = new RepeatButton { Content = "Space", Delay = 300, Interval = 100, Width = 120 };
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            await ShowAsync(button, async () =>
            {
                await FocusAsync(button);
                SendKey(Key.Space);
                await Task.Delay(1000);
                int held = clicks;
                SendKey(Key.Space, down: false);
                await Capture.SettleAsync(Window.GetWindow(button)!, 50);
                rows.Add([T("Space held for 1 s, one key-down event (no OS key repeat): clicks while held / after release", "Space を 1 秒押し続ける、キーダウンは 1 回（OS のキーリピートなし）: 押している間のクリック数 / 離した後"),
                    $"{held} / {clicks - held}"]);

                clicks = 0;
                ((IInvokeProvider)new RepeatButtonAutomationPeer(button)).Invoke();
                await Capture.SettleAsync(Window.GetWindow(button)!, 50);
                rows.Add([T("UI Automation Invoke: clicks", "UI オートメーションの Invoke: クリック数"), clicks.ToString()]);
            }, activate: true);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureValuesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        rows.Add([T("Delay = -1 / Delay = 0 set from code", "コードから Delay = -1 / Delay = 0"),
            $"{Throws(() => new RepeatButton { Delay = -1 })} / {Throws(() => new RepeatButton { Delay = 0 })}"]);
        rows.Add([T("Interval = 0 / Interval = 1 set from code", "コードから Interval = 0 / Interval = 1"),
            $"{Throws(() => new RepeatButton { Interval = 0 })} / {Throws(() => new RepeatButton { Interval = 1 })}"]);

        {
            // デモアプリと同じく、TextBox の Text を Delay / Interval にバインドする。
            var delayText = new TextBox { Text = "100" };
            var intervalText = new TextBox { Text = "100" };
            var button = new RepeatButton { Content = "Hold down" };
            button.SetBinding(RepeatButton.DelayProperty, new Binding(nameof(TextBox.Text)) { Source = delayText });
            button.SetBinding(RepeatButton.IntervalProperty, new Binding(nameof(TextBox.Text)) { Source = intervalText });
            var panel = new StackPanel { Width = 200, Children = { delayText, intervalText, button } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                var results = new List<string>();
                foreach (string text in new[] { "200", "-1", "", "abc" })
                {
                    delayText.Text = text;
                    await Capture.SettleAsync(window, 20);
                    results.Add($"\"{text}\" -> {button.Delay}");
                }

                rows.Add([T("demo binding, Delay from TextBox.Text (in order)", "デモのバインド、TextBox.Text から Delay へ（入力した順）"), string.Join(", ", results)]);

                results.Clear();
                foreach (string text in new[] { "50", "0" })
                {
                    intervalText.Text = text;
                    await Capture.SettleAsync(window, 20);
                    results.Add($"\"{text}\" -> {button.Interval}");
                }

                rows.Add([T("demo binding, Interval from TextBox.Text (in order)", "デモのバインド、TextBox.Text から Interval へ（入力した順）"), string.Join(", ", results)]);
            });
        }

        {
            var scrollBar = new ScrollBar { Orientation = Orientation.Vertical, Height = 200 };
            var horizontal = new ScrollBar { Orientation = Orientation.Horizontal, Width = 200 };
            var slider = new Slider { Width = 200 };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Children = { scrollBar, new StackPanel { Children = { horizontal, slider } } } };
            await ShowAsync(panel, async () =>
            {
                string Commands(Control control) => string.Join(", ", Descendants(control).OfType<RepeatButton>()
                    .Select(r => r.Command is RoutedCommand routed ? routed.Name : WpfProbe.Describe(r.Command)));
                rows.Add([T("RepeatButtons in a vertical ScrollBar template (their commands)", "縦の ScrollBar のテンプレートの中の RepeatButton（そのコマンド）"), Commands(scrollBar)]);
                rows.Add([T("RepeatButtons in a horizontal ScrollBar template (their commands)", "横の ScrollBar のテンプレートの中の RepeatButton（そのコマンド）"), Commands(horizontal)]);
                rows.Add([T("RepeatButtons in the Slider template (their commands)", "Slider のテンプレートの中の RepeatButton（そのコマンド）"), Commands(slider)]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }
}
