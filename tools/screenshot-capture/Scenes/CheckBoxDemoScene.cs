using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「CheckBox」（apps/wpf-standard-control-demo/checkbox.md と日本語版）の記述を実測する。
///
/// クリックは ToggleButton の UI オートメーション（Toggle）で行う。クリックと同じ切り替え処理（OnToggle）を通る。
/// 同意の CheckBox（IsEnabled のバインド）のクリックだけは、実際のマウス（<see cref="RealMouse"/>）で行う。
/// Space キーは、表示したウィンドウでキーを押す・離すイベントを送って再現する。
/// </summary>
internal sealed class CheckBoxDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-checkbox";

    public string ImageDirectory => DemoProbe.ImageDirectory("checkbox");

    public IReadOnlyList<string> Verifies =>
    [
        "CheckBox と RadioButton の基底クラス、IsChecked が既定で TwoWay か、Checked / Unchecked / Indeterminate がバブルするか、VerticalContentAlignment の既定値とその出どころ",
        "IsThreeState が True と False のときに、クリックを繰り返したときの IsChecked の移り変わり（null から始める場合を含む）",
        "bool? と bool のプロパティにバインドしたときに、IsChecked が null になった場合のソースの値とバインドエラー",
        "Space キーで IsChecked が切り替わるか",
        "ラベル（Content）の位置をヒットテストしたときに当たる要素が CheckBox の中の要素か",
        "複数行のラベルで VerticalContentAlignment を Top と Center にしたときの、チェックの記号とラベルの縦位置（CheckBox を高さ 200 に引き伸ばした場合と、ラベルの高さに合わせた場合）",
        "親要素に置いた Checked のハンドラーが子の CheckBox の変化を受け取ること",
        "Button の IsEnabled を CheckBox の IsChecked にバインドし、CheckBox を実際にクリックする前と後の IsEnabled",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "CheckBox: type, states, bindings, keyboard and layout",
            ["case", "measured"],
            await MeasureAsync(),
            "checkbox-behavior.svg");
    }

    private sealed class Source<T> : INotifyPropertyChanged
    {
        private T _value = default!;

        public T Value
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

    private static void Click(CheckBox box) =>
        ((IToggleProvider)new CheckBoxAutomationPeer(box)).Toggle();

    private static string State(bool? value) => value switch { null => "null", true => "true", false => "false" };

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["base class of CheckBox / RadioButton", $"{typeof(CheckBox).BaseType!.Name} / {typeof(RadioButton).BaseType!.Name}"]);
        rows.Add(["IsChecked: BindsTwoWayByDefault",
            ((FrameworkPropertyMetadata)ToggleButton.IsCheckedProperty.GetMetadata(typeof(CheckBox))).BindsTwoWayByDefault.ToString()]);
        rows.Add(["Checked / Unchecked / Indeterminate routing",
            $"{ToggleButton.CheckedEvent.RoutingStrategy} / {ToggleButton.UncheckedEvent.RoutingStrategy} / {ToggleButton.IndeterminateEvent.RoutingStrategy}"]);

        {
            var box = new CheckBox { Content = "CheckBox" };
            var host = new Grid();
            host.Children.Add(box);
            Layout(host, 200, 50);
            rows.Add(["VerticalContentAlignment (value source)", WpfProbe.ValueAndSource(box, Control.VerticalContentAlignmentProperty)]);
        }

        foreach ((bool threeState, bool? start) in new (bool, bool?)[] { (true, false), (false, false), (false, null), (true, null) })
        {
            var box = new CheckBox { Content = "CheckBox", IsThreeState = threeState, IsChecked = start };
            await ShowAsync(box, async () =>
            {
                var states = new List<string> { State(box.IsChecked) };
                for (int i = 0; i < 3; i++)
                {
                    Click(box);
                    states.Add(State(box.IsChecked));
                }

                rows.Add([$"IsThreeState={threeState}, starting at {State(start)}: IsChecked after 3 clicks",
                    string.Join(" -> ", states)]);
                await Task.CompletedTask;
            });
        }

        {
            var nullable = new Source<bool?> { Value = true };
            var plain = new Source<bool> { Value = true };
            var boxNullable = new CheckBox { IsThreeState = true };
            var boxPlain = new CheckBox { IsThreeState = true };
            boxNullable.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Value") { Source = nullable });
            boxPlain.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Value") { Source = plain });
            var panel = new StackPanel();
            panel.Children.Add(boxNullable);
            panel.Children.Add(boxPlain);
            await ShowAsync(panel, async () =>
            {
                Click(boxNullable);
                Click(boxPlain);
                BindingExpression? plainBinding = BindingOperations.GetBindingExpression(boxPlain, ToggleButton.IsCheckedProperty);
                rows.Add(["IsThreeState, bound to bool? (true), clicked to null: IsChecked / source",
                    $"{State(boxNullable.IsChecked)} / {State(nullable.Value)}"]);
                rows.Add(["IsThreeState, bound to bool (true), clicked to null: IsChecked / source / binding error",
                    $"{State(boxPlain.IsChecked)} / {plain.Value.ToString().ToLowerInvariant()} / HasError {plainBinding?.HasError}"]);
                await Task.CompletedTask;
            });
        }

        {
            var box = new CheckBox { Content = "CheckBox" };
            await ShowAsync(box, async () =>
            {
                box.Focus();
                PresentationSource source = PresentationSource.FromVisual(box)!;
                box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
                box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
                await Capture.SettleAsync(Window.GetWindow(box)!);
                rows.Add(["Space pressed and released on a focused CheckBox (false before)", State(box.IsChecked)]);
            }, activate: true);
        }

        {
            var box = new CheckBox { Content = "A fairly long label" };
            var host = new Grid();
            host.Children.Add(box);
            Layout(host, 300, 50);
            var content = Descendants(box).OfType<ContentPresenter>().First();
            Rect label = Bounds(content, host);
            DependencyObject? hit = VisualTreeHelper.HitTest(host, new Point(label.X + label.Width / 2, label.Y + label.Height / 2))?.VisualHit;
            bool inside = hit is not null && (ReferenceEquals(hit, box) || box.IsAncestorOf(hit));
            rows.Add(["hit test in the middle of the label: element hit / inside the CheckBox",
                $"{hit?.GetType().Name ?? "(nothing)"} / {inside}"]);
        }

        // CheckBox を高さ 200 に引き伸ばした場合と、ラベルの高さに合わせた場合（VerticalAlignment=Top）で比べる。
        foreach ((VerticalAlignment alignment, bool fitToLabel) in new[]
        {
            (VerticalAlignment.Top, false),
            (VerticalAlignment.Center, false),
            (VerticalAlignment.Center, true),
        })
        {
            var box = new CheckBox
            {
                Width = 120,
                VerticalAlignment = fitToLabel ? VerticalAlignment.Top : VerticalAlignment.Stretch,
                VerticalContentAlignment = alignment,
                Content = new TextBlock { Text = "a label long enough to wrap onto three lines", TextWrapping = TextWrapping.Wrap },
            };
            var host = new Grid();
            host.Children.Add(box);
            Layout(host, 200, 200);
            FrameworkElement glyph = Descendants(box).OfType<Border>().First(b => b.Name.Contains("checkBoxBorder", StringComparison.OrdinalIgnoreCase));
            var content = Descendants(box).OfType<ContentPresenter>().First();
            Rect g = Bounds(glyph, box);
            Rect c = Bounds(content, box);
            rows.Add([$"3-line label, VerticalContentAlignment={alignment}, CheckBox {D(box.ActualHeight)} high: glyph y / label y",
                $"{D(g.Y)} (height {D(g.Height)}) / {D(c.Y)} (height {D(c.Height)})"]);
        }

        {
            var panel = new StackPanel();
            int received = 0;
            panel.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => received++));
            var box = new CheckBox { Content = "child" };
            panel.Children.Add(box);
            await ShowAsync(panel, async () =>
            {
                Click(box);
                rows.Add(["Checked handler on the parent StackPanel, child clicked: handler calls", received.ToString()]);
                await Task.CompletedTask;
            });
        }


        {
            // 使用例「同意のチェックボックスで、ボタンを有効にする」を、IsEnabled のバインドと実際のクリックで確かめる。
            var agree = new CheckBox { Content = "I agree" };
            var next = new Button { Content = "Next" };
            next.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = agree });
            var panel = new StackPanel { Width = 160, Children = { agree, next } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                string before = next.IsEnabled.ToString();
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(agree);
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add(["Button IsEnabled bound to a CheckBox's IsChecked: before / after a real click on the CheckBox",
                    $"{before} / {next.IsEnabled}"]);
            });
        }

        return rows;
    }
}
