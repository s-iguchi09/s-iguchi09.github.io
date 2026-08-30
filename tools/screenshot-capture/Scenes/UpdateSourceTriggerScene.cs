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
    ];

    public string Slug => "wpf-textbox-updatesourcetrigger-binding-timing";

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
            // 入力欄にフォーカスを残したまま値を変える。
            // 既定の LostFocus ではフォーカスが外れないためソースは更新されない。
            defaultBox.Text = TypedText;
            immediateBox.Text = TypedText;
            immediateBox.Focus();
            immediateBox.CaretIndex = TypedText.Length;
            await Task.Delay(200);
        });

        await context.SaveTableAsync(
            "DefaultUpdateSourceTrigger read from property metadata",
            ["dependency property", "DefaultUpdateSourceTrigger", "BindsTwoWayByDefault"],
            SelectionAndTriggerMeasurements.DefaultUpdateSourceTriggers(),
            "updatesourcetrigger-defaults.svg");

        await context.SaveTableAsync(
            "source value after one keystroke, then after focus moves away",
            ["UpdateSourceTrigger", "after input", "after LostFocus", "final"],
            await SelectionAndTriggerMeasurements.UpdateTimingAsync(),
            "updatesourcetrigger-timing.svg");
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
