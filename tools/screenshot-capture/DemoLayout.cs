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

    /// <summary>見出し（コード表記）と、その状態を示す UI の組。</summary>
    internal sealed record Panel(string Caption, UIElement Content);

    /// <summary>式と、それを実際に評価した結果の並び。</summary>
    internal sealed record Sequence(string Expression, IReadOnlyList<string> Cells);

    private static readonly Brush CellFill = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFB));

    /// <summary>
    /// 「式 → 評価結果の並び」を縦に並べたウィンドウを作る。
    /// 結果は実際にメソッドを実行して得た値をそのまま描画する。
    /// </summary>
    public static Window BuildSequenceWindow(string title, IEnumerable<Sequence> rows)
    {
        var grid = new Grid { Margin = new Thickness(18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        int index = 0;
        foreach (Sequence row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var topMargin = new Thickness(0, index == 0 ? 0 : 9, 0, 0);

            var expression = new TextBlock
            {
                Text = row.Expression,
                FontFamily = CodeFont,
                FontSize = 13,
                Foreground = CodeBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, topMargin.Top, 22, 0),
            };
            Grid.SetRow(expression, index);
            Grid.SetColumn(expression, 0);
            grid.Children.Add(expression);

            var cells = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = topMargin,
            };

            foreach (string cell in row.Cells)
            {
                cells.Children.Add(new Border
                {
                    Background = CellFill,
                    BorderBrush = FrameBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(9, 3, 9, 3),
                    Margin = new Thickness(0, 0, 6, 0),
                    Child = new TextBlock
                    {
                        Text = cell,
                        FontFamily = CodeFont,
                        FontSize = 12.5,
                        Foreground = CodeBrush,
                    },
                });
            }

            Grid.SetRow(cells, index);
            Grid.SetColumn(cells, 1);
            grid.Children.Add(cells);

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

    /// <summary>
    /// 「状態ごとの見た目」を並べたウィンドウを作る。
    /// 見出しは日英で共有するため、コード表記だけで書く。
    /// </summary>
    public static Window BuildPanelWindow(
        string title,
        IEnumerable<Panel> panels,
        Orientation orientation = Orientation.Horizontal)
    {
        var stack = new StackPanel
        {
            Orientation = orientation,
            Margin = new Thickness(18),
        };

        bool first = true;
        foreach (Panel panel in panels)
        {
            var caption = new TextBlock
            {
                Text = panel.Caption,
                FontFamily = CodeFont,
                FontSize = 12,
                Foreground = CodeBrush,
                Margin = new Thickness(0, 0, 0, 6),
            };

            var group = new StackPanel
            {
                Margin = orientation == Orientation.Horizontal
                    ? new Thickness(first ? 0 : 18, 0, 0, 0)
                    : new Thickness(0, first ? 0 : 18, 0, 0),
                Children = { caption, panel.Content },
            };

            stack.Children.Add(group);
            first = false;
        }

        return new Window
        {
            Title = title,
            Content = stack,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };
    }

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
