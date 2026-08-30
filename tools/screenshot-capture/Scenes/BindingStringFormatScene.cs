using System.Windows;
using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF Binding.StringFormat で数値・通貨・日付を書式化する方法と制約」の図。
/// 記事が落とし穴として挙げる 2 点、
/// 「書式化のカルチャが既定で en-US であること」と
/// 「ContentControl では StringFormat が無視されること」を実際の描画で示す。
/// </summary>
internal sealed class BindingStringFormatScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "FrameworkElement.Language の既定値と CultureInfo.CurrentCulture を読み出す",
        "ConverterCulture 未指定のとき、OS の地域設定ではなく en-US で書式化されること",
        "ConverterCulture を指定すると書式が変わること",
        "Label.Content では StringFormat が効かず、ContentStringFormat が要ること",
    ];

    public string Slug => "wpf-binding-stringformat-number-currency-date";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildCultureWindow(), "stringformat-converterculture.png");
        await context.ShootAsync(BuildContentControlWindow(), "stringformat-contentcontrol.png");

        await context.SaveTableAsync(
            "what StringFormat actually produces",
            ["binding target and format", "culture", "rendered text"],
            await FormatAndSortMeasurements.StringFormatAsync(),
            "stringformat-culture-matrix.svg");
    }

    /// <summary>
    /// ConverterCulture の有無で通貨記号と日付の並びが変わることを示す。
    /// </summary>
    private static Window BuildCultureWindow()
    {
        var rows = new[]
        {
            TextBlockRow("{Binding Price, StringFormat=C}"),
            TextBlockRow("{Binding Price, StringFormat=C, ConverterCulture=ja-JP}"),
            TextBlockRow("{Binding OrderDate, StringFormat=d}"),
            TextBlockRow("{Binding OrderDate, StringFormat=d, ConverterCulture=ja-JP}"),
        };

        Window window = DemoLayout.BuildComparisonWindow("Binding.StringFormat / ConverterCulture", rows);
        window.DataContext = new FormatSample();
        return window;
    }

    /// <summary>
    /// Content は object 型のため StringFormat が効かず、ContentStringFormat が必要になることを示す。
    /// </summary>
    private static Window BuildContentControlWindow()
    {
        var rows = new[]
        {
            new DemoLayout.Row(
                """<Label Content="{Binding Price, StringFormat=C}" />""",
                SceneContext.LoadXaml<Label>(
                    """<Label Content="{Binding Price, StringFormat=C}" Padding="0" />""")),
            new DemoLayout.Row(
                """<Label Content="{Binding Price}" ContentStringFormat="C" />""",
                SceneContext.LoadXaml<Label>(
                    """<Label Content="{Binding Price}" ContentStringFormat="C" Padding="0" />""")),
            new DemoLayout.Row(
                """<TextBlock Text="{Binding Price, StringFormat=C}" />""",
                SceneContext.LoadXaml<TextBlock>(
                    """<TextBlock Text="{Binding Price, StringFormat=C}" />""")),
        };

        Window window = DemoLayout.BuildComparisonWindow("StringFormat vs ContentStringFormat", rows);
        window.DataContext = new FormatSample();
        return window;
    }

    /// <summary>
    /// バインディング式をそのまま見出しに使い、同じ式を適用した TextBlock を描画する。
    /// </summary>
    private static DemoLayout.Row TextBlockRow(string bindingExpression) => new(
        bindingExpression,
        SceneContext.LoadXaml<TextBlock>($"""<TextBlock Text="{bindingExpression}" />"""));
}
