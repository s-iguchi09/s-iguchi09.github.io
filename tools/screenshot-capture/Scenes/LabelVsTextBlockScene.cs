using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針」の図。
/// 同じ文字列を同じ個数だけ並べ、visual tree の要素数とレイアウト時間を実測して表にする。
/// </summary>
internal sealed class LabelVsTextBlockScene : IScene
{
    private const int ItemCount = 1000;

    /// <summary>計測のばらつきを避けるため複数回実行し、最小値を採る。</summary>
    private const int Iterations = 7;

    public string Slug => "wpf-label-vs-textblock-performance";

    public async Task CaptureAsync(SceneContext context)
    {
        Measurement label = Measure(() => new Label { Content = "Status: Running", Padding = new Thickness(0) });
        Measurement textBlock = Measure(() => new TextBlock { Text = "Status: Running" });

        Window window = BuildResultWindow(label, textBlock);
        await context.ShootAsync(window, "label-vs-textblock-measurement.png");
    }

    private readonly record struct Measurement(int VisualCount, double LayoutMilliseconds);

    /// <summary>
    /// 要素を <see cref="ItemCount"/> 個並べ、レイアウト完了までの時間と
    /// 生成された visual の総数を測る。
    /// </summary>
    private static Measurement Measure(Func<FrameworkElement> createItem)
    {
        double best = double.MaxValue;
        int visualCount = 0;

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            var panel = new StackPanel();
            var host = new Border { Width = 400, Height = 600, Child = panel };

            for (int i = 0; i < ItemCount; i++)
            {
                panel.Children.Add(createItem());
            }

            var stopwatch = Stopwatch.StartNew();
            host.Measure(new Size(400, 600));
            host.Arrange(new Rect(0, 0, 400, 600));
            host.UpdateLayout();
            stopwatch.Stop();

            best = Math.Min(best, stopwatch.Elapsed.TotalMilliseconds);
            visualCount = CountVisuals(host);
        }

        return new Measurement(visualCount, best);
    }

    private static int CountVisuals(DependencyObject root)
    {
        int count = 1;
        int children = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
        {
            count += CountVisuals(VisualTreeHelper.GetChild(root, i));
        }

        return count;
    }

    /// <summary>
    /// 計測結果を表として描画するウィンドウ。表記はコードと数値だけにする。
    /// </summary>
    private static Window BuildResultWindow(Measurement label, Measurement textBlock)
    {
        var grid = new Grid { Margin = new Thickness(18) };
        for (int i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (int i = 0; i < 3; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        AddCell(grid, 0, 0, $"x {ItemCount}", header: true);
        AddCell(grid, 0, 1, "visual elements", header: true);
        AddCell(grid, 0, 2, "layout (ms)", header: true);

        AddCell(grid, 1, 0, "Label");
        AddCell(grid, 1, 1, label.VisualCount.ToString("N0"));
        AddCell(grid, 1, 2, label.LayoutMilliseconds.ToString("F1"));

        AddCell(grid, 2, 0, "TextBlock");
        AddCell(grid, 2, 1, textBlock.VisualCount.ToString("N0"));
        AddCell(grid, 2, 2, textBlock.LayoutMilliseconds.ToString("F1"));

        return new Window
        {
            Title = "Label vs TextBlock",
            Content = grid,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };
    }

    private static void AddCell(Grid grid, int row, int column, string text, bool header = false)
    {
        var cell = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC3, 0xCC, 0xDB)),
            BorderThickness = new Thickness(
                column == 0 ? 1 : 0,
                row == 0 ? 1 : 0,
                1,
                1),
            Background = header
                ? new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFB))
                : Brushes.White,
            Padding = new Thickness(14, 7, 14, 7),
            MinWidth = column == 0 ? 110 : 130,
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33)),
                HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            },
        };

        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}
