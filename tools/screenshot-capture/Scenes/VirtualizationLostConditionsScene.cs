using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で UI 仮想化が効かなくなる条件と切り分け方」の検証。
///
/// 1,000 件の項目を高さ 200 の領域に表示し、実体化された項目のコンテナーを数える。
/// 仮想化が効いていれば表示範囲とキャッシュの分だけ、効いていなければ 1,000 個すべてが作られる。
/// 条件はコントロールの既定値・配置・設定の 3 つに分け、記事の対処を当てた場合も測る。
///
/// 値は記事の切り分け用のコード（<see cref="Report"/>）で読む。記事に載せたコードと同じ方法で数えるためである。
/// 実体化したコンテナーは ItemContainerGenerator.ContainerFromItem で数える。グループ化した場合も
/// 項目のコンテナーだけを数えられ、ComboBox のようにポップアップの中に項目がある場合も同じ方法で数えられる。
/// </summary>
internal sealed class VirtualizationLostConditionsScene : IScene
{
    private const int ItemCount = 1000;

    public IReadOnlyList<string> Verifies =>
    [
        "高さ 200 の領域に 1,000 件を表示したとき、ListBox・ListView（GridView）・DataGrid は表示範囲とキャッシュの分（10〜11 個）しかコンテナーを作らないこと。DataGrid の項目のパネル DataGridRowsPresenter は VirtualizingStackPanel の派生であること",
        "ScrollViewer に入れた ItemsControl、高さ 200 の Grid に直接置いた ItemsControl、ルートのノードが 1,000 個の TreeView は、1,000 個すべてのコンテナーを作ること。いずれも項目のパネルが StackPanel であり、TreeView は IsVirtualizing と CanContentScroll が False であること",
        "ComboBox はドロップダウンを開く前はコンテナーも項目のパネルも作らず、開くと 1,000 個すべてのコンテナーを作り、閉じた後も 1,000 個を持ち続けること。項目のパネルが StackPanel であること",
        "縦の StackPanel・ScrollViewer・Height=Auto の Grid の行に置いた ListBox は、高さが全項目分まで伸び、1,000 個すべてのコンテナーを作ること。項目のパネル・IsVirtualizing・CanContentScroll は仮想化される場合と同じであること",
        "展開した Expander の中の ListBox は、Expander が Grid の中なら仮想化され、縦の StackPanel の中なら 1,000 個すべてを作ること",
        "ScrollViewer.CanContentScroll=False と VirtualizingPanel.IsVirtualizing=False は、高さが 200 のままでも 1,000 個すべてのコンテナーを作り、項目のパネルは VirtualizingStackPanel のままであること",
        "ItemsPanel を StackPanel または WrapPanel に替えた ListBox は、1,000 個すべてのコンテナーを作ること",
        "GroupStyle を付けてグループ化した ListBox は、最上位の項目のパネルが StackPanel に、CanContentScroll が False になり、1,000 個すべてのコンテナーを作ること。GroupStyle を付けなければ、GroupDescriptions があっても IsGrouping は False で、仮想化は保たれること",
        "DataGrid の EnableRowVirtualization=False は、1,000 個すべての行を作り、IsVirtualizing が False になること",
        "VirtualizingPanel.ScrollUnit=Pixel は仮想化を保つこと",
        "ListBoxItem を 1,000 個 Items に直接追加した ListBox は、ContainerFromItem で数えたコンテナーも、項目のパネルの子要素も 11 個で、データを渡した場合と同じになること。IsItemItsOwnContainer が true の項目は、直接追加では 1,000 個、設定の表のデータを渡した行では 0 個であること",
        "ListBox は VirtualizingPanel.CacheLength=0 にするとコンテナーが 11 個から 10 個に、VirtualizingPanel.CacheLengthUnit=Page にすると 20 個になること",
        "VirtualizingPanel.IsVirtualizing=True の TreeView は、VirtualizingPanel.CacheLength=0 にするとコンテナーが 25 個から 13 個になること",
        "TreeView に VirtualizingPanel.IsVirtualizing=True を指定すると、項目のパネルが VirtualizingStackPanel に、CanContentScroll が True になり、仮想化されること",
        "ItemsControl は VirtualizingStackPanel の ItemsPanel と CanContentScroll=True の ScrollViewer のテンプレートで仮想化され、パネルだけを替えた場合は CanContentScroll=True の外側の ScrollViewer に入れても高さ 200 の Grid に直接置いても 1,000 個すべてを作ること",
        "ComboBox に VirtualizingStackPanel の ItemsPanel、GroupStyle 付きのグループ化に VirtualizingPanel.IsVirtualizingWhenGrouping=True、縦の StackPanel の中の ListBox に MaxHeight を指定すると、それぞれ仮想化されること",
        "検索欄の TextBox と ListBox を、Grid の Auto と * の行、または DockPanel（上に寄せた TextBox と最後の子の ListBox）に置くと、ListBox が仮想化されること",
    ];

