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
    private static readonly FontFamily CodeFont = new("Consolas, Courier New");
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
    /// 図のウィンドウを作る。撮影条件（サイズ・背景・リサイズ可否）を 1 箇所に集約する。
    /// </summary>
    private static Window CreateWindow(string title, UIElement content) => new()
    {
        Title = title,
        Content = content,
        SizeToContent = SizeToContent.WidthAndHeight,
        ResizeMode = ResizeMode.CanMinimize,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
        Background = Brushes.White,
    };

    /// <summary>
    /// 「入力 → 結果」を示す矢印。図をまたいで同じ見た目にするため、ここで一元管理する。
    /// </summary>
    public static TextBlock Arrow(Thickness margin) => new()
    {
        Text = "→",
        FontSize = 15,
        Foreground = ArrowBrush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = margin,
    };

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

        return CreateWindow(title, grid);
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

        return CreateWindow(title, stack);
    }

    /// <summary>
    /// 実測値の表を SVG として組み立てる。
    ///
    /// 表は文字と罫線だけで構成されるため、ウィンドウを撮影せずに直接描ける。
    /// SVG にすると拡大しても文字がぼやけず、差分も読める。
    /// ウィンドウを表示しないので、ディスプレイの電源状態にも影響されない
    /// （<c>PrintWindow</c> が白紙を返す問題が起きない）。
    ///
    /// 実際に動かして得た値を描く点は変わらない。SVG になるのは描画方法だけである。
    /// 一方、実行中のウィンドウそのものを見せる図は SVG にしない（作図で代用しないため）。
    /// </summary>
    public static string BuildTableSvg(
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var body = rows.Select(row => row.ToArray()).ToList();
        int columns = headers.Count;

        // 等幅フォントなので、最も長いセルの文字数から列幅を決められる。
        var widths = new double[columns];
        for (int column = 0; column < columns; column++)
        {
            int longest = DisplayWidth(headers[column]);
            foreach (string[] row in body)
            {
                if (column < row.Length)
                {
                    longest = Math.Max(longest, DisplayWidth(row[column]));
                }
            }

            widths[column] = (longest * CharWidth) + (CellPaddingX * 2);
        }

        double tableWidth = widths.Sum();
        double titleHeight = string.IsNullOrEmpty(title) ? 0 : TitleHeight;
        double tableHeight = (body.Count + 1) * RowHeight;
        double width = tableWidth + (Margin * 2);
        double height = tableHeight + titleHeight + (Margin * 2);

        var svg = new System.Text.StringBuilder();
        svg.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width:F0} {height:F0}" width="{width:F0}" height="{height:F0}" role="img">""");
        svg.AppendLine();
        svg.AppendLine("""
              <defs>
                <style>
                  .bg { fill: #ffffff; }
                  .head { fill: #f5f7fb; }
                  .line { stroke: #c3ccdb; stroke-width: 1; }
                  .cell { font-family: Consolas, "Courier New", monospace; font-size: 13px; fill: #1f2933; }
                  .title { font-family: Consolas, "Courier New", monospace; font-size: 11px; fill: #5b6673; }
                </style>
              </defs>
            """);

        svg.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"""  <rect class="bg" x="0" y="0" width="{width:F0}" height="{height:F0}" />""");
        svg.AppendLine();

        if (titleHeight > 0)
        {
            svg.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"""  <text class="title" x="{Margin}" y="{Margin + 12}">{Escape(title)}</text>""");
            svg.AppendLine();
        }

        double top = Margin + titleHeight;

        // ヘッダー行の背景。
        svg.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"""  <rect class="head" x="{Margin}" y="{top:F0}" width="{tableWidth:F0}" height="{RowHeight}" />""");
        svg.AppendLine();

        AppendRow(svg, headers.ToArray(), widths, top, header: true);
        for (int i = 0; i < body.Count; i++)
        {
            AppendRow(svg, body[i], widths, top + ((i + 1) * RowHeight), header: false);
        }

        AppendGrid(svg, widths, top, body.Count + 1, tableWidth, tableHeight);

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private const double CharWidth = 7.85;
    private const double CellPaddingX = 14;
    private const double RowHeight = 30;
    private const double TitleHeight = 26;
    private const double Margin = 12;

    /// <summary>
    /// 全角文字を 2 文字分として数える。等幅フォントでの列幅計算に使う。
    /// </summary>
    private static int DisplayWidth(string text)
    {
        int width = 0;
        foreach (char c in text)
        {
            width += c >= 0x1100 && (c <= 0x115F || (c >= 0x2E80 && c <= 0xA4CF)
                || (c >= 0xAC00 && c <= 0xD7A3) || (c >= 0xF900 && c <= 0xFAFF)
                || (c >= 0xFE30 && c <= 0xFE6F) || (c >= 0xFF00 && c <= 0xFF60)
                || (c >= 0xFFE0 && c <= 0xFFE6)) ? 2 : 1;
        }

        return width;
    }

    private static void AppendRow(System.Text.StringBuilder svg, string[] cells, double[] widths, double top, bool header)
    {
        double x = Margin;
        double baseline = top + (RowHeight / 2) + 4.5;

        for (int column = 0; column < widths.Length; column++)
        {
            string text = column < cells.Length ? cells[column] : string.Empty;

            // 先頭列は左寄せ、それ以外は数値の桁を揃えるため右寄せにする。
            if (column == 0)
            {
                svg.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $"""  <text class="cell" x="{x + CellPaddingX:F0}" y="{baseline:F1}">{Escape(text)}</text>""");
            }
            else
            {
                svg.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $"""  <text class="cell" x="{x + widths[column] - CellPaddingX:F0}" y="{baseline:F1}" text-anchor="end">{Escape(text)}</text>""");
            }

            svg.AppendLine();
            x += widths[column];
        }
    }

    /// <summary>罫線を引く。</summary>
    private static void AppendGrid(
        System.Text.StringBuilder svg,
        double[] widths,
        double top,
        int rowCount,
        double tableWidth,
        double tableHeight)
    {
        for (int row = 0; row <= rowCount; row++)
        {
            double y = top + (row * RowHeight);
            svg.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"""  <line class="line" x1="{Margin}" y1="{y:F0}" x2="{Margin + tableWidth:F0}" y2="{y:F0}" />""");
            svg.AppendLine();
        }

        double x = Margin;
        for (int column = 0; column <= widths.Length; column++)
        {
            svg.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"""  <line class="line" x1="{x:F0}" y1="{top:F0}" x2="{x:F0}" y2="{top + tableHeight:F0}" />""");
            svg.AppendLine();
            if (column < widths.Length)
            {
                x += widths[column];
            }
        }
    }

    private static string Escape(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    /// <summary>
    /// 実測値を表として並べたウィンドウを作る。
    /// 図中の文言は日英で共有するため、識別子と数値だけで構成する。
    /// 先頭列は左寄せ、それ以外は数値の桁を揃えるため右寄せにする。
    /// </summary>
    public static Window BuildTableWindow(
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var grid = new Grid { Margin = new Thickness(18) };
        for (int i = 0; i < headers.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int column = 0; column < headers.Count; column++)
        {
            AddTableCell(grid, 0, column, headers[column], header: true);
        }

        int rowIndex = 1;
        foreach (IReadOnlyList<string> row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int column = 0; column < headers.Count; column++)
            {
                AddTableCell(grid, rowIndex, column, column < row.Count ? row[column] : string.Empty);
            }

            rowIndex++;
        }

        return CreateWindow(title, grid);
    }

    private static void AddTableCell(Grid grid, int row, int column, string text, bool header = false)
    {
        var cell = new Border
        {
            BorderBrush = FrameBrush,
            // 隣接するセルで枠線が二重にならないよう、左辺と上辺は先頭の行・列だけ描く。
            BorderThickness = new Thickness(column == 0 ? 1 : 0, row == 0 ? 1 : 0, 1, 1),
            Background = header ? CellFill : Brushes.White,
            Padding = new Thickness(14, 7, 14, 7),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = CodeFont,
                FontSize = 13,
                Foreground = CodeBrush,
                HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            },
        };

        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
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

            TextBlock arrow = Arrow(new Thickness(16, index == 0 ? 0 : 10, 16, 0));
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

        return CreateWindow(title, grid);
    }
}
