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

    public string Slug => "wpf-label-vs-textblock-performance";

    public async Task CaptureAsync(SceneContext context)
    {
        // JIT とテンプレート初期化の影響を計測から外す。
        for (int i = 0; i < 3; i++)
        {
            MeasureOnce(200, CreateLabel);
            MeasureOnce(200, CreateTextBlock);
        }

        await context.ShootAsync(BuildBaselineWindow(), "label-vs-textblock-measurement.png");
        await context.ShootAsync(BuildVariantWindow(), "label-vs-textblock-variants.png");
        await context.ShootAsync(BuildVirtualizedWindow(), "label-vs-textblock-virtualized.png");
    }

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
            ("Label (Content has '_')", () => new Label { Content = "Status: _Running", Padding = new Thickness(0) }),
            ("Label + ContentTemplate", () => new Label { Content = Text, Padding = new Thickness(0), ContentTemplate = TextBlockTemplate() }),
            ("ContentPresenter", () => new ContentPresenter { Content = Text }),
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

        for (int i = 0; i < 12; i++)
        {
            label.Add(MeasureVirtualized(labelItem));
            textBlock.Add(MeasureVirtualized(textBlockItem));
        }

        return DemoLayout.BuildTableWindow(
            "ListBox, virtualized",
            ["", "visuals", "layout ms"],
            [
                ["Label", label.Visuals.ToString("N0"), label.BestMilliseconds.ToString("F0")],
                ["TextBlock", textBlock.Visuals.ToString("N0"), textBlock.BestMilliseconds.ToString("F0")],
            ]);
    }

    private readonly record struct Sample(int VisualCount, double Milliseconds);

    /// <summary>同一条件の試行をまとめ、最小値と visual 数を保持する。</summary>
    private sealed class Measurement
    {
        public double BestMilliseconds { get; private set; } = double.MaxValue;

        public int Visuals { get; private set; }

        public void Add(Sample sample)
        {
            BestMilliseconds = Math.Min(BestMilliseconds, sample.Milliseconds);
            Visuals = sample.VisualCount;
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

        // ホストの Border 自身は数えない。
        return new Sample(CountVisuals(host) - 1, stopwatch.Elapsed.TotalMilliseconds);
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
