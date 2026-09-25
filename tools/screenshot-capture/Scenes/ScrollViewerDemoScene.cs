using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「ScrollViewer」（apps/wpf-standard-control-demo/scrollviewer.md と日本語版）の記述を実測する。
///
/// 内容はデモアプリと同じく、枠付きの Label を並べた StackPanel を高さ 100 の ScrollViewer に入れる。
/// つまみのドラッグは、マウスのドラッグと同じ DragStarted / DragDelta / DragCompleted をつまみに発生させて再現する。
/// </summary>
internal sealed class ScrollViewerDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-scrollviewer";

    public string ImageDirectory => DemoProbe.ImageDirectory("scrollviewer");

    public IReadOnlyList<string> Verifies =>
    [
        "VerticalScrollBarVisibility / HorizontalScrollBarVisibility の既定値",
        "VerticalScrollBarVisibility の 4 つの値ごとに、内容が収まる場合とはみ出す場合の ComputedVerticalScrollBarVisibility、ViewportWidth、ExtentHeight、ScrollToVerticalOffset(50) 後の VerticalOffset",
        "CanContentScroll が True と False のときの ExtentHeight / ViewportHeight の単位と、LineDown 1 回の移動量",
        "ListBox（1000 項目）で CanContentScroll を False にしたときに生成されるコンテナーの数",
        "IsDeferredScrollingEnabled が True と False のとき、つまみのドラッグ中とドラッグ後の VerticalOffset と ContentVerticalOffset",
        "ListBox・TextBox・TreeView・DataGrid の中の ScrollViewer と、その HorizontalScrollBarVisibility の値、ListBox に付けた添付プロパティと外側の ScrollViewer の設定が届くか",
        "レイアウト前に読んだ ExtentHeight / ViewportHeight",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ScrollViewer 200 x 100: 2 items (fit) and 9 items (overflow)",
            ["VerticalScrollBarVisibility", "items", "Computed...", "ViewportWidth", "ExtentHeight", "offset after scrolling to 50"],
            MeasureVisibility(),
            "scrollviewer-visibility.svg");

        await context.SaveTableAsync(
            "ScrollViewer: CanContentScroll, deferred scrolling and ScrollViewers inside controls",
            ["case", "measured"],
            await MeasureScrollingAsync(),
            "scrollviewer-scrolling.svg");
    }

    /// <summary>デモアプリと同じ、枠付きの Label を並べた StackPanel。</summary>
    private static StackPanel Items(int count)
    {
        var panel = new StackPanel();
        for (int i = 1; i <= count; i++)
        {
            panel.Children.Add(new Label { Content = $"Item{i}", BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) });
        }

        return panel;
    }

    private static ScrollViewer Viewer(int count, ScrollBarVisibility visibility) =>
        new() { Height = 100, Width = 200, VerticalScrollBarVisibility = visibility, Content = Items(count) };

    private static void Settle(ScrollViewer viewer)
    {
        var host = viewer.Parent as Grid ?? new Grid { Children = { viewer } };
        Layout(host, 200, 100);
    }

    private static List<IReadOnlyList<string>> MeasureVisibility()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (ScrollBarVisibility visibility in new[] { ScrollBarVisibility.Disabled, ScrollBarVisibility.Auto, ScrollBarVisibility.Hidden, ScrollBarVisibility.Visible })
        {
            foreach (int count in new[] { 2, 9 })
            {
                ScrollViewer viewer = Viewer(count, visibility);
                Settle(viewer);
                string computed = viewer.ComputedVerticalScrollBarVisibility.ToString();
                string viewport = D(viewer.ViewportWidth);
                string extent = D(viewer.ExtentHeight);
                viewer.ScrollToVerticalOffset(50);
                Settle(viewer);
                rows.Add([visibility.ToString(), count.ToString(), computed, viewport, extent, D(viewer.VerticalOffset)]);
            }
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureScrollingAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new ScrollViewer();
        rows.Add(["defaults: VerticalScrollBarVisibility / HorizontalScrollBarVisibility",
            $"{defaults.VerticalScrollBarVisibility} / {defaults.HorizontalScrollBarVisibility}"]);

        foreach (bool canContentScroll in new[] { false, true })
        {
            ScrollViewer viewer = Viewer(9, ScrollBarVisibility.Auto);
            viewer.CanContentScroll = canContentScroll;
            Settle(viewer);
            string sizes = $"extent {D(viewer.ExtentHeight)}, viewport {D(viewer.ViewportHeight)}";
            viewer.LineDown();
            Settle(viewer);
            rows.Add([$"demo items, CanContentScroll={canContentScroll}: sizes / offset after LineDown",
                $"{sizes} / {D(viewer.VerticalOffset)}"]);
        }

        foreach (bool canContentScroll in new[] { true, false })
        {
            var list = new ListBox { Height = 200, Width = 200, ItemsSource = Enumerable.Range(1, 1000).Select(i => $"Item {i}").ToList() };
            ScrollViewer.SetCanContentScroll(list, canContentScroll);
            var host = new Grid { Children = { list } };
            Layout(host, 200, 200);
            int realized = Descendants(list).OfType<ListBoxItem>().Count();
            rows.Add([$"ListBox, 1000 items, CanContentScroll={canContentScroll}: items created", realized.ToString()]);
        }

        foreach (bool deferred in new[] { false, true })
        {
            ScrollViewer viewer = Viewer(9, ScrollBarVisibility.Visible);
            viewer.IsDeferredScrollingEnabled = deferred;
            await ShowAsync(viewer, async () =>
            {
                var bar = Descendants(viewer).OfType<ScrollBar>().First(b => b.Orientation == Orientation.Vertical);
                Thumb thumb = bar.Track.Thumb;
                thumb.RaiseEvent(new DragStartedEventArgs(0, 0));
                thumb.RaiseEvent(new DragDeltaEventArgs(0, 30));
                await Capture.SettleAsync(Window.GetWindow(viewer)!, 20);
                string during = $"{D(viewer.VerticalOffset)} / {D(viewer.ContentVerticalOffset)}";
                thumb.RaiseEvent(new DragCompletedEventArgs(0, 30, false));
                await Capture.SettleAsync(Window.GetWindow(viewer)!, 20);
                rows.Add([$"IsDeferredScrollingEnabled={deferred}: offset / content offset, dragging; released",
                    $"{during}; {D(viewer.VerticalOffset)} / {D(viewer.ContentVerticalOffset)}"]);
            });
        }

        {
            var list = new ListBox { ItemsSource = new[] { "a" } };
            var text = new TextBox();
            var tree = new TreeView();
            var grid = new DataGrid();
            var panel = new StackPanel { Width = 200, Children = { list, text, tree, grid } };
            var attached = new ListBox { ItemsSource = new[] { "a" } };
            ScrollViewer.SetHorizontalScrollBarVisibility(attached, ScrollBarVisibility.Disabled);
            var wrappedList = new ListBox { ItemsSource = new[] { "a" } };
            var outer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = wrappedList, Height = 60 };
            panel.Children.Add(attached);
            panel.Children.Add(outer);
            await ShowAsync(panel, async () =>
            {
                string Inner(Control control) => Descendants(control).OfType<ScrollViewer>().FirstOrDefault() is { } inner
                    ? inner.HorizontalScrollBarVisibility.ToString()
                    : "no ScrollViewer";
                rows.Add(["HorizontalScrollBarVisibility inside ListBox / TextBox / TreeView / DataGrid",
                    $"{Inner(list)} / {Inner(text)} / {Inner(tree)} / {Inner(grid)}"]);
                rows.Add(["inside ListBox: attached Disabled / outer ScrollViewer Disabled",
                    $"{Inner(attached)} / {Inner(wrappedList)}"]);
                await Task.CompletedTask;
            });
        }

        {
            ScrollViewer viewer = Viewer(9, ScrollBarVisibility.Auto);
            string before = $"{D(viewer.ExtentHeight)} / {D(viewer.ViewportHeight)}";
            Settle(viewer);
            rows.Add(["ExtentHeight / ViewportHeight before layout; after layout", $"{before}; {D(viewer.ExtentHeight)} / {D(viewer.ViewportHeight)}"]);
        }

        return rows;
    }
}
