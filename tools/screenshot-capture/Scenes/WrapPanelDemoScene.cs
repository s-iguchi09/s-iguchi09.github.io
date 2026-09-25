using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「WrapPanel」（apps/wpf-standard-control-demo/wrappanel.md と日本語版）の記述を実測する。
///
/// 子はデモアプリと同じ、余白 2・枠 1 の Label を使う。
/// レイアウトはウィンドウを作らずに Measure / Arrange で行い、子の配置を読む。
/// </summary>
internal sealed class WrapPanelDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-wrappanel";

    public string ImageDirectory => DemoProbe.ImageDirectory("wrappanel");

    public IReadOnlyList<string> Verifies =>
    [
        "Orientation / ItemWidth / ItemHeight の既定値",
        "デモアプリの 5 つの Label を、幅 150 の横と高さ 60 の縦の WrapPanel に置いたときの行（列）の数と位置",
        "高さ 20 と 40 の子が同じ行にあるときの行の高さと、低い子の配置（VerticalAlignment が Stretch と Top）",
        "ItemWidth=100（デモアプリの初期値）で、デモアプリの Label と幅 150 の子に割り当てられる幅と、はみ出した部分の切り抜き",
        "ItemHeight=100（デモアプリの初期値）での Label の高さと 2 行目の位置",
        "横スクロールのできる ScrollViewer と、既定の ScrollViewer の中での折り返し",
        "Background が null / Transparent のときの、子と子の間のヒットテスト",
        "重なった 2 つの子の ZIndex による上下",
        "ListBox の ItemsPanel を WrapPanel にしたときに作られる項目の数（1000 項目）",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "WrapPanel: wrapping, line size, ItemWidth and ItemHeight",
            ["case", "measured"],
            Measure(),
            "wrappanel-behavior.svg");
        await Task.CompletedTask;
    }

    private static Label DemoLabel(int number) => new()
    {
        Name = $"Item{number}",
        Content = $"Item{number}",
        Margin = new Thickness(2),
        BorderBrush = Brushes.Black,
        BorderThickness = new Thickness(1),
    };

    private static WrapPanel DemoPanel(int count, Orientation orientation = Orientation.Horizontal)
    {
        var panel = new WrapPanel { Orientation = orientation };
        for (int i = 1; i <= count; i++)
        {
            panel.Children.Add(DemoLabel(i));
        }

        return panel;
    }

    /// <summary>子の位置から行（列）を数え、各行（列）の子の数を並べる。</summary>
    private static string Lines(WrapPanel panel, bool byRow)
    {
        var counts = panel.Children.Cast<FrameworkElement>()
            .Select(c => Bounds(c, panel))
            .GroupBy(p => byRow ? p.Y : p.X)
            .Select(g => g.Count().ToString())
            .ToList();
        return $"{counts.Count} {(byRow ? "rows" : "columns")} of {string.Join(", ", counts)}";
    }

    private static List<IReadOnlyList<string>> Measure()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new WrapPanel();
        rows.Add(["defaults: Orientation / ItemWidth / ItemHeight",
            $"{defaults.Orientation} / {D(defaults.ItemWidth)} / {D(defaults.ItemHeight)}"]);

        {
            WrapPanel panel = DemoPanel(5);
            Layout(new Grid { Children = { panel } }, 150, 200);
            rows.Add(["the demo's 5 labels, Horizontal, 150 wide", Lines(panel, byRow: true)]);
        }

        {
            WrapPanel panel = DemoPanel(5, Orientation.Vertical);
            Layout(new Grid { Children = { panel } }, 300, 60);
            rows.Add(["the demo's 5 labels, Vertical, 60 high", Lines(panel, byRow: false)]);
        }

        foreach (VerticalAlignment alignment in new[] { VerticalAlignment.Stretch, VerticalAlignment.Top })
        {
            var low = new Border { Width = 40, Height = double.NaN, MinHeight = 20, VerticalAlignment = alignment };
            var lowContent = new MeasureCounter { Content = new Size(40, 20) };
            low.Child = lowContent;
            var high = new Border { Width = 40, Height = 40 };
            var panel = new WrapPanel { Children = { low, high } };
            Layout(new Grid { Children = { panel } }, 200, 100);
            rows.Add([$"children 20 and 40 high in a row, lower one {alignment}: its height",
                D(low.RenderSize.Height)]);
        }

        {
            WrapPanel panel = DemoPanel(3);
            panel.ItemWidth = 100;
            var wide = new Border { Name = "Wide", Width = 150, Height = 20, Background = Brushes.Gray };
            panel.Children.Add(wide);
            Layout(new Grid { Children = { panel } }, 400, 200);
            var label = (FrameworkElement)panel.Children[0];
            Geometry? clip = LayoutInformation.GetLayoutClip(wide);
            Rect wideBounds = Bounds(wide, panel);
            rows.Add(["ItemWidth=100: demo label width / 150-wide child: x, layout clip",
                $"{D(label.RenderSize.Width)} / {D(wideBounds.X)}, {(clip is null ? "none" : D(clip.Bounds.Width))}"]);
            rows.Add(["  hit test on the 150-wide child, 20 past its 100",
                HitName(panel, new Point(wideBounds.X + 120, wideBounds.Y + 10))]);
        }

        {
            WrapPanel panel = DemoPanel(5);
            panel.ItemHeight = 100;
            Layout(new Grid { Children = { panel } }, 150, 300);
            var label = (FrameworkElement)panel.Children[0];
            var fourth = (FrameworkElement)panel.Children[3];
            rows.Add(["ItemHeight=100, 5 labels, 150 wide: label height / second row starts at y / its first label at y",
                $"{D(label.RenderSize.Height)} / {D(LayoutInformation.GetLayoutSlot(fourth).Y)} / {D(Bounds(fourth, panel).Y)}"]);
        }

        foreach ((string label, ScrollBarVisibility horizontal) in new[] { ("ScrollViewer, horizontal Disabled (default)", ScrollBarVisibility.Disabled), ("ScrollViewer, horizontal Auto", ScrollBarVisibility.Auto) })
        {
            WrapPanel panel = DemoPanel(5);
            var viewer = new ScrollViewer { HorizontalScrollBarVisibility = horizontal, Content = panel };
            Layout(viewer, 150, 200);
            rows.Add([$"5 labels in a {label}, 150 wide", Lines(panel, byRow: true)]);
        }

        foreach ((string label, Brush? background) in new (string, Brush?)[] { ("null", null), ("Transparent", Brushes.Transparent) })
        {
            WrapPanel panel = DemoPanel(2);
            panel.Name = "Panel";
            panel.Background = background;
            var host = new Grid { Children = { panel } };
            Layout(host, 300, 60);
            Rect first = Bounds((FrameworkElement)panel.Children[0], host);
            // 1 つ目の子の右の余白（Margin 2 の内側）を突く。
            rows.Add([$"Background={label}: hit test in the gap between two labels",
                HitName(host, new Point(first.Right + 1, first.Y + first.Height / 2))]);
        }

        foreach ((int first, int second) in new[] { (1, 2), (3, 2) })
        {
            Label item1 = DemoLabel(1);
            Label item2 = DemoLabel(2);
            item1.Background = Brushes.LightBlue;
            item2.Background = Brushes.SkyBlue;
            item2.Margin = new Thickness(-15, 2, 2, 2);
            Panel.SetZIndex(item1, first);
            Panel.SetZIndex(item2, second);
            var panel = new WrapPanel { Children = { item1, item2 } };
            Layout(new Grid { Children = { panel } }, 300, 60);
            Rect overlap = Rect.Intersect(Bounds(item1, panel), Bounds(item2, panel));
            rows.Add([$"two labels overlapping by 15, ZIndex {first} and {second}: on top",
                HitName(panel, new Point(overlap.X + overlap.Width / 2, overlap.Y + overlap.Height / 2))]);
        }

        {
            var list = new ListBox
            {
                Width = 300,
                Height = 200,
                ItemsSource = Enumerable.Range(1, 1000).Select(i => $"Item {i}").ToList(),
                ItemsPanel = (ItemsPanelTemplate)XamlReader.Parse(
                    """
                    <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                      <WrapPanel />
                    </ItemsPanelTemplate>
                    """),
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            Layout(new Grid { Children = { list } }, 300, 200);
            rows.Add(["ListBox 300 x 200, WrapPanel as ItemsPanel, 1000 items: items created",
                Descendants(list).OfType<ListBoxItem>().Count().ToString()]);
        }

        return rows;
    }
}
