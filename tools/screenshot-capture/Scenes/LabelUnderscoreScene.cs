using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の Label でアンダーバーが消える理由と回避方法」の図。
/// 本文の表と実装例に対応する実際の描画結果を取得する。
/// </summary>
internal sealed class LabelUnderscoreScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "同じ文字列を各コントロールへ与え、visual ツリーに AccessText が生成されるかを確かめる",
        "アンダーバーが消えるのは Label だけではなく、Header を持つコントロールでも起きること",
        "対象コントロール自身のテンプレートに属する ContentPresenter の個数と RecognizesAccessKey",
        "TemplatedParent で絞らないと、子コントロールのテンプレート部品まで数えてしまうこと",
    ];

    public string Slug => "wpf-label-underscore-issue";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildSymptomWindow(), "label-underscore-rendering.png");
        await context.ShootAsync(BuildAffectedControlsWindow(), "label-underscore-affected-controls.png");
        await context.ShootAsync(BuildWorkaroundWindow(), "label-underscore-workarounds.png");
        await MeasureAccessKeyScopeAsync(context);
    }

    /// <summary>
    /// 同じ文字列を各コントロールへ与え、アンダーバーが消える範囲が
    /// <c>Label</c> に限らないことを示す。
    /// 既定テンプレートの <c>ContentPresenter.RecognizesAccessKey</c> が
    /// <c>True</c> かどうかで結果が分かれる。
    /// </summary>
    private static Window BuildAffectedControlsWindow()
    {
        var rows = new[]
        {
            new DemoLayout.Row(
                """<Label Content="my_var" />""",
                SceneContext.LoadXaml<Label>("""<Label Content="my_var" Padding="0" />""")),
            new DemoLayout.Row(
                """<Button Content="my_var" />""",
                SceneContext.LoadXaml<Button>("""<Button Content="my_var" Padding="8,2" />""")),
            new DemoLayout.Row(
                """<CheckBox Content="my_var" />""",
                SceneContext.LoadXaml<CheckBox>("""<CheckBox Content="my_var" />""")),
            new DemoLayout.Row(
                """<GroupBox Header="my_var" />""",
                SceneContext.LoadXaml<GroupBox>("""<GroupBox Header="my_var" Width="150" Height="44" />""")),
            new DemoLayout.Row(
                """<ListBoxItem Content="my_var" />""",
                SceneContext.LoadXaml<ListBoxItem>("""<ListBoxItem Content="my_var" Padding="0" />""")),
            new DemoLayout.Row(
                """<TextBlock Text="my_var" />""",
                SceneContext.LoadXaml<TextBlock>("""<TextBlock Text="my_var" />""")),
        };

        return DemoLayout.BuildComparisonWindow("RecognizesAccessKey", rows);
    }

    /// <summary>
    /// 本文「原因・背景」の表に対応する 3 例を、実際の Label で描画する。
    /// </summary>
    private static Window BuildSymptomWindow()
    {
        var rows = new[]
        {
            Row("""<Label Content="_File" />"""),
            Row("""<Label Content="my_var" />"""),
            Row("""<Label Content="name_" />"""),
        };

        return DemoLayout.BuildComparisonWindow("AccessText", rows);

        static DemoLayout.Row Row(string markup) => new(
            markup,
            SceneContext.LoadXaml<Label>(markup.Replace(" />", """ Padding="0" />""")));
    }

    /// <summary>
    /// 本文「実装例」の 4 つの回避方法が、いずれも my_variable と表示されることを示す。
    /// </summary>
    private static Window BuildWorkaroundWindow()
    {
        var rows = new[]
        {
            new DemoLayout.Row(
                """<Label Content="my__variable" />""",
                SceneContext.LoadXaml<Label>("""<Label Content="my__variable" Padding="0" />""")),
            new DemoLayout.Row(
                """<TextBlock Text="my_variable" />""",
                SceneContext.LoadXaml<TextBlock>("""<TextBlock Text="my_variable" />""")),
            new DemoLayout.Row(
                """<Label ContentTemplate="{TextBlock}" />""",
                SceneContext.LoadXaml<Label>(
                    """
                    <Label Content="my_variable" Padding="0">
                      <Label.ContentTemplate>
                        <DataTemplate>
                          <TextBlock Text="{Binding}" />
                        </DataTemplate>
                      </Label.ContentTemplate>
                    </Label>
                    """)),
            // 差し替えた ControlTemplate のうち、結果を決めている要素だけを見出しに出す。
            new DemoLayout.Row(
                """<ContentPresenter RecognizesAccessKey="False" />""",
                SceneContext.LoadXaml<Label>(
                    """
                    <Label Content="my_variable" Padding="0">
                      <Label.Template>
                        <ControlTemplate TargetType="Label">
                          <ContentPresenter RecognizesAccessKey="False" />
                        </ControlTemplate>
                      </Label.Template>
                    </Label>
                    """)),
        };

        return DemoLayout.BuildComparisonWindow("Label / TextBlock", rows);
    }

    private const string ProbeText = "my_var";

    /// <summary>
    /// 同じ文字列を各コントロールへ与えて実際に描画し、visual ツリーに
    /// <see cref="AccessText"/> が生成されるかを調べる。
    /// アンダーバーが消えるかどうかは、この 1 点で決まる。
    ///
    /// あわせて、対象コントロール自身のテンプレートに属する
    /// <see cref="ContentPresenter"/> を数える。visual ツリーを単に走査すると
    /// 子コントロールのテンプレート部品まで入るため、<c>TemplatedParent</c> で絞る。
    /// </summary>
    private static async Task MeasureAccessKeyScopeAsync(SceneContext context)
    {
        var panel = new StackPanel { Margin = new Thickness(8) };
        List<Probe> probes = CreateProbes().ToList();

        foreach (Probe probe in probes)
        {
            panel.Children.Add(probe.Host);
        }

        var window = new Window
        {
            Title = "AccessText scope",
            Content = new ScrollViewer { Content = panel },
            Width = 360,
            Height = 720,
        };

        try
        {
            await Capture.ShowAndSettleAsync(window);

            // ComboBoxItem やサブメニューのコンテナは、開くまで作られない。
            foreach (Probe probe in probes)
            {
                probe.Realize?.Invoke();
            }

            await Capture.SettleAsync(window);

            var affected = new List<IReadOnlyList<string>>();
            var presenters = new List<IReadOnlyList<string>>();

            foreach (Probe probe in probes)
            {
                DependencyObject[] tree = Descendants(probe.Target).ToArray();
                bool hasAccessText = tree.OfType<AccessText>().Any();

                affected.Add([
                    probe.Name,
                    hasAccessText ? "disappears" : "kept",
                    probe.Property,
                ]);

                if (!probe.CountPresenters)
                {
                    continue;
                }

                ContentPresenter[] own = tree
                    .OfType<ContentPresenter>()
                    .Where(presenter => ReferenceEquals(presenter.TemplatedParent, probe.Target))
                    .ToArray();

                presenters.Add([
                    probe.Name,
                    own.Length.ToString(),
                    own.Length == 0
                        ? "-"
                        : string.Join("  ", own.Select(p =>
                            $"{(string.IsNullOrEmpty(p.Name) ? "(unnamed)" : p.Name)}={p.RecognizesAccessKey}")),
                ]);
            }

            await context.SaveTableAsync(
                $"""Content / Header = "{ProbeText}" — is AccessText created?""",
                ["control", "underscore", "property"],
                affected,
                "label-underscore-affected-matrix.svg");

            await context.SaveTableAsync(
                "ContentPresenter whose TemplatedParent is the control itself",
                ["control", "count", "name = RecognizesAccessKey"],
                presenters,
                "label-underscore-presenter-matrix.svg");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>1 つのコントロールを測るための、表示に必要なホストと測定対象の組。</summary>
    /// <param name="Host">ウィンドウへ載せる要素。単体で成立するなら測定対象と同じ。</param>
    /// <param name="Target">測定対象。<c>TemplatedParent</c> の比較にも使う。</param>
    /// <param name="Realize">コンテナを実体化させるために表示後へ行う操作。</param>
    private sealed record Probe(
        string Name,
        string Property,
        FrameworkElement Host,
        DependencyObject Target,
        bool CountPresenters = false,
        Action? Realize = null);

    private static IEnumerable<Probe> CreateProbes()
    {
        var label = new Label { Content = ProbeText };
        yield return new Probe("Label", "Content", label, label);

        var button = new Button { Content = ProbeText };
        yield return new Probe("Button", "Content", button, button);

        var checkBox = new CheckBox { Content = ProbeText };
        yield return new Probe("CheckBox", "Content", checkBox, checkBox);

        var radioButton = new RadioButton { Content = ProbeText };
        yield return new Probe("RadioButton", "Content", radioButton, radioButton);

        var toggleButton = new ToggleButton { Content = ProbeText };
        yield return new Probe("ToggleButton", "Content", toggleButton, toggleButton);

        var groupBox = new GroupBox { Header = ProbeText, Content = new TextBlock { Text = "body" } };
        yield return new Probe("GroupBox", "Header", groupBox, groupBox, CountPresenters: true);

        var expander = new Expander
        {
            Header = ProbeText,
            Content = new TextBlock { Text = "body" },
            IsExpanded = true,
        };
        yield return new Probe("Expander", "Header", expander, expander, CountPresenters: true);

        var tabItem = new TabItem { Header = ProbeText, Content = new TextBlock { Text = "body" } };
        var tabControl = new TabControl();
        tabControl.Items.Add(tabItem);
        yield return new Probe("TabItem", "Header", tabControl, tabItem, CountPresenters: true);

        // メニューは階層で既定テンプレートが変わる。上位と下位を分けて測る。
        var subMenuItem = new MenuItem { Header = ProbeText };
        var topMenuItem = new MenuItem { Header = ProbeText };
        topMenuItem.Items.Add(subMenuItem);
        var menu = new Menu();
        menu.Items.Add(topMenuItem);
        yield return new Probe("MenuItem (top level)", "Header", menu, topMenuItem, CountPresenters: true);
        yield return new Probe(
            "MenuItem (submenu)", "Header", new TextBlock(), subMenuItem, CountPresenters: true,
            Realize: () => topMenuItem.IsSubmenuOpen = true);

        var treeViewItem = new TreeViewItem { Header = ProbeText };
        var treeView = new TreeView();
        treeView.Items.Add(treeViewItem);
        yield return new Probe("TreeViewItem", "Header", treeView, treeViewItem);

        var listBoxItem = new ListBoxItem { Content = ProbeText };
        var listBox = new ListBox();
        listBox.Items.Add(listBoxItem);
        yield return new Probe("ListBoxItem", "Content", listBox, listBoxItem);

        var comboBoxItem = new ComboBoxItem { Content = ProbeText };
        var comboBox = new ComboBox();
        comboBox.Items.Add(comboBoxItem);
        yield return new Probe(
            "ComboBoxItem", "Content", comboBox, comboBoxItem,
            Realize: () => comboBox.IsDropDownOpen = true);

        var statusBarItem = new StatusBarItem { Content = ProbeText };
        var statusBar = new StatusBar();
        statusBar.Items.Add(statusBarItem);
        yield return new Probe("StatusBarItem", "Content", statusBar, statusBarItem);

        var textBlock = new TextBlock { Text = ProbeText };
        yield return new Probe("TextBlock", "Text", textBlock, textBlock);
    }

    /// <summary>visual ツリーを、自分自身を含めて列挙する。</summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (DependencyObject descendant in Descendants(VisualTreeHelper.GetChild(root, i)))
            {
                yield return descendant;
            }
        }
    }
}
