using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「StackPanel」（apps/wpf-standard-control-demo/stackpanel.md と日本語版）の記述を実測する。
///
/// レイアウトはウィンドウを作らずに Measure / Arrange で行い、子に渡された大きさと配置を読む。
/// 背景と ZIndex は、指定した位置のヒットテストで最初に当たる要素の名前で確かめる。
/// </summary>
internal sealed class StackPanelDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-stackpanel";

    public string ImageDirectory => DemoProbe.ImageDirectory("stackpanel");

    public IReadOnlyList<string> Verifies =>
    [
        "Orientation の既定値と、Spacing プロパティの有無",
        "縦と横の StackPanel が子を測るときに渡す大きさ（積む方向が無限大か）と、交差する方向で子が引き伸ばされるか",
        "幅が足りない横の StackPanel で、子が折り返すか、はみ出した子の位置、ClipToBounds とレイアウトの切り抜き、はみ出した部分のヒットテスト",
        "ScrollViewer の中の StackPanel に 1000 個の子を置いたとき、測られる子の数",
        "高さ 200 の領域で、1000 項目の ListBox を直接置いた場合と縦の StackPanel に入れた場合の、高さ・スクロールできる量・作られた項目の数",
        "Background が null / Transparent / 色のとき、子のない位置のヒットテスト（デモアプリの高さ 30 の空の StackPanel）",
        "デモアプリと同じ重なった 2 つの Label（-15 の余白）で、ZIndex を入れ替えたときに上になる要素",
        "内側のパネルの子に大きな ZIndex を付けても、外側のパネルの兄弟より上にならないこと",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "StackPanel: sizes given to children, overflow, background and ZIndex",
            [T("case", "条件"), T("measured", "計測値")],
            Measure(),
            "stackpanel-behavior.svg");
        await Task.CompletedTask;
    }

    /// <summary>MeasureOverride に渡された大きさを記録する子。</summary>
    private sealed class SizeProbe : FrameworkElement
    {
        public Size Available { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Available = availableSize;
            return new Size(40, 20);
        }
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static string Size(Size size) => $"{D(size.Width)} x {D(size.Height)}";

    private static Label DemoLabel(string name, string text, int zIndex, Thickness margin, Brush background)
    {
        var label = new Label
        {
            Name = name,
            Content = text,
            VerticalAlignment = VerticalAlignment.Top,
            Background = background,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Margin = margin,
        };
        Panel.SetZIndex(label, zIndex);
        return label;
    }

    private static List<IReadOnlyList<Loc>> Measure()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        rows.Add([T("default Orientation / Spacing property", "Orientation の既定値 / Spacing プロパティ"),
            typeof(StackPanel).GetProperty("Spacing") is null ? T($"{new StackPanel().Orientation} / none", $"{new StackPanel().Orientation} / なし") : T($"{new StackPanel().Orientation} / exists", $"{new StackPanel().Orientation} / ある")]);

        foreach (Orientation orientation in new[] { Orientation.Vertical, Orientation.Horizontal })
        {
            var probe = new SizeProbe();
            var panel = new StackPanel { Orientation = orientation, Children = { probe } };
            Layout(panel, 200, 100);
            rows.Add([T($"{orientation}, panel 200 x 100: size given to the child / child's arranged size (desired 40 x 20)", $"{orientation}、200 x 100 のパネル: 子に渡される大きさ / 配置された子の大きさ（希望は 40 x 20）"),
                $"{Size(probe.Available)} / {D(probe.RenderSize.Width)} x {D(probe.RenderSize.Height)}"]);
        }

        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 1; i <= 3; i++)
            {
                panel.Children.Add(new Label { Content = $"Item{i}", Margin = new Thickness(2), BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) });
            }

            var host = new Grid { Width = 200, Children = { panel } };
            panel.Width = 80;
            panel.HorizontalAlignment = HorizontalAlignment.Left;
            Layout(host, 200, 60);
            var last = (FrameworkElement)panel.Children[2];
            ((FrameworkElement)panel.Children[2]).Name = "Item3";
            Rect lastBounds = Bounds(last, host);
            Geometry? clip = LayoutInformation.GetLayoutClip(panel);
            rows.Add([T("Horizontal, the demo's 3 labels, Width 80: last label x / ClipToBounds / layout clip width", "Horizontal、デモの 3 つの Label、Width 80: 最後の Label の x / ClipToBounds / レイアウトのクリップの幅"),
                T($"{D(lastBounds.X)} / {panel.ClipToBounds} / {(clip is null ? "none" : D(clip.Bounds.Width))}", $"{D(lastBounds.X)} / {panel.ClipToBounds} / {(clip is null ? "なし" : D(clip.Bounds.Width))}")]);
            rows.Add([T("  hit test on the last label, outside the 80", "  80 の外にある最後の Label のヒットテスト"), HitName(host, new Point(lastBounds.X + 10, lastBounds.Y + lastBounds.Height / 2))]);
        }

        {
            var panel = new StackPanel();
            var counters = Enumerable.Range(0, 1000).Select(_ => new MeasureCounter()).ToList();
            foreach (MeasureCounter counter in counters)
            {
                panel.Children.Add(counter);
            }

            Layout(new ScrollViewer { Content = panel }, 200, 200);
            rows.Add([T("1000 children in a StackPanel in a ScrollViewer 200 high: children measured", "高さ 200 の ScrollViewer の中の StackPanel に 1000 個の子: 測定された子の数"),
                counters.Count(c => c.MeasureCount > 0).ToString()]);
        }

        foreach (bool inStackPanel in new[] { false, true })
        {
            // 高さ 200 の領域に、1000 項目の ListBox を直接置く場合と、縦の StackPanel に入れて置く場合。
            var list = new ListBox { ItemsSource = Enumerable.Range(1, 1000).Select(i => $"Item {i}").ToList() };
            UIElement content = inStackPanel ? new StackPanel { Children = { list } } : list;
            var host = new Grid { Height = 200, Children = { content } };
            Layout(host, 200, 200);
            var viewer = Descendants(list).OfType<ScrollViewer>().First();
            rows.Add([T($"1000-item ListBox in 200 high, {(inStackPanel ? "inside a vertical StackPanel" : "directly")}: height / ScrollableHeight / items created", $"高さ 200 に 1000 項目の ListBox、{(inStackPanel ? "縦の StackPanel の中" : "直接")}: 高さ / ScrollableHeight / 作られた項目の数"),
                $"{D(list.ActualHeight)} / {D(viewer.ScrollableHeight)} / {Descendants(list).OfType<ListBoxItem>().Count()}"]);
        }

        foreach ((string label, Brush? background) in new (string, Brush?)[] { ("null", null), ("Transparent", Brushes.Transparent), ("AliceBlue", Brushes.AliceBlue) })
        {
            var panel = new StackPanel { Name = "Panel", Height = 30, Width = 200, Background = background };
            var host = new Grid { Children = { panel } };
            Layout(host, 200, 30);
            rows.Add([T($"empty StackPanel 30 high, Background={label}: hit test in the middle", $"高さ 30 の空の StackPanel、Background={label}: 中央のヒットテスト"), HitName(host, new Point(100, 15))]);
        }

        foreach ((int first, int second) in new[] { (1, 2), (3, 2) })
        {
            // デモアプリの ZIndex 欄と同じ 2 つの Label。2 つ目は上の余白 -15 で 1 つ目に重なる。
            var item1 = DemoLabel("Item1", "Item1", first, new Thickness(0), Brushes.LightBlue);
            var item2 = DemoLabel("Item2", "Item2", second, new Thickness(0, -15, 0, 0), Brushes.SkyBlue);
            var panel = new StackPanel { Width = 200, Children = { item1, item2 } };
            Layout(panel, 200, 100);
            Rect overlap = Rect.Intersect(Bounds(item1, panel), Bounds(item2, panel));
            rows.Add([T($"demo labels, ZIndex Item1={first}, Item2={second}: element on top where they overlap", $"デモの Label、ZIndex Item1={first}、Item2={second}: 重なった部分で上にある要素"),
                HitName(panel, new Point(overlap.X + overlap.Width / 2, overlap.Y + overlap.Height / 2))]);
        }

        {
            var inner = DemoLabel("InnerChild", "inner", 100, new Thickness(0), Brushes.LightBlue);
            var innerPanel = new StackPanel { Children = { inner } };
            var sibling = DemoLabel("OuterSibling", "outer", 0, new Thickness(0, -15, 0, 0), Brushes.SkyBlue);
            var outer = new StackPanel { Width = 200, Children = { innerPanel, sibling } };
            Layout(outer, 200, 100);
            Rect overlap = Rect.Intersect(Bounds(inner, outer), Bounds(sibling, outer));
            rows.Add([T("child of an inner panel with ZIndex=100 vs the inner panel's sibling (ZIndex 0): on top", "ZIndex=100 の内側のパネルの子と、内側のパネルの兄弟（ZIndex 0）: 上にあるもの"),
                HitName(outer, new Point(overlap.X + overlap.Width / 2, overlap.Y + overlap.Height / 2))]);
        }

        return rows;
    }
}
