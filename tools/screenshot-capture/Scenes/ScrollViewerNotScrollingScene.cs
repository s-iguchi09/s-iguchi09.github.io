using System.Windows;
using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で ScrollViewer がスクロールしない原因と解決方法」の図。
/// 同じ高さの領域に同じ内容を置き、外側のコンテナが StackPanel か
/// Grid の Star 行かでスクロールバーの有無が変わることを示す。
/// </summary>
internal sealed class ScrollViewerNotScrollingScene : IScene
{
    /// <summary>どちらの領域にも同じ高さ・同じ項目数を与える。</summary>
    private const int ItemCount = 12;

    private const double RegionHeight = 190;

    public IReadOnlyList<string> Verifies =>
    [
        "親のレイアウトを変えて ScrollViewer の ExtentHeight / ViewportHeight / ScrollableHeight を測る",
        "StackPanel の中では ScrollableHeight が 0 のままでスクロールバーが出ないこと",
        "Grid や DockPanel では高さが制約され、スクロールできること",
        "StackPanel でも高さを明示すればスクロールできること",
    ];

    public string Slug => "wpf-scrollviewer-not-scrolling";

    public async Task CaptureAsync(SceneContext context)
    {
        Window window = DemoLayout.BuildPanelWindow(
            "ScrollViewer inside StackPanel vs Grid",
            [
                new DemoLayout.Panel("StackPanel", BuildRegion(BuildStackPanelLayout())),
                new DemoLayout.Panel("Grid  RowDefinition Height=\"*\"", BuildRegion(BuildGridLayout())),
            ]);

        await context.ShootAsync(window, "scrollviewer-stackpanel-vs-grid.png");

        await context.SaveTableAsync(
            "ScrollViewer with 40 rows of 20px, parent constrained to 200px",
            ["parent layout", "Extent", "Viewport", "Scrollable", "scrollbar"],
            await ViewAndTemplateMeasurements.ScrollViewerHeightAsync(),
            "scrollviewer-height-matrix.svg");
    }

    /// <summary>
    /// 高さが決まった表示領域。はみ出した分は切り取られ、実際の画面と同じ見え方になる。
    /// </summary>
    private static Border BuildRegion(UIElement content) => new()
    {
        BorderBrush = System.Windows.Media.Brushes.Gainsboro,
        BorderThickness = new Thickness(1),
        Width = 220,
        Height = RegionHeight,
        ClipToBounds = true,
        Child = content,
    };

    /// <summary>
    /// 記事の「問題」と同じ構成。StackPanel が高さを制約しないためスクロールバーが出ない。
    /// </summary>
    private static UIElement BuildStackPanelLayout()
    {
        var outer = new StackPanel();
        outer.Children.Add(BuildHeader());
        outer.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildItems(),
        });

        return outer;
    }

    /// <summary>
    /// 記事の解決方法。Star 行が有限の高さを渡すためスクロールバーが出る。
    /// </summary>
    private static UIElement BuildGridLayout()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        FrameworkElement header = BuildHeader();
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildItems(),
        };
        Grid.SetRow(scrollViewer, 1);
        grid.Children.Add(scrollViewer);

        return grid;
    }

    private static FrameworkElement BuildHeader() => new TextBlock
    {
        Text = "Header",
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(8, 6, 8, 6),
    };

    private static UIElement BuildItems()
    {
        var items = new StackPanel();
        for (int i = 1; i <= ItemCount; i++)
        {
            items.Children.Add(new TextBlock
            {
                Text = $"Item {i:00}",
                Margin = new Thickness(8, 3, 8, 3),
            });
        }

        return items;
    }
}