    public string Slug => "wpf-ui-virtualization-lost-conditions";

    public async Task CaptureAsync(SceneContext context)
    {
        IReadOnlyList<Loc> headers =
        [
            T("condition", "条件"),
            T("containers realized", "実体化したコンテナー"),
            T("items panel", "項目のパネル"),
            T("VirtualizingStackPanel?", "VirtualizingStackPanel か"),
            "IsVirtualizing",
            "CanContentScroll",
            T("height", "高さ"),
        ];

        await context.SaveTableAsync(
            "1,000 items in a 200-high area: default of each control",
            headers,
            await MeasureAsync(Defaults()),
            "virtualization-defaults.svg");

        await context.SaveTableAsync(
            "1,000 items in a 200-high area: where the ListBox is placed",
            headers,
            await MeasureAsync(Placements()),
            "virtualization-placement.svg");

        await context.SaveTableAsync(
            "1,000 items in a 200-high area: settings on the ListBox",
            [.. headers, "IsGrouping", T("children of the items panel", "項目のパネルの子要素"), T("items that are their own container", "自分自身がコンテナーの項目")],
            await MeasureAsync(Settings(), withGrouping: true),
            "virtualization-settings.svg");

        await context.SaveTableAsync(
            "1,000 items in a 200-high area: after the fixes in the article",
            headers,
            await MeasureAsync(Fixes()),
            "virtualization-fixes.svg");
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    public sealed record Row(int Index)
    {
        public string Text => $"item {Index}";

        public int Group => Index / 100;
    }

    private static List<Row> Rows() => Enumerable.Range(0, ItemCount).Select(i => new Row(i)).ToList();

    /// <summary>1 つの条件。<paramref name="Act"/> は表示した後、数える前に行う操作（ドロップダウンを開くなど）。</summary>
    private sealed record Case(Loc Label, FrameworkElement Root, ItemsControl Items, Func<ItemsControl, Task>? Act = null);

    /// <summary>
    /// ドロップダウンの高さ。既定値（MaxDropDownHeight）は画面の高さの 3 分の 1 から決まり、
    /// 画面によって表示範囲とコンテナーの数が変わるため、固定する。
    /// </summary>
    private const double DropDownHeight = 360;

    /// <summary>ComboBox のドロップダウンを開く。</summary>
    private static Task Open(ItemsControl items)
    {
        var combo = (ComboBox)items;
        combo.MaxDropDownHeight = DropDownHeight;
        combo.IsDropDownOpen = true;
        return Task.CompletedTask;
    }

    /// <summary>ComboBox のドロップダウンを開いてから閉じる。</summary>
    private static async Task OpenAndClose(ItemsControl items)
    {
        var combo = (ComboBox)items;
        combo.MaxDropDownHeight = DropDownHeight;
        combo.IsDropDownOpen = true;
        await Capture.SettleAsync(Window.GetWindow(combo)!, 200);
        combo.IsDropDownOpen = false;
    }

    private static IEnumerable<Case> Defaults()
    {
        yield return Fill("ListBox", new ListBox { DisplayMemberPath = nameof(Row.Text) });

        var view = new GridView();
        view.Columns.Add(new GridViewColumn { Header = "Text", DisplayMemberBinding = new Binding(nameof(Row.Text)) });
        yield return Fill("ListView + GridView", new ListView { View = view });

        yield return Fill("DataGrid", new DataGrid { AutoGenerateColumns = true, IsReadOnly = true });

        var items = new ItemsControl { ItemsSource = Rows(), DisplayMemberPath = nameof(Row.Text) };
        yield return new Case(T("ItemsControl in a ScrollViewer", "ScrollViewer に入れた ItemsControl"), Area(new ScrollViewer { Content = items }), items);

        var direct = new ItemsControl { ItemsSource = Rows(), DisplayMemberPath = nameof(Row.Text) };
        yield return new Case(T("ItemsControl directly in the 200-high Grid", "高さ 200 の Grid に直接置いた ItemsControl"), Area(direct), direct);

        var closed = new ComboBox { ItemsSource = Rows(), DisplayMemberPath = nameof(Row.Text), VerticalAlignment = VerticalAlignment.Top };
        yield return new Case(T("ComboBox, before the drop-down is opened", "ComboBox、ドロップダウンを開く前"), Area(closed), closed);

        var combo = new ComboBox { ItemsSource = Rows(), DisplayMemberPath = nameof(Row.Text), VerticalAlignment = VerticalAlignment.Top };
        yield return new Case(T("ComboBox, drop-down open", "ComboBox、ドロップダウンを開く"), Area(combo), combo, Open);

        var again = new ComboBox { ItemsSource = Rows(), DisplayMemberPath = nameof(Row.Text), VerticalAlignment = VerticalAlignment.Top };
        yield return new Case(T("ComboBox, after the drop-down is opened and closed", "ComboBox、ドロップダウンを開いて閉じた後"), Area(again), again, OpenAndClose);

        var tree = new TreeView { ItemTemplate = TextTemplate() };
        tree.ItemsSource = Rows();
        yield return new Case(T("TreeView, 1,000 root nodes", "TreeView、ルートのノード 1,000 個"), Area(tree), tree);
    }

    private static IEnumerable<Case> Placements()
    {
        {
            ListBox list = NewList();
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(list);
            yield return new Case(T("Grid row Height=*", "Height=* の Grid の行"), Area(grid), list);
        }

        {
            ListBox list = NewList();
            yield return new Case(T("vertical StackPanel", "縦の StackPanel"), Area(new StackPanel { Children = { list } }), list);
        }

        {
            ListBox list = NewList();
            yield return new Case(T("ScrollViewer", "ScrollViewer"), Area(new ScrollViewer { Content = list }), list);
        }

        {
            ListBox list = NewList();
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(list);
            yield return new Case(T("Grid row Height=Auto", "Height=Auto の Grid の行"), Area(grid), list);
        }

        {
            ListBox list = NewList();
            yield return new Case(T("expanded Expander in a Grid", "Grid の中の、展開した Expander"), Area(new Expander { Header = "Items", IsExpanded = true, Content = list }), list);
        }

        {
            ListBox list = NewList();
            var expander = new Expander { Header = "Items", IsExpanded = true, Content = list };
            yield return new Case(T("expanded Expander in a vertical StackPanel", "縦の StackPanel の中の、展開した Expander"), Area(new StackPanel { Children = { expander } }), list);
        }
    }

    private static IEnumerable<Case> Settings()
    {
        {
            ListBox list = NewList();
            ScrollViewer.SetCanContentScroll(list, false);
            yield return new Case("ScrollViewer.CanContentScroll=False", Area(list), list);
        }

        {
            ListBox list = NewList();
            VirtualizingPanel.SetIsVirtualizing(list, false);
            yield return new Case("VirtualizingPanel.IsVirtualizing=False", Area(list), list);
        }

        {
            ListBox list = NewList();
            list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
            yield return new Case("ItemsPanel = StackPanel", Area(list), list);
        }

        {
            ListBox list = NewList();
            list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
            yield return new Case("ItemsPanel = WrapPanel", Area(list), list);
        }

        {
            ListBox list = NewList();
            list.ItemsSource = Grouped();
            yield return new Case(T("grouped, no GroupStyle", "グループ化、GroupStyle なし"), Area(list), list);
        }

        {
            ListBox list = NewList();
            list.ItemsSource = Grouped();
            list.GroupStyle.Add(new GroupStyle());
            yield return new Case(T("grouped, with GroupStyle", "グループ化、GroupStyle あり"), Area(list), list);
        }

        {
            var grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, EnableRowVirtualization = false, ItemsSource = Rows() };
            yield return new Case(T("DataGrid, EnableRowVirtualization=False", "DataGrid、EnableRowVirtualization=False"), Area(grid), grid);
        }

        {
            ListBox list = NewList();
            VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
            yield return new Case("VirtualizingPanel.ScrollUnit=Pixel", Area(list), list);
        }

        {
            ListBox list = NewList();
            VirtualizingPanel.SetCacheLength(list, new VirtualizationCacheLength(0));
            yield return new Case("VirtualizingPanel.CacheLength=0", Area(list), list);
        }

        {
            ListBox list = NewList();
            VirtualizingPanel.SetCacheLengthUnit(list, VirtualizationCacheLengthUnit.Page);
            yield return new Case("VirtualizingPanel.CacheLengthUnit=Page", Area(list), list);
        }

        {
            var list = new ListBox();
            foreach (Row row in Rows())
            {
                list.Items.Add(new ListBoxItem { Content = row.Text });
            }

            yield return new Case(T("ListBoxItem added directly to Items (1,000)", "ListBoxItem を Items に直接追加（1,000 個）"), Area(list), list);
        }
    }

