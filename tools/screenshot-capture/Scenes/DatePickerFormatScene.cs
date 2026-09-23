using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「DatePicker の表示形式をカスタマイズする方法」の図。
/// 同じ日付を、既定の表示と DatePickerTextBox のスタイルで整形した表示で並べる。
/// </summary>
internal sealed class DatePickerFormatScene : IScene
{
    // Language（xml:lang）を明示しない既定表示はスレッドの CurrentCulture に従うため、
    // 撮影マシン（日本語環境）では 2026/04/15 になり、記事が示す yyyy/MM/dd との差が出ない。
    // 明示した Language は CurrentCulture より優先される（datepicker-culture-matrix.svg で実測）。
    // そこで両方に xml:lang="en-US" を与え、ロケールが異なる環境でも表示が固定されることを示す。
    private const string DefaultXaml =
        """<DatePicker xml:lang="en-US" SelectedDate="2026-04-15" Width="190" />""";

    /// <summary>記事の「XAML スタイルによる方法」と同じ指定。</summary>
    private const string FormattedXaml =
        """
        <DatePicker xml:lang="en-US" SelectedDate="2026-04-15" Width="190">
          <DatePicker.Resources>
            <Style TargetType="DatePickerTextBox">
              <Setter Property="Text"
                      Value="{Binding SelectedDate,
                                      RelativeSource={RelativeSource AncestorType=DatePicker},
                                      StringFormat='yyyy\/MM\/dd'}" />
            </Style>
          </DatePicker.Resources>
        </DatePicker>
        """;

    public IReadOnlyList<string> Verifies =>
    [
        "SelectedDateFormat が Short と Long の 2 つしか持たず、任意の書式にできないこと",
        "SelectedDateFormat を設定しない場合の実効値が Short であり、それが依存関係プロパティのメタデータ既定値（Long）ではなく既定スタイル由来であること",
        "それぞれの設定で実際に表示される文字列",
        "テンプレート内のテキスト部分を書き換えると任意の書式にできること",
        "既定表示のカルチャが要素の Language（xml:lang）とスレッドの CurrentCulture のどちらに従うか（両者を独立に変えて表示文字列を測る）",
    ];

    public string Slug => "wpf-datepicker-custom-format";

    public async Task CaptureAsync(SceneContext context)
    {
        var rows = new[]
        {
            new DemoLayout.Row(
                "<DatePicker />",
                SceneContext.LoadXaml<DatePicker>(DefaultXaml)),
            new DemoLayout.Row(
                "StringFormat='yyyy\\/MM\\/dd'",
                SceneContext.LoadXaml<DatePicker>(FormattedXaml)),
        };

        await context.ShootAsync(
            DemoLayout.BuildComparisonWindow("DatePicker", rows),
            "datepicker-default-vs-custom-format.png");

        await context.SaveTableAsync(
            "text shown in the DatePicker",
            ["configuration", "SelectedDateFormat", "displayed text"],
            await ComboBoxAndDatePickerMeasurements.DatePickerFormatsAsync(),
            "datepicker-format-matrix.svg");

        await context.SaveTableAsync(
            "default text of a DatePicker (SelectedDate = 2026-04-15)",
            ["xml:lang", "CurrentCulture", "Language (value source)", "displayed text"],
            await ComboBoxAndDatePickerMeasurements.DatePickerCultureAsync(),
            "datepicker-culture-matrix.svg");
    }
}
