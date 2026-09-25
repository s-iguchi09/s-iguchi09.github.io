using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「DockPanel」（apps/wpf-standard-control-demo/dockpanel.md と日本語版）の記述を実測する。
///
/// 子はデモアプリと同じ、枠 1 の Label を使う。
/// レイアウトはウィンドウを作らずに Measure / Arrange で行い、子の矩形を読む。
/// </summary>
internal sealed class DockPanelDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-dockpanel";

    public string ImageDirectory => DemoProbe.ImageDirectory("dockpanel");

    public IReadOnlyList<string> Verifies =>
    [
        "LastChildFill の既定値と、DockPanel.Dock を設定しない子の既定値",
        "デモアプリの 2 つの Label で LastChildFill が False と True のときの矩形",
        "LastChildFill が True のとき、最後の子に設定した Dock=Top が無視されるか",
        "デモアプリの 1 つの Label（LastChildFill=False、高さ 100）を Left / Top / Right / Bottom に置いたときの矩形",
        "上・下・左・内容の順に置いた場合と、左を先に置いた場合の、左の子の矩形",
        "LastChildFill=False で子の覆わない場所の、Background が null と色のときのヒットテスト",
        "デモアプリと同じ重なり（2 つ目の Label の左余白 -30）で、ZIndex を入れ替えたときに上になる要素",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "DockPanel: docking, LastChildFill, order, background and ZIndex (panel 300 x 100)",
            ["case", "measured"],
            Measure(),
            "dockpanel-behavior.svg");
        await Task.CompletedTask;
    }

    private static Label DemoLabel(string name, Brush background, Dock? dock = null)
    {
        var label = new Label
        {
            Name = name,
            Content = name,
            Background = background,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
        };
        if (dock is { } value)
        {
            DockPanel.SetDock(label, value);
        }

        return label;
    }

    private static List<IReadOnlyList<string>> Measure()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["defaults: LastChildFill / Dock of a child",
            $"{new DockPanel().LastChildFill} / {DockPanel.GetDock(new Label())}"]);

        foreach (bool fill in new[] { false, true })
        {
            Label item1 = DemoLabel("Item1", Brushes.LightBlue);
            Label item2 = DemoLabel("Item2", Brushes.SkyBlue);
            var panel = new DockPanel { LastChildFill = fill, Children = { item1, item2 } };
            Layout(panel, 300, 100);
            rows.Add([$"demo's two labels, LastChildFill={fill}: Item1 / Item2",
                $"{Format(Bounds(item1, panel))} / {Format(Bounds(item2, panel))}"]);
        }

        {
            Label item1 = DemoLabel("Item1", Brushes.LightBlue);
            Label item2 = DemoLabel("Item2", Brushes.SkyBlue, Dock.Top);
            var panel = new DockPanel { LastChildFill = true, Children = { item1, item2 } };
            Layout(panel, 300, 100);
            rows.Add(["LastChildFill=True, last label with Dock=Top: its rect", Format(Bounds(item2, panel))]);
        }

        foreach (Dock dock in new[] { Dock.Left, Dock.Top, Dock.Right, Dock.Bottom })
        {
            Label item = DemoLabel("Item1", Brushes.LightBlue, dock);
            var panel = new DockPanel { LastChildFill = false, Height = 100, Children = { item } };
            Layout(panel, 300, 100);
            rows.Add([$"demo's single label, LastChildFill=False, Dock={dock}", Format(Bounds(item, panel))]);
        }

        foreach (bool leftFirst in new[] { false, true })
        {
            Label top = DemoLabel("Top", Brushes.LightBlue, Dock.Top);
            Label bottom = DemoLabel("Bottom", Brushes.LightBlue, Dock.Bottom);
            Label left = DemoLabel("Left", Brushes.SkyBlue, Dock.Left);
            Label content = DemoLabel("Content", Brushes.White);
            var panel = new DockPanel();
            foreach (Label child in leftFirst ? new[] { left, top, bottom, content } : new[] { top, bottom, left, content })
            {
                panel.Children.Add(child);
            }

            Layout(panel, 300, 100);
            rows.Add([$"{(leftFirst ? "Left, Top, Bottom, content" : "Top, Bottom, Left, content")}: Left / content",
                $"{Format(Bounds(left, panel))} / {Format(Bounds(content, panel))}"]);
        }

        foreach ((string label, Brush? background) in new (string, Brush?)[] { ("null", null), ("AliceBlue", Brushes.AliceBlue) })
        {
            Label item = DemoLabel("Item1", Brushes.LightBlue, Dock.Left);
            var panel = new DockPanel { Name = "Panel", LastChildFill = false, Background = background, Children = { item } };
            var host = new Grid { Children = { panel } };
            Layout(host, 300, 100);
            rows.Add([$"LastChildFill=False, Background={label}: hit test in the empty area", HitName(host, new Point(250, 50))]);
        }

        foreach ((int first, int second) in new[] { (1, 2), (3, 2) })
        {
            Label item1 = DemoLabel("Item1", Brushes.LightBlue);
            Label item2 = DemoLabel("Item2", Brushes.SkyBlue);
            item2.Margin = new Thickness(-30, 0, 0, 0);
            Panel.SetZIndex(item1, first);
            Panel.SetZIndex(item2, second);
            var panel = new DockPanel { Children = { item1, item2 } };
            Layout(panel, 300, 100);
            Rect overlap = Rect.Intersect(Bounds(item1, panel), Bounds(item2, panel));
            rows.Add([$"demo's ZIndex labels (Item2 margin -30), ZIndex {first} and {second}: on top where they overlap",
                HitName(panel, new Point(overlap.X + overlap.Width / 2, overlap.Y + overlap.Height / 2))]);
        }

        return rows;
    }
}