    /// <summary>記事の「原因別の対処」の XAML。読み込んだ要素をそのまま測る。</summary>
    private static IEnumerable<Case> Fixes()
    {
        {
            var tree = SceneContext.LoadXaml<TreeView>("""
                <TreeView VirtualizingPanel.IsVirtualizing="True" />
                """);
            tree.ItemTemplate = TextTemplate();
            tree.ItemsSource = Rows();
            yield return new Case(T("TreeView: VirtualizingPanel.IsVirtualizing=True", "TreeView: VirtualizingPanel.IsVirtualizing=True"), Area(tree), tree);
        }

        {
            var tree = SceneContext.LoadXaml<TreeView>("""
                <TreeView VirtualizingPanel.IsVirtualizing="True" VirtualizingPanel.CacheLength="0" />
                """);
            tree.ItemTemplate = TextTemplate();
            tree.ItemsSource = Rows();
            yield return new Case(T("TreeView: VirtualizingPanel.IsVirtualizing=True, CacheLength=0", "TreeView: VirtualizingPanel.IsVirtualizing=True、CacheLength=0"), Area(tree), tree);
        }

        {
            var items = SceneContext.LoadXaml<ItemsControl>("""
                <ItemsControl>
                  <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                      <VirtualizingStackPanel />
                    </ItemsPanelTemplate>
                  </ItemsControl.ItemsPanel>
                  <ItemsControl.Template>
                    <ControlTemplate TargetType="ItemsControl">
                      <ScrollViewer CanContentScroll="True" Focusable="False">
                        <ItemsPresenter />
                      </ScrollViewer>
                    </ControlTemplate>
                  </ItemsControl.Template>
                </ItemsControl>
                """);
            items.DisplayMemberPath = nameof(Row.Text);
            items.ItemsSource = Rows();
            yield return new Case(T("ItemsControl: VirtualizingStackPanel + ScrollViewer in the template", "ItemsControl: VirtualizingStackPanel + テンプレートの ScrollViewer"), Area(items), items);
        }

        {
            var items = SceneContext.LoadXaml<ItemsControl>("""
                <ItemsControl>
                  <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                      <VirtualizingStackPanel />
                    </ItemsPanelTemplate>
                  </ItemsControl.ItemsPanel>
                </ItemsControl>
                """);
            items.DisplayMemberPath = nameof(Row.Text);
            items.ItemsSource = Rows();
            yield return new Case(T("ItemsControl: VirtualizingStackPanel only, in an outer ScrollViewer with CanContentScroll=True", "ItemsControl: VirtualizingStackPanel だけ、CanContentScroll=True の外側の ScrollViewer に入れる"), Area(new ScrollViewer { Content = items, CanContentScroll = true }), items);
        }

        {
            var items = SceneContext.LoadXaml<ItemsControl>("""
                <ItemsControl>
                  <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                      <VirtualizingStackPanel />
                    </ItemsPanelTemplate>
                  </ItemsControl.ItemsPanel>
                </ItemsControl>
                """);
            items.DisplayMemberPath = nameof(Row.Text);
            items.ItemsSource = Rows();
            yield return new Case(T("ItemsControl: VirtualizingStackPanel only, directly in the 200-high Grid", "ItemsControl: VirtualizingStackPanel だけ、高さ 200 の Grid に直接置く"), Area(items), items);
        }

        {
            var combo = SceneContext.LoadXaml<ComboBox>("""
                <ComboBox VerticalAlignment="Top">
                  <ComboBox.ItemsPanel>
                    <ItemsPanelTemplate>
                      <VirtualizingStackPanel />
                    </ItemsPanelTemplate>
                  </ComboBox.ItemsPanel>
                </ComboBox>
                """);
            combo.DisplayMemberPath = nameof(Row.Text);
            combo.ItemsSource = Rows();
            yield return new Case(T("ComboBox: ItemsPanel = VirtualizingStackPanel, drop-down open", "ComboBox: ItemsPanel = VirtualizingStackPanel、ドロップダウンを開く"), Area(combo), combo, Open);
        }

        {
            ListBox list = NewList();
            list.ItemsSource = Grouped();
            list.GroupStyle.Add(new GroupStyle());
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(list, true);
            yield return new Case(T("grouped with GroupStyle: VirtualizingPanel.IsVirtualizingWhenGrouping=True", "GroupStyle ありのグループ化: VirtualizingPanel.IsVirtualizingWhenGrouping=True"), Area(list), list);
        }

        {
            var grid = SceneContext.LoadXaml<Grid>("""
                <Grid>
                  <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                  </Grid.RowDefinitions>
                  <TextBox Grid.Row="0" />
                  <ListBox Grid.Row="1" x:Name="List" />
                </Grid>
                """);
            var list = (ListBox)grid.FindName("List");
            list.DisplayMemberPath = nameof(Row.Text);
            list.ItemsSource = Rows();
            yield return new Case(T("search box: TextBox in an Auto row, ListBox in a * row", "検索欄: Auto の行に TextBox、* の行に ListBox"), Area(grid), list);
        }

        {
            var dock = SceneContext.LoadXaml<DockPanel>("""
                <DockPanel>
                  <TextBox DockPanel.Dock="Top" />
                  <ListBox x:Name="List" />
                </DockPanel>
                """);
            var list = (ListBox)dock.FindName("List");
            list.DisplayMemberPath = nameof(Row.Text);
            list.ItemsSource = Rows();
            yield return new Case(T("search box: TextBox docked at the top, ListBox as the last child of a DockPanel", "検索欄: 上に寄せた TextBox、DockPanel の最後の子に ListBox"), Area(dock), list);
        }

        {
            ListBox list = NewList();
            list.MaxHeight = 200;
            yield return new Case(T("vertical StackPanel: ListBox MaxHeight=200", "縦の StackPanel: ListBox に MaxHeight=200"), Area(new StackPanel { Children = { list } }), list);
        }
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureAsync(IEnumerable<Case> cases, bool withGrouping = false)
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach (Case @case in cases)
        {
            await ShowAsync(@case.Root, async () =>
            {
                if (@case.Act is not null)
                {
                    await @case.Act(@case.Items);
                }

                await Capture.SettleAsync(Window.GetWindow(@case.Root)!, 200);
                Report report = Report.Of(@case.Items);
                var cells = new List<Loc>
                {
                    @case.Label,
                    $"{report.Realized:N0}",
                    report.Panel,
                    WpfProbe.Describe(report.Virtualizing),
                    WpfProbe.Describe(report.IsVirtualizing),
                    report.CanContentScroll is { } scroll ? WpfProbe.Describe(scroll) : "-",
                    report.Height.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture),
                };
                if (withGrouping)
                {
                    cells.Add(WpfProbe.Describe(@case.Items.IsGrouping));
                    cells.Add(report.PanelChildren is { } children ? $"{children:N0}" : "-");
                    cells.Add($"{report.OwnContainers:N0}");
                }

                rows.Add(cells);
            });
        }

