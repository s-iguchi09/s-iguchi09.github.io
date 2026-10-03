using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「TabControl」（apps/wpf-standard-control-demo/tabcontrol.md と日本語版）の記述を実測する。
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
            ["TabStripPlacement", T("Tab1 header", "Tab1 の見出し"), T("Tab2 header", "Tab2 の見出し"), T("content area", "内容の領域")],
            await PlacementRowsAsync(),
            "tabcontrol-placement.svg");

        await context.SaveTableAsync(
            "TabControl: string format, selection, items and keyboard",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureAsync(),
            "tabcontrol-behavior.svg");
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

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

    private static async Task<List<IReadOnlyList<Loc>>> PlacementRowsAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
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

    private static async Task<List<IReadOnlyList<Loc>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        rows.Add([T("base class / default TabStripPlacement / first Dock value (demo start)", "基底クラス / TabStripPlacement の既定値 / Dock の最初の値（デモの初期値）"),
            $"{typeof(TabControl).BaseType!.Name} / {new TabControl().TabStripPlacement} / {Enum.GetValues<Dock>()[0]}"]);

        {
            TabControl tabs = DemoTabs();
            tabs.ContentStringFormat = "Format: {0}.";
            Item(tabs, 1).ContentStringFormat = "Own {0}";
            string before = tabs.SelectedIndex.ToString();
            await ShowAsync(tabs, async () =>
            {
                rows.Add([T("SelectedIndex before showing / after showing", "表示前の SelectedIndex / 表示後"), $"{before} / {tabs.SelectedIndex}"]);
                rows.Add([T("demo \"Format: {0}.\": text / SelectedContent / SelectedContentStringFormat", "デモの \"Format: {0}.\": 表示される文字列 / SelectedContent / SelectedContentStringFormat"),
                    $"{Shown(tabs)} / {WpfProbe.Describe(tabs.SelectedContent)} / {WpfProbe.Describe(tabs.SelectedContentStringFormat)}"]);
                tabs.SelectedIndex = 1;
                await Capture.SettleAsync(Window.GetWindow(tabs)!, 20);
                rows.Add([T("  switched to Tab2 (its own \"Own {0}\"): text / SelectedContentStringFormat", "  Tab2（自身の \"Own {0}\"）に切り替え: 表示される文字列 / SelectedContentStringFormat"),
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
                rows.Add([T("  Tab2 selected before showing: text / SelectedContentStringFormat", "  表示前に Tab2 を選択: 表示される文字列 / SelectedContentStringFormat"),
                    $"{Shown(tabs)} / {WpfProbe.Describe(tabs.SelectedContentStringFormat)}"]);
                await Task.CompletedTask;
            });
        }

        {
            TabControl tabs = DemoTabs();
            await ShowAsync(tabs, async () =>
            {
                string thrown = Throws(() => tabs.SelectedIndex = 5);
                rows.Add([T("2 tabs, SelectedIndex = 5: exception / SelectedIndex", "タブ 2 つ、SelectedIndex = 5: 例外 / SelectedIndex"), $"{thrown} / {tabs.SelectedIndex}"]);
                string belowMinusOne = Throws(() => tabs.SelectedIndex = -2);
                rows.Add([T("  SelectedIndex = -2: exception / SelectedIndex", "  SelectedIndex = -2: 例外 / SelectedIndex"), $"{belowMinusOne} / {tabs.SelectedIndex}"]);
                tabs.SelectedIndex = 1;
                tabs.SelectedItem = new TabItem { Header = "not in the list" };
                rows.Add([T("Tab2 selected, SelectedItem = a TabItem not in the list", "Tab2 を選択中、一覧に無い TabItem を SelectedItem に"), tabs.SelectedIndex.ToString()]);
                tabs.SelectedIndex = -1;
                await Capture.SettleAsync(Window.GetWindow(tabs)!, 20);
                rows.Add([T("SelectedIndex = -1: SelectedContent / text shown", "SelectedIndex = -1: SelectedContent / 表示される文字列"), $"{WpfProbe.Describe(tabs.SelectedContent)} / {Shown(tabs)}"]);
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
                rows.Add([T("3 tabs, Tab2 selected and removed: SelectedIndex / selected tab", "タブ 3 つ、選択中の Tab2 を削除: SelectedIndex / 選ばれているタブ"), $"{tabs.SelectedIndex} / {selected}"]);
            });
        }

        {
            TabControl filled = DemoTabs();
            string first = Throws(() => filled.ItemsSource = new[] { "a", "b" });
            var bound = new TabControl { ItemsSource = new[] { "a", "b" } };
            string second = Throws(() => bound.Items.Add(new TabItem { Header = "Tab3" }));
            rows.Add([T("Items filled, then ItemsSource / ItemsSource, then Items.Add", "Items を埋めてから ItemsSource / ItemsSource を設定してから Items.Add"), $"{first} / {second}"]);
        }

        {
            TabControl tabs = DemoTabs(3);
            await ShowAsync(tabs, async () =>
            {
                await FocusAsync(Item(tabs, 0));
                SendKey(Key.Right);
                await Capture.SettleAsync(Window.GetWindow(tabs)!, 50);
                rows.Add([T("focus on Tab1's header, Right arrow: SelectedIndex / focused header", "Tab1 の見出しにフォーカス、右矢印キー: SelectedIndex / フォーカスのある見出し"),
                    $"{tabs.SelectedIndex} / {(Keyboard.FocusedElement is TabItem { Header: string h } ? h : "(not a header)")}"]);
            }, activate: true);
        }

        return rows;
    }
}
