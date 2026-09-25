using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「RadioButton」（apps/wpf-standard-control-demo/radiobutton.md と日本語版）の記述を実測する。
///
/// クリックは ButtonBase の OnClick を呼んで行う（実際のクリックと同じく OnToggle を通る）。
/// 矢印キーと Tab は <see cref="DemoProbe.SendKey"/> で、実際のキー入力と同じ経路から送る。
/// </summary>
internal sealed class RadioButtonDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-radiobutton";

    public string ImageDirectory => DemoProbe.ImageDirectory("radiobutton");

    public IReadOnlyList<string> Verifies =>
    [
        "RadioButton の基底クラスと IsChecked の既定値、チェック済みの RadioButton をクリックしたときの IsChecked",
        "GroupName を設定しない RadioButton が、どの範囲でまとまるか（同じ親、別の親、Border で包んだ場合、ItemsControl の項目テンプレート）",
        "同じ親の中の GroupName なしと GroupName ありの RadioButton が互いに影響するか",
        "同じ GroupName が別の親、別のウィンドウ、Popup の中でまとまるか",
        "IsChecked を別々の bool にバインドしたとき、別の RadioButton を選ぶと元のプロパティが false になるか、バインドが残るか、ソースから true にしたときに他が外れるか",
        "矢印キーと Tab キーでのフォーカスと選択の移動",
        "VerticalContentAlignment の既定値（既定のスタイルの適用後）と、複数行のラベルで Top と Center にしたときの記号とラベルの縦位置（RadioButton を高さ 200 に引き伸ばした場合と、ラベルの高さに合わせた場合）",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "RadioButton: which buttons form a group",
            ["case", "measured"],
            await MeasureGroupsAsync(),
            "radiobutton-groups.svg");

        await context.SaveTableAsync(
            "RadioButton: bindings, keyboard and VerticalContentAlignment",
            ["case", "measured"],
            await MeasureBindingAndKeysAsync(),
            "radiobutton-binding-keys.svg");
    }

    private static readonly System.Reflection.MethodInfo OnClickMethod =
        typeof(ToggleButton).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    private static void Click(RadioButton button) => OnClickMethod.Invoke(button, null);

    private static RadioButton Radio(string name, string? group = null) =>
        new() { Content = name, GroupName = group ?? string.Empty };

    /// <summary>チェックされている RadioButton の名前。</summary>
    private static string Checked(IEnumerable<RadioButton> buttons)
    {
        var names = buttons.Where(b => b.IsChecked == true).Select(b => (string)b.Content).ToList();
        return names.Count == 0 ? "(none)" : string.Join(", ", names);
    }

    /// <summary>全部を順にクリックし、最後にチェックが残っているものを返す。</summary>
    private static string ClickAll(IReadOnlyList<RadioButton> buttons)
    {
        foreach (RadioButton button in buttons)
        {
            Click(button);
        }

        return Checked(buttons);
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureGroupsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["base class / default IsChecked", $"{typeof(RadioButton).BaseType!.Name} / {WpfProbe.Describe(new RadioButton().IsChecked)}"]);

        {
            var a = Radio("A");
            var b = Radio("B");
            var panel = new StackPanel { Children = { a, b } };
            await ShowAsync(panel, async () =>
            {
                Click(a);
                Click(a);
                rows.Add(["checked RadioButton clicked again: IsChecked", WpfProbe.Describe(a.IsChecked)]);
                await Task.CompletedTask;
            });
        }

        {
            // GroupName なし。同じ親の 3 つ、別の StackPanel の 2 つ、同じ親の中で Border に包んだ 2 つ。
            var same = new[] { Radio("A"), Radio("B"), Radio("C") };
            var otherPanel = new[] { Radio("D"), Radio("E") };
            var wrapped = new[] { Radio("F"), Radio("G") };
            var root = new StackPanel();
            var first = new StackPanel();
            foreach (RadioButton button in same)
            {
                first.Children.Add(button);
            }

            var second = new StackPanel();
            foreach (RadioButton button in otherPanel)
            {
                second.Children.Add(button);
            }

            var third = new StackPanel();
            foreach (RadioButton button in wrapped)
            {
                third.Children.Add(new Border { Child = button });
            }

            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            await ShowAsync(root, async () =>
            {
                rows.Add(["no GroupName, A-C in one StackPanel: all clicked in turn -> checked", ClickAll(same)]);
                rows.Add(["  D-E in another StackPanel, clicked after A-C -> checked in A-E",
                    $"{ClickAll(otherPanel)}; A-C: {Checked(same)}"]);
                rows.Add(["  F and G each wrapped in a Border in one StackPanel: both clicked -> checked", ClickAll(wrapped)]);
                await Task.CompletedTask;
            });
        }

        {
            var items = new ItemsControl
            {
                ItemsSource = new[] { "H", "I", "J" },
                ItemTemplate = (DataTemplate)XamlReader.Parse(
                    """
                    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                      <RadioButton Content="{Binding}" />
                    </DataTemplate>
                    """),
            };
            await ShowAsync(items, async () =>
            {
                var buttons = Descendants(items).OfType<RadioButton>().ToList();
                string parent = WpfProbe.Describe(buttons[0].Parent?.GetType().Name);
                rows.Add(["no GroupName, RadioButton in an ItemsControl's ItemTemplate: all clicked -> checked (Parent)",
                    $"{ClickAll(buttons)} ({parent})"]);
                await Task.CompletedTask;
            });
        }

        {
            var plain = new[] { Radio("A"), Radio("B") };
            var named = Radio("X", "1");
            var panel = new StackPanel { Children = { plain[0], plain[1], named } };
            await ShowAsync(panel, async () =>
            {
                Click(named);
                Click(plain[0]);
                Click(plain[1]);
                rows.Add(["same parent: A, B without GroupName and X with GroupName=\"1\"; X, A, B clicked -> checked",
                    Checked([.. plain, named])]);
                await Task.CompletedTask;
            });
        }

        {
            var left = Radio("K", "shared");
            var right = Radio("L", "shared");
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { new GroupBox { Content = left }, new GroupBox { Content = right } },
            };
            await ShowAsync(panel, async () =>
            {
                Click(left);
                Click(right);
                rows.Add(["GroupName=\"shared\" in two different GroupBoxes: both clicked -> checked", Checked([left, right])]);
                await Task.CompletedTask;
            });
        }

        {
            var inMain = Radio("M", "shared");
            var inOther = Radio("N", "shared");
            var inPopup = Radio("P", "shared");
            var popup = new Popup { Child = new Border { Background = Brushes.White, Child = inPopup } };
            var mainPanel = new StackPanel { Children = { inMain, popup } };
            var otherWindow = new Window { Content = inOther, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false };
            await ShowAsync(mainPanel, async () =>
            {
                popup.PlacementTarget = inMain;
                popup.IsOpen = true;
                otherWindow.Show();
                await Capture.SettleAsync(otherWindow);
                try
                {
                    Click(inMain);
                    Click(inOther);
                    Click(inPopup);
                    rows.Add(["GroupName=\"shared\" in the window, another window and a Popup: all clicked -> checked",
                        Checked([inMain, inOther, inPopup])]);
                }
                finally
                {
                    popup.IsOpen = false;
                    otherWindow.Close();
                }
            });
        }

        return rows;
    }

    public sealed class Options : INotifyPropertyChanged
    {
        private bool _a = true;
        private bool _b;
        private bool _c;

        public bool A { get => _a; set => Set(ref _a, value); }

        public bool B { get => _b; set => Set(ref _b, value); }

        public bool C { get => _c; set => Set(ref _c, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => $"A={A}, B={B}, C={C}";

        private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureBindingAndKeysAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            var options = new Options();
            var a = Radio("A");
            var b = Radio("B");
            var c = Radio("C");
            a.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(Options.A)));
            b.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(Options.B)));
            c.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(Options.C)));
            var panel = new StackPanel { DataContext = options, Children = { a, b, c } };
            await ShowAsync(panel, async () =>
            {
                Click(b);
                rows.Add(["IsChecked bound to bool A, B, C (Mode not set); B clicked: source / A still bound",
                    $"{options} / {BindingOperations.IsDataBound(a, ToggleButton.IsCheckedProperty)}"]);

                options.C = true;
                await Capture.SettleAsync(Window.GetWindow(panel)!, 20);
                rows.Add(["  then C set to true in the source: source / checked", $"{options} / {Checked([a, b, c])}"]);
            });
        }

        {
            var a = Radio("A");
            var b = Radio("B");
            var c = Radio("C");
            var after = new Button { Content = "after" };
            var panel = new StackPanel { Children = { a, b, c, after } };
            await ShowAsync(panel, async () =>
            {
                Click(a);
                await FocusAsync(a);
                SendKey(Key.Down);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                string focused = Keyboard.FocusedElement is ContentControl { Content: string name } ? name : "?";
                rows.Add(["A checked and focused, Down arrow: focus / checked", $"{focused} / {Checked([a, b, c])}"]);

                await FocusAsync(a);
                SendKey(Key.Tab);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                focused = Keyboard.FocusedElement is ContentControl { Content: string next } ? next : "?";
                rows.Add(["focus on A, Tab: focus", focused]);
            }, activate: true);
        }

        {
            // 既定のスタイルが適用された後の値と出どころを読む（Grid に置いてレイアウトする）。
            var box = new RadioButton { Content = "RadioButton" };
            var host = new Grid();
            host.Children.Add(box);
            Layout(host, 200, 50);
            rows.Add(["VerticalContentAlignment (value source)", WpfProbe.ValueAndSource(box, Control.VerticalContentAlignmentProperty)]);
        }

        // RadioButton を高さ 200 に引き伸ばした場合と、ラベルの高さに合わせた場合（VerticalAlignment=Top）で比べる。
        foreach ((VerticalAlignment alignment, bool fitToLabel) in new[]
        {
            (VerticalAlignment.Top, false),
            (VerticalAlignment.Center, false),
            (VerticalAlignment.Center, true),
        })
        {
            var box = new RadioButton
            {
                Width = 120,
                VerticalAlignment = fitToLabel ? VerticalAlignment.Top : VerticalAlignment.Stretch,
                VerticalContentAlignment = alignment,
                Content = new TextBlock { Text = "a label long enough to wrap onto three lines", TextWrapping = TextWrapping.Wrap },
            };
            var host = new Grid();
            host.Children.Add(box);
            Layout(host, 200, 200);
            FrameworkElement glyph = Descendants(box).OfType<Border>().First(b => b.Name.Contains("radioButtonBorder", StringComparison.OrdinalIgnoreCase));
            var content = Descendants(box).OfType<ContentPresenter>().First();
            Rect g = Bounds(glyph, box);
            Rect c = Bounds(content, box);
            rows.Add([$"3-line label, VerticalContentAlignment={alignment}, RadioButton {D(box.ActualHeight)} high: glyph y / label y",
                $"{D(g.Y)} (height {D(g.Height)}) / {D(c.Y)} (height {D(c.Height)})"]);
        }

        return rows;
    }
}
