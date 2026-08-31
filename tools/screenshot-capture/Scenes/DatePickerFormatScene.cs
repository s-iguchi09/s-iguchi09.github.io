using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「DatePicker の表示形式をカスタマイズする方法」の図。
/// 同じ日付を、既定の表示と DatePickerTextBox のスタイルで整形した表示で並べる。
/// </summary>
internal sealed class DatePickerFormatScene : IScene
{
    // 既定表示はターゲット要素の Language（xml:lang）に従う。撮影マシンのロケール
    // （日本語）のままだと既定表示も 2026/04/15 になり、記事が示す yyyy/MM/dd との
    // 差が出ない。そこで両方に xml:lang="en-US" を与え、ロケールが異なる環境でも
    // 表示が固定されることを示す。
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
    }
}
