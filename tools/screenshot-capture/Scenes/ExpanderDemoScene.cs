using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Expander」（apps/wpf-standard-control-demo/expander.md と日本語版）の記述を実測する。
///
/// 見出しのクリックは実際のマウス（<see cref="RealMouse"/>）で行う。
/// 位置と大きさは Expander から見た矩形で読む。
/// </summary>
internal sealed class ExpanderDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-expander";

    public string ImageDirectory => DemoProbe.ImageDirectory("expander");

    public IReadOnlyList<string> Verifies =>
    [
        "Expander の基底クラス、IsExpanded の既定値と既定で TwoWay か、ExpandDirection の既定値",
        "既定のテンプレートの見出しの要素（ToggleButton）と、VisualStateGroup・トリガーの有無、折りたたんだ直後の内容の Visibility（アニメーションの有無）",
        "実際のマウスで見出しをクリックしたときの IsExpanded と、Mode 指定なしで IsExpanded にバインドした CheckBox、Expanded / Collapsed イベント",
        "折りたたんだ Expander の中の要素が Loaded になるか、測られるか、中の ListBox（1000 項目）のコンテナーが作られるか、展開したときの変化",
        "ExpandDirection ごとの見出しと内容の位置と、変形を含めた見出しの文字の矩形（横書きのままか）",
        "Header が null のときの HasHeader と、見出しの ToggleButton の表示",
        "FontWeight・Foreground を Expander に設定したときの見出しと内容の文字、Background を塗る範囲が見出しと内容を含むか",
        "Padding と BorderThickness を別々に設定したときの見出しと内容の位置",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Expander: type, template, clicks and collapsed content",
            ["case", "measured"],
            await MeasureBehaviorAsync(),
            "expander-behavior.svg");

        await context.SaveTableAsync(
            "Expander: ExpandDirection, header and the Control properties",
            ["case", "measured"],
            await MeasureLayoutAsync(),
            "expander-layout.svg");
    }

    private static ToggleButton HeaderSite(Expander expander) =>
        Descendants(expander).OfType<ToggleButton>().First();

    private static FrameworkElement ExpandSite(Expander expander) =>
        (FrameworkElement)expander.Template.FindName("ExpandSite", expander);

    private static async Task<List<IReadOnlyList<string>>> MeasureBehaviorAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new Expander();
        var metadata = (FrameworkPropertyMetadata)Expander.IsExpandedProperty.GetMetadata(typeof(Expander));
        rows.Add(["base class / IsExpanded default, two-way by default / ExpandDirection",
            $"{typeof(Expander).BaseType!.Name} / {defaults.IsExpanded}, {metadata.BindsTwoWayByDefault} / {defaults.ExpandDirection}"]);

        {
            var expander = new Expander { Header = "Expander", Content = "CONTENT", IsExpanded = true, Width = 200 };
            await ShowAsync(expander, async () =>
            {
                var root = (FrameworkElement)VisualTreeHelper.GetChild(expander, 0);
                int groups = VisualStateManager.GetVisualStateGroups(root)?.Count ?? 0;
                var triggers = expander.Template.Triggers.OfType<Trigger>()
                    .GroupBy(t => t.Property.Name)
                    .Select(g => $"{g.Key}={string.Join(", ", g.Select(t => WpfProbe.Describe(t.Value)))}");
                rows.Add(["default template: header element / VisualStateGroups",
                    $"{HeaderSite(expander).GetType().Name} named {HeaderSite(expander).Name} / {groups}"]);
                rows.Add(["  triggers", string.Join("; ", triggers)]);

                expander.IsExpanded = false;
                string immediately = ExpandSite(expander).Visibility.ToString();
                await Capture.SettleAsync(Window.GetWindow(expander)!, 50);
                rows.Add(["IsExpanded set to False: content Visibility at once / after 50 ms",
                    $"{immediately} / {ExpandSite(expander).Visibility}"]);
            });
        }

        {
            // デモアプリの IsExpanded 欄と同じく、CheckBox の IsChecked を Mode 指定なしで IsExpanded に結ぶ。
            var check = new CheckBox { Content = "IsExpanded" };
            var expander = new Expander { Header = "Expander", Content = "CONTENT", Width = 200 };
            expander.SetBinding(Expander.IsExpandedProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = check });
            var events = new List<string>();
            expander.Expanded += (_, _) => events.Add("Expanded");
            expander.Collapsed += (_, _) => events.Add("Collapsed");
            var panel = new StackPanel { Children = { check, expander } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                window.Topmost = true;
                await Capture.SettleAsync(window);
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(HeaderSite(expander));
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add(["real mouse click on header: IsExpanded / bound CheckBox / events",
                    $"{expander.IsExpanded} / {WpfProbe.Describe(check.IsChecked)} / {string.Join(", ", events)}"]);

                events.Clear();
                check.IsChecked = false;
                await Capture.SettleAsync(window, 50);
                rows.Add(["  then the CheckBox cleared: IsExpanded / events", $"{expander.IsExpanded} / {string.Join(", ", events)}"]);
            });
        }

        {
            var counter = new MeasureCounter();
            int loaded = 0;
            counter.Loaded += (_, _) => loaded++;
            var list = new ListBox { Height = 200, ItemsSource = Enumerable.Range(1, 1000).Select(i => $"Item {i}").ToList() };
            var expander = new Expander { Header = "Collapsed", Width = 200, Content = new StackPanel { Children = { counter, list } } };
            await ShowAsync(expander, async () =>
            {
                int containers = Descendants(expander).OfType<ListBoxItem>().Count();
                rows.Add(["collapsed: child Loaded / measured / items in a 1000-item ListBox",
                    $"{loaded} / {counter.MeasureCount} / {containers}"]);
                expander.IsExpanded = true;
                await Capture.SettleAsync(Window.GetWindow(expander)!);
                rows.Add(["  after expanding", $"{loaded} / {counter.MeasureCount} / {Descendants(expander).OfType<ListBoxItem>().Count()}"]);
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureLayoutAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (ExpandDirection direction in new[] { ExpandDirection.Down, ExpandDirection.Up, ExpandDirection.Left, ExpandDirection.Right })
        {
            var content = new Border { Width = 80, Height = 40, Background = Brushes.LightGray };
            var expander = new Expander { Header = "Expander", Content = content, IsExpanded = true, ExpandDirection = direction };
            await ShowAsync(expander, async () =>
            {
                ToggleButton header = HeaderSite(expander);
                Rect h = Bounds(header, expander);
                Rect c = Bounds(content, expander);
                                var text = Descendants(header).OfType<ContentPresenter>().First(p => ReferenceEquals(p.Content, expander.Header));
                // 変形を含めた、Expander から見た見出しの文字の矩形。幅が高さより大きければ横書きのまま。
                Rect t = Bounds(text, expander);
                rows.Add([$"ExpandDirection={direction}: header / content / header text",
                    $"{Format(h)} / {Format(c)} / {Format(t)}"]);
                await Task.CompletedTask;
            });
        }

        {
            var expander = new Expander { Content = "CONTENT", IsExpanded = true, Width = 200 };
            await ShowAsync(expander, async () =>
            {
                ToggleButton header = HeaderSite(expander);
                rows.Add(["Header=null: HasHeader / header ToggleButton Visibility, size",
                    $"{expander.HasHeader} / {header.Visibility}, {D(header.ActualWidth)} x {D(header.ActualHeight)}"]);
                await Task.CompletedTask;
            });
        }

        {
            var headerText = new TextBlock { Text = "Expander" };
            var contentText = new TextBlock { Text = "CONTENT" };
            var inner = new Border { Child = contentText };
            var expander = new Expander
            {
                Header = headerText,
                Content = inner,
                IsExpanded = true,
                Width = 200,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Red,
                Background = Brushes.LightYellow,
            };
            await ShowAsync(expander, async () =>
            {
                rows.Add(["FontWeight=Bold, Foreground=Red: header text / content text",
                    $"{headerText.FontWeight}, {headerText.Foreground} / {contentText.FontWeight}, {contentText.Foreground}"]);
                // Background を描いている要素の範囲が、見出しと内容を含むか。Background は継承しないプロパティなので、子の値は読まない。
                var painted = Descendants(expander).OfType<Border>().First(b => ReferenceEquals(b.Background, expander.Background));
                Rect area = Bounds(painted, expander);
                Rect header = Bounds(HeaderSite(expander), expander);
                Rect body = Bounds(inner, expander);
                rows.Add(["Background: painted area / header / content (inside it)",
                    $"{Format(area)} / {Format(header)} / {Format(body)} ({area.Contains(header) && area.Contains(body)})"]);
                await Task.CompletedTask;
            });
        }

        // Padding と BorderThickness の効果を分けて測る。
        foreach ((double padding, double border) in new[] { (0d, 0d), (20d, 0d), (0d, 5d) })
        {
            var content = new Border { Width = 80, Height = 40 };
            var expander = new Expander { Header = "Expander", Content = content, IsExpanded = true, Width = 200, Padding = new Thickness(padding), BorderThickness = new Thickness(border), BorderBrush = Brushes.Black };
            await ShowAsync(expander, async () =>
            {
                rows.Add([$"Padding={D(padding)}, BorderThickness={D(border)}: header / content",
                    $"{Format(Bounds(HeaderSite(expander), expander))} / {Format(Bounds(content, expander))}"]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }
}
