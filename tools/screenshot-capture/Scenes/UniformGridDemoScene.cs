using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「UniformGrid」（apps/wpf-standard-control-demo/uniformgrid.md と日本語版）の記述を実測する。
///
/// 子はデモアプリと同じ、枠 1 の Label を使う。
/// レイアウトはウィンドウを作らずに Measure / Arrange で行い、子の矩形から行と列を読む。
/// </summary>
internal sealed class UniformGridDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-uniformgrid";

    public string ImageDirectory => DemoProbe.ImageDirectory("uniformgrid");

    public IReadOnlyList<string> Verifies =>
    [
        "UniformGrid の名前空間と、Rows / Columns / FirstColumn の既定値",
        "デモアプリの 5 つの Label で、Columns だけ、Rows だけ、どちらも設定しない場合の行数・列数とセルの大きさ",
        "Rows と Columns の積より子が多いときの、はみ出した子の位置",
        "デモアプリの FirstColumn の欄（FirstColumn=1、Columns=3、9 項目）での子の位置と行数、FirstColumn が Columns 以上のときの配置とレイアウト後の FirstColumn の値、テキストボックスにバインドしたときにバインドが残るか",
        "Collapsed の子がセルを使うか",
        "幅 150 の子があるときのセルの幅（幅 300 に広げた場合と、大きさを子に合わせる場合）",
        "Background が null / Transparent のときの、空いたセルのヒットテスト",
        "デモアプリと同じ重なり（2 つ目の Label の上余白 -15）で、ZIndex による上下",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "UniformGrid: rows, columns, FirstColumn and cell size (the demo's labels)",
            ["case", "measured"],
            Measure(),
            "uniformgrid-behavior.svg");
        await Task.CompletedTask;
    }

    private static UniformGrid DemoGrid(int count)
    {
        var grid = new UniformGrid();
        for (int i = 1; i <= count; i++)
        {
            grid.Children.Add(new Label { Name = $"Item{i}", Content = $"Item{i}", BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) });
        }

        return grid;
    }

    /// <summary>子の矩形から行数と列数を数え、セルの大きさを添える。</summary>
    private static string Shape(UniformGrid grid)
    {
        var rects = grid.Children.Cast<FrameworkElement>()
            .Where(c => c.Visibility == Visibility.Visible)
            .Select(c => Bounds(c, grid)).ToList();
        int rows = rects.Select(r => Math.Round(r.Y, 2)).Distinct().Count();
        int columns = rects.Select(r => Math.Round(r.X, 2)).Distinct().Count();
        return $"{rows} rows x {columns} columns, cell {D(rects[0].Width)} x {D(rects[0].Height)}";
    }

    private static string Cell(UniformGrid grid, int index)
    {
        Rect r = Bounds((FrameworkElement)grid.Children[index], grid);
        return $"({D(r.X)}, {D(r.Y)})";
    }

    private static UniformGrid Laid(UniformGrid grid, double width = 300, double height = 200)
    {
        Layout(new Grid { Children = { grid } }, width, height);
        return grid;
    }

    private static List<IReadOnlyList<string>> Measure()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new UniformGrid();
        rows.Add(["namespace / defaults: Rows, Columns, FirstColumn",
            $"{typeof(UniformGrid).Namespace} / {defaults.Rows}, {defaults.Columns}, {defaults.FirstColumn}"]);

        foreach ((string label, int r, int c) in new[] { ("Columns=1 (demo start)", 0, 1), ("Columns=2", 0, 2), ("Rows=1 (demo start)", 1, 0), ("Rows=2", 2, 0), ("neither set", 0, 0) })
        {
            UniformGrid grid = DemoGrid(5);
            grid.Rows = r;
            grid.Columns = c;
            rows.Add([$"5 labels, 300 x 200, {label}", Shape(Laid(grid))]);
        }

        {
            UniformGrid grid = DemoGrid(5);
            grid.Rows = 2;
            grid.Columns = 2;
            Laid(grid);
            rows.Add(["5 labels, Rows=2, Columns=2 (4 cells), 300 x 200: 4th / 5th label at",
                $"{Cell(grid, 3)} / {Cell(grid, 4)}"]);
        }

        {
            UniformGrid grid = DemoGrid(9);
            grid.Columns = 3;
            grid.FirstColumn = 1;
            Laid(grid);
            rows.Add(["demo FirstColumn=1, Columns=3, 9 labels: Item1 / Item3 at, shape",
                $"{Cell(grid, 0)} / {Cell(grid, 2)}, {Shape(grid).Split(',')[0]}"]);
        }

        foreach (int first in new[] { 3, 4 })
        {
            UniformGrid grid = DemoGrid(9);
            grid.Columns = 3;
            grid.FirstColumn = first;
            Laid(grid);
            rows.Add([$"FirstColumn={first}, Columns=3: Item1 at, shape; FirstColumn after layout",
                $"{Cell(grid, 0)}, {Shape(grid).Split(',')[0]}; {grid.FirstColumn}"]);
        }

        {
            // デモアプリと同じく、FirstColumn をテキストボックスにバインドする。レイアウトで 0 に戻されたとき、バインドが残るか。
            var box = new TextBox { Text = "4" };
            UniformGrid grid = DemoGrid(9);
            grid.Columns = 3;
            grid.SetBinding(UniformGrid.FirstColumnProperty, new Binding(nameof(TextBox.Text)) { Source = box });
            Laid(grid);
            string after = $"{grid.FirstColumn}, binding {(BindingOperations.GetBindingExpression(grid, UniformGrid.FirstColumnProperty) is null ? "removed" : "kept")}";
            box.Text = "1";
            grid.UpdateLayout();
            rows.Add(["bound to text \"4\" (demo): FirstColumn, binding; text then \"1\": FirstColumn, Item1 at",
                $"{after}; {grid.FirstColumn}, {Cell(grid, 0)}"]);
        }

        {
            UniformGrid grid = DemoGrid(5);
            grid.Columns = 2;
            grid.Children[1].Visibility = Visibility.Collapsed;
            Laid(grid);
            rows.Add(["5 labels, Columns=2, Item2 Collapsed: Item3 at / shape", $"{Cell(grid, 2)} / {Shape(grid)}"]);
        }

        {
            UniformGrid grid = DemoGrid(2);
            grid.Columns = 3;
            grid.Children.Add(new Border { Name = "Wide", Width = 150, Height = 20 });
            Laid(grid, 300, 100);
            string stretched = Shape(grid);
            UniformGrid auto = DemoGrid(2);
            auto.Columns = 3;
            auto.Children.Add(new Border { Name = "Wide", Width = 150, Height = 20 });
            var host = new StackPanel { Orientation = Orientation.Horizontal, Children = { auto } };
            Layout(host, 600, 100);
            rows.Add(["Columns=3 with a child 150 wide: stretched to 300 / sized to content",
                $"{stretched.Split(", cell ")[1]} / {Shape(auto).Split(", cell ")[1]}"]);
        }

        foreach ((string label, Brush? background) in new (string, Brush?)[] { ("null", null), ("Transparent", Brushes.Transparent) })
        {
            UniformGrid grid = DemoGrid(1);
            grid.Name = "Grid";
            grid.Columns = 2;
            grid.Background = background;
            var host = new Grid { Children = { grid } };
            Layout(host, 300, 60);
            rows.Add([$"Columns=2 with one label, Background={label}: hit test in the empty cell", HitName(host, new Point(225, 30))]);
        }

        foreach ((int first, int second) in new[] { (1, 2), (3, 2) })
        {
            UniformGrid grid = DemoGrid(2);
            grid.Columns = 1;
            var item1 = (Label)grid.Children[0];
            var item2 = (Label)grid.Children[1];
            item1.Background = Brushes.LightBlue;
            item2.Background = Brushes.SkyBlue;
            item2.Margin = new Thickness(0, -15, 0, 0);
            Panel.SetZIndex(item1, first);
            Panel.SetZIndex(item2, second);
            Laid(grid, 300, 60);
            Rect overlap = Rect.Intersect(Bounds(item1, grid), Bounds(item2, grid));
            rows.Add([$"demo ZIndex labels (Item2 top margin -15), ZIndex {first} and {second}: on top",
                HitName(grid, new Point(overlap.X + overlap.Width / 2, overlap.Y + overlap.Height / 2))]);
        }

        return rows;
    }
}
