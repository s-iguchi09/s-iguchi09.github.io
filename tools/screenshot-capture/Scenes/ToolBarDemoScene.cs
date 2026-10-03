using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「ToolBar」（apps/wpf-standard-control-demo/toolbar.md と日本語版）の記述を実測する。
///
/// ToolBar のドラッグは、ToolBar のつまみ（Thumb）に DragDelta イベントを発生させて再現する。
/// マウスでドラッグしたときに Thumb が発生させるのと同じイベントである。
/// </summary>
internal sealed class ToolBarDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-toolbar";

    public string ImageDirectory => DemoProbe.ImageDirectory("toolbar");

    public IReadOnlyList<string> Verifies =>
    [
        "ToolBar の基底クラス、HasOverflowItems・IsOverflowItem・ToolBar の Orientation が読み取り専用か、OverflowMode の既定値",
        "ToolBarTray に入れない ToolBar で、幅（デモアプリの 100・200・400）と OverflowMode ごとに、対象の項目がオーバーフローに移るか",
        "すべての項目を Never にして幅を狭めたときの、項目の幅の合計と表示される幅",
        "はみ出す項目がないときに IsOverflowOpen を True にしたときの、オーバーフローボタンの状態とポップアップ",
        "ToolBar の中の Button・ToggleButton・ComboBox・Separator に、ToolBar 用のスタイルが適用されるか",
        "ToolBar に Background を設定したときに、テンプレートの背景に反映されるか",
        "ToolBarTray の中の Band / BandIndex と実際の位置、つまみのドラッグで Band / BandIndex が変わるか、IsLocked のときのつまみ",
        "ToolBarTray の Orientation を Vertical にしたときの ToolBar の Orientation",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ToolBar outside a ToolBarTray (the demo app's overflow section): where the target item goes",
            [T("ToolBar width", "ToolBar の幅"), "AsNeeded", "Never", "Always"],
            await OverflowMatrixAsync(),
            "toolbar-overflow-matrix.svg");

        await context.SaveTableAsync(
            "ToolBar: overflow",
            [T("case", "条件"), T("measured", "計測値")],
            await OverflowAsync(),
            "toolbar-overflow.svg");

        await context.SaveTableAsync(
            "ToolBar: styles, background, ToolBarTray bands, locking and orientation",
            [T("case", "条件"), T("measured", "計測値")],
            await TrayAsync(),
            "toolbar-tray.svg");
    }

    /// <summary>デモアプリの Overflow 欄と同じ 5 つのボタン。2 つ目が OverflowMode を変える対象。</summary>
    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static (ToolBar Bar, Button Target) DemoToolBar(double width, OverflowMode mode)
    {
        var bar = new ToolBar { Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        bar.Items.Add(new Button { Content = "Item 1" });
        var target = new Button { Content = "Target Item" };
        ToolBar.SetOverflowMode(target, mode);
        bar.Items.Add(target);
        bar.Items.Add(new Button { Content = "Item 2" });
        bar.Items.Add(new Button { Content = "Item 3" });
        bar.Items.Add(new Button { Content = "Item 4" });
        return (bar, target);
    }

    private static async Task Settle(FrameworkElement element) =>
        await Capture.SettleAsync(Window.GetWindow(element)!);

    private static async Task<List<IReadOnlyList<Loc>>> OverflowMatrixAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach (double width in new[] { 100.0, 200.0, 400.0 })
        {
            var cells = new List<Loc> { D(width) };
            foreach (OverflowMode mode in new[] { OverflowMode.AsNeeded, OverflowMode.Never, OverflowMode.Always })
            {
                (ToolBar bar, Button target) = DemoToolBar(width, mode);
                var host = new Grid { Width = 420 };
                host.Children.Add(bar);
                await ShowAsync(host, async () =>
                {
                    int overflowCount = bar.Items.Cast<UIElement>().Count(ToolBar.GetIsOverflowItem);
                    cells.Add(ToolBar.GetIsOverflowItem(target)
                        ? T($"overflow ({overflowCount} of 5 in overflow)", $"オーバーフロー（5 個中 {overflowCount} 個がオーバーフロー）")
                        : T($"bar ({overflowCount} of 5 in overflow)", $"バー（5 個中 {overflowCount} 個がオーバーフロー）"));
                    await Task.CompletedTask;
                });
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> OverflowAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        rows.Add([T("base class", "基底クラス"), typeof(ToolBar).BaseType!.Name]);
        rows.Add([T("read-only: HasOverflowItems / IsOverflowItem / ToolBar.Orientation", "読み取り専用か: HasOverflowItems / IsOverflowItem / ToolBar.Orientation"),
            $"{ToolBar.HasOverflowItemsProperty.ReadOnly} / {ToolBar.IsOverflowItemProperty.ReadOnly} / {ToolBar.OrientationProperty.ReadOnly}"]);
        rows.Add([T("OverflowMode default", "OverflowMode の既定値"), ToolBar.OverflowModeProperty.DefaultMetadata.DefaultValue!.ToString()!]);

        {
            var bar = new ToolBar { Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 1; i <= 5; i++)
            {
                var button = new Button { Content = $"Item {i}" };
                ToolBar.SetOverflowMode(button, OverflowMode.Never);
                bar.Items.Add(button);
            }

            var host = new Grid { Width = 420 };
            host.Children.Add(bar);
            await ShowAsync(host, async () =>
            {
                double itemsWidth = bar.Items.Cast<FrameworkElement>().Sum(b => b.ActualWidth);
                var panel = Descendants(bar).OfType<ToolBarPanel>().First();
                rows.Add([T("width 100, all 5 Never: HasOverflowItems / items' width / panel width", "幅 100、5 個すべて Never: HasOverflowItems / 項目の幅 / パネルの幅"),
                    $"{bar.HasOverflowItems} / {D(itemsWidth)} / {D(panel.ActualWidth)}"]);
                await Task.CompletedTask;
            });
        }

        {
            (ToolBar bar, _) = DemoToolBar(400, OverflowMode.AsNeeded);
            var host = new Grid { Width = 420 };
            host.Children.Add(bar);
            await ShowAsync(host, async () =>
            {
                bar.IsOverflowOpen = true;
                await Settle(bar);
                var button = (ToggleButton)bar.Template.FindName("OverflowButton", bar);
                var popup = (Popup)bar.Template.FindName("OverflowPopup", bar);
                rows.Add([T("width 400, IsOverflowOpen = true: HasOverflowItems / button / popup", "幅 400、IsOverflowOpen = true: HasOverflowItems / ボタン / ポップアップ"),
                    $"{bar.HasOverflowItems} / {button.Visibility}, IsEnabled {button.IsEnabled} / IsOpen {popup.IsOpen}"]);
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> TrayAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        {
            var bar = new ToolBar();
            var button = new Button { Content = "B" };
            var toggle = new ToggleButton { Content = "T" };
            var combo = new ComboBox();
            var separator = new Separator();
            bar.Items.Add(button);
            bar.Items.Add(toggle);
            bar.Items.Add(combo);
            bar.Items.Add(separator);
            await ShowAsync(bar, async () =>
            {
                Loc Check(FrameworkElement element, ResourceKey key) =>
                    ReferenceEquals(element.Style, bar.FindResource(key)) ? T("ToolBar style", "ToolBar のスタイル") : T("other", "その他");
                rows.Add([T("Button / ToggleButton / ComboBox / Separator in a ToolBar: style", "ToolBar の中の Button / ToggleButton / ComboBox / Separator: スタイル"),
                    T($"{Check(button, ToolBar.ButtonStyleKey).En} / {Check(toggle, ToolBar.ToggleButtonStyleKey).En} / {Check(combo, ToolBar.ComboBoxStyleKey).En} / {Check(separator, ToolBar.SeparatorStyleKey).En}",
                      $"{Check(button, ToolBar.ButtonStyleKey).Ja} / {Check(toggle, ToolBar.ToggleButtonStyleKey).Ja} / {Check(combo, ToolBar.ComboBoxStyleKey).Ja} / {Check(separator, ToolBar.SeparatorStyleKey).Ja}")]);
                rows.Add([T("  Separator in a horizontal ToolBar: width / height", "  横の ToolBar の中の Separator: 幅 / 高さ"), $"{D(separator.ActualWidth)} / {D(separator.ActualHeight)}"]);
                await Task.CompletedTask;
            });
        }

        foreach (Brush? background in new Brush?[] { null, Brushes.LightYellow })
        {
            var bar = new ToolBar();
            bar.Items.Add(new Button { Content = "B" });
            if (background is not null)
            {
                bar.Background = background;
            }

            await ShowAsync(bar, async () =>
            {
                Border? main = Descendants(bar).OfType<Border>().FirstOrDefault(b => b.TemplatedParent == bar && b.Background is not null);
                rows.Add([T($"Background {(background is null ? "not set" : "LightYellow")}: value (source) / template border", $"Background {(background is null ? "指定なし" : "LightYellow")}: 値（出どころ） / テンプレートの枠"),
                    T($"{WpfProbe.ValueAndSource(bar, Control.BackgroundProperty)} / {main?.Background?.ToString() ?? "none"}", $"{WpfProbe.ValueAndSource(bar, Control.BackgroundProperty)} / {main?.Background?.ToString() ?? "なし"}")]);
                await Task.CompletedTask;
            });
        }

        {
            // デモアプリの Band 欄と同じ 4 つの ToolBar。
            var tray = new ToolBarTray { Width = 400, MinHeight = 120 };
            var bars = new List<ToolBar>();
            foreach ((int band, int index) in new[] { (0, 0), (0, 1), (1, 0), (1, 1) })
            {
                var bar = new ToolBar { Band = band, BandIndex = index };
                bar.Items.Add(new TextBlock { Text = $"[Band:{band}, BandIndex:{index}]", Margin = new Thickness(5) });
                tray.ToolBars.Add(bar);
                bars.Add(bar);
            }

            await ShowAsync(tray, async () =>
            {
                rows.Add([T("tray, (Band, BandIndex) (0,0) (0,1) (1,0) (1,1): positions", "トレイ、(Band, BandIndex) (0,0) (0,1) (1,0) (1,1): 位置"),
                    string.Join("  ", bars.Select(b => { Rect r = Bounds(b, tray); return $"({D(r.X)}, {D(r.Y)})"; }))]);

                // (1,0) の ToolBar をつまみで上へドラッグし、1 行目へ移せるかを見る。
                ToolBar moved = bars[2];
                var thumb = (Thumb)moved.Template.FindName("ToolBarThumb", moved);
                thumb.RaiseEvent(new DragStartedEventArgs(0, 0));
                thumb.RaiseEvent(new DragDeltaEventArgs(250, -30));
                thumb.RaiseEvent(new DragCompletedEventArgs(250, -30, false));
                await Settle(tray);
                rows.Add([T("  thumb of (1,0) dragged by (250, -30): its Band / BandIndex", "  (1,0) のつまみを (250, -30) ドラッグ: その Band / BandIndex"), $"{moved.Band} / {moved.BandIndex}"]);
            });
        }

        foreach (bool locked in new[] { false, true })
        {
            var tray = new ToolBarTray { Width = 300, IsLocked = locked };
            var bar = new ToolBar();
            bar.Items.Add(new Button { Content = "New" });
            tray.ToolBars.Add(bar);
            await ShowAsync(tray, async () =>
            {
                var thumb = (Thumb)bar.Template.FindName("ToolBarThumb", bar);
                rows.Add([T($"ToolBarTray IsLocked={locked}: ToolBar's IsLocked / thumb visibility", $"ToolBarTray IsLocked={locked}: ToolBar の IsLocked / つまみの Visibility"),
                    $"{ToolBarTray.GetIsLocked(bar)} / {thumb.Visibility}"]);
                await Task.CompletedTask;
            });
        }

        {
            var tray = new ToolBarTray { Orientation = Orientation.Vertical, Height = 200 };
            var bar = new ToolBar();
            bar.Items.Add(new Button { Content = "Cut" });
            tray.ToolBars.Add(bar);
            var standalone = new ToolBar();
            var panel = new StackPanel();
            panel.Children.Add(tray);
            panel.Children.Add(standalone);
            await ShowAsync(panel, async () =>
            {
                rows.Add([T("tray Vertical: ToolBar.Orientation (source) / ToolBar outside a tray", "トレイが Vertical: ToolBar.Orientation（出どころ）/ トレイの外の ToolBar"),
                    $"{WpfProbe.ValueAndSource(bar, ToolBar.OrientationProperty)} / {standalone.Orientation}"]);
                rows.Add([T("  setting ToolBar.Orientation", "  ToolBar.Orientation を設定"), Throws(() => bar.SetValue(ToolBar.OrientationProperty, Orientation.Horizontal))]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }
}
