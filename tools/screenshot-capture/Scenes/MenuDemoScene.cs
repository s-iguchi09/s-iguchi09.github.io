using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Menu」（apps/wpf-standard-control-demo/menu.html と日本語版）の記述を実測する。
///
/// メニューの構成はデモアプリ（MenuUsageControl.xaml）の各節と同じにする。
/// クリックとホバーは実際のマウス（<see cref="RealMouse"/>）、Alt・F10・Ctrl+O は実際のキー入力（<see cref="RealKeyboard"/>）で行う。
/// メニューモードへの切り替えとアクセスキーは OS のキー入力から判断されるためである。
/// Copy のメニュー項目はクリックしない（計測する PC のクリップボードを書き換えないため）。有効かどうかだけを読む。
/// </summary>
internal sealed class MenuDemoScene : IScene
{
    private const ushort VkAlt = 0x12;
    private const ushort VkControl = 0x11;
    private const ushort VkEscape = 0x1B;
    private const ushort VkF10 = 0x79;
    private const ushort VkM = 0x4D;
    private const ushort VkO = 0x4F;

    public string Slug => "wpf-standard-control-demo-menu";

    public string ImageDirectory => DemoProbe.ImageDirectory("menu");

    public IReadOnlyList<string> Verifies =>
    [
        "Menu の IsMainMenu と、MenuItem の IsCheckable / IsChecked / StaysOpenOnClick / InputGestureText の既定値",
        "F10 が効かなかった条件の切り分け（フォーカスが Button にあるとき、WPF の InputManager を通して F10 を送ったとき）",
        "デモアプリの Role の節と同じ階層での各 MenuItem の Role と、子のない最上位項目に子を追加したときの Role",
        "実際のマウスで項目をクリックしたときの IsChecked と、サブメニューが開いたままか（IsCheckable / StaysOpenOnClick の組み合わせ）",
        "IsChecked を TwoWay で結んだ CheckBox がクリックに追従するか、IsCheckable の 2 項目が排他になるか",
        "実際の Alt / F10 / Alt+M を押したときにメニューへ入るか（IsMainMenu が True / False、デモアプリの Main Menu(_M)）",
        "InputGestureText=\"Ctrl+O\" の項目の表示と、実際の Ctrl+O で Click が起きるか。Command=ApplicationCommands.Copy の項目の InputGestureText",
        "Command=ApplicationCommands.Copy の項目の IsEnabled（メニューを閉じたまま / 実際のクリックで開いたとき。TextBox の選択の有無、Button にフォーカスがあるとき）",
        "実際のマウスでホバー・押下・解放したときの最上位項目の IsHighlighted / IsPressed / IsSubmenuOpen / IsSuspendingPopupAnimation",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Menu: roles, checkable items, the main menu, gestures and commands (the demo's menus)",
            ["case", "measured"],
            await MeasureAsync(),
            "menu-behavior.svg");
    }

    /// <summary>F10 のようなシステムキーを、WPF の入力管理（InputManager）を通してフォーカスのある要素へ送る。</summary>
    private static void SendSystemKey(Key key, bool down)
    {
        var target = (DependencyObject)Keyboard.FocusedElement!;
        PresentationSource source = PresentationSource.FromDependencyObject(target)!;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent,
        };

        // OS からは Key が System、実際のキー（SystemKey）が F10 として届く。内部のフィールドを同じ形にする。
        System.Reflection.FieldInfo field = typeof(KeyEventArgs).GetField("_key", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("KeyEventArgs._key が見つからない。");
        field.SetValue(args, Key.System);
        if (args.Key != Key.System || args.SystemKey != key)
        {
            throw new InvalidOperationException($"システムキーの形にできない（Key {args.Key}, SystemKey {args.SystemKey}）。");
        }

        InputManager.Current.ProcessInput(args);
    }

    private static MenuItem Item(string header, params MenuItem[] children)
    {
        var item = new MenuItem { Header = header };
        foreach (MenuItem child in children)
        {
            item.Items.Add(child);
        }

        return item;
    }

