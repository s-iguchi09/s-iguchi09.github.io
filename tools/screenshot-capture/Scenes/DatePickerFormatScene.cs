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
        "SelectedDateChanged で DatePicker.Text を書き換える方法で、表示が変わるか（ハンドラーの入口と、代入の直後の Text も記録する）",
        "記事の Style（DatePickerTextBox.Text へのバインド）が、表示後に SelectedDate を変えても効き続けるか。TextBox.Text の値の出どころも記録する",
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
            ["xml:lang", "handler calls", "Text at handler entry → right after the assignment", "Text / shown after the handler"],
            [await CodeBehindAsync()],
            "datepicker-codebehind.svg");

        await context.SaveTableAsync(
            @"the article's Style (StringFormat='yyyy\\/MM\\/dd', xml:lang=""de-DE""): text shown over time",
            ["step", "shown text", "TextBox.Text value source"],
            await StyleLifecycleAsync(),
            "datepicker-style-lifecycle.svg");
    }

    /// <summary>
    /// 記事の Style を当てた DatePicker で、表示直後・コードで SelectedDate を変えた後・
    /// カレンダーで日付を選んだ後の表示を測る。既定の書式（de-DE なら dd.MM.yyyy）に戻れば、
    /// DatePicker が TextBox.Text を書き換えて Style のバインドに勝ったことになる。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> StyleLifecycleAsync()
    {
        // 記事の XAML と同じ書き方（XAML の中で \\/ と書く）。
        DatePicker picker = SceneContext.LoadXaml<DatePicker>(StyledPicker(@"'yyyy\\/MM\\/dd'", "de-DE"));
        picker.SelectedDate = new DateTime(2026, 4, 15);
        var window = new Window { Content = picker, Width = 260, Height = 320, ShowActivated = false, ShowInTaskbar = false };
        var rows = new List<IReadOnlyList<string>>();
        try
        {
            await Capture.ShowAndSettleAsync(window);
            rows.Add(Row("shown (SelectedDate = 2026-04-15)"));

            picker.SelectedDate = new DateTime(2026, 5, 20);
            await Capture.SettleAsync(window);
            rows.Add(Row("SelectedDate = 2026-05-20 in code"));

            // カレンダーを開き、カレンダー側の選択として日付を選ぶ。利用者がクリックで選んだときと同じく、
            // Calendar.SelectedDate から DatePicker.SelectedDate へ伝わる経路を通る。
            picker.IsDropDownOpen = true;
            await Capture.SettleAsync(window);
            System.Windows.Controls.Calendar calendar = FindCalendar(picker) ?? throw new InvalidOperationException("カレンダーが見つからない。");
            calendar.SelectedDate = new DateTime(2026, 6, 3);
            picker.IsDropDownOpen = false;
            await Capture.SettleAsync(window);
            rows.Add(Row("2026-06-03 selected in the calendar"));
        }
        finally
        {
            window.Close();
        }

        return rows;

        IReadOnlyList<string> Row(string step)
        {
            DatePickerTextBox box = FindTextBox(picker) ?? throw new InvalidOperationException("テキスト部分が見つからない。");
            ValueSource source = DependencyPropertyHelper.GetValueSource(box, TextBox.TextProperty);
            string flags = (source.IsExpression ? ", binding" : "") + (source.IsCurrent ? ", current value" : "");
            return [step, Visible(box.Text), source.BaseValueSource + flags];
        }
    }

    private static DatePickerTextBox? FindTextBox(DependencyObject root)
    {
        if (root is DatePickerTextBox box) return box;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DatePickerTextBox? found = FindTextBox(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }

        return null;
    }

    /// <summary>カレンダーは Popup の中にあり、DatePicker の visual ツリーには入らないため、Popup の子から探す。</summary>
    private static System.Windows.Controls.Calendar? FindCalendar(DatePicker picker)
    {
        picker.ApplyTemplate();
        return picker.Template.FindName("PART_Popup", picker) is System.Windows.Controls.Primitives.Popup { Child: System.Windows.Controls.Calendar calendar }
            ? calendar
            : null;
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
        var trace = new List<string>();
        picker.SelectedDateChanged += (_, _) =>
        {
            calls++;
            string entry = picker.Text;
            picker.Text = picker.SelectedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? string.Empty;
            trace.Add($"{entry} → {picker.Text}");
        };
        var window = new Window { Content = picker, Width = 260, Height = 100, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            await Capture.ShowAndSettleAsync(window);
            picker.SelectedDate = new DateTime(2026, 4, 15);
            await Capture.SettleAsync(window);
            return ["en-US", calls.ToString(CultureInfo.InvariantCulture), string.Join("; ", trace), $"{picker.Text} / {ShownText(picker)}"];
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
