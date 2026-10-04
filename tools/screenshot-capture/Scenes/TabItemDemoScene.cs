using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「TabItem」（apps/wpf-standard-control-demo/tabitem.md と日本語版）の記述を実測する。
/// </summary>
internal sealed class TabItemDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-tabitem";

    public string ImageDirectory => DemoProbe.ImageDirectory("tabitem");

    public IReadOnlyList<string> Verifies =>
    [
        "TabItem の基底クラスと、IsSelected・TabStripPlacement の依存関係プロパティのメタデータ（既定で TwoWay か、読み取り専用か）",
        "TabItem の TabStripPlacement が親の TabControl の値に従うこと",
        "IsSelected を True にしたときの SelectedIndex と他のタブの IsSelected、XAML で複数のタブに IsSelected=True を書いた場合とコードから順に設定した場合に選ばれるタブ",
        "IsSelected にバインドしたソースが、SelectedIndex の変更と、実際のマウスでのタブの見出しのクリックで更新されること",
        "IsEnabled=False のタブを、コード（SelectedIndex）、支援技術が使う UI オートメーション（ISelectionItemProvider.Select）、実際のマウスでの見出しのクリックで選べるか",
        "文字列の Header がどの要素で表示されるか（AccessText が作られ、アンダースコアの次の文字がアクセスキーになること）",
        "Header の長さによるタブの幅の違い",
        "XAML に直接書いた Content と ItemsSource + ContentTemplate で作った内容の、Loaded の回数と起動時に測られるか、タブを切り替えたときに同じインスタンスが再利用されるか",
        "TabControl に HeaderTemplate プロパティがあるか（ItemTemplate との対応）",
        "既定の TabItem テンプレートが VisualStateManager の状態を持つか、Trigger で見た目を切り替えているか",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "TabItem: type, metadata, selection and IsEnabled",
            [T("case", "条件"), T("measured", "計測値")],
            await SelectionAsync(),
            "tabitem-selection.svg");

        await context.SaveTableAsync(
            "TabItem: header and template",
            [T("case", "条件"), T("measured", "計測値")],
            await HeaderAndTemplateAsync(),
            "tabitem-header.svg");

        await context.SaveTableAsync(
            "TabItem content: when it is loaded, and whether it is reused (three tabs, switching 1 -> 2 -> 1)",
            [T("how the content is given", "内容の渡し方"), T("Loaded count per tab", "タブごとの Loaded の回数"), T("same instance after switching back", "戻したときに同じインスタンスか")],
            await ContentLifetimeAsync(),
            "tabitem-content-lifetime.svg");
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>TemplateProperties の " (read-only)" の印を、日本語の表では日本語にする。</summary>
    private static Loc ReadOnlyMarked(string properties) => T(properties, properties.Replace(" (read-only)", "（読み取り専用）"));

    private static TabControl NewTabs(int count, Action<int, TabItem>? configure = null)
    {
        var tabs = new TabControl { Width = 360, Height = 160 };
        for (int i = 0; i < count; i++)
        {
            var item = new TabItem { Header = $"Tab{i + 1}", Content = $"Item{i + 1}" };
            configure?.Invoke(i, item);
            tabs.Items.Add(item);
        }

        return tabs;
    }

    private static TabItem Item(TabControl tabs, int index) => (TabItem)tabs.Items[index]!;

    /// <summary>
    /// 名前が Template で終わるプロパティを、対応する依存関係プロパティが読み取り専用なら "(read-only)" を付けて並べる。
    /// </summary>
    private static string TemplateProperties(Type type) =>
        string.Join(", ", type.GetProperties()
            .Where(p => p.Name.EndsWith("Template"))
            .Select(p => p.Name)
            .Distinct()
            .Select(name =>
            {
                var field = type.GetField(name + "Property",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                bool readOnly = field?.GetValue(null) is DependencyProperty { ReadOnly: true };
                return readOnly ? $"{name} (read-only)" : name;
            }));


    private sealed class Flag : INotifyPropertyChanged
    {
        private bool _value;

        public bool Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static async Task<List<IReadOnlyList<Loc>>> SelectionAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        var isSelected = (FrameworkPropertyMetadata)TabItem.IsSelectedProperty.GetMetadata(typeof(TabItem));

        rows.Add([T("base class", "基底クラス"), typeof(TabItem).BaseType!.Name]);
        rows.Add(["IsSelected: BindsTwoWayByDefault", isSelected.BindsTwoWayByDefault.ToString()]);
        rows.Add([T("TabStripPlacement on TabItem: read-only", "TabItem の TabStripPlacement: 読み取り専用か"), TabItem.TabStripPlacementProperty.ReadOnly.ToString()]);

        {
            TabControl tabs = NewTabs(2);
            tabs.TabStripPlacement = Dock.Left;
            await ShowAsync(tabs, async () =>
            {
                rows.Add([T("TabControl.TabStripPlacement=Left: TabItem.TabStripPlacement of both tabs", "TabControl.TabStripPlacement=Left: 両方のタブの TabItem.TabStripPlacement"),
                    $"{Item(tabs, 0).TabStripPlacement} / {Item(tabs, 1).TabStripPlacement}"]);
                await Task.CompletedTask;
            });
        }

        {
            TabControl tabs = NewTabs(3);
            await ShowAsync(tabs, async () =>
            {
                Item(tabs, 2).IsSelected = true;
                rows.Add([T("Tab3.IsSelected = true: SelectedIndex / IsSelected of Tab1..3", "Tab3.IsSelected = true: SelectedIndex / Tab1..3 の IsSelected"),
                    $"{tabs.SelectedIndex} / {string.Join(", ", tabs.Items.Cast<TabItem>().Select(t => t.IsSelected))}"]);
                await Task.CompletedTask;
            });
        }

        {
            // XAML で 2 つのタブに IsSelected="True" を書く。
            var tabs = (TabControl)XamlReader.Parse(
                """
                <TabControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="360" Height="160">
                  <TabItem Header="Tab1" Content="Item1" />
                  <TabItem Header="Tab2" Content="Item2" IsSelected="True" />
                  <TabItem Header="Tab3" Content="Item3" IsSelected="True" />
                </TabControl>
                """);
            await ShowAsync(tabs, async () =>
            {
                rows.Add([T("XAML: IsSelected=\"True\" on Tab2 and Tab3: SelectedIndex / IsSelected of Tab1..3", "XAML: Tab2 と Tab3 に IsSelected=\"True\": SelectedIndex / Tab1..3 の IsSelected"),
                    $"{tabs.SelectedIndex} / {string.Join(", ", tabs.Items.Cast<TabItem>().Select(t => t.IsSelected))}"]);
                await Task.CompletedTask;
            });
        }

        {
            TabControl tabs = NewTabs(3);
            await ShowAsync(tabs, async () =>
            {
                Item(tabs, 1).IsSelected = true;
                Item(tabs, 2).IsSelected = true;
                rows.Add([T("code: Tab2 then Tab3 set to IsSelected=true: SelectedIndex", "コード: Tab2、続けて Tab3 を IsSelected=true に: SelectedIndex"),
                    tabs.SelectedIndex.ToString()]);
                await Task.CompletedTask;
            });
        }

        {
            // デモアプリの IsSelected 欄と同じく、CheckBox 相当のソースへ IsSelected を結ぶ（Mode 指定なし）。
            var flag1 = new Flag { Value = true };
            var flag2 = new Flag();
            TabControl tabs = NewTabs(2);
            Item(tabs, 0).SetBinding(TabItem.IsSelectedProperty, new Binding(nameof(Flag.Value)) { Source = flag1 });
            Item(tabs, 1).SetBinding(TabItem.IsSelectedProperty, new Binding(nameof(Flag.Value)) { Source = flag2 });
            await ShowAsync(tabs, async () =>
            {
                tabs.SelectedIndex = 1;
                rows.Add([T("IsSelected bound without Mode; SelectedIndex = 1: source values of Tab1 / Tab2", "Mode なしで IsSelected をバインド、SelectedIndex = 1: Tab1 / Tab2 のソースの値"),
                    $"{flag1.Value} / {flag2.Value}"]);

                // デモアプリで Tab1 のチェックボックスをオンにする操作に当たる。
                flag1.Value = true;
                rows.Add([T("  then Tab1's source set to True: SelectedIndex / source values of Tab1 / Tab2", "  続けて Tab1 のソースを True に: SelectedIndex / Tab1 / Tab2 のソースの値"),
                    $"{tabs.SelectedIndex} / {flag1.Value} / {flag2.Value}"]);

                // 実際のマウスで Tab2 の見出しをクリックする。
                Window window = Window.GetWindow(tabs)!;
                window.Topmost = true;
                await Capture.SettleAsync(window);
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(Item(tabs, 1));
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add([T("  then Tab2's header clicked with the real mouse: SelectedIndex / sources of Tab1 / Tab2", "  続けて Tab2 の見出しを実際にマウスでクリック: SelectedIndex / Tab1 / Tab2 のソースの値"),
                    $"{tabs.SelectedIndex} / {flag1.Value} / {flag2.Value}"]);
            });
        }

        {
            TabControl tabs = NewTabs(3, (i, item) => item.IsEnabled = i != 1);
            await ShowAsync(tabs, async () =>
            {
                tabs.SelectedIndex = 1;
                tabs.UpdateLayout();
                var host = (ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
                rows.Add([T("Tab2 IsEnabled=False; SelectedIndex = 1 from code: SelectedIndex / content shown", "Tab2 が IsEnabled=False、コードから SelectedIndex = 1: SelectedIndex / 表示される内容"),
                    $"{tabs.SelectedIndex} / {WpfProbe.Describe(host.Content)}"]);

                tabs.SelectedIndex = 0;
                var peer = (TabItemAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(tabs)
                    .GetChildren().OfType<TabItemAutomationPeer>().ElementAt(1);
                string thrown = Throws(() => ((ISelectionItemProvider)peer).Select());
                rows.Add([T("Tab2 IsEnabled=False; UI Automation ISelectionItemProvider.Select(): result / SelectedIndex", "Tab2 が IsEnabled=False、UI オートメーションの ISelectionItemProvider.Select(): 結果 / SelectedIndex"),
                    $"{thrown} / {tabs.SelectedIndex}"]);

                // 実際のマウスで、無効な Tab2 の見出しをクリックする。
                Window window = Window.GetWindow(tabs)!;
                window.Topmost = true;
                await Capture.SettleAsync(window);
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(Item(tabs, 1));
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add([T("Tab2 IsEnabled=False; header clicked with the real mouse: SelectedIndex", "Tab2 が IsEnabled=False、見出しを実際にマウスでクリック: SelectedIndex"), tabs.SelectedIndex.ToString()]);
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> HeaderAndTemplateAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        {
            TabControl tabs = NewTabs(2, (i, item) => item.Header = i == 0 ? "File_Name" : "A");
            await ShowAsync(tabs, async () =>
            {
                TabItem first = Item(tabs, 0);
                var presenter = Descendants(first).OfType<ContentPresenter>()
                    .First(p => ReferenceEquals(p.Content, first.Header));
                DependencyObject? shown = Descendants(presenter).FirstOrDefault(d => d is TextBlock or AccessText);
                Loc text = shown switch
                {
                    // アンダースコアが表示から消えることは、記事 wpf-label-underscore-issue の
                    // LabelUnderscoreScene で撮影して確かめている。ここでは AccessText が作られることと
                    // アクセスキーだけを読む。
                    AccessText access => T($"AccessText, access key '{access.AccessKey}'", $"AccessText、アクセスキー '{access.AccessKey}'"),
                    TextBlock block => T($"TextBlock, Text \"{block.Text}\"", $"TextBlock、Text \"{block.Text}\""),
                    _ => "(none)",
                };
                rows.Add([T("Header=\"File_Name\": element that shows it", "Header=\"File_Name\": 表示する要素"), text]);
                rows.Add([T("Header \"File_Name\" vs \"A\": tab widths", "Header \"File_Name\" と \"A\": タブの幅"),
                    $"{D(first.ActualWidth)} / {D(Item(tabs, 1).ActualWidth)}"]);
                await Task.CompletedTask;
            });
        }

        // 名前だけでなく、対応する依存関係プロパティが読み取り専用かどうかも示す。
        rows.Add([T("TabControl *Template properties (read-only marked)", "TabControl の *Template プロパティ（読み取り専用には印）"), ReadOnlyMarked(TemplateProperties(typeof(TabControl)))]);
        rows.Add([T("TabItem *Template properties (read-only marked)", "TabItem の *Template プロパティ（読み取り専用には印）"), ReadOnlyMarked(TemplateProperties(typeof(TabItem)))]);

        {
            TabControl tabs = NewTabs(2);
            await ShowAsync(tabs, async () =>
            {
                TabItem item = Item(tabs, 0);
                var root = (FrameworkElement)VisualTreeHelper.GetChild(item, 0);
                var groups = VisualStateManager.GetVisualStateGroups(root)?.Cast<VisualStateGroup>().ToList() ?? [];
                rows.Add([T("default TabItem template: VisualStateGroups on the root", "既定の TabItem テンプレート: ルートの VisualStateGroups"),
                    groups.Count == 0 ? T("none", "なし") : string.Join("; ", groups.Select(g => $"{g.Name}: {string.Join(", ", g.States.Cast<VisualState>().Select(s => s.Name))}"))]);
                rows.Add([T("default TabItem template: number of Triggers", "既定の TabItem テンプレート: Triggers の数"), item.Template.Triggers.Count.ToString()]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }

    /// <summary>
    /// 内容の要素が読み込まれた回数を数える。
    /// </summary>
    private sealed class LoadCounter : Border
    {
        public int Loads { get; private set; }

        public int Measures { get; private set; }

        public LoadCounter()
        {
            Width = 50;
            Height = 20;
            Loaded += (_, _) => Loads++;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Measures++;
            return base.MeasureOverride(constraint);
        }
    }

    private static async Task<List<IReadOnlyList<Loc>>> ContentLifetimeAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // XAML に直接書いた内容（要素そのものが Content）。
        {
            var counters = new[] { new LoadCounter(), new LoadCounter(), new LoadCounter() };
            TabControl tabs = NewTabs(3, (i, item) => item.Content = counters[i]);
            await ShowAsync(tabs, async () =>
            {
                string atStart = string.Join(" / ", counters.Select(c => c.Loads));
                string measuredAtStart = string.Join(" / ", counters.Select(c => c.Measures));
                var host = (ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
                object? first = Descendants(host).OfType<LoadCounter>().FirstOrDefault();
                await Switch(tabs, 1);
                await Switch(tabs, 0);
                object? again = Descendants(host).OfType<LoadCounter>().FirstOrDefault();
                rows.Add([T("elements as TabItem.Content", "TabItem.Content に要素"),
                    T($"at start {atStart}; after 1 -> 2 -> 1: {string.Join(" / ", counters.Select(c => c.Loads))}", $"最初 {atStart}、1 -> 2 -> 1 の後: {string.Join(" / ", counters.Select(c => c.Loads))}"),
                    ReferenceEquals(first, again).ToString()]);
                rows.Add([T("  same, measure calls per tab at start", "  同じ条件、最初のタブごとの測定の回数"), measuredAtStart, "-"]);
            });
        }

        // ItemsSource と ContentTemplate（MVVM でよく使う形）。
        {
            var created = new List<LoadCounter>();
            var tabs = new TabControl
            {
                Width = 360,
                Height = 160,
                ItemsSource = new[] { "A", "B", "C" },
                ContentTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(LoadCounter)) },
            };
            await ShowAsync(tabs, async () =>
            {
                var host = (ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
                LoadCounter? first = Descendants(host).OfType<LoadCounter>().FirstOrDefault();
                if (first is not null)
                {
                    created.Add(first);
                }

                Loc atStart = T($"{created.Count} created", $"{created.Count} 個作成");
                await Switch(tabs, 1);
                LoadCounter? second = Descendants(host).OfType<LoadCounter>().FirstOrDefault();
                await Switch(tabs, 0);
                LoadCounter? again = Descendants(host).OfType<LoadCounter>().FirstOrDefault();
                foreach (LoadCounter? c in new[] { second, again })
                {
                    if (c is not null && !created.Contains(c))
                    {
                        created.Add(c);
                    }
                }

                rows.Add(["ItemsSource + ContentTemplate",
                    T($"at start {atStart.En}; after 1 -> 2 -> 1: {created.Count} instances created", $"最初 {atStart.Ja}、1 -> 2 -> 1 の後: {created.Count} 個作成"),
                    ReferenceEquals(first, again).ToString()]);
            });
        }

        return rows;

        static async Task Switch(TabControl tabs, int index)
        {
            tabs.SelectedIndex = index;
            await Capture.SettleAsync(Window.GetWindow(tabs)!);
        }
    }
}
