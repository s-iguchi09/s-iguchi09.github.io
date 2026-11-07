using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の KeyBinding が反応しない原因をキーボードフォーカスの位置から切り分ける」の計測。
///
/// 記事と同じ XAML の Window（Window と UserControl に KeyBinding を置いたもの）を表示し、
/// キーボードフォーカスを置く場所を変えて、実際のキーボード（<see cref="RealKeyboard"/>）でキーを押す。
/// どのコマンドが実行されたか、押したときのキーボードフォーカス、記事の切り分けの手順による判定、
/// Window にキーのイベントが届いたか、TextBox の文字列を読む。
/// 文字の入力は <see cref="DemoProbe.TypeInto"/>（InputManager 経由）で行う（IME の状態に左右されないため）。
/// </summary>
internal sealed class KeyBindingFocusScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "Window の KeyBinding（Ctrl+S）は、キーボードフォーカスが Window の TextBox・Button・Window 自体にあるときに実行され、Keyboard.ClearFocus() の後（Keyboard.FocusedElement が null）と、Owner 付きの別ウィンドウの TextBox にフォーカスがあるときは実行されず、そのどちらでも Window の PreviewKeyDown は呼ばれないこと",
        "Owner 付きの別ウィンドウに同じ Ctrl+S の KeyBinding を置き、そのウィンドウの DataContext に同じビューモデルを設定すると、そのウィンドウの TextBox にフォーカスがあるときに実行され、DataContext を設定しなければ、その KeyBinding の Command が null のままで、判定が 4（経路の上）でも実行されないこと",
        "表示して Activate しただけのとき、フォーカスのある Button の IsEnabled を False にした後、フォーカスのある TextBox の Visibility を Collapsed にした後、Keyboard.ClearFocus() の後に Keyboard.Focus(window) を呼んだ後は、いずれもキーボードフォーカスが Window にあり、Window の KeyBinding が実行されること",
        "UserControl の KeyBinding（Ctrl+D）は、UserControl の中の TextBox にフォーカスがあるときは実行され、UserControl の外の TextBox にフォーカスがあるときは実行されないこと",
        "外の TextBox にフォーカスを置いたまま UserControl の中の TextBlock を実際のマウスでクリックしても、UserControl の Focusable が False でも True でもフォーカスは外の TextBox に残り、UserControl の KeyBinding は実行されないこと",
        "Keyboard.Focus(panel) は、Focusable が False の UserControl にはフォーカスを移さず（フォーカスは Window のまま）KeyBinding も実行されず、Focusable が True なら UserControl にフォーカスが移って KeyBinding が実行されること",
        "UserControl の KeyBinding を外して同じ KeyBinding を Window に置くと、UserControl の外の TextBox にフォーカスがあっても実行されること",
        "押したキーと修飾キーが KeyBinding の Key・Modifiers と一致しない（Ctrl+S の KeyBinding に Ctrl+Shift+S を押す）と、判定が 4（経路の上）で、キーを押す直前に Command が null でないことを確かめていても実行されないこと",
        "記事の切り分けの手順（Keyboard.FocusedElement が null か、Window.GetWindow が KeyBinding を置いたウィンドウか、KeyBinding を置いた要素の IsKeyboardFocusWithin）で判定すると、表 1・2 の各行で、判定が 1〜3 の行ではコマンドが実行されず、判定が 4 で何も実行されなかった行は、ジェスチャーが一致しない行と DataContext を設定しない別ウィンドウの行を除き、フォーカスが TextBox にあること。各行で実行されたビューモデルのコマンド、キーを押す前のフォーカス、キーの後の titleBox の文字列が、行ごとに想定した値と一致すること。Keyboard.Focus(window) は戻り値が Window であること",
        "TextBox にフォーカスがあるとき、Window の Ctrl+Z の KeyBinding は、何も入力していなくても \"abc\" を入力した後でも実行されず、入力した後は TextBox の元に戻す操作で文字列が \"\" に戻ること。Button にフォーカスがあるときは実行されること",
        "Window に Ctrl+Z の KeyBinding を残したまま TextBox の InputBindings に Ctrl+Z の KeyBinding（TextUndoCommand）を置くと、TextBox 側のコマンドだけが実行されて Window の UndoCommand は実行されず、TextBox の文字列は \"abc\" のまま残ること。Window.PreviewKeyDown で TextUndoCommand を実行して Handled にした場合も同じ結果になること",
        "Window の Enter の KeyBinding は、AcceptsReturn が False の TextBox にフォーカスがあるときは実行され、AcceptsReturn が True の TextBox では実行されずに改行が入り、Button では実行されずに Button のコマンドが実行されること",
        "XAML の Key 属性だけで書いた KeyBinding（Key=\"A\"・Key=\"F2\"・Key=\"Enter\"、Modifiers なし）は、Window 自体にフォーカスがある（Keyboard.FocusedElement が Window であることを確かめた）とき、いずれも実行されること",
        "XAML の Gesture=\"A\" は読み込み時に例外になり、Gesture=\"F2\" は実行されること。コードの new KeyGesture(Key.A) と new KeyBinding(command, Key.A, ModifierKeys.None) は NotSupportedException になること",
        "記事の実装例の XAML では、Ctrl+S が UserControl の中と外の TextBox で、Ctrl+D が中と外の TextBox で実行され、Ctrl+Z は TextBox の InputBindings のコマンドが実行されて文字列が \"abc\" のまま残り、Enter は AcceptsReturn が False の TextBox では SubmitCommand が、Button では Button のコマンドが実行されること",
        "記事の切り分け用のデバッグコード（UIElement の GotKeyboardFocus と LostKeyboardFocus のクラスハンドラー）の最後の行が、Keyboard.ClearFocus() の後は null、その後 Keyboard.Focus(window) を呼ぶと Window、TextBox へ移した後は TextBox になること",
    ];

    public string Slug => "wpf-keybinding-not-firing-focus";

    public async Task CaptureAsync(SceneContext context)
    {
        Loc key = T("key", "キー");
        Loc place = T("where the KeyBinding is", "KeyBinding の置き場所");
        Loc before = T("before pressing the key", "キーを押す前の操作");
        Loc focus = T("keyboard focus when the key is pressed", "キーを押したときのキーボードフォーカス");
        Loc judged = T("isolation step that applies", "切り分けの手順の判定");
        Loc reached = T("Window's PreviewKeyDown raised", "Window の PreviewKeyDown");
        Loc command = T("view-model command that ran", "実行されたビューモデルのコマンド");
        Loc text = T("titleBox text after the key", "キーの後の titleBox の文字列");

        await context.SaveTableAsync(
            "Which KeyBinding runs, by where keyboard focus is",
            [key, place, before, focus, judged, reached, command],
            (await RunAllAsync(FocusCases())).Select(r => (IReadOnlyList<Loc>)new Loc[] { r.Key, r.Place, r.Before, r.Focus, r.Judged, r.Reached, r.Command }),
            "keybinding-focus-position.svg");

        await context.SaveTableAsync(
            "Keys the focused control uses itself",
            [key, place, before, focus, judged, command, text],
            (await RunAllAsync(ConsumedCases())).Select(r => (IReadOnlyList<Loc>)new Loc[] { r.Key, r.Place, r.Before, r.Focus, r.Judged, r.Command, r.Text }),
            "keybinding-key-consumed.svg");

        await context.SaveTableAsync(
            "The implementation example",
            [key, place, before, focus, command, text],
            (await RunAllAsync(ImplementationCases())).Select(r => (IReadOnlyList<Loc>)new Loc[] { r.Key, r.Place, r.Before, r.Focus, r.Command, r.Text }),
            "keybinding-implementation.svg");

        var noModifier = (await RunAllAsync(NoModifierCases()))
            .Select(r => (IReadOnlyList<Loc>)new Loc[] { r.Key, r.Place, r.Command })
            .Concat(await CodeGestureRowsAsync());
        await context.SaveTableAsync(
            "KeyBindings without modifier keys",
            [key, T("how the KeyBinding is written", "KeyBinding の書き方"), T("result", "結果")],
            noModifier,
            "keybinding-no-modifier.svg");
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static async Task<List<Result>> RunAllAsync(IEnumerable<Case> cases)
    {
        var rows = new List<Result>();
        foreach (Case c in cases)
        {
            Result row = await RunAsync(c);
            Console.WriteLine(string.Join(" | ", new[] { row.Key, row.Place, row.Before, row.Focus, row.Judged, row.Reached, row.Command, row.Text }.Select(cell => cell.Ja)));
            rows.Add(row);
        }

        return rows;
    }

    // ---------------------------------------------------------------- 記事の XAML とビューモデル

    /// <summary>
    /// 記事の「問題」の XAML。{0} は Window の InputBindings に足す KeyBinding、{1} は titleBox の属性、
    /// {2} は titleBox の子要素（TextBox.InputBindings）、{3} は UserControl の属性、{4} は UserControl の InputBindings の中身。
    /// </summary>
    private const string WindowXaml = """
        <Window Title="Editor" SizeToContent="WidthAndHeight" ResizeMode="NoResize">
          <Window.InputBindings>
            <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveCommand}" />
            <KeyBinding Key="Z" Modifiers="Control" Command="{Binding UndoCommand}" />
            <KeyBinding Key="Enter" Command="{Binding SubmitCommand}" />{0}
          </Window.InputBindings>
          <StackPanel Margin="8" Width="240">
            <TextBox x:Name="titleBox" Text="{Binding Title}"{1}>{2}</TextBox>
            <Button x:Name="okButton" Content="OK" Command="{Binding OkCommand}" Margin="0,4" />
            <UserControl x:Name="panel" Background="Transparent"{3}>
              <UserControl.InputBindings>{4}</UserControl.InputBindings>
              <StackPanel>
                <TextBlock x:Name="panelLabel" Text="Panel" Padding="0,8" />
                <TextBox x:Name="panelBox" />
              </StackPanel>
            </UserControl>
          </StackPanel>
        </Window>
        """;

    /// <summary>記事の XAML の UserControl に置く KeyBinding。</summary>
    private const string PanelBinding = """

                <KeyBinding Key="D" Modifiers="Control" Command="{Binding DeleteCommand}" />

        """;

    /// <summary>記事の「実装例」の XAML。画面全体のショートカットを Window に、TextBox と重なる Ctrl+Z を TextBox に置く。</summary>
    private const string ImplementationXaml = """
        <Window Title="Editor" SizeToContent="WidthAndHeight" ResizeMode="NoResize">
          <Window.InputBindings>
            <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveCommand}" />
            <KeyBinding Key="D" Modifiers="Control" Command="{Binding DeleteCommand}" />
            <KeyBinding Key="Enter" Command="{Binding SubmitCommand}" />
          </Window.InputBindings>
          <StackPanel Margin="8" Width="240">
            <TextBox x:Name="titleBox" Text="{Binding Title}">
              <TextBox.InputBindings>
                <KeyBinding Key="Z" Modifiers="Control" Command="{Binding UndoCommand}" />
              </TextBox.InputBindings>
            </TextBox>
            <Button x:Name="okButton" Content="OK" Command="{Binding OkCommand}" Margin="0,4" />
            <UserControl x:Name="panel">
              <StackPanel>
                <TextBlock Text="Panel" Padding="0,8" />
                <TextBox x:Name="panelBox" />
              </StackPanel>
            </UserControl>
          </StackPanel>
        </Window>
        """;

    /// <summary>ほかのウィンドウにフォーカスがある場合に使う、Owner 付きのツールウィンドウ。{0} は InputBindings の中身。</summary>
    private const string ToolWindowXaml = """
        <Window Title="Tool" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False">
          <Window.InputBindings>{0}</Window.InputBindings>
          <TextBox x:Name="toolBox" Width="160" Margin="8" />
        </Window>
        """;

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }

    /// <summary>記事の XAML の DataContext。各コマンドは実行された名前を記録するだけである。</summary>
    private sealed class EditorViewModel : INotifyPropertyChanged
    {
        private string _title = "";

        public EditorViewModel()
        {
            SaveCommand = new DelegateCommand(() => Executed.Add("SaveCommand"));
            UndoCommand = new DelegateCommand(() => Executed.Add("UndoCommand"));
            TextUndoCommand = new DelegateCommand(() => Executed.Add("TextUndoCommand"));
            SubmitCommand = new DelegateCommand(() => Executed.Add("SubmitCommand"));
            DeleteCommand = new DelegateCommand(() => Executed.Add("DeleteCommand"));
            OkCommand = new DelegateCommand(() => Executed.Add("OkCommand"));
        }

        public List<string> Executed { get; } = [];

        public ICommand SaveCommand { get; }

        public ICommand UndoCommand { get; }

        /// <summary>titleBox に置く KeyBinding と PreviewKeyDown のハンドラーが実行するコマンド。Window の UndoCommand と区別するために分ける。</summary>
        public ICommand TextUndoCommand { get; }

        public ICommand SubmitCommand { get; }

        public ICommand DeleteCommand { get; }

        public ICommand OkCommand { get; }

        public string Title
        {
            get => _title;
            set { _title = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ---------------------------------------------------------------- ケース

    private enum Focus
    {
        /// <summary>表示して Activate しただけで、どこにもフォーカスを移さない。</summary>
        AsShown,

        /// <summary>Keyboard.ClearFocus() でフォーカスを外す。</summary>
        Cleared,

        /// <summary>Keyboard.ClearFocus() の後、Keyboard.Focus(window) で Window へ移す。</summary>
        ClearedThenWindow,
        TitleBox,
        OkButton,
        PanelBox,

        /// <summary>titleBox にフォーカスを置いてから、UserControl の TextBlock を実際のマウスでクリックする。</summary>
        ClickPanelLabel,

        /// <summary>Owner 付きのツールウィンドウの TextBox。</summary>
        ToolWindow,

        /// <summary>Keyboard.Focus(panel) で UserControl 自体へ移す（移らなくても続ける）。</summary>
        PanelItself,

        /// <summary>Button にフォーカスを置いてから、その Button の IsEnabled を False にする。</summary>
        OkButtonThenDisabled,

        /// <summary>titleBox にフォーカスを置いてから、その TextBox の Visibility を Collapsed にする。</summary>
        TitleBoxThenCollapsed,
    }

    /// <summary>判定に使う、KeyBinding を置いた要素。</summary>
    private enum Host
    {
        Window,
        Panel,
        TitleBox,

        /// <summary>Owner 付きの別ウィンドウ。判定もそのウィンドウを基準にする。</summary>
        Tool,
    }

    private sealed record Case(
        Loc KeyLabel,
        ushort VirtualKey,
        ushort[] Modifiers,
        Loc Condition,
        Focus Focus,
        Host Host = Host.Window,
        string WindowExtra = "",
        string TitleBoxAttributes = "",
        string TitleBoxChildren = "",
        string PanelAttributes = "",
        string PanelBindings = PanelBinding,
        string ToolBindings = "",
        bool ToolDataContext = true,
        string? TypeFirst = null,
        bool PreviewKeyDownUndo = false,
        bool ShowText = false,
        string? Xaml = null,
        string Expect = "",
        bool ExpectLoadFailure = false,
        string? ExpectText = null,
        bool GestureMismatch = false);

    private sealed record Result(Loc Key, Loc Place, Loc Before, Loc Focus, Loc Judged, Loc Reached, Loc Command, Loc Text);

    private const ushort VkControl = 0x11;
    private const ushort VkReturn = 0x0D;
    private const ushort VkS = 0x53;
    private const ushort VkD = 0x44;
    private const ushort VkZ = 0x5A;
    private const ushort VkA = 0x41;
    private const ushort VkF2 = 0x71;
    private const ushort VkShift = 0x10;

    private const string WindowCtrlD = "\n    <KeyBinding Key=\"D\" Modifiers=\"Control\" Command=\"{Binding DeleteCommand}\" />";

    private static Loc Describe(Focus focus) => focus switch
    {
        Focus.AsShown => T("show and Activate only", "表示して Activate しただけ"),
        Focus.Cleared => "Keyboard.ClearFocus()",
        Focus.ClearedThenWindow => T("Keyboard.ClearFocus(), then Keyboard.Focus(window)", "Keyboard.ClearFocus() の後、Keyboard.Focus(window)"),
        Focus.TitleBox => "Keyboard.Focus(titleBox)",
        Focus.OkButton => "Keyboard.Focus(okButton)",
        Focus.PanelBox => "Keyboard.Focus(panelBox)",
        Focus.ClickPanelLabel => T("Keyboard.Focus(titleBox), then click the TextBlock in the UserControl", "Keyboard.Focus(titleBox) の後、UserControl の TextBlock をクリック"),
        Focus.ToolWindow => T("focus the TextBox in an owned window", "Owner 付きの別ウィンドウの TextBox にフォーカス"),
        Focus.PanelItself => "Keyboard.Focus(panel)",
        Focus.OkButtonThenDisabled => T("Keyboard.Focus(okButton), then okButton.IsEnabled = false", "Keyboard.Focus(okButton) の後、okButton.IsEnabled = false"),
        Focus.TitleBoxThenCollapsed => T("Keyboard.Focus(titleBox), then titleBox.Visibility = Collapsed", "Keyboard.Focus(titleBox) の後、titleBox.Visibility = Collapsed"),
        _ => throw new ArgumentOutOfRangeException(nameof(focus)),
    };

    private static IEnumerable<Case> FocusCases()
    {
        Loc ctrlS = "Ctrl+S";
        Loc ctrlD = "Ctrl+D";
        Loc onWindow = "Window";
        Loc onPanel = "UserControl";
        Loc focusable = T("UserControl with Focusable=\"True\"", "UserControl（Focusable=\"True\"）");

        const string save = "SaveCommand";
        const string delete = "DeleteCommand";

        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.TitleBox, Expect: save);
        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.OkButton, Expect: save);
        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.AsShown, Expect: save);
        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.Cleared);
        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.ClearedThenWindow, Expect: save);
        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.OkButtonThenDisabled, Expect: save);
        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.TitleBoxThenCollapsed, Expect: save);
        yield return new("Ctrl+Shift+S", VkS, [VkControl, VkShift], T("Window (Ctrl+S; Ctrl+Shift+S pressed)", "Window（Ctrl+S。押したのは Ctrl+Shift+S）"), Focus.OkButton, GestureMismatch: true);
        yield return new(ctrlS, VkS, [VkControl], onWindow, Focus.ToolWindow);
        yield return new(ctrlS, VkS, [VkControl], T("Window and the owned window", "Window と Owner 付きの別ウィンドウ"), Focus.ToolWindow, Host.Tool,
            ToolBindings: "\n    <KeyBinding Key=\"S\" Modifiers=\"Control\" Command=\"{Binding SaveCommand}\" />", Expect: save);
        yield return new(ctrlS, VkS, [VkControl], T("Window and the owned window (no DataContext on the owned window)", "Window と Owner 付きの別ウィンドウ（別ウィンドウに DataContext を設定しない）"), Focus.ToolWindow, Host.Tool,
            ToolBindings: "\n    <KeyBinding Key=\"S\" Modifiers=\"Control\" Command=\"{Binding SaveCommand}\" />", ToolDataContext: false);

        yield return new(ctrlD, VkD, [VkControl], onPanel, Focus.PanelBox, Host.Panel, Expect: delete);
        yield return new(ctrlD, VkD, [VkControl], onPanel, Focus.TitleBox, Host.Panel);
        yield return new(ctrlD, VkD, [VkControl], onPanel, Focus.ClickPanelLabel, Host.Panel);
        yield return new(ctrlD, VkD, [VkControl], focusable, Focus.ClickPanelLabel, Host.Panel, PanelAttributes: " Focusable=\"True\"");
        yield return new(ctrlD, VkD, [VkControl], onPanel, Focus.PanelItself, Host.Panel);
        yield return new(ctrlD, VkD, [VkControl], focusable, Focus.PanelItself, Host.Panel, PanelAttributes: " Focusable=\"True\"", Expect: delete);
        yield return new(ctrlD, VkD, [VkControl], T("Window (removed from the UserControl)", "Window（UserControl からは外す）"), Focus.TitleBox,
            WindowExtra: WindowCtrlD, PanelBindings: "", Expect: delete);
    }

    private static IEnumerable<Case> ConsumedCases()
    {
        Loc ctrlZ = "Ctrl+Z";
        Loc enter = "Enter";
        Loc onWindow = "Window";

        yield return new(ctrlZ, VkZ, [VkControl], onWindow, Focus.TitleBox, ShowText: true, ExpectText: "");
        yield return new(ctrlZ, VkZ, [VkControl], onWindow, Focus.TitleBox, TypeFirst: "abc", ShowText: true, ExpectText: "");
        yield return new(ctrlZ, VkZ, [VkControl], onWindow, Focus.OkButton, Expect: "UndoCommand");
        yield return new(ctrlZ, VkZ, [VkControl], T("Window and titleBox.InputBindings (TextUndoCommand)", "Window と titleBox.InputBindings（TextUndoCommand）"), Focus.TitleBox, Host.TitleBox, TypeFirst: "abc", ShowText: true, ExpectText: "abc",
            TitleBoxChildren: "\n      <TextBox.InputBindings>\n        <KeyBinding Key=\"Z\" Modifiers=\"Control\" Command=\"{Binding TextUndoCommand}\" />\n      </TextBox.InputBindings>\n    ", Expect: "TextUndoCommand");
        yield return new(ctrlZ, VkZ, [VkControl], T("Window and Window.PreviewKeyDown (TextUndoCommand)", "Window と Window.PreviewKeyDown（TextUndoCommand）"), Focus.TitleBox, TypeFirst: "abc", PreviewKeyDownUndo: true, ShowText: true, ExpectText: "abc", Expect: "TextUndoCommand");

        yield return new(enter, VkReturn, [], onWindow, Focus.TitleBox, ShowText: true, ExpectText: "", Expect: "SubmitCommand");
        yield return new(enter, VkReturn, [], T("Window (titleBox with AcceptsReturn=\"True\")", "Window（titleBox は AcceptsReturn=\"True\"）"), Focus.TitleBox, TitleBoxAttributes: " AcceptsReturn=\"True\"", ShowText: true, ExpectText: "\r\n");
        yield return new(enter, VkReturn, [], onWindow, Focus.OkButton, Expect: "OkCommand");
    }

    /// <summary>記事の「実装例」の XAML そのもので、各キーを押す。</summary>
    private static IEnumerable<Case> ImplementationCases()
    {
        Loc onWindow = "Window";
        yield return new("Ctrl+S", VkS, [VkControl], onWindow, Focus.TitleBox, Xaml: ImplementationXaml, Expect: "SaveCommand");
        yield return new("Ctrl+S", VkS, [VkControl], onWindow, Focus.PanelBox, Xaml: ImplementationXaml, Expect: "SaveCommand");
        yield return new("Ctrl+D", VkD, [VkControl], onWindow, Focus.TitleBox, Xaml: ImplementationXaml, Expect: "DeleteCommand");
        yield return new("Ctrl+D", VkD, [VkControl], onWindow, Focus.PanelBox, Xaml: ImplementationXaml, Expect: "DeleteCommand");
        yield return new("Ctrl+Z", VkZ, [VkControl], "titleBox.InputBindings", Focus.TitleBox, Host.TitleBox, TypeFirst: "abc", ShowText: true, ExpectText: "abc", Xaml: ImplementationXaml, Expect: "UndoCommand");
        yield return new("Enter", VkReturn, [], onWindow, Focus.TitleBox, ShowText: true, ExpectText: "", Xaml: ImplementationXaml, Expect: "SubmitCommand");
        yield return new("Enter", VkReturn, [], onWindow, Focus.OkButton, Xaml: ImplementationXaml, Expect: "OkCommand");
    }

    /// <summary>修飾キーの無い KeyBinding。どのコントロールにもキーを使わせないよう、フォーカスは Window 自体に置く。</summary>
    private static IEnumerable<Case> NoModifierCases()
    {
        yield return new("A", VkA, [], "Key=\"A\"", Focus.AsShown, WindowExtra: "\n    <KeyBinding Key=\"A\" Command=\"{Binding DeleteCommand}\" />", Expect: "DeleteCommand");
        yield return new("F2", VkF2, [], "Key=\"F2\"", Focus.AsShown, WindowExtra: "\n    <KeyBinding Key=\"F2\" Command=\"{Binding DeleteCommand}\" />", Expect: "DeleteCommand");
        yield return new("Enter", VkReturn, [], "Key=\"Enter\"", Focus.AsShown, Expect: "SubmitCommand");
        yield return new("A", VkA, [], "Gesture=\"A\"", Focus.AsShown, WindowExtra: "\n    <KeyBinding Gesture=\"A\" Command=\"{Binding DeleteCommand}\" />", ExpectLoadFailure: true);
        yield return new("F2", VkF2, [], "Gesture=\"F2\"", Focus.AsShown, WindowExtra: "\n    <KeyBinding Gesture=\"F2\" Command=\"{Binding DeleteCommand}\" />", Expect: "DeleteCommand");
    }

    /// <summary>コードで修飾キーの無い KeyGesture・KeyBinding を作る。作れたものは無いので、例外の型を表に出す。</summary>
    private static Task<IReadOnlyList<Loc>[]> CodeGestureRowsAsync()
    {
        var command = new DelegateCommand(() => { });
        IReadOnlyList<Loc>[] rows =
        [
            ["A", "new KeyGesture(Key.A)", ThrowsNotSupported("new KeyGesture(Key.A)", () => _ = new KeyGesture(Key.A))],
            ["A", "new KeyBinding(command, Key.A, ModifierKeys.None)", ThrowsNotSupported("new KeyBinding(command, Key.A, ModifierKeys.None)", () => _ = new KeyBinding(command, Key.A, ModifierKeys.None))],
        ];
        return Task.FromResult(rows);
    }

    /// <summary>
    /// NotSupportedException になることを確かめ、その型を返す。
    /// 例外が出ない、または別の例外になったときは、記事と Verifies の主張が成り立たないので計測を止める。
    /// </summary>
    private static Loc ThrowsNotSupported(string label, Action action)
    {
        try
        {
            action();
        }
        catch (NotSupportedException ex)
        {
            return ex.GetType().Name;
        }

        throw new InvalidOperationException($"{label} が NotSupportedException にならなかった。");
    }

    // ---------------------------------------------------------------- 切り分け用のデバッグコード

    /// <summary>記事の「切り分けの手順」のデバッグ用コードが書き出す行。null のときは書き出さない。</summary>
    private static List<string>? s_focusLog;

    private static bool s_focusLogRegistered;

    /// <summary>
    /// 記事の「切り分けの手順」と同じクラスハンドラーを登録する。
    /// クラスハンドラーは外せないので、プロセスで 1 回だけ登録し、記録するかは <see cref="s_focusLog"/> で切り替える。
    /// </summary>
    private static void RegisterFocusLog()
    {
        if (s_focusLogRegistered)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(UIElement),
            Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((sender, e) =>
            {
                if (sender == e.OriginalSource)
                {
                    s_focusLog?.Add($"focus: {e.NewFocus}");
                }
            }),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(UIElement),
            Keyboard.LostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((sender, e) =>
            {
                if (sender == e.OriginalSource && e.NewFocus is null)
                {
                    s_focusLog?.Add("focus: null");
                }
            }),
            handledEventsToo: true);
        s_focusLogRegistered = true;
    }

    /// <summary>
    /// デバッグ用コードの最後の行が、キーを押す直前の Keyboard.FocusedElement と一致することを確かめる。
    /// 一致しなければ計測を止める。
    /// </summary>
    private static void VerifyFocusLog(Focus focus, List<string> log)
    {
        string? last = log.LastOrDefault();
        IInputElement? actual = Keyboard.FocusedElement;
        string expected = actual is null ? "focus: null" : $"focus: {actual}";

        // 操作でフォーカスが動かなかったケース（表示しただけなど）は行が出ないので、比べない。
        if (last is not null && last != expected)
        {
            throw new InvalidOperationException($"デバッグ用コードの最後の行がフォーカスと一致しない（{focus}）: {last} / 実際: {expected}");
        }

        // 記事に書いた出力の文字列そのものも確かめる。
        bool textOk = focus switch
        {
            Focus.Cleared => last == "focus: null",
            Focus.ClearedThenWindow => last == "focus: System.Windows.Window",
            Focus.TitleBox => last?.StartsWith("focus: System.Windows.Controls.TextBox", StringComparison.Ordinal) == true,
            _ => true,
        };
        if (!textOk)
        {
            throw new InvalidOperationException($"デバッグ用コードの出力が記事に書いた文字列と異なる（{focus}）: {last}");
        }

        if (focus is Focus.Cleared or Focus.ClearedThenWindow or Focus.TitleBox && last is null)
        {
            throw new InvalidOperationException($"デバッグ用コードが行を書き出していない（{focus}）。");
        }

        if (focus is Focus.Cleared or Focus.ClearedThenWindow or Focus.TitleBox)
        {
            Console.WriteLine($"focus log ({focus}): {string.Join(" / ", log)}");
        }
    }

    // ---------------------------------------------------------------- 実行

    private static async Task<Result> RunAsync(Case c)
    {
        string xaml = c.Xaml ?? WindowXaml
            .Replace("{0}", c.WindowExtra)
            .Replace("{1}", c.TitleBoxAttributes)
            .Replace("{2}", c.TitleBoxChildren)
            .Replace("{3}", c.PanelAttributes)
            .Replace("{4}", c.PanelBindings);
        Loc before = c.TypeFirst is { } typed
            ? T($"{Describe(c.Focus).En}, then type \"{typed}\"", $"{Describe(c.Focus).Ja} の後、\"{typed}\" を入力")
            : Describe(c.Focus);

        Window window;
        try
        {
            window = SceneContext.LoadXaml<Window>(xaml);
        }
        catch (Exception ex) when (c.ExpectLoadFailure)
        {
            // Gesture="A" のように、読み込みの時点で失敗すると想定したケースだけ、例外の型を結果として表に出す。
            // ほかのケースで読み込みに失敗したら、XAML の誤りなので計測を止める（when で受け止めない）。
            if (ex is not XamlParseException)
            {
                throw new InvalidOperationException($"XAML の読み込みの失敗が XamlParseException ではない（{ex.GetType().Name}）。", ex);
            }

            Loc failed = T($"{ex.GetType().Name} when loading the XAML", $"XAML の読み込みで {ex.GetType().Name}");
            return new(c.KeyLabel, c.Condition, before, "-", "-", "-", failed, "-");
        }

        if (c.ExpectLoadFailure)
        {
            throw new InvalidOperationException($"読み込みで失敗するはずの XAML が読み込めた: {c.Condition.Ja}");
        }

        var vm = new EditorViewModel();
        window.DataContext = vm;
        var titleBox = (TextBox)window.FindName("titleBox");
        bool windowReached = false;
        window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, _) => windowReached = true), handledEventsToo: true);

        if (c.PreviewKeyDownUndo)
        {
            // 記事の「実装例」で触れる、コードビハインドで先に受け取る方法。Ctrl+Z を TextBox より先に受け取り、ビューモデルのコマンドを実行して止める。
            window.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    vm.TextUndoCommand.Execute(null);
                    e.Handled = true;
                }
            };
        }

        Window? tool = null;
        try
        {
            await ShowFrontAsync(window);
            Window target = window;
            RegisterFocusLog();
            s_focusLog = [];

            switch (c.Focus)
            {
                case Focus.AsShown:
                    break;
                case Focus.Cleared:
                    Keyboard.ClearFocus();
                    await Capture.SettleAsync(window, 100);
                    break;
                case Focus.ClearedThenWindow:
                    Keyboard.ClearFocus();
                    await Capture.SettleAsync(window, 100);
                    if (Keyboard.Focus(window) != window)
                    {
                        throw new InvalidOperationException("Keyboard.Focus(window) の戻り値が Window ではない。");
                    }

                    await Capture.SettleAsync(window, 100);
                    break;
                case Focus.TitleBox:
                    await FocusAsync(titleBox);
                    break;
                case Focus.OkButton:
                    await FocusAsync((UIElement)window.FindName("okButton"));
                    break;
                case Focus.PanelBox:
                    await FocusAsync((UIElement)window.FindName("panelBox"));
                    break;
                case Focus.ClickPanelLabel:
                    await FocusAsync(titleBox);
                    using (RealMouse.Preserve())
                    {
                        await RealMouse.ClickAsync(window, (FrameworkElement)window.FindName("panelLabel"));
                    }

                    await Capture.SettleAsync(window, 100);
                    break;
                case Focus.PanelItself:
                    // Focusable が False なら移らない。移らなかったことも結果として表に出すので、FocusAsync（移らなければ例外）は使わない。
                    Keyboard.Focus((UIElement)window.FindName("panel"));
                    await Capture.SettleAsync(window, 100);
                    break;
                case Focus.OkButtonThenDisabled:
                    {
                        var ok = (Button)window.FindName("okButton");
                        await FocusAsync(ok);
                        ok.IsEnabled = false;
                        await Capture.SettleAsync(window, 200);
                        break;
                    }

                case Focus.TitleBoxThenCollapsed:
                    await FocusAsync(titleBox);
                    titleBox.Visibility = Visibility.Collapsed;
                    await Capture.SettleAsync(window, 200);
                    break;
                case Focus.ToolWindow:
                    tool = SceneContext.LoadXaml<Window>(ToolWindowXaml.Replace("{0}", c.ToolBindings));
                    tool.Owner = window;
                    if (c.ToolDataContext)
                    {
                        tool.DataContext = vm;
                    }

                    tool.Left = window.Left + window.ActualWidth + 16;
                    tool.Top = window.Top;
                    await ShowFrontAsync(tool, center: false);
                    await FocusAsync((UIElement)tool.FindName("toolBox"));
                    target = tool;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(c));
            }

            // TextBox.ToString() は文字列を含むので、文字を打つ前に、フォーカスを移した操作の記録と照らし合わせる。
            List<string> focusLog = s_focusLog;
            s_focusLog = null;
            VerifyFocusLog(c.Focus, focusLog);

            if (c.TypeFirst is { } letters)
            {
                TypeInto(titleBox, letters);
                await Capture.SettleAsync(window, 100);
            }

            Loc focus = DescribeFocus(window);
            (Window judgeWindow, UIElement host) = c.Host switch
            {
                Host.Window => (window, (UIElement)window),
                Host.Panel => (window, (UIElement)window.FindName("panel")),
                Host.TitleBox => (window, titleBox),
                Host.Tool => (tool ?? throw new InvalidOperationException("別ウィンドウを表示していない。"), (UIElement)tool),
                _ => throw new ArgumentOutOfRangeException(nameof(c)),
            };
            int step = Judge(judgeWindow, host);
            Loc judged = DescribeStep(step);
            IInputElement? expectedFocus = ExpectedFocus(c, window, tool, titleBox);
            if (Keyboard.FocusedElement != expectedFocus)
            {
                throw new InvalidOperationException($"キーを押す前のフォーカスが想定と異なる（想定: {expectedFocus?.GetType().Name ?? "null"}、実際: {Keyboard.FocusedElement?.GetType().Name ?? "null"}）: {c.KeyLabel.Ja} / {c.Condition.Ja} / {c.Focus}");
            }
            // 別ウィンドウの KeyBinding の Command が、DataContext を設定したときだけ解決されていることを、キーを押す直前に確かめる。
            if (tool is not null && tool.InputBindings.Count > 0)
            {
                bool resolved = tool.InputBindings[0].Command is not null;
                if (resolved != c.ToolDataContext)
                {
                    throw new InvalidOperationException($"別ウィンドウの KeyBinding の Command が想定と異なる（DataContext あり: {c.ToolDataContext}、Command: {(resolved ? "あり" : "null")}）。");
                }
            }

            // ジェスチャーが一致しない行は、Command が解決されていることをキーを押す直前に確かめる（実行されない原因がバインドではないことを示すため）。
            if (c.GestureMismatch && window.InputBindings[0] is not KeyBinding { Key: Key.S, Modifiers: ModifierKeys.Control, Command: not null })
            {
                throw new InvalidOperationException("ジェスチャーが一致しない行で、Window の Ctrl+S の KeyBinding の Command が解決されていない。");
            }

            windowReached = false;
            await RealKeyboard.PressAsync(target, c.VirtualKey, c.Modifiers);
            await Capture.SettleAsync(target, 100);

            CheckJudgement(c, step, vm.Executed);
            if (c.ShowText && titleBox.Text != c.ExpectText)
            {
                throw new InvalidOperationException($"キーの後の titleBox の文字列が想定と異なる（想定: {Quote(c.ExpectText ?? "")}、実際: {Quote(titleBox.Text)}）: {c.KeyLabel.Ja} / {c.Condition.Ja}");
            }

            Loc executed = vm.Executed.Count == 0 ? T("none", "なし") : string.Join(", ", vm.Executed);
            Loc text = c.ShowText ? Quote(titleBox.Text) : "-";
            return new(c.KeyLabel, c.Condition, before, focus, judged, windowReached ? T("raised", "呼ばれた") : T("not raised", "呼ばれない"), executed, text);
        }
        finally
        {
            // 途中で例外になっても、外せないクラスハンドラーが以後のシーンのフォーカス移動を記録し続けないようにする。
            s_focusLog = null;
            tool?.Close();
            window.Close();
        }
    }

    /// <summary>
    /// キーを押す前の操作から、フォーカスがあるはずの要素を決める。
    /// 操作が想定どおりにフォーカスを動かしたかを、表の値とは別に確かめるために使う。
    /// </summary>
    private static IInputElement? ExpectedFocus(Case c, Window window, Window? tool, TextBox titleBox) => c.Focus switch
    {
        Focus.Cleared => null,
        Focus.AsShown or Focus.ClearedThenWindow or Focus.OkButtonThenDisabled or Focus.TitleBoxThenCollapsed => window,
        Focus.TitleBox or Focus.ClickPanelLabel => titleBox,
        Focus.OkButton => (IInputElement)window.FindName("okButton"),
        Focus.PanelBox => (IInputElement)window.FindName("panelBox"),
        Focus.PanelItself => c.PanelAttributes.Contains("Focusable=\"True\"", StringComparison.Ordinal) ? (IInputElement)window.FindName("panel") : window,
        Focus.ToolWindow => (IInputElement)(tool ?? throw new InvalidOperationException("別ウィンドウを表示していない。")).FindName("toolBox"),
        _ => throw new ArgumentOutOfRangeException(nameof(c)),
    };

    /// <summary>
    /// 記事の「切り分けの手順」の 1〜4 を、そのままコードにしたもの。
    /// <paramref name="host"/> は KeyBinding を置いた要素。
    /// </summary>
    private static int Judge(Window window, UIElement host)
    {
        IInputElement? focused = Keyboard.FocusedElement;
        if (focused is null)
        {
            return 1;
        }

        if (focused is not DependencyObject element || Window.GetWindow(element) != window)
        {
            return 2;
        }

        return host.IsKeyboardFocusWithin ? 4 : 3;
    }

    private static Loc DescribeStep(int step) => step switch
    {
        1 => T("1: no focus", "1: フォーカスが無い"),
        2 => T("2: another window", "2: 別のウィンドウ"),
        3 => T("3: outside the element", "3: 置いた要素の外"),
        4 => T("4: on the path", "4: 経路の上"),
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    /// <summary>
    /// 判定と結果が記事の説明どおりかを確かめる。判定が 1〜3 ならコマンドは実行されず、
    /// 4 で何も実行されなかったなら、キーを自分で使うコントロール（TextBox）にフォーカスがある。
    /// 外れたら計測を止める。
    /// </summary>
    private static void CheckJudgement(Case c, int step, List<string> executed)
    {
        // 実行されるはずのコマンドと完全に一致するかを先に確かめる（計測の退行で、別のコマンドが実行されても見逃さないため）。
        string actual = string.Join(", ", executed);
        if (actual != c.Expect)
        {
            throw new InvalidOperationException($"実行されたコマンド（{actual}）が想定（{c.Expect}）と異なる: {c.KeyLabel.Ja} / {c.Condition.Ja} / {c.Focus}");
        }

        if (c.GestureMismatch)
        {
            // 押したキーと修飾キーが KeyBinding の Key・Modifiers と一致しないので、経路の上（判定 4）にあっても実行されないはずである。
            if (step != 4 || executed.Count != 0)
            {
                throw new InvalidOperationException($"ジェスチャーが一致しない行で、判定が 4 ではないか、コマンドが実行された（判定 {step}、{string.Join(", ", executed)}）。");
            }

            return;
        }

        if (!c.ToolDataContext)
        {
            // 別ウィンドウの KeyBinding の Command が解決されないので、経路の上（判定 4）にあっても実行されないはずである。
            if (step != 4)
            {
                throw new InvalidOperationException($"DataContext の無い別ウィンドウの判定が 4 ではない（{step}）。");
            }

            if (executed.Count != 0)
            {
                throw new InvalidOperationException($"DataContext の無い別ウィンドウで、コマンドが実行された（{string.Join(", ", executed)}）。");
            }

            return;
        }

        bool ok = step switch
        {
            1 or 2 or 3 => executed.Count == 0,
            _ => executed.Count > 0 || Keyboard.FocusedElement is TextBox,
        };
        if (!ok)
        {
            throw new InvalidOperationException($"判定 {step} と結果（{string.Join(", ", executed)}）が記事の説明と食い違う: {c.KeyLabel.Ja} / {c.Condition.Ja} / {c.Focus}");
        }
    }

    /// <summary>改行を見える形にして引用する（AcceptsReturn の TextBox に Enter が入ったかを表に出すため）。</summary>
    private static string Quote(string text) => $"\"{text.Replace("\r\n", "\\r\\n").Replace("\n", "\\n")}\"";

    private static Loc DescribeFocus(Window window) => Keyboard.FocusedElement switch
    {
        null => T("none (Keyboard.FocusedElement is null)", "なし（Keyboard.FocusedElement が null）"),
        TextBox box when box.Name == "titleBox" => T("TextBox in the Window", "Window の TextBox"),
        TextBox box when box.Name == "panelBox" => T("TextBox in the UserControl", "UserControl の TextBox"),
        TextBox box when box.Name == "toolBox" => T("TextBox in the owned window", "Owner 付きの別ウィンドウの TextBox"),
        Button button when button.Name == "okButton" => "Button",
        UserControl => "UserControl",
        Window w when w == window => "Window",
        var element => element.GetType().Name,
    };

    /// <summary>ウィンドウを表示し、最前面にしてアクティブにする（実際のキーをほかのアプリに送らないため）。</summary>
    private static async Task ShowFrontAsync(Window window, bool center = true)
    {
        window.ShowActivated = true;
        await Capture.ShowAndSettleAsync(window);
        if (center)
        {
            Rect area = SystemParameters.WorkArea;
            window.Left = area.Left + (area.Width - window.ActualWidth) / 2;
            window.Top = area.Top + (area.Height - window.ActualHeight) / 2;
        }

        window.Topmost = true;
        window.Activate();
        await Capture.SettleAsync(window, 100);
        if (!window.IsActive)
        {
            throw new InvalidOperationException("計測用のウィンドウをアクティブにできない（ほかのウィンドウが前面を保持している）。");
        }
    }
}
