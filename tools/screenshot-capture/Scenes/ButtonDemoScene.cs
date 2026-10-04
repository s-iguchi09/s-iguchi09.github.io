using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Button」（apps/wpf-standard-control-demo/button.md と日本語版）の記述を実測する。
///
/// Esc・Enter・Space は <see cref="DemoProbe.SendKey"/> で、実際のキー入力と同じ経路から送る。
/// IsCancel / IsDefault はその経路の後処理（アクセスキー）で動くためである。
/// ClickMode の Release と Hover、項目のテンプレートの行のボタンのクリックは、実際のマウス（<see cref="RealMouse"/>）で確かめる。
/// </summary>
internal sealed class ButtonDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-button";

    public string ImageDirectory => DemoProbe.ImageDirectory("button");

    public IReadOnlyList<string> Verifies =>
    [
        "UserControl の中に置いた IsCancel / IsDefault のボタンが、Esc / Enter で押されること（フォーカスは TextBox）",
        "AcceptsReturn の TextBox と、別のボタンにフォーカスがあるときの Enter と、そのときの IsDefaulted",
        "IsCancel のボタンが 2 つあるときの Esc の結果",
        "IsCancel のボタンを Esc で押したとき、ShowDialog のウィンドウは DialogResult false で閉じ、Show のウィンドウは閉じないこと",
        "ClickMode の既定値と、実際のマウスでの Hover / Press / Release のクリックの時点と IsPressed、Release で押したままボタンの外へ出て離したときの結果",
        "Space と Enter のキーでのクリックの時点と IsPressed（ClickMode ごと）、IsPressed が読み取り専用であること",
        "CanExecute が false のときの IsEnabled（IsEnabled を True に設定した場合も）、RequerySuggested に委譲したコマンドの再評価の時点",
        "フォーカスの移動 1 回で CanExecute が呼ばれる回数（同じコマンドを持つボタン 20 個）",
        "デモアプリと同じ XAML（Command の後に CommandParameter をバインド）で読み込んだときに CanExecute へ渡る値、CommandParameter の変化で CanExecute が呼ばれるか、Execute に渡る値",
        "Button の基底クラス、既定のテンプレートの VisualStateGroup とトリガー、ContentPresenter の RecognizesAccessKey",
        "UI オートメーションの名前（文字列の内容、アンダースコア付きの内容、画像だけの内容、AutomationProperties.Name）",
        "ItemTemplate の中のボタンに CommandParameter=\"{Binding}\" を設定し、2 行目を実際にクリックしたときに Execute が受け取る値",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Button: IsCancel and IsDefault (keys sent through the input pipeline)",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureKeysAsync(),
            "button-keys.svg");

        await context.SaveTableAsync(
            "Button: ClickMode and IsPressed (real mouse, keyboard)",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureClickModeAsync(),
            "button-clickmode.svg");

        await context.SaveTableAsync(
            "Button: Command and CommandParameter",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureCommandAsync(),
            "button-command.svg");

        await context.SaveTableAsync(
            "Button: type, default template and UI Automation name",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureTemplateAsync(),
            "button-template.svg");
    }

    /// <summary>押されたボタンを記録する。</summary>
    private sealed class ClickLog
    {
        private readonly List<string> _clicked = [];

        public void Watch(Button button) =>
            button.Click += (_, _) => _clicked.Add((string)button.Content);

        /// <summary>前回読んでから押されたボタン。読むと空にする。</summary>
        public Loc Take()
        {
            Loc result = _clicked.Count == 0 ? Loc.Of("(none)", "（なし）") : string.Join(", ", _clicked);
            _clicked.Clear();
            return result;
        }
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>計測値を " / " で並べたセル。日本語でも区切りは同じにする。</summary>
    private static Loc Both(Loc first, Loc second) => T($"{first.En} / {second.En}", $"{first.Ja} / {second.Ja}");

    private static async Task<List<IReadOnlyList<Loc>>> MeasureKeysAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        var log = new ClickLog();

        {
            // デモアプリと同じく、ボタンは UserControl の中に置く。
            var text = new TextBox { Text = "text" };
            var multiLine = new TextBox { AcceptsReturn = true };
            var other = new Button { Content = "Other" };
            var cancel = new Button { Content = "IsCancel", IsCancel = true };
            var ok = new Button { Content = "IsDefault", IsDefault = true };
            foreach (Button button in new[] { other, cancel, ok })
            {
                log.Watch(button);
            }

            var user = new UserControl { Content = new StackPanel { Width = 200, Children = { text, multiLine, other, cancel, ok } } };
            await ShowAsync(user, async () =>
            {
                Window window = Window.GetWindow(user)!;

                await FocusAsync(text);
                bool defaultedInText = ok.IsDefaulted;
                SendKey(Key.Escape);
                await Capture.SettleAsync(window, 50);
                rows.Add([T("in a UserControl, focus in a TextBox: Esc -> clicked / window still open", "UserControl の中、TextBox にフォーカス: Esc で押されたボタン / ウィンドウが開いたままか"),
                    Both(log.Take(), window.IsVisible.ToString())]);

                SendKey(Key.Enter);
                await Capture.SettleAsync(window, 50);
                Loc entered = log.Take();
                rows.Add([T("  Enter -> clicked (IsDefaulted before the key)", "  Enter で押されたボタン（キーを押す前の IsDefaulted）"), T($"{entered.En} ({defaultedInText})", $"{entered.Ja}（{defaultedInText}）")]);

                await FocusAsync(multiLine);
                SendKey(Key.Enter);
                await Capture.SettleAsync(window, 50);
                rows.Add([T("focus in a TextBox with AcceptsReturn: Enter -> clicked / line breaks in the text", "AcceptsReturn の TextBox にフォーカス: Enter で押されたボタン / 文字列の改行の数"),
                    Both(log.Take(), multiLine.Text.Count(c => c == '\n').ToString())]);

                await FocusAsync(other);
                bool defaultedOnOther = ok.IsDefaulted;
                SendKey(Key.Enter);
                await Capture.SettleAsync(window, 50);
                Loc onOther = log.Take();
                rows.Add([T("focus on another Button: Enter -> clicked (IsDefaulted of the IsDefault button)", "別の Button にフォーカス: Enter で押されたボタン（IsDefault のボタンの IsDefaulted）"),
                    T($"{onOther.En} ({defaultedOnOther})", $"{onOther.Ja}（{defaultedOnOther}）")]);
            }, activate: true);
        }

        {
            var text = new TextBox();
            var first = new Button { Content = "Cancel 1", IsCancel = true };
            var second = new Button { Content = "Cancel 2", IsCancel = true };
            log.Watch(first);
            log.Watch(second);
            var panel = new StackPanel { Width = 200, Children = { text, first, second } };
            await ShowAsync(panel, async () =>
            {
                await FocusAsync(text);
                SendKey(Key.Escape);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                string focused = Keyboard.FocusedElement is Button { Content: string name } ? name : Keyboard.FocusedElement?.GetType().Name ?? "null";
                rows.Add([T("two IsCancel buttons: Esc -> clicked / keyboard focus", "IsCancel のボタンが 2 つ: Esc で押されたボタン / キーボードフォーカス"), Both(log.Take(), focused)]);

                SendKey(Key.Escape);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                focused = Keyboard.FocusedElement is Button { Content: string again } ? again : Keyboard.FocusedElement?.GetType().Name ?? "null";
                rows.Add([T("  Esc again -> clicked / keyboard focus", "  もう一度 Esc: 押されたボタン / キーボードフォーカス"), Both(log.Take(), focused)]);
            }, activate: true);
        }

        {
            var text = new TextBox();
            var cancel = new Button { Content = "Cancel", IsCancel = true };
            var dialog = new Window
            {
                Content = new StackPanel { Width = 200, Children = { text, cancel } },
                SizeToContent = SizeToContent.WidthAndHeight,
            };
            bool closedByKey = true;

            // ContentRendered のハンドラーは async void なので、完了と例外を TaskCompletionSource で受け取る。
            // Esc でダイアログが閉じると ShowDialog はハンドラーの続きを待たずに戻るため、戻った後にこれを待つ。
            var handled = new TaskCompletionSource();
            dialog.ContentRendered += async (_, _) =>
            {
                try
                {
                    await Capture.SettleAsync(dialog);
                    dialog.Activate();
                    await FocusAsync(text);
                    SendKey(Key.Escape);
                    await Capture.SettleAsync(dialog, 50);
                    if (dialog.IsVisible)
                    {
                        closedByKey = false;
                        dialog.Close();
                    }

                    handled.TrySetResult();
                }
                catch (Exception ex)
                {
                    // ダイアログを閉じないと ShowDialog が戻らないので、閉じてから例外を渡す。
                    if (dialog.IsVisible)
                    {
                        dialog.Close();
                    }

                    handled.TrySetException(ex);
                }
            };

            bool? result = dialog.ShowDialog();
            await handled.Task;
            rows.Add([T("IsCancel in a window shown with ShowDialog: Esc -> window closed / ShowDialog returned", "ShowDialog で開いたウィンドウの IsCancel: Esc でウィンドウが閉じたか / ShowDialog の戻り値"),
                $"{closedByKey} / {WpfProbe.Describe(result)}"]);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureClickModeAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        var log = new ClickLog();

        rows.Add([T("default ClickMode / IsPressed is read-only", "ClickMode の既定値 / IsPressed が読み取り専用か"),
            $"{new Button().ClickMode} / {ButtonBase.IsPressedProperty.ReadOnly}"]);

        {
            var blank = new Border { Width = 120, Height = 40, Background = Brushes.White };
            var hover = new Button { Content = "Hover", ClickMode = ClickMode.Hover, Width = 120, Height = 40 };
            var press = new Button { Content = "Press", ClickMode = ClickMode.Press, Width = 120, Height = 40 };
            var release = new Button { Content = "Release", ClickMode = ClickMode.Release, Width = 120, Height = 40 };
            foreach (Button button in new[] { hover, press, release })
            {
                log.Watch(button);
            }

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Children = { blank, hover, press, release } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                window.Topmost = true;
                await Capture.SettleAsync(window);

                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(blank);
                    log.Take();

                    await RealMouse.MoveToAsync(hover);
                    Loc entered = Pressed(log.Take(), hover.IsPressed);
                    await RealMouse.MoveToAsync(blank);
                    rows.Add([T("Hover (mouse): pointer enters / pointer leaves", "Hover（マウス）: ポインターが入る / 出る"),
                        Both(entered, Pressed(log.Take(), hover.IsPressed))]);

                    await RealMouse.MoveToAsync(press);
                    await RealMouse.LeftDownAsync(window);
                    Loc down = Pressed(log.Take(), press.IsPressed);
                    await RealMouse.LeftUpAsync(window);
                    rows.Add([T("Press (mouse): button down / button up", "Press（マウス）: ボタンを押す / 離す"), Both(down, Pressed(log.Take(), press.IsPressed))]);

                    await RealMouse.MoveToAsync(release);
                    await RealMouse.LeftDownAsync(window);
                    down = Pressed(log.Take(), release.IsPressed);
                    await RealMouse.LeftUpAsync(window);
                    rows.Add([T("Release (mouse): button down / button up", "Release（マウス）: ボタンを押す / 離す"), Both(down, Pressed(log.Take(), release.IsPressed))]);

                    await RealMouse.MoveToAsync(release);
                    await RealMouse.LeftDownAsync(window);
                    log.Take();
                    await RealMouse.MoveToAsync(blank);
                    bool outside = release.IsPressed;
                    await RealMouse.LeftUpAsync(window);
                    rows.Add([T("Release (mouse): down on the button, moved off, released outside", "Release（マウス）: ボタンの上で押し、外へ動かして離す"),
                        Both(T($"IsPressed {outside} after moving off", $"外へ動かした後の IsPressed {outside}"), log.Take())]);
                }
            });
        }

        foreach (ClickMode mode in new[] { ClickMode.Release, ClickMode.Press, ClickMode.Hover })
        {
            var button = new Button { Content = mode.ToString(), ClickMode = mode, Width = 120 };
            log.Watch(button);
            await ShowAsync(button, async () =>
            {
                Window window = Window.GetWindow(button)!;
                await FocusAsync(button);

                SendKey(Key.Space);
                await Capture.SettleAsync(window, 50);
                Loc down = Pressed(log.Take(), button.IsPressed);
                SendKey(Key.Space, down: false);
                await Capture.SettleAsync(window, 50);
                Loc up = Pressed(log.Take(), button.IsPressed);
                SendKey(Key.Enter);
                await Capture.SettleAsync(window, 50);
                Loc enter = log.Take();
                SendKey(Key.Enter, down: false);
                rows.Add([T($"{mode} (keyboard): Space down / Space up / Enter down", $"{mode}（キーボード）: Space を押す / Space を離す / Enter を押す"), Both(Both(down, up), enter)]);
            }, activate: true);
        }

        return rows;
    }

    /// <summary>CanExecute の呼び出しを数え、渡された値を記録するコマンド。デモアプリの RelayCommand と同じく RequerySuggested に委譲する。</summary>
    private sealed class ProbeCommand : ICommand
    {
        public Func<object?, bool> Can { get; set; } = _ => true;

        public int CanExecuteCalls { get; set; }

        public List<string> Parameters { get; } = [];

        public string Executed { get; private set; } = "not executed";

        public bool CanExecute(object? parameter)
        {
            CanExecuteCalls++;
            Parameters.Add(WpfProbe.Describe(parameter));
            return Can(parameter);
        }

        public void Execute(object? parameter) => Executed = WpfProbe.Describe(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public sealed class CommandHolder
    {
        public ICommand? Command { get; init; }
    }

    /// <summary>押されたボタンと、その時点の IsPressed。</summary>
    private static Loc Pressed(Loc clicked, bool isPressed) => T($"{clicked.En}, IsPressed {isPressed}", $"{clicked.Ja}、IsPressed {isPressed}");

    private static async Task<List<IReadOnlyList<Loc>>> MeasureCommandAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        {
            bool allowed = false;
            var command = new ProbeCommand { Can = _ => allowed };
            var plain = new Button { Content = "Command", Command = command };
            var forced = new Button { Content = "Command + IsEnabled", Command = command, IsEnabled = true };
            var panel = new StackPanel { Children = { plain, forced } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                rows.Add([T("CanExecute false: IsEnabled / with IsEnabled=\"True\" set", "CanExecute が false: IsEnabled / IsEnabled=\"True\" を指定したボタン"),
                    $"{plain.IsEnabled} / {forced.IsEnabled}"]);

                allowed = true;
                await Capture.SettleAsync(window);
                string before = plain.IsEnabled.ToString();
                CommandManager.InvalidateRequerySuggested();
                await Capture.SettleAsync(window);
                rows.Add([T("CanExecute turns true: IsEnabled / after InvalidateRequerySuggested", "CanExecute が true に変わる: IsEnabled / InvalidateRequerySuggested の後"),
                    $"{before} / {plain.IsEnabled}"]);
            });
        }

        {
            var command = new ProbeCommand();
            var first = new TextBox();
            var second = new TextBox();
            var panel = new StackPanel { Width = 200, Children = { first, second } };
            for (int i = 0; i < 20; i++)
            {
                panel.Children.Add(new Button { Content = $"Button {i}", Command = command });
            }

            await ShowAsync(panel, async () =>
            {
                await FocusAsync(first);
                command.CanExecuteCalls = 0;
                await FocusAsync(second);
                await Capture.SettleAsync(Window.GetWindow(panel)!);
                rows.Add([T("20 buttons, same command: CanExecute calls per focus change", "同じコマンドのボタンが 20 個: フォーカスが 1 回移るごとの CanExecute の呼び出し回数"),
                    command.CanExecuteCalls.ToString()]);
            }, activate: true);
        }

        {
            // デモアプリの CommandParameter の欄と同じ並び（Command の後に CommandParameter）。
            var command = new ProbeCommand { Can = p => p is string { Length: > 0 } };
            var root = (StackPanel)XamlReader.Parse(
                """
                <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="200">
                  <TextBox x:Name="CommandParameterText" Text="ShowMessageText" />
                  <Button x:Name="ClickWithParameterCommandButton"
                          Command="{Binding Command}"
                          CommandParameter="{Binding Text, ElementName=CommandParameterText}"
                          Content="ShowMessageBox" />
                </StackPanel>
                """);
            root.DataContext = new CommandHolder { Command = command };
            var text = (TextBox)root.FindName("CommandParameterText");
            var button = (Button)root.FindName("ClickWithParameterCommandButton");
            await ShowAsync(root, async () =>
            {
                Window window = Window.GetWindow(root)!;
                rows.Add([T("demo XAML: parameters passed to CanExecute while loading", "デモの XAML: 読み込み中に CanExecute へ渡された引数"),
                    string.Join(", ", command.Parameters)]);

                int calls = command.CanExecuteCalls;
                text.Text = "";
                await Capture.SettleAsync(window);
                rows.Add([T("  TextBox.Text set to empty from code: CanExecute calls / IsEnabled", "  コードから TextBox.Text を空にする: CanExecute の呼び出し回数 / IsEnabled"),
                    $"{command.CanExecuteCalls - calls} / {button.IsEnabled}"]);

                text.Text = "Hello";
                await Capture.SettleAsync(window);
                ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
                await Capture.SettleAsync(window);
                rows.Add([T("  TextBox.Text set to Hello, then clicked: parameter of Execute", "  TextBox.Text を Hello にしてクリック: Execute の引数"), command.Executed]);
            });
        }


        {
            // 使用例「一覧の行のボタンが、その行の項目を CommandParameter として渡す」を実際のクリックで確かめる。
            var command = new ProbeCommand();
            var factory = new FrameworkElementFactory(typeof(Button));
            factory.SetBinding(ContentControl.ContentProperty, new Binding());
            factory.SetValue(ButtonBase.CommandProperty, command);
            factory.SetBinding(ButtonBase.CommandParameterProperty, new Binding());
            var list = new ItemsControl { ItemsSource = new[] { "Row 1", "Row 2", "Row 3" }, ItemTemplate = new DataTemplate { VisualTree = factory } };
            var panel = new StackPanel { Width = 160, Children = { list } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                var row = (ContentPresenter)list.ItemContainerGenerator.ContainerFromIndex(1);
                var button = (Button)VisualTreeHelper.GetChild(row, 0);
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(button);
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add([T("item template, CommandParameter=\"{Binding}\": real click on the second row: Execute received", "項目のテンプレート、CommandParameter=\"{Binding}\": 2 行目を実際にクリック: Execute が受け取った値"), command.Executed]);
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureTemplateAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        {
            var button = new Button { Content = "Button" };
            await ShowAsync(button, async () =>
            {
                var root = (FrameworkElement)VisualTreeHelper.GetChild(button, 0);
                int groups = VisualStateManager.GetVisualStateGroups(root)?.Count ?? 0;
                var triggers = button.Template.Triggers.OfType<Trigger>()
                    .Select(t => $"{t.Property.Name}={WpfProbe.Describe(t.Value)}");
                var presenter = Descendants(button).OfType<ContentPresenter>().First();
                rows.Add([T("base classes", "基底クラス"), $"{typeof(Button).BaseType!.Name} -> {typeof(ButtonBase).BaseType!.Name}"]);
                rows.Add([T("default template: VisualStateGroups / triggers", "既定のテンプレート: VisualStateGroups の数 / トリガー"),
                    $"{groups} / {string.Join(", ", triggers)}"]);
                rows.Add(["  ContentPresenter.RecognizesAccessKey", presenter.RecognizesAccessKey.ToString()]);
                await Task.CompletedTask;
            });
        }

        {
            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);
            var text = new Button { Content = "Save" };
            var access = new Button { Content = "_Save" };
            var image = new Button { Content = new Image { Source = bitmap, Width = 16, Height = 16 } };
            var named = new Button { Content = new Image { Source = bitmap, Width = 16, Height = 16 } };
            AutomationProperties.SetName(named, "Save");
            var panel = new StackPanel { Children = { text, access, image, named } };
            await ShowAsync(panel, async () =>
            {
                string Name(Button b) => WpfProbe.Describe(new ButtonAutomationPeer(b).GetName());
                Loc key = Descendants(access).OfType<AccessText>().FirstOrDefault() is { } accessText
                    ? T($"access key {accessText.AccessKey}", $"アクセスキー {accessText.AccessKey}")
                    : T("no AccessText", "AccessText なし");
                rows.Add([T("UI Automation name: Content \"Save\"", "UI オートメーションの名前: Content が \"Save\""), Name(text)]);
                rows.Add([T("  Content \"_Save\"", "  Content が \"_Save\""), T($"{Name(access)} ({key.En})", $"{Name(access)}（{key.Ja}）")]);
                rows.Add([T("  Content is an Image", "  Content が Image"), Name(image)]);
                rows.Add([T("  an Image with AutomationProperties.Name=\"Save\"", "  AutomationProperties.Name=\"Save\" を付けた Image"), Name(named)]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }
}
