using System.Windows;
using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPFで編集不可のままテキストを選択・コピー可能に表示する方法」の図。
/// 同じ文字列を TextBlock と読み取り専用 TextBox で表示し、
/// 後者だけが範囲選択できることを実際の描画で示す。
/// </summary>
internal sealed class SelectableReadOnlyTextScene : IScene
{
    private const string Message = "System.IO.FileNotFoundException: config.json";

    public IReadOnlyList<string> Verifies =>
    [
        "TextBlock にはテキストを選択する API が無いこと",
        "IsReadOnly の TextBox では選択できること",
        "見た目を寄せる設定を加えても選択できること",
        "IsTabStop を切ってもフォーカス可能なままであること",
    ];

    public string Slug => "wpf-selectable-readonly-text-display";

    public async Task CaptureAsync(SceneContext context)
    {
        var textBlock = SceneContext.LoadXaml<TextBlock>(
            $"""<TextBlock Text="{Message}" Width="330" TextWrapping="Wrap" />""");

        // 記事の実装例と同じ設定。TextBlock に近い見た目のまま選択できる。
        var textBox = SceneContext.LoadXaml<TextBox>(
            $"""
            <TextBox Text="{Message}"
                     IsReadOnly="True"
                     IsReadOnlyCaretVisible="False"
                     Background="Transparent"
                     BorderThickness="0"
                     Padding="0"
                     TextWrapping="Wrap"
                     Width="330" />
            """);

        Window window = DemoLayout.BuildPanelWindow(
            "TextBlock / read-only TextBox",
            [
                new DemoLayout.Panel("TextBlock", textBlock),
                new DemoLayout.Panel("TextBox IsReadOnly=\"True\"", textBox),
            ],
            Orientation.Vertical);

        await context.ShootAsync(window, "selectable-readonly-text.png", async _ =>
        {
            // 例外の型名だけを選択し、選択範囲が描画されることを示す。
            textBox.Focus();
            textBox.Select(0, "System.IO.FileNotFoundException".Length);
            await Task.Delay(200);
        });

        await context.SaveTableAsync(
            "can the text be selected, and how does it take focus?",
            ["control", "SelectAll() selects", "Focusable", "IsTabStop"],
            await SelectionAndTriggerMeasurements.SelectableTextAsync(),
            "selectable-text-matrix.svg");
    }
}
