using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「DatePicker の表示形式をカスタマイズする方法」の図。
/// 同じ日付を、既定の表示と DatePickerTextBox のスタイルで整形した表示で並べる。
/// </summary>
internal sealed class DatePickerFormatScene : IScene
{
    // Language（xml:lang）の値の出どころが Default（自身にも親要素にも指定が無い）の既定表示は
    // スレッドの CurrentCulture に従うため、
    // 撮影マシン（日本語環境）では 2026/04/15 になり、記事が示す yyyy/MM/dd との差が出ない。
    // 自身への指定や親要素からの継承がある Language は CurrentCulture より優先される
    // （datepicker-culture-matrix.svg で実測）。
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
                                      StringFormat='yyyy\\/MM\\/dd'}" />
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
        @"XAML のマークアップ拡張の中の StringFormat の書き方（\/、\\/、\'/\'、エスケープしない '/'）ごとの、解析後の書式と、xml:lang を 6 つの文化圏に変えたときの表示",
        "SelectedDateChanged で DatePicker.Text を書き換える方法で、表示が変わるか",
        "既定表示のカルチャが要素の Language（xml:lang）とスレッドの CurrentCulture のどちらに従うか（自身への指定・親要素からの継承・CurrentCulture を独立に変えて表示文字列を測る）",
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
                "StringFormat='yyyy\\\\/MM\\\\/dd'",
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

        await context.SaveTableAsync(
            "StringFormat inside a XAML Binding: format after parsing and text shown (2026-04-15)",
            ["StringFormat in XAML", "format after parsing", .. Cultures],
            await EscapeMatrixAsync(),
            "datepicker-stringformat-escape.svg");

        await context.SaveTableAsync(
            "setting DatePicker.Text in SelectedDateChanged",
            ["xml:lang", "handler calls", "Text / shown after the handler set yyyy/MM/dd"],
            [await CodeBehindAsync()],
            "datepicker-codebehind.svg");
    }

    private static readonly string[] Cultures = ["en-US", "ja-JP", "de-DE", "th-TH", "ar-SA", "fa-IR"];

    /// <summary>記事の書き方の候補。マークアップ拡張の中では \ がエスケープ文字として扱われる。</summary>
    private static readonly string[] Formats =
    [
        @"'yyyy\/MM\/dd'",
        @"'yyyy\\/MM\\/dd'",
        @"yyyy\'/\'MM\'/\'dd",
        @"yyyy'/'MM'/'dd",
    ];

    private static string StyledPicker(string format, string language) =>
        "<DatePicker xml:lang=\"" + language + "\" Width=\"200\"><DatePicker.Resources><Style TargetType=\"DatePickerTextBox\">" +
        "<Setter Property=\"Text\" Value=\"{Binding SelectedDate, RelativeSource={RelativeSource AncestorType=DatePicker}, StringFormat=" + format + "}\" />" +
        "</Style></DatePicker.Resources></DatePicker>";

    private static async Task<List<IReadOnlyList<string>>> EscapeMatrixAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (string format in Formats)
        {
            string? effective = null;
            var cells = new List<string>();
            foreach (string culture in Cultures)
            {
                DatePicker picker;
                try
                {
                    picker = SceneContext.LoadXaml<DatePicker>(StyledPicker(format, culture));
                }
                catch (Exception e)
                {
                    effective ??= e.GetType().Name;
                    cells.Add("-");
                    continue;
                }

                picker.SelectedDate = new DateTime(2026, 4, 15);
                var window = new Window { Content = picker, Width = 260, Height = 100, ShowActivated = false, ShowInTaskbar = false };
                try
                {
                    await Capture.ShowAndSettleAsync(window);
                    var style = (Style)picker.Resources[typeof(DatePickerTextBox)];
                    effective ??= ((Binding)((Setter)style.Setters[0]).Value).StringFormat;
                    cells.Add(Visible(ShownText(picker)));
                }
                finally
                {
                    window.Close();
                }
            }

            rows.Add([format, effective ?? "-", .. cells]);
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> CodeBehindAsync()
    {
        var picker = new DatePicker { Width = 200, Language = XmlLanguage.GetLanguage("en-US") };
        int calls = 0;
        picker.SelectedDateChanged += (_, _) =>
        {
            calls++;
            picker.Text = picker.SelectedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? string.Empty;
        };
        var window = new Window { Content = picker, Width = 260, Height = 100, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            await Capture.ShowAndSettleAsync(window);
            picker.SelectedDate = new DateTime(2026, 4, 15);
            await Capture.SettleAsync(window);
            return ["en-US", calls.ToString(CultureInfo.InvariantCulture), $"{picker.Text} / {ShownText(picker)}"];
        }
        finally
        {
            window.Close();
        }
    }

    private static string ShownText(DatePicker picker)
    {
        DatePickerTextBox? box = null;
        void Walk(DependencyObject node)
        {
            if (node is DatePickerTextBox found) box = found;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
        }

        Walk(picker);
        return box?.Text ?? "(no text box)";
    }

    /// <summary>ar-SA などは区切りの前に見えない方向記号を入れるため、ASCII 以外は符号位置で示す。</summary>
    private static string Visible(string text)
        => string.Concat(text.Select(c => c < 128 ? c.ToString() : $"<U+{(int)c:X4}>"));
}
