using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    private const int Iterations = 15;

    /// <summary>本文の実装例と同じ文字列を使う。</summary>
    private const string Text = "Status: Running";

    public IReadOnlyList<string> Verifies =>
    [
        "Label と TextBlock を同数だけ並べ、visual 要素数とレイアウト時間を測る",
        "非仮想化の StackPanel と仮想化した ListBox の両方で測り、差が仮想化で消えること",
        "Content にアンダーバーを含めると AccessText が 1 段挟まり、要素数とレイアウト時間が変わること",
        "1,000 個配置時のマネージドヒープ増分（GC 後に残る量）",
        "ContentTemplate を指定した Label に、アンダーバーを含む文字列を与えても AccessText が挟まらず、コストが増えないこと",
        "AccessText 単体と TextBlock 単体のレイアウト時間",
        "15 回の試行の最小値・中央値・最大値（仮想化時の差がばらつきより小さいかを見る）",
        "ScrollViewer.CanContentScroll の既定値と、ListBox の既定スタイルが与える値",
        "仮想化したまま VirtualizingPanel.ScrollUnit=\"Pixel\" でピクセル単位にスクロールできること、CanContentScroll=False で全件が実体化されること",
    ];

    public string Slug => "wpf-label-vs-textblock-performance";

    /// <summary>
    /// 表に出した試行のうち、ばらつきを示す条件。別に測り直すと最小値が表ごとに食い違うため、
    /// 他の表を作った試行をそのまま使う。
    /// </summary>
    private static readonly List<(string Name, Measurement Result)> Spread = [];

    public async Task CaptureAsync(SceneContext context)
    {
        Spread.Clear();

        // JIT とテンプレート初期化の影響を計測から外す。
        for (int i = 0; i < 3; i++)
        {
            MeasureOnce(200, CreateLabel);
            MeasureOnce(200, CreateTextBlock);
        }

        await context.ShootAsync(BuildBaselineWindow(), "label-vs-textblock-measurement.png");
        await context.ShootAsync(BuildVariantWindow(), "label-vs-textblock-variants.png");
        await context.ShootAsync(BuildVirtualizedWindow(), "label-vs-textblock-virtualized.png");

        await context.SaveTableAsync(
            $"layout ms over {Iterations} runs",
            ["", "min", "median", "max"],
            Spread.Select(entry => (IReadOnlyList<string>)
            [
                entry.Name,
                entry.Result.BestMilliseconds.ToString("F1"),
                entry.Result.MedianMilliseconds.ToString("F1"),
                entry.Result.WorstMilliseconds.ToString("F1"),
            ]).ToList(),
            "label-vs-textblock-spread.svg");

        await context.SaveTableAsync(
            $"managed heap kept after laying out {ItemCount:N0} items",
            ["", "KB", "vs TextBlock"],
            MeasureRetainedHeap(),
            "label-vs-textblock-memory.svg");

        await context.SaveTableAsync(
            "ListBox with 2,000 items: scrolling and virtualization",
            ["", "measured"],
            await MeasureScrollingAsync(),
            "label-vs-textblock-scrolling.svg");
    }

    private static Label CreateUnderscoreLabel() => new() { Content = "Status: _Running", Padding = new Thickness(0) };

    private static Label CreateLabel() => new() { Content = Text, Padding = new Thickness(0) };

    private static TextBlock CreateTextBlock() => new() { Text = Text };

    /// <summary>
    /// 非仮想化の <see cref="StackPanel"/> に並べたときの、要素数ごとの実測値。
    /// <see cref="Label"/> と <see cref="TextBlock"/> を交互に測り、実行順の影響を避ける。
    /// </summary>
    private static Window BuildBaselineWindow()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (int count in new[] { 250, 1000, 4000 })
        {
            var label = new Measurement();
            var textBlock = new Measurement();

            for (int i = 0; i < Iterations; i++)
            {
                label.Add(MeasureOnce(count, CreateLabel));
                textBlock.Add(MeasureOnce(count, CreateTextBlock));
            }

            if (count == ItemCount)
            {
                Spread.Add(($"StackPanel {count:N0}, Label", label));
                Spread.Add(($"StackPanel {count:N0}, TextBlock", textBlock));
            }

            rows.Add(
            [
                count.ToString("N0"),
                label.Visuals.ToString("N0"),
                label.BestMilliseconds.ToString("F0"),
                textBlock.Visuals.ToString("N0"),
                textBlock.BestMilliseconds.ToString("F0"),
            ]);
        }

        return DemoLayout.BuildTableWindow(
            "StackPanel (no virtualization)",
            ["items", "Label visuals", "Label ms", "TextBlock visuals", "TextBlock ms"],
            rows);
    }

    /// <summary>
    /// 1,000 個で固定し、<see cref="Label"/> の構成を変えたときの差を測る。
    /// アクセスキーを含む文字列が <c>AccessText</c> を挟むことによる負荷を示す。
    /// </summary>
    private static Window BuildVariantWindow()
    {
        (string Name, Func<FrameworkElement> Make)[] variants =
        [
            ("Label", CreateLabel),
            ("Label (Content has '_')", CreateUnderscoreLabel),
            ("Label + ContentTemplate", () => new Label { Content = Text, Padding = new Thickness(0), ContentTemplate = TextBlockTemplate() }),
            ("Label + ContentTemplate, '_'", () => new Label { Content = "Status: _Running", Padding = new Thickness(0), ContentTemplate = TextBlockTemplate() }),
            ("ContentPresenter", () => new ContentPresenter { Content = Text }),
            ("AccessText ('_')", () => new AccessText { Text = "Status: _Running" }),
            ("TextBlock", CreateTextBlock),
        ];

        var results = new Measurement[variants.Length];
        for (int i = 0; i < results.Length; i++)
        {
            results[i] = new Measurement();
        }

        // 実行順による偏りを避けるため、各構成を 1 回ずつ回す試行を繰り返す。
        for (int round = 0; round < Iterations; round++)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                results[i].Add(MeasureOnce(ItemCount, variants[i].Make));
            }
        }

        var rows = new List<IReadOnlyList<string>>();
        for (int i = 0; i < variants.Length; i++)
        {
            rows.Add(
            [
                variants[i].Name,
                results[i].Visuals.ToString("N0"),
                results[i].BestMilliseconds.ToString("F0"),
            ]);
        }

        return DemoLayout.BuildTableWindow(
            $"x {ItemCount:N0} in a StackPanel",
            ["", "visuals", "layout ms"],
            rows);
    }

    /// <summary>
    /// 仮想化された <see cref="ListBox"/> に 10,000 件を流したときの実測値。
    /// 非仮想化で見えた差が、仮想化すると残らないことを示す。
    /// </summary>
    private static Window BuildVirtualizedWindow()
    {
        const string labelItem = """<Label Content="{Binding}" Padding="0" />""";
        const string textBlockItem = """<TextBlock Text="{Binding}" />""";

        // ウォームアップ。
        MeasureVirtualized(labelItem);
        MeasureVirtualized(textBlockItem);

        var label = new Measurement();
        var textBlock = new Measurement();

        for (int i = 0; i < Iterations; i++)
        {
            label.Add(MeasureVirtualized(labelItem));
            textBlock.Add(MeasureVirtualized(textBlockItem));
        }

        Spread.Add(("ListBox 10,000, Label", label));
        Spread.Add(("ListBox 10,000, TextBlock", textBlock));

        return DemoLayout.BuildTableWindow(
            "ListBox, virtualized",
            ["", "visuals", "layout ms"],
            [
                ["Label", label.Visuals.ToString("N0"), label.BestMilliseconds.ToString("F0")],
                ["TextBlock", textBlock.Visuals.ToString("N0"), textBlock.BestMilliseconds.ToString("F0")],
            ]);
    }

    private readonly record struct Sample(int VisualCount, double Milliseconds);

    /// <summary>同一条件の試行をまとめ、所要時間の全試行と visual 数を保持する。</summary>
    private sealed class Measurement
    {
        private readonly List<double> milliseconds = [];

        public double BestMilliseconds => milliseconds.Min();

        public double MedianMilliseconds
        {
            get
            {
                double[] sorted = milliseconds.Order().ToArray();
                int middle = sorted.Length / 2;
                return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
            }
        }

        public double WorstMilliseconds => milliseconds.Max();

        public int Visuals { get; private set; }

        public void Add(Sample sample)
        {
            milliseconds.Add(sample.Milliseconds);
            Visuals = sample.VisualCount;
        }
    }

    /// <summary>
    /// 1,000 個を並べてレイアウトしたあと、GC を掛けても残るマネージドヒープの量を測る。
    /// 5 回測り、中央値を採る。
    /// </summary>
    private static List<IReadOnlyList<string>> MeasureRetainedHeap()
    {
        (string Name, Func<FrameworkElement> Make)[] variants =
        [
            ("Label", CreateLabel),
            ("Label (Content has '_')", CreateUnderscoreLabel),
            ("TextBlock", CreateTextBlock),
        ];

        long[] medians = variants
            .Select(variant =>
            {
                long[] samples = Enumerable.Range(0, 5).Select(_ => RetainedBytes(variant.Make)).Order().ToArray();
                return samples[samples.Length / 2];
            })
            .ToArray();

        long textBlock = medians[^1];
        return variants
            .Select((variant, i) => (IReadOnlyList<string>)
            [
                variant.Name,
                (medians[i] / 1024.0).ToString("N0"),
                ((double)medians[i] / textBlock).ToString("0.0") + "x",
            ])
            .ToList();

        static long RetainedBytes(Func<FrameworkElement> createItem)
        {
            long before = GC.GetTotalMemory(forceFullCollection: true);

            var panel = new StackPanel();
            var host = new Border { Width = 400, Height = 600, Child = panel };
            for (int i = 0; i < ItemCount; i++)
            {
                panel.Children.Add(createItem());
            }

            host.Measure(new Size(400, 600));
            host.Arrange(new Rect(0, 0, 400, 600));
            host.UpdateLayout();

            long after = GC.GetTotalMemory(forceFullCollection: true);
            GC.KeepAlive(host);
            return after - before;
        }
    }

    /// <summary>
    /// CanContentScroll の既定値と、仮想化とスクロール単位の関係を実際の ListBox で確かめる。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> MeasureScrollingAsync()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "ScrollViewer.CanContentScroll default", WpfProbe.Describe(ScrollViewer.CanContentScrollProperty.DefaultMetadata.DefaultValue) },
        };

        rows.AddRange(await WpfProbe.MeasureAsync(
        [
            ScrollCase("ListBox (default style)", null),
            ScrollCase("ScrollUnit=\"Pixel\"", listBox => VirtualizingPanel.SetScrollUnit(listBox, ScrollUnit.Pixel)),
            ScrollCase("CanContentScroll=\"False\"", listBox => ScrollViewer.SetCanContentScroll(listBox, false)),
        ]));

        return rows;

        // ExtentHeight は、項目単位のスクロールなら件数、ピクセル単位ならピクセル数になる。
        // 途中までスクロールしてから数え、スクロール後も表示範囲の分しか実体化されないことを見る。
        static WpfProbe.Case ScrollCase(string name, Action<ListBox>? configure)
        {
            var listBox = new ListBox { ItemsSource = Enumerable.Range(0, 2000).Select(i => $"item {i}").ToList() };
            configure?.Invoke(listBox);

            return new WpfProbe.Case(
                name,
                listBox,
                _ =>
                {
                    ScrollViewer viewer = FindDescendants<ScrollViewer>(listBox).First();
                    int realized = FindDescendants<ListBoxItem>(listBox).Count();
                    return [$"CanContentScroll {viewer.CanContentScroll}, ExtentHeight {viewer.ExtentHeight:N0}, realized {realized:N0}"];
                },
                _ =>
                {
                    ScrollViewer viewer = FindDescendants<ScrollViewer>(listBox).First();
                    viewer.ScrollToVerticalOffset(viewer.ExtentHeight / 2);
                    return Task.CompletedTask;
                });
        }
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        int children = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 要素を <paramref name="count"/> 個並べ、レイアウト完了までの時間と
    /// 生成された visual の総数を 1 回測る。
    /// </summary>
    private static Sample MeasureOnce(int count, Func<FrameworkElement> createItem)
    {
        var panel = new StackPanel();
        var host = new Border { Width = 400, Height = 600, Child = panel };

        for (int i = 0; i < count; i++)
        {
            panel.Children.Add(createItem());
        }

        var stopwatch = Stopwatch.StartNew();
        host.Measure(new Size(400, 600));
        host.Arrange(new Rect(0, 0, 400, 600));
        host.UpdateLayout();
        stopwatch.Stop();

        // 並べた要素の分だけを数えるため、入れ物である Border と StackPanel の
        // 2 個を除く。こうすると 1 個あたりの visual 数がそのまま倍率になる。
        return new Sample(CountVisuals(host) - 2, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// 仮想化された <see cref="ListBox"/> を組み立て、レイアウト完了までを 1 回測る。
    /// </summary>
    private static Sample MeasureVirtualized(string itemXaml)
    {
        var items = new List<string>();
        for (int i = 0; i < 10_000; i++)
        {
            items.Add(Text);
        }

        var listBox = new ListBox
        {
            ItemsSource = items,
            Width = 400,
            Height = 600,
            ItemTemplate = SceneContext.LoadXaml<DataTemplate>($"<DataTemplate>{itemXaml}</DataTemplate>"),
        };
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);

        var host = new Border { Width = 400, Height = 600, Child = listBox };

        var stopwatch = Stopwatch.StartNew();
        host.Measure(new Size(400, 600));
        host.Arrange(new Rect(0, 0, 400, 600));
        host.UpdateLayout();
        stopwatch.Stop();

        return new Sample(CountVisuals(listBox), stopwatch.Elapsed.TotalMilliseconds);
    }

    private static DataTemplate TextBlockTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new Binding());
        return new DataTemplate { VisualTree = factory };
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
}
