using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Canvas」（apps/wpf-standard-control-demo/canvas.html と日本語版）の記述を実測する。
///
/// 子はデモアプリと同じ 100 x 100 の Rectangle を使う。
/// レイアウトはウィンドウを作らずに Measure / Arrange で行い、Canvas から見た子の矩形を読む。
/// </summary>
internal sealed class CanvasDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-canvas";

    public string ImageDirectory => DemoProbe.ImageDirectory("canvas");

    public IReadOnlyList<string> Verifies =>
    [
        "Canvas が子を測るときに渡す大きさと、Width のない子（TextBlock、HorizontalAlignment=Stretch）の配置後の幅",
        "子を持つ Canvas の DesiredSize と、ClipToBounds の既定値",
        "デモアプリの初期値（Top 20・Left 20）での矩形と、Left と Right、Top と Bottom を両方設定したときに優先される側",
        "Right / Bottom だけを設定したときの位置（Canvas が 300 x 200 に広がる場合と、大きさ 0 の場合）",
        "位置を設定しない子と、負の Left の位置",
        "Canvas の外にはみ出した子のヒットテスト（ClipToBounds が False と True）",
        "デモアプリの 2 つの Rectangle の重なりで、ZIndex による上下と、ZIndex が等しいときの上下",
        "デモアプリと同じ、空文字の TextBox を FallbackValue=NaN で Canvas.Right にバインドしたときの値",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Canvas: sizes, positions, clipping and ZIndex (rectangles 100 x 100)",
            ["case", "measured"],
            Measure(),
            "canvas-behavior.svg");
        await Task.CompletedTask;
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

    private static Rectangle Rect100(string name, Brush fill) =>
        new() { Name = name, Width = 100, Height = 100, Fill = fill };

    private static string At(Canvas canvas, FrameworkElement child) => Format(Bounds(child, canvas));

    /// <summary>Canvas を指定の大きさに広げてレイアウトする。</summary>
    private static Canvas Stretched(double width, double height, params UIElement[] children)
    {
        var canvas = new Canvas();
        foreach (UIElement child in children)
        {
            canvas.Children.Add(child);
        }

        Layout(new Grid { Children = { canvas } }, width, height);
        return canvas;
    }

    private static List<IReadOnlyList<string>> Measure()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            var probe = new SizeProbe();
            var text = new TextBlock { Text = "A TextBlock" };
            var stretch = new Border { HorizontalAlignment = HorizontalAlignment.Stretch, Child = new TextBlock { Text = "Stretch" } };
            Canvas canvas = Stretched(300, 200, probe, text, stretch);
            rows.Add(["size given to a child / width of a TextBlock / width of a Stretch Border (Canvas 300 wide)",
                $"{D(probe.Available.Width)} x {D(probe.Available.Height)} / {D(text.RenderSize.Width)} / {D(stretch.RenderSize.Width)}"]);
        }

        {
            var canvas = new Canvas { Children = { Rect100("A", Brushes.SkyBlue) } };
            Canvas.SetLeft(canvas.Children[0], 20);
            Canvas.SetTop(canvas.Children[0], 20);
            var host = new StackPanel { Orientation = Orientation.Horizontal, Children = { canvas } };
            Layout(host, 300, 200);
            rows.Add(["Canvas with a child at (20, 20), in a horizontal StackPanel: DesiredSize / ClipToBounds default",
                $"{D(canvas.DesiredSize.Width)} x {D(canvas.DesiredSize.Height)} / {canvas.ClipToBounds}"]);
        }

        {
            Rectangle a = Rect100("A", Brushes.SkyBlue);
            Canvas.SetTop(a, 20);
            Canvas.SetLeft(a, 20);
            Canvas canvas = Stretched(300, 200, a);
            rows.Add(["demo's initial values Top 20, Left 20", At(canvas, a)]);
        }

        {
            Rectangle a = Rect100("A", Brushes.SkyBlue);
            Canvas.SetLeft(a, 20);
            Canvas.SetRight(a, 20);
            Canvas.SetTop(a, 20);
            Canvas.SetBottom(a, 20);
            Canvas canvas = Stretched(300, 200, a);
            rows.Add(["Left 20 and Right 20, Top 20 and Bottom 20 (Canvas 300 x 200)", At(canvas, a)]);
        }

        {
            Rectangle a = Rect100("A", Brushes.SkyBlue);
            Canvas.SetRight(a, 20);
            Canvas.SetBottom(a, 20);
            Canvas canvas = Stretched(300, 200, a);
            rows.Add(["only Right 20, Bottom 20, Canvas stretched to 300 x 200 (no Width set)", At(canvas, a)]);
        }

        {
            Rectangle a = Rect100("A", Brushes.SkyBlue);
            Canvas.SetRight(a, 20);
            Canvas.SetBottom(a, 20);
            var canvas = new Canvas { Children = { a } };
            var host = new StackPanel { Orientation = Orientation.Horizontal, Children = { new StackPanel { Children = { canvas } } } };
            Layout(host, 300, 200);
            rows.Add(["  same in a Canvas of size 0 (inside StackPanels)", At(canvas, a)]);
        }

        {
            Rectangle plain = Rect100("Plain", Brushes.SkyBlue);
            Rectangle negative = Rect100("Negative", Brushes.LightCoral);
            Canvas.SetLeft(negative, -30);
            Canvas.SetTop(negative, 150);
            Canvas canvas = Stretched(300, 300, plain, negative);
            rows.Add(["no position set / Left -30", $"{At(canvas, plain)} / {At(canvas, negative)}"]);
        }

        foreach (bool clip in new[] { false, true })
        {
            Rectangle a = Rect100("A", Brushes.SkyBlue);
            Canvas.SetLeft(a, 150);
            var canvas = new Canvas { Name = "Canvas", Width = 200, Height = 100, ClipToBounds = clip, Children = { a }, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var host = new Grid { Children = { canvas } };
            Layout(host, 400, 200);
            rows.Add([$"Canvas 200 wide, child at Left 150 (to 250), ClipToBounds={clip}: hit test at x=230",
                HitName(host, new Point(230, 50))]);
        }

        foreach ((int zA, int zB) in new[] { (0, 1), (2, 1), (0, 0) })
        {
            // デモアプリの 2 つの Rectangle。A は (20, 20)、B は (50, 50)。
            Rectangle a = Rect100("A", Brushes.SkyBlue);
            Rectangle b = Rect100("B", Brushes.LightCoral);
            Canvas.SetLeft(a, 20);
            Canvas.SetTop(a, 20);
            Canvas.SetLeft(b, 50);
            Canvas.SetTop(b, 50);
            Panel.SetZIndex(a, zA);
            Panel.SetZIndex(b, zB);
            Canvas canvas = Stretched(300, 200, a, b);
            rows.Add([$"demo's rectangles A (20, 20) and B (50, 50), ZIndex A={zA}, B={zB}: on top at (85, 85)",
                HitName(canvas, new Point(85, 85))]);
        }

        {
            var text = new TextBox { Text = "" };
            Rectangle a = Rect100("A", Brushes.SkyBlue);
            BindingOperations.SetBinding(a, Canvas.RightProperty,
                new Binding(nameof(TextBox.Text)) { Source = text, TargetNullValue = double.NaN, FallbackValue = double.NaN });
            string empty = D(Canvas.GetRight(a));
            text.Text = "30";
            rows.Add(["demo binding with FallbackValue=NaN: Canvas.Right for text \"\" / \"30\"", $"{empty} / {D(Canvas.GetRight(a))}"]);
        }

        return rows;
    }
}
