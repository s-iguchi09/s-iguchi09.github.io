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
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureBehaviorAsync(),
            "expander-behavior.svg");

        await context.SaveTableAsync(
            "Expander: ExpandDirection, header and the Control properties",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureLayoutAsync(),
            "expander-layout.svg");
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static ToggleButton HeaderSite(Expander expander) =>
        Descendants(expander).OfType<ToggleButton>().First();

    private static FrameworkElement ExpandSite(Expander expander) =>
        (FrameworkElement)expander.Template.FindName("ExpandSite", expander);

    private static async Task<List<IReadOnlyList<Loc>>> MeasureBehaviorAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        var defaults = new Expander();
        var metadata = (FrameworkPropertyMetadata)Expander.IsExpandedProperty.GetMetadata(typeof(Expander));
        rows.Add([T("base class / IsExpanded default, two-way by default / ExpandDirection", "基底クラス / IsExpanded の既定値、既定で双方向か / ExpandDirection"),
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
                rows.Add([T("default template: header element / VisualStateGroups", "既定のテンプレート: 見出しの要素 / VisualStateGroups"),
                    T($"{HeaderSite(expander).GetType().Name} named {HeaderSite(expander).Name} / {groups}", $"{HeaderSite(expander).GetType().Name}（名前 {HeaderSite(expander).Name}） / {groups}")]);
                rows.Add([T("  triggers", "  トリガー"), string.Join("; ", triggers)]);

                expander.IsExpanded = false;
                string immediately = ExpandSite(expander).Visibility.ToString();
                await Capture.SettleAsync(Window.GetWindow(expander)!, 50);
                rows.Add([T("IsExpanded set to False: content Visibility at once / after 50 ms", "IsExpanded を False に: 内容の Visibility（直後 / 50 ms 後）"),
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

                rows.Add([T("real mouse click on header: IsExpanded / bound CheckBox / events", "見出しを実際にマウスでクリック: IsExpanded / バインドした CheckBox / イベント"),
                    $"{expander.IsExpanded} / {WpfProbe.Describe(check.IsChecked)} / {string.Join(", ", events)}"]);

                events.Clear();
                check.IsChecked = false;
                await Capture.SettleAsync(window, 50);
                rows.Add([T("  then the CheckBox cleared: IsExpanded / events", "  続けて CheckBox を外す: IsExpanded / イベント"), $"{expander.IsExpanded} / {string.Join(", ", events)}"]);
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
                rows.Add([T("collapsed: child Loaded / measured / items in a 1000-item ListBox", "折りたたみ中: 子の Loaded / 測定の回数 / 1000 項目の ListBox で作られた項目の数"),
                    $"{loaded} / {counter.MeasureCount} / {containers}"]);
                expander.IsExpanded = true;
                await Capture.SettleAsync(Window.GetWindow(expander)!);
                rows.Add([T("  after expanding", "  展開した後"), $"{loaded} / {counter.MeasureCount} / {Descendants(expander).OfType<ListBoxItem>().Count()}"]);
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureLayoutAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

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
                rows.Add([T($"ExpandDirection={direction}: header / content / header text", $"ExpandDirection={direction}: 見出し / 内容 / 見出しの文字"),
                    $"{Format(h)} / {Format(c)} / {Format(t)}"]);
                await Task.CompletedTask;
            });
        }

        {
            var expander = new Expander { Content = "CONTENT", IsExpanded = true, Width = 200 };
            await ShowAsync(expander, async () =>
            {
                ToggleButton header = HeaderSite(expander);
                rows.Add([T("Header=null: HasHeader / header ToggleButton Visibility, size", "Header=null: HasHeader / 見出しの ToggleButton の Visibility、大きさ"),
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
                rows.Add([T("FontWeight=Bold, Foreground=Red: header text / content text", "FontWeight=Bold、Foreground=Red: 見出しの文字 / 内容の文字"),
                    $"{headerText.FontWeight}, {headerText.Foreground} / {contentText.FontWeight}, {contentText.Foreground}"]);
                // Background を描いている要素の範囲が、見出しと内容を含むか。Background は継承しないプロパティなので、子の値は読まない。
                var painted = Descendants(expander).OfType<Border>().First(b => ReferenceEquals(b.Background, expander.Background));
                Rect area = Bounds(painted, expander);
                Rect header = Bounds(HeaderSite(expander), expander);
                Rect body = Bounds(inner, expander);
                rows.Add([T("Background: painted area / header / content (inside it)", "Background: 塗られる範囲 / 見出し / 内容（範囲の中か）"),
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
                rows.Add([T($"Padding={D(padding)}, BorderThickness={D(border)}: header / content", $"Padding={D(padding)}、BorderThickness={D(border)}: 見出し / 内容"),
                    $"{Format(Bounds(HeaderSite(expander), expander))} / {Format(Bounds(content, expander))}"]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }
}
