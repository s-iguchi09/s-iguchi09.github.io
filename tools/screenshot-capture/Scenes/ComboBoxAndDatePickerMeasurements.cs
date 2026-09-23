using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// ComboBox の選択プロパティと、DatePicker の表示書式を実測する部品。
/// </summary>
internal static class ComboBoxAndDatePickerMeasurements
{
    private sealed class Item
    {
        public required int Id { get; init; }

        public required string Name { get; init; }

        public override string ToString() => Name;
    }

    private static Item[] Items =>
    [
        new Item { Id = 10, Name = "alpha" },
        new Item { Id = 20, Name = "beta" },
        new Item { Id = 30, Name = "gamma" },
    ];

    /// <summary>
    /// 3 つの選択プロパティが同じ選択に対して何を返すかを測る。
    ///
    /// <c>SelectedValuePath</c> の有無で <c>SelectedValue</c> の中身が変わることは、
    /// 並べて出さないと伝わらない。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> SelectionPropertiesAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(await MeasureAsync("no DisplayMemberPath / no SelectedValuePath", null, null));
        rows.Add(await MeasureAsync("DisplayMemberPath=Name", nameof(Item.Name), null));
        rows.Add(await MeasureAsync("+ SelectedValuePath=Id", nameof(Item.Name), nameof(Item.Id)));
        rows.Add(await MeasureAsync("SelectedValuePath=Name", nameof(Item.Name), nameof(Item.Name)));

