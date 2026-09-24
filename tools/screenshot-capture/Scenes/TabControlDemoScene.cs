using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「TabControl」（apps/wpf-standard-control-demo/tabcontrol.html と日本語版）の記述を実測する。
///
/// タブはデモアプリと同じく Header="Tab1" / Content="Item1" の TabItem を並べる。
/// 表示中の内容の文字は、テンプレートの PART_SelectedContentHost の中の TextBlock から読む。
/// </summary>
internal sealed class TabControlDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-tabcontrol";

    public string ImageDirectory => DemoProbe.ImageDirectory("tabcontrol");

    public IReadOnlyList<string> Verifies =>
    [
        "TabControl の基底クラスと TabStripPlacement の既定値、デモアプリのコンボボックスの初期値（Dock の先頭の値）",
        "TabStripPlacement ごとの、2 つのタブの見出しと内容の領域の位置",
        "デモアプリと同じ ContentStringFormat=\"Format: {0}.\" で表示される内容の文字、SelectedContent と SelectedContentStringFormat、TabItem 自身の ContentStringFormat との優先",
        "表示の前と後の SelectedIndex（最初のタブが自動で選ばれるか）",
        "項目数以上の SelectedIndex（5）、-1、-1 未満（-2）、含まれない SelectedItem を設定したときの例外の有無と選択",
        "選択中のタブを取り除いたときに選ばれるタブ",
        "Items に TabItem があるときに ItemsSource を設定した場合と、その逆の例外",
        "タブの見出しにフォーカスがあるときの右矢印キーでの選択の移動",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "TabControl: TabStripPlacement (300 x 150, two tabs)",
            ["TabStripPlacement", "Tab1 header", "Tab2 header", "content area"],
            await PlacementRowsAsync(),
            "tabcontrol-placement.svg");

        await context.SaveTableAsync(
            "TabControl: string format, selection, items and keyboard",
            ["case", "measured"],
            await MeasureAsync(),
            "tabcontrol-behavior.svg");
    }

    private static TabControl DemoTabs(int count = 2)
    {
        var tabs = new TabControl { Width = 300, Height = 150 };
        for (int i = 1; i <= count; i++)
        {
            tabs.Items.Add(new TabItem { Header = $"Tab{i}", Content = $"Item{i}" });
        }

        return tabs;
    }

    private static TabItem Item(TabControl tabs, int index) => (TabItem)tabs.Items[index]!;

    private static string Shown(TabControl tabs)
    {
        var host = (ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
        return Descendants(host).OfType<TextBlock>().FirstOrDefault()?.Text ?? "(nothing)";
    }

    private static async Task<List<IReadOnlyList<string>>> PlacementRowsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (Dock placement in new[] { Dock.Top, Dock.Bottom, Dock.Left, Dock.Right })
        {
            TabControl tabs = DemoTabs();
            tabs.TabStripPlacement = placement;
            await ShowAsync(tabs, async () =>
            {
                var host = (FrameworkElement)tabs.Template.FindName("PART_SelectedContentHost", tabs);
                rows.Add([placement.ToString(), Format(Bounds(Item(tabs, 0), tabs)), Format(Bounds(Item(tabs, 1), tabs)), Format(Bounds(host, tabs))]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["base class / default TabStripPlacement / first Dock value (demo start)",
            $"{typeof(TabControl).BaseType!.Name} / {new TabControl().TabStripPlacement} / {Enum.GetValues<Dock>()[0]}"]);

        {
            TabControl tabs = DemoTabs();
            tabs.ContentStringFormat = "Format: {0}.";
            Item(tabs, 1).ContentStringFormat = "Own {0}";
            string before = tabs.SelectedIndex.ToString();
            await ShowAsync(tabs, async () =>
            {
                rows.Add(["SelectedIndex before showing / after showing", $"{before} / {tabs.SelectedIndex}"]);
                rows.Add(["demo \"Format: {0}.\": text / SelectedContent / SelectedContentStringFormat",
                    $"{Shown(tabs)} / {WpfProbe.Describe(tabs.SelectedContent)} / {WpfProbe.Describe(tabs.SelectedContentStringFormat)}"]);
                tabs.SelectedIndex = 1;
                await Capture.SettleAsync(Window.GetWindow(tabs)!, 20);
                rows.Add(["  switched to Tab2 (its own \"Own {0}\"): text / SelectedContentStringFormat",
                    $"{Shown(tabs)} / {WpfProbe.Describe(tabs.SelectedContentStringFormat)}"]);
            });
        }

        {
            // 切り替えではなく、表示の前から Tab2 を選んでおく。
            TabControl tabs = DemoTabs();
            tabs.ContentStringFormat = "Format: {0}.";
            Item(tabs, 1).ContentStringFormat = "Own {0}";
            tabs.SelectedIndex = 1;
            await ShowAsync(tabs, async () =>
            {
                rows.Add(["  Tab2 selected before showing: text / SelectedContentStringFormat",
                    $"{Shown(tabs)} / {WpfProbe.Describe(tabs.SelectedContentStringFormat)}"]);
                await Task.CompletedTask;
            });
        }

        {
            TabControl tabs = DemoTabs();
            await ShowAsync(tabs, async () =>
            {
                string thrown = Throws(() => tabs.SelectedIndex = 5);
                rows.Add(["2 tabs, SelectedIndex = 5: exception / SelectedIndex", $"{thrown} / {tabs.SelectedIndex}"]);
                string belowMinusOne = Throws(() => tabs.SelectedIndex = -2);
                rows.Add(["  SelectedIndex = -2: exception / SelectedIndex", $"{belowMinusOne} / {tabs.SelectedIndex}"]);
                tabs.SelectedIndex = 1;
                tabs.SelectedItem = new TabItem { Header = "not in the list" };
                rows.Add(["Tab2 selected, SelectedItem = a TabItem not in the list", tabs.SelectedIndex.ToString()]);
                tabs.SelectedIndex = -1;
                await Capture.SettleAsync(Window.GetWindow(tabs)!, 20);
                rows.Add(["SelectedIndex = -1: SelectedContent / text shown", $"{WpfProbe.Describe(tabs.SelectedContent)} / {Shown(tabs)}"]);
            });
        }

        {
            TabControl tabs = DemoTabs(3);
            await ShowAsync(tabs, async () =>
            {
                tabs.SelectedIndex = 1;
                tabs.Items.RemoveAt(1);
                await Capture.SettleAsync(Window.GetWindow(tabs)!, 20);
                string selected = tabs.SelectedItem is TabItem { Header: string header } ? header : "none";
                rows.Add(["3 tabs, Tab2 selected and removed: SelectedIndex / selected tab", $"{tabs.SelectedIndex} / {selected}"]);
            });
        }

        {
            TabControl filled = DemoTabs();
            string first = Throws(() => filled.ItemsSource = new[] { "a", "b" });
            var bound = new TabControl { ItemsSource = new[] { "a", "b" } };
            string second = Throws(() => bound.Items.Add(new TabItem { Header = "Tab3" }));
            rows.Add(["Items filled, then ItemsSource / ItemsSource, then Items.Add", $"{first} / {second}"]);
        }

        {
            TabControl tabs = DemoTabs(3);
            await ShowAsync(tabs, async () =>
            {
                await FocusAsync(Item(tabs, 0));
                SendKey(Key.Right);
                await Capture.SettleAsync(Window.GetWindow(tabs)!, 50);
                rows.Add(["focus on Tab1's header, Right arrow: SelectedIndex / focused header",
                    $"{tabs.SelectedIndex} / {(Keyboard.FocusedElement is TabItem { Header: string h } ? h : "not a header")}"]);
            }, activate: true);
        }

        return rows;
    }
}
