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
    }

    /// <summary>
    /// 「入力欄 → ViewModel のプロパティ値」を横に並べた 1 行を作る。
    /// </summary>
    private static (UIElement Row, TextBox Box) BuildRow(UserNameViewModel viewModel, string textBoxXaml)
    {
        var textBox = SceneContext.LoadXaml<TextBox>(textBoxXaml);

        var arrow = new TextBlock
        {
            Text = "→",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x93, 0xA3)),
            Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

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
