using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「ProgressBar」（apps/wpf-standard-control-demo/progressbar.html と日本語版）の記述を実測する。
///
/// 塗りの長さはテンプレートの PART_Indicator の矩形で読む。
/// 不定モードのアニメーションが動いているかは、テンプレート内の要素の位置と変形を 300 ms 空けて 2 回読み、変わったかどうかで判断する。
/// </summary>
internal sealed class ProgressBarDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-progressbar";

    public string ImageDirectory => DemoProbe.ImageDirectory("progressbar");

    public IReadOnlyList<string> Verifies =>
    [
        "ProgressBar の基底クラスと、Minimum / Maximum / Value / IsIndeterminate / Orientation の既定値",
        "塗りの長さ（PART_Indicator）が (Value - Minimum) / (Maximum - Minimum) に比例すること（Minimum が 0 以外の場合も）",
        "Maximum と Minimum が等しいとき、Maximum を Minimum より小さくしたとき、Value が範囲外のときの塗りと値",
        "範囲外の Value が、後で Maximum を広げたときに元の値へ戻るか",
        "Orientation が Vertical のときの塗りの位置（下から上か）",
        "IsIndeterminate が True のときの塗りと表示状態、アニメーションが動いているか（表示中と Collapsed）",
        "別スレッドから Value を設定したときの例外と、Progress<T> のコールバックが UI スレッドで動くこと",
        "UI オートメーションの RangeValue パターン（通常時と不定モード）と IsReadOnly",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ProgressBar: range, fill and orientation",
            ["case", "measured"],
            MeasureRange(),
            "progressbar-range.svg");

        await context.SaveTableAsync(
            "ProgressBar: indeterminate mode, threads and UI Automation",
            ["case", "measured"],
            await MeasureModesAsync(),
            "progressbar-modes.svg");
    }

    private static Rect Indicator(ProgressBar bar)
    {
        var indicator = (FrameworkElement)bar.Template.FindName("PART_Indicator", bar);
        return Bounds(indicator, bar);
    }

    private static ProgressBar Bar(double min, double max, double value) =>
        Host(new ProgressBar { Minimum = min, Maximum = max, Value = value, Width = 200, Height = 20 });

    /// <summary>パネルに入れてレイアウトする（入れてあればレイアウトし直す）。単独でレイアウトすると既定のテンプレートが適用されないため。</summary>
    private static ProgressBar Host(ProgressBar bar)
    {
        var host = bar.Parent as Grid ?? new Grid { Children = { bar } };
        Layout(host, bar.Width, bar.Height);
        return bar;
    }

    private static List<IReadOnlyList<string>> MeasureRange()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new ProgressBar();
        rows.Add(["base class / defaults: Minimum, Maximum, Value, IsIndeterminate, Orientation",
            $"{typeof(ProgressBar).BaseType!.Name} / {D(defaults.Minimum)}, {D(defaults.Maximum)}, {D(defaults.Value)}, {defaults.IsIndeterminate}, {defaults.Orientation}"]);

        foreach ((double min, double max, double value) in new[] { (0d, 100d, 30d), (20d, 120d, 70d), (0d, 100d, 0d) })
        {
            ProgressBar bar = Bar(min, max, value);
            rows.Add([$"width 200, Minimum {D(min)}, Maximum {D(max)}, Value {D(value)}: indicator width",
                D(Indicator(bar).Width)]);
        }

        {
            ProgressBar bar = Bar(50, 50, 50);
            rows.Add(["Minimum = Maximum = 50: indicator width (exception)", $"{D(Indicator(bar).Width)} (none)"]);
        }

        {
            var bar = new ProgressBar { Minimum = 50, Width = 200, Height = 20 };
            bar.Maximum = 10;
            Host(bar);
            rows.Add(["Minimum 50, then Maximum = 10: Maximum / Value / indicator width",
                $"{D(bar.Maximum)} / {D(bar.Value)} / {D(Indicator(bar).Width)}"]);
        }

        {
            ProgressBar bar = Bar(0, 100, 150);
            string first = $"{D(bar.Value)} / {D(Indicator(bar).Width)}";
            bar.Maximum = 200;
            Host(bar);
            rows.Add(["Value = 150 with Maximum 100: Value / indicator width; then Maximum = 200: Value",
                $"{first}; {D(bar.Value)}"]);
        }

        {
            var bar = Host(new ProgressBar { Orientation = Orientation.Vertical, Value = 75, Width = 20, Height = 200 });
            rows.Add(["Orientation=Vertical, 20 x 200, Value 75: indicator bounds", Format(Indicator(bar))]);
        }

        return rows;
    }

    /// <summary>テンプレート内の要素の位置・余白・変形（変形の原点を含む。既定のテンプレートはこれを動かす）を文字列にする。2 回読んで比べ、動いているかを判断する。</summary>
    private static string Snapshot(ProgressBar bar) => string.Join("|", Descendants(bar).OfType<FrameworkElement>()
        .Select(e => $"{e.Margin}{e.RenderTransform.Value}{e.RenderTransformOrigin}{e.Opacity}{(e is System.Windows.Shapes.Shape or Border ? e.ActualWidth : 0)}"));

    private static async Task<List<IReadOnlyList<string>>> MeasureModesAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            var bar = new ProgressBar { Value = 30, Width = 200, Height = 20, IsIndeterminate = true };
            var panel = new StackPanel { Children = { bar } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                var root = (FrameworkElement)VisualTreeHelper.GetChild(bar, 0);
                string states = string.Join(", ", VisualStateManager.GetVisualStateGroups(root)!
                    .Cast<VisualStateGroup>()
                    .Select(g => $"{g.Name}: {g.CurrentState?.Name ?? "none"}"));
                rows.Add(["IsIndeterminate=True, Value 30: indicator width / visual state",
                    $"{D(Indicator(bar).Width)} / {states}"]);

                string before = Snapshot(bar);
                await Task.Delay(300);
                bool movingVisible = Snapshot(bar) != before;

                bar.Visibility = Visibility.Collapsed;
                await Capture.SettleAsync(window, 100);
                before = Snapshot(bar);
                await Task.Delay(300);
                bool movingCollapsed = Snapshot(bar) != before;

                bar.Visibility = Visibility.Visible;
                bar.IsIndeterminate = false;
                await Capture.SettleAsync(window, 100);
                before = Snapshot(bar);
                await Task.Delay(300);
                bool movingDeterminate = Snapshot(bar) != before;

                rows.Add(["animation running (template changes in 300 ms): visible / Collapsed / determinate",
                    $"{movingVisible} / {movingCollapsed} / {movingDeterminate}"]);
            });
        }

        {
            var bar = new ProgressBar();
            string fromWorker = await Task.Run(() => Throws(() => bar.Value = 10));
            int uiThread = Environment.CurrentManagedThreadId;
            var reported = new TaskCompletionSource<int>();
            var progress = new Progress<double>(v =>
            {
                bar.Value = v;
                reported.TrySetResult(Environment.CurrentManagedThreadId);
            });
            await Task.Run(() => ((IProgress<double>)progress).Report(40));
            int callbackThread = await reported.Task;
            rows.Add(["Value set from a worker thread", fromWorker]);
            rows.Add(["Progress<double> made on the UI thread, Report(40) from a worker: callback on the UI thread / Value",
                $"{callbackThread == uiThread} / {D(bar.Value)}"]);
        }

        {
            var bar = new ProgressBar { Value = 30, Width = 200, Height = 20 };
            await ShowAsync(bar, async () =>
            {
                var peer = new ProgressBarAutomationPeer(bar);
                string Pattern() => peer.GetPattern(PatternInterface.RangeValue) is System.Windows.Automation.Provider.IRangeValueProvider range
                    ? $"value {D(range.Value)}, IsReadOnly {range.IsReadOnly}"
                    : "not supported";
                string determinate = Pattern();
                bar.IsIndeterminate = true;
                rows.Add(["UI Automation RangeValue pattern: determinate / IsIndeterminate=True",
                    $"{determinate} / {Pattern()}"]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }
}
