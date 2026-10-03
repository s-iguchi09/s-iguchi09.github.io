using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する」の図。
/// フォーカスを保持したまま入力した文字が、既定（LostFocus）ではソースへ届かず、
/// PropertyChanged では即座に届くことを、同じ ViewModel を参照する表示で示す。
/// </summary>
internal sealed class UpdateSourceTriggerScene : IScene
{
    private const string TypedText = "sato";

    public IReadOnlyList<string> Verifies =>
    [
        "プロパティごとの DefaultUpdateSourceTrigger をメタデータから読み出す",
        "TextBox.Text だけが LostFocus で、他の多くは PropertyChanged であること",
        "既定・PropertyChanged・Explicit で、ソースへ値が渡る時点が異なること",
        "図は、各入力欄にフォーカスを移して InputManager 経由で打ち込んで撮っていること",
        "既定のバインドのまま、フォーカスを残して Enter で既定ボタン（IsDefault）を押すと、Click の時点でソースが更新されていないこと",
        "ErrorsChanged をバックグラウンドのスレッドから発生させてもエラーが反映されるか",
    ];

    public string Slug => "wpf-textbox-updatesourcetrigger-binding-timing";

    /// <summary>表の英語の言い回しの日本語。前から順に置き換える（長いものを先に置く）。</summary>
    private static readonly (string En, string Ja)[] Words =
    [
        ("default binding, type \"sato\", then Enter on an IsDefault button", "既定のバインド、\"sato\" と入力し、IsDefault のボタンで Enter"),
        ("in Click: ", "Click の中: "),
        (", TextBox focused ", "、TextBox のフォーカス "),
        ("FrameworkElement.Tag (on PasswordBox)", "FrameworkElement.Tag（PasswordBox）"),
        ("ErrorsChanged raised on a background thread", "バックグラウンドスレッドで ErrorsChanged を発生させる"),
        ("ErrorsChanged raised on the UI thread", "UI スレッドで ErrorsChanged を発生させる"),
        (" (raised on thread other than UI)", "（UI 以外のスレッドで発生）"),
        (" (raised on thread UI)", "（UI スレッドで発生）"),
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        var defaultViewModel = new UserNameViewModel();
        var immediateViewModel = new UserNameViewModel();

        (UIElement defaultRow, TextBox defaultBox) = BuildRow(
            defaultViewModel,
            """<TextBox Text="{Binding UserName, Mode=TwoWay}" Width="150" />""");

        (UIElement immediateRow, TextBox immediateBox) = BuildRow(
            immediateViewModel,
            """<TextBox Text="{Binding UserName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="150" />""");

        Window window = DemoLayout.BuildPanelWindow(
            "UpdateSourceTrigger",
            [
                new DemoLayout.Panel("{Binding UserName}  —  default: LostFocus", defaultRow),
                new DemoLayout.Panel("{Binding UserName, UpdateSourceTrigger=PropertyChanged}", immediateRow),
            ],
            Orientation.Vertical);

        await context.ShootAsync(window, "updatesourcetrigger-lostfocus-vs-propertychanged.png", async _ =>
        {
            // それぞれの入力欄にフォーカスを移し、WPF の入力処理（InputManager）を通して打ち込む。
            // 既定の LostFocus は、フォーカスが外れた時点でソースを更新する。上の入力欄（既定）を後に打ち込み、
            // フォーカスを残したまま撮る。先に打ち込む下の入力欄（PropertyChanged）は、1 文字ごとに更新される。
            foreach (TextBox box in new[] { immediateBox, defaultBox })
            {
                await DemoProbe.FocusAsync(box);
                box.SelectAll();
                DemoProbe.TypeInto(box, TypedText);
                await Task.Delay(100);
            }
        });

        await context.SaveTableAsync(
            "DefaultUpdateSourceTrigger read from property metadata",
            [Loc.Of("dependency property", "依存関係プロパティ"), "DefaultUpdateSourceTrigger", "BindsTwoWayByDefault"],
            LocTable.Translate(SelectionAndTriggerMeasurements.DefaultUpdateSourceTriggers(), Words),
            "updatesourcetrigger-defaults.svg");

        await context.SaveTableAsync(
            "source value after one keystroke, then after focus moves away",
            ["UpdateSourceTrigger", Loc.Of("after input", "入力の後"), Loc.Of("after LostFocus", "LostFocus の後"), Loc.Of("final", "最後")],
            LocTable.Translate(await SelectionAndTriggerMeasurements.UpdateTimingAsync(), Words),
            "updatesourcetrigger-timing.svg");

        await context.SaveTableAsync(
            "confirming without moving focus, and the thread that raises ErrorsChanged",
            [Loc.Of("case", "条件"), Loc.Of("measured", "計測値")],
            LocTable.Translate([await DefaultButtonEnterAsync(), .. await ValidationAndScopeMeasurements.ErrorsChangedThreadAsync()], Words),
            "updatesourcetrigger-confirm-errors.svg");
    }

    /// <summary>
    /// 既定のバインド（LostFocus）の TextBox に打ち込み、フォーカスを残したまま Enter で既定ボタンを押す。
    /// ボタンの Click で、ソースの値とフォーカスの位置を読む。
    /// </summary>
    private static async Task<IReadOnlyList<string>> DefaultButtonEnterAsync()
    {
        var viewModel = new UserNameViewModel();
        var box = SceneContext.LoadXaml<TextBox>("""<TextBox Text="{Binding UserName, Mode=TwoWay}" Width="150" />""");
        var button = new Button { Content = "Save", IsDefault = true };
        string seen = "not clicked";
        button.Click += (_, _) => seen = $"UserName = {viewModel.UserName}, TextBox focused {WpfProbe.Describe(box.IsKeyboardFocused)}";
        var panel = new StackPanel { DataContext = viewModel, Children = { box, button } };

        List<IReadOnlyList<string>> rows = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "default binding, type \"sato\", then Enter on an IsDefault button",
                panel,
                _ => [$"in Click: {seen}"],
                Act: async _ =>
                {
                    await DemoProbe.FocusAsync(box);
                    box.SelectAll();
                    DemoProbe.TypeInto(box, "sato");
                    DemoProbe.SendKey(System.Windows.Input.Key.Enter);
                    DemoProbe.SendKey(System.Windows.Input.Key.Enter, down: false);
                }),
        ]);

        return rows[0];
    }

    /// <summary>
    /// 「入力欄 → ViewModel のプロパティ値」を横に並べた 1 行を作る。
    /// </summary>
    private static (UIElement Row, TextBox Box) BuildRow(UserNameViewModel viewModel, string textBoxXaml)
    {
        var textBox = SceneContext.LoadXaml<TextBox>(textBoxXaml);

        TextBlock arrow = DemoLayout.Arrow(new Thickness(14, 0, 14, 0));

        var mirror = SceneContext.LoadXaml<Border>(
            """
            <Border BorderBrush="#C3CCDB" BorderThickness="1" CornerRadius="4" Padding="8,4" MinWidth="150">
              <TextBlock>
                <Run Text="UserName = " Foreground="#8A93A3" FontFamily="Consolas" />
                <Run Text="{Binding UserName, Mode=OneWay}" FontFamily="Consolas" />
              </TextBlock>
            </Border>
            """);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            DataContext = viewModel,
            Children = { textBox, arrow, mirror },
        };

        return (row, textBox);
    }
}