        return rows;
    }

    private static async Task<IReadOnlyList<string>> MeasureAsync(
        string label, string? displayMemberPath, string? selectedValuePath)
    {
        var combo = new ComboBox { ItemsSource = Items, Width = 160 };

        if (displayMemberPath is not null)
        {
            combo.DisplayMemberPath = displayMemberPath;
        }

        if (selectedValuePath is not null)
        {
            combo.SelectedValuePath = selectedValuePath;
        }

        var host = new Grid();
        host.Children.Add(combo);

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                label,
                host,
                _ =>
                [
                    combo.SelectedItem is Item item ? $"Item({item.Id}, {item.Name})" : WpfProbe.Describe(combo.SelectedItem),
                    combo.SelectedValue switch
                    {
                        Item value => $"Item({value.Id}, {value.Name})",
                        null => "null",
                        object value => $"{value} ({value.GetType().Name})",
                    },
                    combo.SelectedIndex.ToString(),
                    DisplayedText(combo) ?? "(nothing)",
                ],
                Act: _ =>
                {
                    // 2 番目の項目を選ぶ。ユーザーが選んだのと同じ状態にする。
                    combo.SelectedIndex = 1;
                    combo.UpdateLayout();
                    return Task.CompletedTask;
                }),
        ]);

        return measured[0];
    }

    /// <summary>閉じた状態の <see cref="ComboBox"/> に表示されている文字列。</summary>
    private static string? DisplayedText(ComboBox combo)
    {
        ContentPresenter? presenter = Descendants(combo)
            .OfType<ContentPresenter>()
            .FirstOrDefault(p => p.Name == "ContentSite");

        return presenter is null
            ? Descendants(combo).OfType<TextBlock>().FirstOrDefault()?.Text
            : Descendants(presenter).OfType<TextBlock>().FirstOrDefault()?.Text;
    }

    // ------------------------------------------------------------------
    // DatePicker の表示書式
    // ------------------------------------------------------------------

    /// <summary>
    /// <see cref="DatePicker"/> の表示形式を変える方法ごとに、実際に出る文字列を測る。
    ///
    /// <c>SelectedDateFormat</c> は Short と Long の 2 つしか持たないため、
    /// 任意の書式にするにはテキスト部分を直接書き換えるしかない、というのが記事の主張である。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> DatePickerFormatsAsync()
    {
        var date = new DateTime(2026, 7, 17);

        var rows = new List<IReadOnlyList<string>>();

        // 依存関係プロパティのメタデータ既定値と、設定しなかった場合の実効値は一致しない。
        // どちらか一方だけを「既定値」と書くと誤るため、両方を表に出す。
        var metadataDefault = (DatePickerFormat)DatePicker.SelectedDateFormatProperty
            .GetMetadata(typeof(DatePicker)).DefaultValue!;

        rows.Add(await MeasureDateAsync($"default (property metadata: {metadataDefault})", date, null, null));

        rows.Add(await MeasureDateAsync("SelectedDateFormat=Short", date, DatePickerFormat.Short, null));
        rows.Add(await MeasureDateAsync("SelectedDateFormat=Long", date, DatePickerFormat.Long, null));
        rows.Add(await MeasureDateAsync("text part overwritten", date, DatePickerFormat.Short, "yyyy/MM/dd (ddd)"));

        return rows;
    }

    /// <summary>
    /// 既定表示のカルチャが、要素の Language（xml:lang）とスレッドの CurrentCulture の
    /// どちらから来るかを切り分ける。両者を独立に変え、実際に出る文字列を測る。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> DatePickerCultureAsync()
    {
        var date = new DateTime(2026, 4, 15);
        var rows = new List<IReadOnlyList<string>>();

        foreach (string? language in new string?[] { null, "en-US", "de-DE" })
        {
            foreach (string currentCulture in new[] { "ja-JP", "en-US" })
            {
                rows.Add(await MeasureDateCultureAsync(date, language, currentCulture));
            }
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> MeasureDateCultureAsync(
        DateTime date, string? language, string currentCulture)
    {
        CultureInfo savedCulture = CultureInfo.CurrentCulture;
        CultureInfo savedUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            // DatePicker は SelectedDate の設定時とテンプレート適用時に文字列を作るため、
            // コントロールを作る前にスレッドのカルチャを切り替えておく。
            CultureInfo.CurrentCulture = new CultureInfo(currentCulture);
            CultureInfo.CurrentUICulture = new CultureInfo(currentCulture);

            var picker = new DatePicker { Width = 200 };
            if (language is not null)
            {
                picker.Language = XmlLanguage.GetLanguage(language);
            }
            picker.SelectedDate = date;

            var host = new Grid();
            host.Children.Add(picker);

            List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    language is null ? "(not set)" : $"xml:lang=\"{language}\"",
                    host,
                    _ =>
                    [
                        currentCulture,
                        WpfProbe.ValueAndSource(picker, FrameworkElement.LanguageProperty),
                        DatePickerText(picker) ?? "(nothing)",
                    ],
                    Act: _ =>
                    {
                        picker.UpdateLayout();
                        return Task.CompletedTask;
                    }),
            ]);

            return measured[0];
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
            CultureInfo.CurrentUICulture = savedUiCulture;
        }
    }

    private static async Task<IReadOnlyList<string>> MeasureDateAsync(
        string label, DateTime date, DatePickerFormat? format, string? customFormat)
    {
        var picker = new DatePicker { SelectedDate = date, Width = 200 };

        if (format is not null)
        {
            picker.SelectedDateFormat = format.Value;
        }

        var host = new Grid();
        host.Children.Add(picker);

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                label,
                host,
                _ =>
                [
                    // 値そのものだけでなく、どこから来た値かも出す。
                    // 設定しない場合の実効値は、メタデータの既定値とは限らない。
                    WpfProbe.ValueAndSource(picker, DatePicker.SelectedDateFormatProperty),
                    DatePickerText(picker) ?? "(nothing)",
                ],
                Act: _ =>
                {
                    picker.UpdateLayout();

                    if (customFormat is not null)
                    {
                        // 記事の方法と同じく、テンプレート内のテキスト部分を直接書き換える。
                        TextBox? part = Descendants(picker).OfType<TextBox>().FirstOrDefault();
                        if (part is not null)
                        {
                            part.Text = date.ToString(customFormat, CultureInfo.CurrentCulture);
                        }
                    }

                    picker.UpdateLayout();
                    return Task.CompletedTask;
                }),
        ]);

        return measured[0];
    }

    private static string? DatePickerText(DatePicker picker) =>
        Descendants(picker).OfType<TextBox>().FirstOrDefault()?.Text;

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