    private static Menu MenuOf(bool isMainMenu, params MenuItem[] items)
    {
        var menu = new Menu { IsMainMenu = isMainMenu };
        foreach (MenuItem item in items)
        {
            menu.Items.Add(item);
        }

        return menu;
    }

    private static async Task ClickAsync(Window window, FrameworkElement target)
    {
        await RealMouse.MoveToAsync(target, null, window);
        await RealMouse.LeftDownAsync(window, target);
        await RealMouse.LeftUpAsync(window);
    }

    /// <summary>開いたサブメニューの中の項目のコンテナ（ポップアップの中にある）。</summary>
    private static MenuItem Container(MenuItem parent, int index) =>
        (MenuItem)parent.ItemContainerGenerator.ContainerFromIndex(index);

    private static T? Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T found && match(found))
            {
                return found;
            }

            if (Find(child, match) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaultItem = new MenuItem();
        rows.Add(["defaults: Menu.IsMainMenu; MenuItem IsCheckable, IsChecked, StaysOpenOnClick, InputGestureText",
            $"{new Menu().IsMainMenu}; {defaultItem.IsCheckable}, {defaultItem.IsChecked}, {defaultItem.StaysOpenOnClick}, {WpfProbe.Describe(defaultItem.InputGestureText)}"]);

        {
            MenuItem subItem = Item("SubmenuItem");
            MenuItem subHeader = Item("SubmenuHeader", subItem);
            MenuItem topHeader = Item("TopLevelHeader", subHeader);
            MenuItem topItem = Item("TopLevelItem");
            Menu menu = MenuOf(false, topHeader, topItem);
            await ShowAsync(menu, async () =>
            {
                rows.Add(["demo's Role section: the four items",
                    $"{topHeader.Role}, {subHeader.Role}, {subItem.Role}, {topItem.Role}"]);
                topItem.Items.Add(new MenuItem { Header = "added" });
                await Capture.SettleAsync(Window.GetWindow(menu)!, 20);
                rows.Add(["  a child added to TopLevelItem: its Role", topItem.Role.ToString()]);
            });
        }

        foreach ((bool checkable, bool staysOpen) in new[] { (true, true), (true, false), (false, true) })
        {
            MenuItem interactive = Item("Interactive Item");
            interactive.IsCheckable = checkable;
            interactive.StaysOpenOnClick = staysOpen;
            MenuItem options = Item("Options", interactive);
            var check = new CheckBox { Content = "IsChecked" };
            check.SetBinding(ToggleButton.IsCheckedProperty,
                new Binding(nameof(MenuItem.IsChecked)) { Source = interactive, Mode = BindingMode.TwoWay });
            var panel = new StackPanel { Width = 240, Children = { MenuOf(false, options), check } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                using (RealMouse.Preserve())
                {
                    await ClickAsync(window, options);
                    bool opened = options.IsSubmenuOpen;
                    await ClickAsync(window, Container(options, 0));
                    string first = $"{interactive.IsChecked} / {WpfProbe.Describe(check.IsChecked)} / {options.IsSubmenuOpen}";
                    string second = "-";
                    if (options.IsSubmenuOpen)
                    {
                        await ClickAsync(window, Container(options, 0));
                        second = $"{interactive.IsChecked} / {options.IsSubmenuOpen}";
                    }

                    rows.Add([$"IsCheckable {checkable}, StaysOpenOnClick {staysOpen}: opened; click: checked / CheckBox / open; again",
                        $"{opened}; {first}; {second}"]);
                }

                options.IsSubmenuOpen = false;
                await Capture.SettleAsync(window, 50);
                check.IsChecked = true;
                await Capture.SettleAsync(window, 20);
                if (checkable && staysOpen)
                {
                    rows.Add(["  the CheckBox checked in code: item IsChecked", interactive.IsChecked.ToString()]);
                }
            });
        }

        {
            MenuItem first = Item("First");
            MenuItem second = Item("Second");
            first.IsCheckable = second.IsCheckable = true;
            first.StaysOpenOnClick = second.StaysOpenOnClick = true;
            MenuItem options = Item("Options", first, second);
            Menu menu = MenuOf(false, options);
            var panel = new StackPanel { Width = 240, Height = 120, Children = { menu } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                using (RealMouse.Preserve())
                {
                    await ClickAsync(window, options);
                    await ClickAsync(window, Container(options, 0));
                    await ClickAsync(window, Container(options, 1));
                }

                rows.Add(["two checkable items, both clicked: IsChecked", $"{first.IsChecked}, {second.IsChecked}"]);
                options.IsSubmenuOpen = false;
                await Capture.SettleAsync(window, 50);
            });
        }

        foreach (bool isMainMenu in new[] { true, false })
        {
            foreach ((string name, ushort key, ushort[] modifiers) in new[]
                     {
                         ("Alt", VkAlt, Array.Empty<ushort>()),
                         ("F10", VkF10, Array.Empty<ushort>()),
                         ("Alt+M", VkM, new[] { VkAlt }),
                     })
            {
                MenuItem main = Item("Main Menu(_M)", Item("Item 1"));
                var box = new TextBox { Width = 200 };
                var panel = new StackPanel { Width = 240, Height = 120, Children = { MenuOf(isMainMenu, main), box } };
                await ShowAsync(panel, async () =>
                {
                    Window window = await FrontAsync(panel);
                    await FocusAsync(box);
                    await RealKeyboard.PressAsync(window, key, modifiers);
                    rows.Add([$"IsMainMenu {isMainMenu}, real {name}: highlighted / submenu open / TextBox keeps focus",
                        $"{main.IsHighlighted} / {main.IsSubmenuOpen} / {box.IsKeyboardFocused}"]);
                    if (main.IsHighlighted || main.IsSubmenuOpen)
                    {
                        await RealKeyboard.PressAsync(window, VkEscape);
                        await RealKeyboard.PressAsync(window, VkEscape);
                    }
                });
            }
        }

        // 公式ドキュメントは IsMainMenu を「ALT と F10 の通知を受けるか」と説明している。F10 が効かなかった条件を切り分ける。
        foreach ((string name, bool onButton, bool throughInputManager) in new[]
                 {
                     ("real F10, a Button focused", true, false),
                     ("F10 through WPF's InputManager, TextBox focused", false, true),
                 })
        {
            MenuItem main = Item("Main Menu(_M)", Item("Item 1"));
            var box = new TextBox { Width = 200 };
            var button = new Button { Content = "Button" };
            var panel = new StackPanel { Width = 240, Height = 140, Children = { MenuOf(true, main), box, button } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                await FocusAsync(onButton ? button : box);
                if (throughInputManager)
                {
                    // F10 は WPF ではシステムキー（Key.System、SystemKey=F10）として届く。
                    SendSystemKey(Key.F10, down: true);
                    SendSystemKey(Key.F10, down: false);
                    await Capture.SettleAsync(window, 100);
                }
                else
                {
                    await RealKeyboard.PressAsync(window, VkF10);
                }

                rows.Add([$"IsMainMenu True, {name}: highlighted / submenu open / focus moved to the menu",
                    $"{main.IsHighlighted} / {main.IsSubmenuOpen} / {main.IsKeyboardFocusWithin}"]);
                if (main.IsHighlighted || main.IsKeyboardFocusWithin)
                {
                    await RealKeyboard.PressAsync(window, VkEscape);
                }
            });
        }

        {
            int clicks = 0;
            MenuItem open = Item("Open...");
            open.InputGestureText = "Ctrl+O";
            open.Icon = new Rectangle { Width = 12, Height = 12, Fill = Brushes.DodgerBlue };
            open.Click += (_, _) => clicks++;
            MenuItem copy = new() { Header = "Copy to Clipboard", Command = ApplicationCommands.Copy };
            MenuItem file = Item("File", open, copy);
            var box = new TextBox { Width = 200 };
            var panel = new StackPanel { Width = 260, Height = 140, Children = { MenuOf(false, file), box } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                file.IsSubmenuOpen = true;
                await Capture.SettleAsync(window, 150);
                MenuItem openContainer = Container(file, 0);
                string shown = Find<TextBlock>(openContainer, t => t.Text == "Ctrl+O") is { } gesture
                    ? $"shown ({D(gesture.ActualWidth)} wide)"
                    : "not shown";
                rows.Add(["InputGestureText \"Ctrl+O\": shown / InputGestureText of the Copy item",
                    $"{shown} / {WpfProbe.Describe(copy.InputGestureText)}"]);
                file.IsSubmenuOpen = false;
                await Capture.SettleAsync(window, 50);
                await FocusAsync(box);
                await RealKeyboard.PressAsync(window, VkO, VkControl);
                rows.Add(["  real Ctrl+O with a TextBox focused: Click count", clicks.ToString()]);
            });
        }

        {
            MenuItem copy = new() { Header = "Copy to Clipboard", Command = ApplicationCommands.Copy };
            MenuItem execute = Item("Execute", copy);
            var box = new TextBox { Width = 200, Text = "Selected text to Copy" };
            var button = new Button { Content = "other" };
            var panel = new StackPanel { Width = 260, Height = 140, Children = { box, MenuOf(false, execute), button } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);

                string focusWhileOpen = "";

                async Task<bool> OpenedAsync()
                {
                    using (RealMouse.Preserve())
                    {
                        await ClickAsync(window, execute);
                    }

                    bool state = copy.IsEnabled;
                    focusWhileOpen = Keyboard.FocusedElement?.GetType().Name ?? "none";
                    execute.IsSubmenuOpen = false;
                    await Capture.SettleAsync(window, 50);
                    return state;
                }

                await FocusAsync(box);
                box.Select(0, 0);
                CommandManager.InvalidateRequerySuggested();
                await Capture.SettleAsync(window, 50);
                rows.Add(["Copy item, menu closed, TextBox focused, no selection: IsEnabled / CanExecute",
                    $"{copy.IsEnabled} / {ApplicationCommands.Copy.CanExecute(null, copy)}"]);
                bool none = await OpenedAsync();
                await FocusAsync(box);
                box.SelectAll();
                bool selected = await OpenedAsync();
                string focusSelected = focusWhileOpen;
                await FocusAsync(button);
                bool onButton = await OpenedAsync();
                rows.Add(["  opened by real click: no selection; all selected (focus); Button focused",
                    $"{none}; {selected} ({focusSelected}); {onButton}"]);
            });
        }

        {
            MenuItem target = Item("Observation Target", Item("Sub Item 1"), Item("Sub Item 2"));
            var panel = new StackPanel { Width = 240, Height = 120, Children = { MenuOf(false, target) } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                string State() =>
                    $"{target.IsHighlighted} / {target.IsPressed} / {target.IsSubmenuOpen} / {target.IsSuspendingPopupAnimation}";

                using (RealMouse.Preserve())
                {
                    string before = State();
                    await RealMouse.MoveToAsync(target);
                    string hover = State();
                    await RealMouse.LeftDownAsync(window);
                    string down = State();
                    await RealMouse.LeftUpAsync(window);
                    string up = State();
                    rows.Add(["Observation Target: Highlighted / Pressed / SubmenuOpen / Suspending: before; hover",
                        $"{before}; {hover}"]);
                    rows.Add(["  real button down; up", $"{down}; {up}"]);
                }

                target.IsSubmenuOpen = false;
                await Capture.SettleAsync(window, 50);
            });
        }

        {
            MenuItem file = Item("File", Item("Open"));
            MenuItem edit = Item("Edit", Item("Undo"));
            var panel = new StackPanel { Width = 240, Height = 120, Children = { MenuOf(false, file, edit) } };
            await ShowAsync(panel, async () =>
            {
                Window window = await FrontAsync(panel);
                using (RealMouse.Preserve())
                {
                    await ClickAsync(window, file);
                    string opened = $"{file.IsSubmenuOpen} / {file.IsSuspendingPopupAnimation}";
                    await RealMouse.MoveToAsync(edit);
                    rows.Add(["click File: open / suspending; hover Edit: File open; Edit open / suspending",
                        $"{opened}; {file.IsSubmenuOpen}; {edit.IsSubmenuOpen} / {edit.IsSuspendingPopupAnimation}"]);
                }

                file.IsSubmenuOpen = false;
                edit.IsSubmenuOpen = false;
                await Capture.SettleAsync(window, 50);
            });
        }

        return rows;
    }
}
