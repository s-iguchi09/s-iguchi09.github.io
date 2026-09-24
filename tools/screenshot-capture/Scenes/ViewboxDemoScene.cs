using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Viewbox」（apps/wpf-standard-control-demo/viewbox.html と日本語版）の記述を実測する。
///
/// 子はデモアプリと同じ、Padding 20・枠 1 の Label を使う。
/// 拡大率は、子の本来の大きさ（ActualWidth / ActualHeight）と、Viewbox から見た子の矩形の比で読む。
/// </summary>
internal sealed class ViewboxDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-viewbox";

    public string ImageDirectory => DemoProbe.ImageDirectory("viewbox");

    public IReadOnlyList<string> Verifies =>
    [
        "Viewbox の基底クラスと Stretch / StretchDirection の既定値、デモアプリのコンボボックスの初期値（先頭の項目）",
        "Viewbox が子を測るときに渡す大きさと、子の本来の大きさ",
        "Stretch 4 種類と StretchDirection 3 種類の組み合わせごとの、横長の領域（300 x 100）と小さい領域（40 x 20）での拡大率",
        "UniformToFill で領域からはみ出した部分が切れるか（ヒットテスト）",
        "大きさを内容に合わせる親（横の StackPanel、高さ 100）の中での拡大率",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Viewbox: scale of the demo's label (x, y) by Stretch and StretchDirection",
            ["Stretch", "area", "UpOnly", "DownOnly", "Both"],
            MatrixRows(),
            "viewbox-matrix.svg");

        await context.SaveTableAsync(
            "Viewbox: type, measuring, clipping and unconstrained parents",
            ["case", "measured"],
            OtherRows(),
            "viewbox-behavior.svg");
        await Task.CompletedTask;
    }

    private static Label DemoLabel() => new()
    {
        Name = "Item1",
        Content = "Item1",
        Padding = new Thickness(20),
        BorderBrush = Brushes.Black,
        BorderThickness = new Thickness(1),
    };

    private static string Scale(Viewbox box, Label label)
    {
        Rect r = Bounds(label, box);
        return $"{D(r.Width / label.ActualWidth)}, {D(r.Height / label.ActualHeight)}";
    }

    private static (Viewbox Box, Label Label) Laid(Stretch stretch, StretchDirection direction, double width, double height)
    {
        Label label = DemoLabel();
        var box = new Viewbox { Stretch = stretch, StretchDirection = direction, Child = label };
        Layout(new Grid { Children = { box } }, width, height);
        return (box, label);
    }

    private static List<IReadOnlyList<string>> MatrixRows()
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (Stretch stretch in new[] { Stretch.None, Stretch.Fill, Stretch.Uniform, Stretch.UniformToFill })
        {
            foreach ((double w, double h) in new[] { (300d, 100d), (40d, 20d) })
            {
                var cells = new List<string> { stretch.ToString(), $"{D(w)} x {D(h)}" };
                foreach (StretchDirection direction in new[] { StretchDirection.UpOnly, StretchDirection.DownOnly, StretchDirection.Both })
                {
                    (Viewbox box, Label label) = Laid(stretch, direction, w, h);
                    cells.Add(Scale(box, label));
                }

                rows.Add(cells);
            }
        }

        return rows;
    }

    private sealed class SizeProbe : FrameworkElement
    {
        public Size Available { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Available = availableSize;
            return new Size(40, 20);
        }
    }

    private static List<IReadOnlyList<string>> OtherRows()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new Viewbox();
        rows.Add(["base class / defaults: Stretch, StretchDirection",
            $"{typeof(Viewbox).BaseType!.Name} / {defaults.Stretch}, {defaults.StretchDirection}"]);
        rows.Add(["first item of the demo's combo boxes (enum order): Stretch / StretchDirection",
            $"{Enum.GetValues<Stretch>()[0]} / {Enum.GetValues<StretchDirection>()[0]}"]);

        {
            var probe = new SizeProbe();
            var box = new Viewbox { Child = probe };
            Layout(new Grid { Children = { box } }, 300, 100);
            (Viewbox _, Label label) = Laid(Stretch.Uniform, StretchDirection.Both, 300, 100);
            rows.Add(["size given to the child in a 300 x 100 Viewbox / natural size of the demo's label",
                $"{D(probe.Available.Width)} x {D(probe.Available.Height)} / {D(label.ActualWidth)} x {D(label.ActualHeight)}"]);
        }

        {
            Label label = DemoLabel();
            var box = new Viewbox { Name = "Box", Stretch = Stretch.UniformToFill, Child = label, Width = 300, Height = 100 };
            var host = new Grid { Width = 600, Height = 300, Children = { box } };
            Layout(host, 600, 300);
            Rect labelRect = Bounds(label, host);

            // Viewbox のレイアウト上の枠（幅 300・高さ 100）を基準にする。RenderSize は拡大後の子に合わせて変わりうるため使わない。
            Point origin = box.TranslatePoint(new Point(0, 0), host);
            Geometry? clip = LayoutInformation.GetLayoutClip(box);

            // 子のうち、Viewbox のレイアウト上の高さ 100 より 5 下にある部分を突く。
            var outside = new Point(origin.X + box.Width / 2, origin.Y + box.Height + 5);
            rows.Add(["UniformToFill 300 x 100: label rect / Viewbox clip / hit 5 below height 100",
                $"{Format(labelRect)} / {(clip is null ? "none" : $"{D(clip.Bounds.Width)} x {D(clip.Bounds.Height)}")} / {HitName(host, outside)}"]);
        }

        {
            Label label = DemoLabel();
            var box = new Viewbox { Child = label };
            var host = new StackPanel { Orientation = Orientation.Horizontal, Height = 100, Children = { box } };
            Layout(host, 600, 100);
            rows.Add(["Uniform, inside a horizontal StackPanel 100 high: scale", Scale(box, label)]);
        }

        return rows;
    }
}
