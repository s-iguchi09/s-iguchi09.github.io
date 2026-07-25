using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「DatePicker の表示形式をカスタマイズする方法」の図。
/// 同じ日付を、既定の表示と DatePickerTextBox のスタイルで整形した表示で並べる。
/// </summary>
internal sealed class DatePickerFormatScene : IScene
{
    /// <summary>既定の表示。記事の例に合わせて 2026-04-15 を選択済みにする。</summary>
    private const string DefaultXaml =
        """<DatePicker SelectedDate="2026-04-15" Width="190" />""";

    /// <summary>
    /// 記事の「XAML スタイルによる方法」と同じ指定。
    /// 既定表示は OS のロケールに従うため、撮影環境（日本語）でも差が分かるよう
    /// 記事がレポート向けとして挙げている dd MMM yyyy を指定する。
    /// </summary>
    private const string FormattedXaml =
        """
        <DatePicker SelectedDate="2026-04-15" Width="190">
          <DatePicker.Resources>
            <Style TargetType="DatePickerTextBox">
              <Setter Property="Text"
                      Value="{Binding SelectedDate,
                                      RelativeSource={RelativeSource AncestorType=DatePicker},
                                      StringFormat='dd MMM yyyy'}" />
            </Style>
          </DatePicker.Resources>
        </DatePicker>
        """;

    public string Slug => "wpf-datepicker-custom-format";

    public async Task CaptureAsync(SceneContext context)
    {
        var rows = new[]
        {
            new DemoLayout.Row(
                "<DatePicker />",
                SceneContext.LoadXaml<DatePicker>(DefaultXaml)),
            new DemoLayout.Row(
                "StringFormat='dd MMM yyyy'",
                SceneContext.LoadXaml<DatePicker>(FormattedXaml)),
        };

        await context.ShootAsync(
            DemoLayout.BuildComparisonWindow("DatePicker", rows),
            "datepicker-default-vs-custom-format.png");
    }
}