        return rows;
    }

    /// <summary>
    /// 記事の切り分け用のコード。実体化したコンテナーの数と、仮想化を左右する値を読む。
    /// </summary>
    private sealed record Report(int Realized, string Panel, bool Virtualizing, bool IsVirtualizing, bool? CanContentScroll, double Height, int? PanelChildren, int OwnContainers)
    {
        public static Report Of(ItemsControl items)
        {
            int realized = items.Items.Cast<object>()
                .Count(item => items.ItemContainerGenerator.ContainerFromItem(item) is not null);

            // ComboBox の項目はポップアップの中にあり、ComboBox の visual ツリーの外になる。
            DependencyObject root = items is ComboBox combo && combo.Template.FindName("PART_Popup", combo) is Popup { Child: { } child }
                ? child
                : items;
            Panel? host = Descendants(root).OfType<Panel>().FirstOrDefault(p => p.IsItemsHost && ItemsControl.GetItemsOwner(p) == items);

            // 項目のパネルを包む ScrollViewer（テンプレートの中にあるもの）の値。コントロールに付いた添付プロパティの値とは限らない。
            ScrollViewer? viewer = host is null ? null : Ancestors(host).TakeWhile(d => d != items).OfType<ScrollViewer>().FirstOrDefault();

            return new Report(
                realized,
                host?.GetType().Name ?? "-",
                host is VirtualizingStackPanel,
                VirtualizingPanel.GetIsVirtualizing(items),
                viewer?.CanContentScroll,
                items.ActualHeight,
                host is null ? null : System.Windows.Media.VisualTreeHelper.GetChildrenCount(host),
                items.Items.Cast<object>().Count(items.IsItemItsOwnContainer));
        }
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject node)
    {
        for (DependencyObject? current = System.Windows.Media.VisualTreeHelper.GetParent(node); current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            yield return current;
        }
    }

    /// <summary>高さ 200 の表示領域。</summary>
    private static Grid Area(UIElement child) => new() { Width = 300, Height = 200, Children = { child } };

    private static ListBox NewList() => new() { ItemsSource = Rows(), DisplayMemberPath = nameof(Row.Text) };

    private static Case Fill(string name, ItemsControl items)
    {
        items.ItemsSource = Rows();
        return new Case(name, Area(items), items);
    }

    private static DataTemplate TextTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new Binding(nameof(Row.Text)));
        return new DataTemplate { VisualTree = factory };
    }

    private static ListCollectionView Grouped()
    {
        var view = new ListCollectionView(Rows());
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Row.Group)));
        return view;
    }
}
