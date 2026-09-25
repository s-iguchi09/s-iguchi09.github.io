using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「ListBox」（apps/wpf-standard-control-demo/listbox.md と日本語版）の記述を実測する。
///
/// 項目のクリックは、ListBoxItem にマウスの左ボタンのイベントを発生させて再現する。
/// ListBoxItem はこのイベントを受けて ListBox に選択を伝える（修飾キーなしのクリック）。
/// Shift / Ctrl を押しながらのクリックは、キーボードの実際の状態を読むため再現しない。
/// 仮想化と SelectedItems の関係は記事 wpf-listbox-virtualization-selecteditems で実測済み。
/// </summary>
internal sealed class ListBoxDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-listbox";

    public string ImageDirectory => DemoProbe.ImageDirectory("listbox");

    public IReadOnlyList<string> Verifies =>
    [
        "ListBox・ListView・ComboBox の継承関係と、SelectionMode・スクロールバーの既定値（値の出どころ）、ListBoxItem.IsSelected が既定で TwoWay か",
        "SelectionMode（Single / Multiple / Extended）ごとに、修飾キーなしで項目を順にクリックしたときの SelectedItems",
        "SelectAll() を SelectionMode ごとに呼んだときの結果",
        "DisplayMemberPath を設定したときに項目を表示する要素と、存在しないパスを指定したときの表示、ItemTemplate と同時に設定したときの例外",
        "範囲外・-1 の SelectedIndex を設定したときの結果と、SelectedIndex を変えたときの SelectedItem と SelectionChanged",
        "SelectedItem に、同じ内容の別インスタンス（Equals を上書きしないクラスとレコード型）を設定したときに選択されるか",
        "ItemsSource を設定する前に SelectedValue を設定した場合に、後から選択されるか",
        "縦のスクロールバーを Auto と Disabled にしたときのスクロール可能な範囲と、下矢印キーで選択を動かしたときのスクロール",
        "1,000 項目のときに作られる ListBoxItem の数と、コードから SelectedIndex を変えたときにその項目までスクロールするか、ScrollIntoView の効果",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ListBox: types, defaults and selection",
            ["case", "measured"],
            await SelectionAsync(),
            "listbox-selection.svg");

        await context.SaveTableAsync(
            "ListBox: SelectedIndex, SelectedItem, SelectedValue and DisplayMemberPath",
            ["case", "measured"],
            await ValuesAsync(),
            "listbox-values.svg");

        await context.SaveTableAsync(
            "ListBox: scrolling and virtualization (1,000 items, 100 high)",
            ["case", "measured"],
            await ScrollingAsync(),
            "listbox-scrolling.svg");
    }

    private sealed class Device
    {
        public required int Id { get; init; }

        public required string Name { get; init; }
    }

    private sealed record DeviceRecord(int Id, string Name);

    private static ListBox NewList(int count = 4)
    {
        var list = new ListBox { Width = 200, Height = 100 };
        for (int i = 1; i <= count; i++)
        {
            list.Items.Add(new ListBoxItem { Content = $"Item {i}" });
        }

        return list;
    }

    private static void Click(ListBoxItem item) =>
        item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
        });

    private static string Selected(ListBox list) =>
        list.SelectedItems.Count == 0
            ? "(none)"
            : string.Join(", ", list.SelectedItems.Cast<object>().Select(o => o is ListBoxItem i ? i.Content : o));

    private static async Task<List<IReadOnlyList<string>>> SelectionAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["base class of ListBox / ListView / ComboBox",
            $"{typeof(ListBox).BaseType!.Name} / {typeof(ListView).BaseType!.Name} / {typeof(ComboBox).BaseType!.Name}"]);
        rows.Add(["ListBoxItem.IsSelected: BindsTwoWayByDefault",
            ((FrameworkPropertyMetadata)ListBoxItem.IsSelectedProperty.GetMetadata(typeof(ListBoxItem))).BindsTwoWayByDefault.ToString()]);

        {
            ListBox list = NewList();
            var host = new Grid();
            host.Children.Add(list);
            Layout(host, 300, 200);
            rows.Add(["SelectionMode (default)", list.SelectionMode.ToString()]);
            rows.Add(["ScrollViewer.HorizontalScrollBarVisibility / VerticalScrollBarVisibility",
                $"{WpfProbe.ValueAndSource(list, ScrollViewer.HorizontalScrollBarVisibilityProperty)} / " +
                WpfProbe.ValueAndSource(list, ScrollViewer.VerticalScrollBarVisibilityProperty)]);
            rows.Add(["items panel", Descendants(list).OfType<Panel>().First(p => p.IsItemsHost).GetType().Name]);
        }

        foreach (SelectionMode mode in new[] { SelectionMode.Single, SelectionMode.Multiple, SelectionMode.Extended })
        {
            ListBox list = NewList();
            list.SelectionMode = mode;
            await ShowAsync(list, async () =>
            {
                var items = list.Items.Cast<ListBoxItem>().ToArray();
                var steps = new List<string>();
                foreach (int index in new[] { 0, 1, 0 })
                {
                    Click(items[index]);
                    steps.Add(Selected(list));
                }

                rows.Add([$"SelectionMode={mode}: click Item 1, Item 2, Item 1 (no modifier keys)",
                    string.Join("  ->  ", steps)]);
                await Task.CompletedTask;
            }, activate: true);
        }

        foreach (SelectionMode mode in new[] { SelectionMode.Single, SelectionMode.Multiple, SelectionMode.Extended })
        {
            ListBox list = NewList();
            list.SelectionMode = mode;
            string thrown = Throws(list.SelectAll);
            rows.Add([$"SelectionMode={mode}: SelectAll()", $"{thrown}; {list.SelectedItems.Count} selected"]);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> ValuesAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            ListBox list = NewList();
            int changed = 0;
            list.SelectionChanged += (_, _) => changed++;
            list.SelectedIndex = 2;
            string after2 = $"SelectedIndex {list.SelectedIndex}, SelectedItem {((ListBoxItem?)list.SelectedItem)?.Content}, SelectionChanged {changed}";
            string outOfRange = Throws(() => list.SelectedIndex = 10);
            string after10 = $"{outOfRange}; SelectedIndex {list.SelectedIndex}";
            list.SelectedIndex = -1;
            rows.Add(["SelectedIndex = 2", after2]);
            rows.Add(["  then SelectedIndex = 10 (4 items)", after10]);
            rows.Add(["  then SelectedIndex = -1", $"SelectedIndex {list.SelectedIndex}, SelectedItem {WpfProbe.Describe(list.SelectedItem)}"]);
        }

        {
            var items = new[] { new Device { Id = 1, Name = "Desktop" }, new Device { Id = 2, Name = "Laptop" } };
            var list = new ListBox { ItemsSource = items };
            list.SelectedItem = new Device { Id = 2, Name = "Laptop" };
            rows.Add(["class without Equals: SelectedItem = new instance with the same data",
                $"SelectedIndex {list.SelectedIndex}, SelectedItem {WpfProbe.Describe(list.SelectedItem)}"]);
        }

        {
            var items = new[] { new DeviceRecord(1, "Desktop"), new DeviceRecord(2, "Laptop") };
            var list = new ListBox { ItemsSource = items };
            list.SelectedItem = new DeviceRecord(2, "Laptop");
            rows.Add(["record (value equality): SelectedItem = new instance with the same data",
                $"SelectedIndex {list.SelectedIndex}, same instance as the item: {ReferenceEquals(list.SelectedItem, items[1])}"]);
        }

        {
            // ItemsSource より前に SelectedValue を設定する。
            var list = new ListBox { SelectedValuePath = nameof(Device.Id) };
            list.SelectedValue = 2;
            string before = $"SelectedValue {WpfProbe.Describe(list.SelectedValue)}";
            list.ItemsSource = new[] { new Device { Id = 1, Name = "Desktop" }, new Device { Id = 2, Name = "Laptop" } };
            rows.Add(["SelectedValue = 2 before ItemsSource, then ItemsSource set",
                $"{before} -> SelectedIndex {list.SelectedIndex}, SelectedValue {WpfProbe.Describe(list.SelectedValue)}"]);
        }

        foreach (string path in new[] { "Name", "Nope" })
        {
            var list = new ListBox
            {
                Width = 200, Height = 100, DisplayMemberPath = path,
                ItemsSource = new[] { new Device { Id = 1, Name = "Desktop" } },
            };
            await ShowAsync(list, async () =>
            {
                var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                TextBlock? text = Descendants(container).OfType<TextBlock>().FirstOrDefault();
                rows.Add([$"DisplayMemberPath=\"{path}\": element showing item 1",
                    text is null ? "none" : $"TextBlock, Text \"{text.Text}\""]);
                await Task.CompletedTask;
            });
        }

        {
            var list = new ListBox
            {
                Width = 200, Height = 100, DisplayMemberPath = "Name",
                ItemsSource = new[] { new Device { Id = 1, Name = "Desktop" } },
            };
            string thrown = Throws(() => list.ItemTemplate = new DataTemplate());
            string shown = "not shown";
            if (thrown == "no exception")
            {
                try
                {
                    await ShowAsync(list, () => Task.CompletedTask);
                    shown = "shown without exception";
                }
                catch (Exception ex)
                {
                    shown = ex.GetType().Name + " when shown";
                }
            }

            rows.Add(["DisplayMemberPath and ItemTemplate both set", $"{thrown} when set; {shown}"]);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> ScrollingAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        var many = new ObservableCollection<string>(Enumerable.Range(1, 1000).Select(i => $"Item {i}"));

        {
            var list = new ListBox { Width = 200, Height = 100, ItemsSource = many };
            await ShowAsync(list, async () =>
            {
                int realized = Descendants(list).OfType<ListBoxItem>().Count();
                rows.Add(["ListBoxItems created at start", realized.ToString()]);

                var viewer = (ScrollViewer)Descendants(list).OfType<ScrollViewer>().First();
                list.SelectedIndex = 500;
                await Capture.SettleAsync(Window.GetWindow(list)!);
                rows.Add(["SelectedIndex = 500 from code: VerticalOffset / container for item 500",
                    $"{D(viewer.VerticalOffset)} / {(list.ItemContainerGenerator.ContainerFromIndex(500) is null ? "not created" : "created")}"]);

                list.ScrollIntoView(list.SelectedItem);
                await Capture.SettleAsync(Window.GetWindow(list)!);
                rows.Add(["  then ScrollIntoView(SelectedItem): VerticalOffset / container for item 500",
                    $"{D(viewer.VerticalOffset)} / {(list.ItemContainerGenerator.ContainerFromIndex(500) is null ? "not created" : "created")}"]);
            });
        }

        foreach (ScrollBarVisibility visibility in new[] { ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled })
        {
            var list = new ListBox { Width = 200, Height = 100, ItemsSource = many };
            ScrollViewer.SetVerticalScrollBarVisibility(list, visibility);
            await ShowAsync(list, async () =>
            {
                var viewer = (ScrollViewer)Descendants(list).OfType<ScrollViewer>().First();
                string before = $"{D(viewer.ScrollableHeight)} / {viewer.ComputedVerticalScrollBarVisibility}";

                // キーボードで選択を 20 項目下へ動かしたときのスクロール。
                // （マウスホイールはイベントを発生させても Auto の場合すら動かず、再現できなかったため測らない。）
                list.SelectedIndex = 0;
                var first = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0) ?? throw new InvalidOperationException();
                first.Focus();
                for (int i = 0; i < 20; i++)
                {
                    PressKey((UIElement)Keyboard.FocusedElement, Key.Down);
                    await Capture.SettleAsync(Window.GetWindow(list)!);
                }

                rows.Add([$"VerticalScrollBarVisibility={visibility}: scrollable / bar; after 20 Down keys",
                    $"{before}; SelectedIndex {list.SelectedIndex}, offset {D(viewer.VerticalOffset)}"]);
            }, activate: true);
        }

        return rows;
    }
}
