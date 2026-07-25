using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture;

/// <summary>
/// 「記述したマークアップ → 実際の描画結果」を並べる図の共通レイアウト。
/// 図の中の文言は言語に依存しないよう、コードと矢印だけで構成する。
/// </summary>
internal static class DemoLayout
{
    private static readonly FontFamily CodeFont = new("Consolas, Courier New, monospace");
    private static readonly Brush CodeBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x3D, 0x4D));
    private static readonly Brush ArrowBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x93, 0xA3));
    private static readonly Brush FrameBrush = new SolidColorBrush(Color.FromRgb(0xC3, 0xCC, 0xDB));

    /// <summary>マークアップ 1 行と、その描画結果 1 つの組。</summary>
    internal sealed record Row(string Markup, UIElement Rendered);

    /// <summary>
    /// 「マークアップ → 描画結果」の行を縦に並べたウィンドウを作る。
    /// </summary>
    public static Window BuildComparisonWindow(string title, IEnumerable<Row> rows)
    {
        var grid = new Grid { Margin = new Thickness(18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        int index = 0;
        foreach (Row row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var markup = new TextBlock
            {
                Text = row.Markup,
                FontFamily = CodeFont,
                FontSize = 13,
                Foreground = CodeBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, index == 0 ? 0 : 10, 0, 0),
            };
            Grid.SetRow(markup, index);
            Grid.SetColumn(markup, 0);
            grid.Children.Add(markup);

            var arrow = new TextBlock
            {
                Text = "→",
                FontSize = 15,
                Foreground = ArrowBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, index == 0 ? 0 : 10, 16, 0),
            };
            Grid.SetRow(arrow, index);
            Grid.SetColumn(arrow, 1);
            grid.Children.Add(arrow);

            var frame = new Border
            {
                BorderBrush = FrameBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 190,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, index == 0 ? 0 : 10, 0, 0),
                Child = row.Rendered,
            };
            Grid.SetRow(frame, index);
            Grid.SetColumn(frame, 2);
            grid.Children.Add(frame);

            index++;
        }

        return new Window
        {
            Title = title,
            Content = grid,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };
    }
}
