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
        "IsReadOnlyCaretVisible の既定値（読み取り専用の TextBox は既定でキャレットを表示しないこと）",
        "改行を含む文字列を読み取り専用の TextBox に表示したとき、AcceptsReturn の有無で行数と高さが変わらないこと",
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

        await context.SaveTableAsync(
            "read-only TextBox: caret default and AcceptsReturn with three lines of text",
            ["case", "measured"],
            await CaretAndAcceptsReturnAsync(),
            "readonly-textbox-caret-acceptsreturn.svg");
    }

    private static async Task<List<IReadOnlyList<string>>> CaretAndAcceptsReturnAsync()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "IsReadOnlyCaretVisible default", System.Windows.Controls.Primitives.TextBoxBase.IsReadOnlyCaretVisibleProperty.GetMetadata(typeof(TextBox)).DefaultValue?.ToString() ?? "null" },
        };

        foreach (bool acceptsReturn in new[] { false, true })
        {
            var box = new TextBox { IsReadOnly = true, AcceptsReturn = acceptsReturn, Text = "line1\nline2\nline3", Width = 200 };
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case($"IsReadOnly, AcceptsReturn={acceptsReturn}", box, _ => [$"LineCount {box.LineCount}, height {box.ActualHeight:0.##}"]),
            ]));
        }

        return rows;
    }
}
