using System.Windows;
using System.Windows.Automation;
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
/// デモページ「ToggleButton」（apps/wpf-standard-control-demo/togglebutton.html と日本語版）の記述を実測する。
///
/// クリックは UI オートメーションの Toggle（クリックと同じ OnToggle を通る）で行う。
/// ClickMode の違いは、マウスの左ボタンを押す・離すイベントを発生させて確かめる。
/// </summary>
internal sealed class ToggleButtonDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-togglebutton";

    public string ImageDirectory => DemoProbe.ImageDirectory("togglebutton");

    public IReadOnlyList<string> Verifies =>
    [
        "ToggleButton の基底クラスと、ToggleButton を継承するコントロール、IsChecked が既定で TwoWay か、GroupName プロパティの有無",
        "IsThreeState が True と False のときのクリックによる IsChecked の移り変わり（デモアプリと同じく null・false・true から始める）",
        "既定のテンプレートが IsChecked の値（true / null）ごとに見た目を切り替えるトリガーと、VisualStateGroup の有無",
        "UI オートメーションの TogglePattern が報告する状態（ToggleState）",
        "ClickMode が Press のときに、マウスの左ボタンを押した時点と離した時点の IsChecked",
        "クリック処理（OnClick）で Command の Execute が呼ばれた時点の IsChecked と、CommandParameter を自身の IsChecked にバインドしたときに渡る値、UI オートメーションの Toggle では Command が実行されないこと",
        "Popup.IsOpen を IsChecked に結んだとき、Popup を閉じると IsChecked も戻ることと、Popup.IsOpen が既定で TwoWay か",
        "StaysOpen=False の Popup を、実際のマウスでウィンドウの空いた領域をクリックして閉じたとき・開いたままボタンを再クリックしたときの IsOpen と IsChecked",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ToggleButton: type, states, template, ClickMode, Command and Popup",
            ["case", "measured"],
            await MeasureAsync(),
            "togglebutton-behavior.svg");
    }

    private static string State(bool? value) => value switch { null => "null", true => "true", false => "false" };

    private static void Toggle(ToggleButton button) =>
        ((IToggleProvider)new ToggleButtonAutomationPeer(button)).Toggle();

    private sealed class RecordingCommand(ToggleButton owner) : ICommand
    {
        public string Record { get; private set; } = "not executed";

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) =>
            Record = $"IsChecked {State(owner.IsChecked)}, parameter {WpfProbe.Describe(parameter)}";

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["base class / derived: CheckBox, RadioButton",
            $"{typeof(ToggleButton).BaseType!.Name} / {typeof(CheckBox).BaseType!.Name}, {typeof(RadioButton).BaseType!.Name}"]);
        rows.Add(["IsChecked: BindsTwoWayByDefault / GroupName property on ToggleButton",
            $"{((FrameworkPropertyMetadata)ToggleButton.IsCheckedProperty.GetMetadata(typeof(ToggleButton))).BindsTwoWayByDefault} / " +
            $"{typeof(ToggleButton).GetProperty("GroupName") is not null}"]);

        foreach ((bool threeState, bool? start) in new (bool, bool?)[] { (false, null), (false, false), (false, true), (true, false) })
        {
            var button = new ToggleButton { Content = "ToggleButton", IsThreeState = threeState, IsChecked = start };
            await ShowAsync(button, async () =>
            {
                var states = new List<string> { State(button.IsChecked) };
                for (int i = 0; i < 3; i++)
                {
                    Toggle(button);
                    states.Add(State(button.IsChecked));
                }

                rows.Add([$"IsThreeState={threeState}, start {State(start)}: 3 clicks", string.Join(" -> ", states)]);
                await Task.CompletedTask;
            });
        }

        {
            var button = new ToggleButton { Content = "ToggleButton" };
            await ShowAsync(button, async () =>
            {
                var root = (FrameworkElement)VisualTreeHelper.GetChild(button, 0);
                var groups = VisualStateManager.GetVisualStateGroups(root)?.Count ?? 0;
                var triggers = button.Template.Triggers.OfType<Trigger>()
                    .Where(t => t.Property == ToggleButton.IsCheckedProperty)
                    .Select(t => State((bool?)t.Value));
                rows.Add(["default template: VisualStateGroups / triggers on IsChecked (values)",
                    $"{groups} / {string.Join(", ", triggers)}"]);

                var states = new List<string>();
                foreach (bool? value in new bool?[] { false, true, null })
                {
                    button.IsChecked = value;
                    var peer = (IToggleProvider)new ToggleButtonAutomationPeer(button);
                    states.Add($"{State(value)} -> {peer.ToggleState}");
                }

                rows.Add(["UI Automation ToggleState", string.Join(", ", states)]);
                await Task.CompletedTask;
            });
        }

        {
            // ClickMode=Press は、ボタンを押した時点でクリックとして扱う。押したときと離したときの状態を読む。
            // （Release の場合は、実際のマウスのキャプチャが要るため、このイベントの発生だけでは再現できない。）
            var button = new ToggleButton { Content = "ToggleButton", ClickMode = ClickMode.Press };
            await ShowAsync(button, async () =>
            {
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                string down = State(button.IsChecked);
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                rows.Add(["ClickMode=Press: IsChecked after button down / after button up (false before)",
                    $"{down} / {State(button.IsChecked)}"]);
                await Task.CompletedTask;
            }, activate: true);
        }

        {
            // コマンドはクリック処理（OnClick）から実行される。UI オートメーションの Toggle は OnToggle だけを呼ぶため、ここでは OnClick を呼ぶ。
            var button = new ToggleButton { Content = "ToggleButton" };
            var command = new RecordingCommand(button);
            button.Command = command;
            button.SetBinding(ButtonBase.CommandParameterProperty,
                new Binding(nameof(ToggleButton.IsChecked)) { RelativeSource = RelativeSource.Self });
            await ShowAsync(button, async () =>
            {
                typeof(ToggleButton).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(button, null);
                rows.Add(["Command, CommandParameter bound to its own IsChecked; clicked (OnClick) from false: in Execute", command.Record]);

                var withoutClick = new RecordingCommand(button);
                button.Command = withoutClick;
                Toggle(button);
                rows.Add(["  the same button toggled through UI Automation's Toggle: Command", withoutClick.Record]);
                await Task.CompletedTask;
            });
        }

        {
            var button = new ToggleButton { Content = "Open" };
            var popup = new Popup { StaysOpen = false, PlacementTarget = button, Child = new Border { Width = 80, Height = 40, Background = Brushes.White } };
            popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = button });
            var panel = new StackPanel();
            panel.Children.Add(button);
            panel.Children.Add(popup);
            await ShowAsync(panel, async () =>
            {
                Toggle(button);
                await Capture.SettleAsync(Window.GetWindow(button)!);
                string opened = $"IsChecked {State(button.IsChecked)}, IsOpen {popup.IsOpen}";
                popup.IsOpen = false;
                await Capture.SettleAsync(Window.GetWindow(button)!);
                rows.Add(["Popup.IsOpen bound to IsChecked (Mode not set): after click / after the popup closes",
                    $"{opened} / IsChecked {State(button.IsChecked)}"]);
                rows.Add(["  Popup.IsOpen: BindsTwoWayByDefault",
                    ((FrameworkPropertyMetadata)Popup.IsOpenProperty.GetMetadata(typeof(Popup))).BindsTwoWayByDefault.ToString()]);
            });
        }

        {
            // 利用者の操作で閉じる経路を、実際のマウスで確かめる。
            // 「外側」はこのウィンドウの中の空いた領域にする（ウィンドウの外をクリックすると、ほかのアプリを操作してしまうため）。
            var button = new ToggleButton { Content = "Open", Width = 120, Height = 30 };
            var blank = new Border { Width = 200, Height = 120, Background = Brushes.White };
            var popup = new Popup { StaysOpen = false, PlacementTarget = button, Placement = PlacementMode.Right, Child = new Border { Width = 80, Height = 40, Background = Brushes.LightGray } };
            popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = button });
            var panel = new StackPanel { Children = { button, blank, popup } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                window.Topmost = true;
                window.Activate();
                await Capture.SettleAsync(window);

                async Task ClickAsync(FrameworkElement target)
                {
                    await RealMouse.MoveToAsync(target);
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                    await Capture.SettleAsync(window, 100);
                }

                string Now() => $"{popup.IsOpen}, {State(button.IsChecked)}";
                using (RealMouse.Preserve())
                {
                    await ClickAsync(button);
                    string opened = Now();
                    await ClickAsync(blank);
                    rows.Add(["StaysOpen=False, real clicks (IsOpen, IsChecked): button / empty area", $"{opened} / {Now()}"]);

                    await ClickAsync(button);
                    string reopened = Now();
                    await ClickAsync(button);
                    rows.Add(["  button again / button while open", $"{reopened} / {Now()}"]);
                }

                popup.IsOpen = false;
                await Capture.SettleAsync(window);
            });
        }

        return rows;
    }
}
