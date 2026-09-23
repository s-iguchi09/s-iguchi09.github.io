using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「ListView」（apps/wpf-standard-control-demo/listview.html と日本語版）の記述を実測する。
///
/// 選択（SelectionMode・SelectedIndex・IsSelected）は ListBox のページで計測済みなので、ここでは既定値だけを読み、
/// GridView の列（自動幅・幅のバインド・並べ替え・見出しのクリック）とスクロールを計測する。
/// 列見出しのドラッグとクリックは実際のマウス（<see cref="RealMouse"/>）で行う。
/// 項目はデモアプリと同じく、Brushes の静的プロパティ（Name と Value）を使う。
/// </summary>
internal sealed class ListViewDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-listview";

    public string ImageDirectory => DemoProbe.ImageDirectory("listview");

    public IReadOnlyList<string> Verifies =>
    [
        "ListView の基底クラスと SelectionMode / View の既定値、View が null のときの見出しの有無",
        "GridViewColumn の Width が自動（NaN）のときの幅と、読み込み後に長い項目が見える位置に来たとき・追加されたときの幅",
        "GridViewColumn の Width を Slider にバインドしたときの列の幅",
        "実際のマウスで列見出しをドラッグしたときの列の順序（AllowsColumnReorder が True / False）",
        "実際のマウスで列見出しをクリックしたときに項目が並べ替わるか",
        "DisplayMemberBinding と CellTemplate を両方設定したときに、例外が起きるか、どちらが表示されるか",
        "幅 500 の列を持つ ListView（デモアプリの構成）で、ScrollBarVisibility が Disabled（デモアプリの初期値）/ Auto のときにスクロールできる範囲",
        "1,000 項目の GridView で作られた項目コンテナの数",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ListView: defaults, GridView columns, reordering, sorting and scrolling",
            ["case", "measured"],
            await MeasureAsync(),
            "listview-behavior.svg");
    }

    /// <summary>デモアプリの StaticBindingSource と同じ項目。</summary>
    public sealed class StaticItem(string name, object? value)
    {
        public string Name { get; } = name;

        public object? Value { get; } = value;
    }

    private static List<StaticItem> BrushItems() =>
        typeof(Brushes).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(p => new StaticItem(p.Name, p.GetValue(null)))
            .ToList();

    private static GridViewColumn Column(string header, string path, double width = double.NaN) => new()
    {
        Header = header,
        DisplayMemberBinding = new Binding(path),
        Width = width,
    };

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T found)
            {
                yield return found;
            }

            foreach (T deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }

    private static GridViewColumnHeader Header(ListView list, GridViewColumn column) =>
        Descendants<GridViewColumnHeader>(list).First(h => h.Column == column);

    private static string Order(GridView view) => string.Join(", ", view.Columns.Select(c => c.Header));

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            var plain = new ListView { Width = 200, Height = 100, ItemsSource = new[] { "Item 1", "Item 2" } };
            await ShowAsync(plain, async () =>
            {
                await Task.CompletedTask;
                rows.Add(["base class; defaults SelectionMode, View; View null: column headers; container",
                    $"{typeof(ListView).BaseType!.Name}; {plain.SelectionMode}, {WpfProbe.Describe(plain.View)}; {Descendants<GridViewColumnHeader>(plain).Count()}; {plain.ItemContainerGenerator.ContainerFromIndex(0)?.GetType().Name}"]);
            });
        }

        {
            List<StaticItem> items = BrushItems();
            var observable = new System.Collections.ObjectModel.ObservableCollection<StaticItem>(items);
            GridViewColumn name = Column("Name", "Name");
            var list = new ListView { Width = 400, Height = 150, ItemsSource = observable, View = new GridView { Columns = { name } } };
            await ShowAsync(list, async () =>
            {
                Window window = Window.GetWindow(list)!;
                double start = name.ActualWidth;
                StaticItem widest = items.MaxBy(i => i.Name.Length)!;
                bool visibleAtStart = list.ItemContainerGenerator.ContainerFromItem(widest) is not null;
                list.ScrollIntoView(widest);
                await Capture.SettleAsync(window, 100);
                double scrolled = name.ActualWidth;
                observable.Insert(0, new StaticItem("A much longer brush name than any other", null));
                list.ScrollIntoView(observable[0]);
                await Capture.SettleAsync(window, 100);
                rows.Add([$"auto width, {items.Count} brushes: at start; {widest.Name} (shown at start {visibleAtStart}) scrolled in",
                    $"{D(start)}; {D(scrolled)}"]);
                rows.Add(["  a longer name inserted at the top: width", D(name.ActualWidth)]);
            });
        }

        {
            var slider = new Slider { Minimum = 50, Maximum = 300, Value = 150 };
            GridViewColumn value = Column("Value (Dynamic Width)", "Value");
            BindingOperations.SetBinding(value, GridViewColumn.WidthProperty, new Binding(nameof(Slider.Value)) { Source = slider });
            var list = new ListView { Width = 400, Height = 150, ItemsSource = BrushItems(), View = new GridView { Columns = { Column("Name", "Name"), value } } };
            var panel = new StackPanel { Children = { slider, list } };
            await ShowAsync(panel, async () =>
            {
                double start = value.ActualWidth;
                slider.Value = 300;
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                rows.Add(["column Width bound to the demo's slider: 150 (start); 300", $"{D(start)}; {D(value.ActualWidth)}"]);
            });
        }

        foreach (bool allows in new[] { true, false })
        {
            GridViewColumn name = Column("Name", "Name", 120);
            GridViewColumn value = Column("Value", "Value", 120);
            var view = new GridView { AllowsColumnReorder = allows, Columns = { name, value } };
            var list = new ListView { Width = 300, Height = 150, ItemsSource = BrushItems(), View = view };
            await ShowAsync(list, async () =>
            {
                Window window = await FrontAsync(list);
                GridViewColumnHeader nameHeader = Header(list, name);
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(nameHeader);
                    await RealMouse.LeftDownAsync(window);
                    foreach (double x in new[] { 90d, 130, 170, 210 })
                    {
                        await RealMouse.MoveToAsync(list, new Point(x, nameHeader.ActualHeight / 2));
                    }

                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add([$"AllowsColumnReorder {allows} (default {new GridView().AllowsColumnReorder}): real drag of Name past Value: order",
                    Order(view)]);
            });
        }

        {
            GridViewColumn name = Column("Name", "Name", 120);
            List<StaticItem> items = BrushItems();
            var list = new ListView { Width = 300, Height = 150, ItemsSource = items, View = new GridView { Columns = { name } } };
            await ShowAsync(list, async () =>
            {
                Window window = await FrontAsync(list);
                string before = ((StaticItem)list.Items[0]).Name;
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(Header(list, name));
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add(["real click on the Name header: first item before; after; sort descriptions",
                    $"{before}; {((StaticItem)list.Items[0]).Name}; {list.Items.SortDescriptions.Count}"]);
            });
        }

        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetValue(TextBlock.TextProperty, "from CellTemplate");
            template.VisualTree = factory;
            var column = new GridViewColumn { Header = "Name", DisplayMemberBinding = new Binding("Name"), Width = 150 };
            string thrown = Throws(() => column.CellTemplate = template);
            var list = new ListView { Width = 300, Height = 100, ItemsSource = BrushItems(), View = new GridView { Columns = { column } } };
            await ShowAsync(list, async () =>
            {
                await Task.CompletedTask;
                var row = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                string shown = string.Join(" / ", Descendants<TextBlock>(row).Select(t => t.Text));
                rows.Add(["DisplayMemberBinding and CellTemplate both set: exception; first row shows", $"{thrown}; {shown}"]);
            });
        }

        foreach (ScrollBarVisibility visibility in new[] { ScrollBarVisibility.Disabled, ScrollBarVisibility.Auto })
        {
            var list = new ListView { Width = 300, Height = 100, View = new GridView { Columns = { new GridViewColumn { Header = "Very Long Column", Width = 500, DisplayMemberBinding = new Binding() } } } };
            for (int i = 1; i <= 5; i++)
            {
                list.Items.Add(new ListViewItem { Content = $"Scroll Test Item {i}" });
            }

            ScrollViewer.SetHorizontalScrollBarVisibility(list, visibility);
            ScrollViewer.SetVerticalScrollBarVisibility(list, visibility);
            await ShowAsync(list, async () =>
            {
                await Task.CompletedTask;
                ScrollViewer viewer = Descendants<ScrollViewer>(list).First();
                rows.Add([$"demo's scroll section (column 500 in a 300 list), both bars {visibility}: scrollable width; height",
                    $"{D(viewer.ScrollableWidth)}; {D(viewer.ScrollableHeight)}"]);
            });
        }

        {
            var list = new ListView
            {
                Width = 300,
                Height = 150,
                ItemsSource = Enumerable.Range(1, 1000).Select(i => new StaticItem($"Item {i}", i)).ToList(),
                View = new GridView { Columns = { Column("Name", "Name", 150) } },
            };
            await ShowAsync(list, async () =>
            {
                await Task.CompletedTask;
                int created = Enumerable.Range(0, 1000).Count(i => list.ItemContainerGenerator.ContainerFromIndex(i) is not null);
                rows.Add(["1,000 items in a GridView 150 high: containers created", created.ToString()]);
            });
        }

        return rows;
    }
}
