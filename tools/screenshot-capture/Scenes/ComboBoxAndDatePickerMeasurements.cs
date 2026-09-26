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

        // ToString はオーバーライドしない。オーバーライドすると DisplayMemberPath が無くても
        // Name が表示され、DisplayMemberPath の有無による違いが表に出なくなる。
    }

    private enum Priority
    {
        Low = 1,
        High = 5,
    }

    /// <summary>
    /// 記事の注意点に書く 4 つの挙動を測る。
    /// DisplayMemberPath と ItemTemplate の同時設定、SelectedValue とパスの型の違い、
    /// 列挙型の数値を SelectedValuePath で取れるか、SelectedValue を ItemsSource より先に設定したとき。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> PitfallsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(["DisplayMemberPath + ItemTemplate (code)", Throws(() => new ComboBox { DisplayMemberPath = "Name", ItemTemplate = new DataTemplate() })]);
        rows.Add(["DisplayMemberPath + ItemTemplate (XAML)", Throws(() => System.Windows.Markup.XamlReader.Parse(
            "<ComboBox xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" DisplayMemberPath=\"Name\"><ComboBox.ItemTemplate><DataTemplate /></ComboBox.ItemTemplate></ComboBox>"))]);

        // 型の違い: SelectedValuePath の Id は int、バインドしたソースは文字列型のプロパティで値は "20"。
        var typed = new StringHolder { Value = "20" };
        var typedCombo = new ComboBox { ItemsSource = Items, SelectedValuePath = nameof(Item.Id), DisplayMemberPath = nameof(Item.Name), DataContext = typed };
        typedCombo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new System.Windows.Data.Binding(nameof(StringHolder.Value)) { Mode = System.Windows.Data.BindingMode.TwoWay });
        string typedInitial = "";
        rows.AddRange(await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "SelectedValuePath=Id (int), source string property \"20\"",
                typedCombo,
                _ => [$"shown: SelectedIndex {typedInitial}; after selecting index 2, source = {typed.Value} ({typed.Value?.GetType().Name})"],
                Act: _ =>
                {
                    typedInitial = typedCombo.SelectedIndex.ToString();
                    typedCombo.SelectedIndex = 2;
                    return Task.CompletedTask;
                }),
        ]));

        // 同じ値を、宣言型が object のプロパティに入れた場合。書き戻しで型が変わる。
        var mismatch = new ValueHolder { Value = "20" };
        var mismatchCombo = new ComboBox { ItemsSource = Items, SelectedValuePath = nameof(Item.Id), DisplayMemberPath = nameof(Item.Name), DataContext = mismatch };
        mismatchCombo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new System.Windows.Data.Binding(nameof(ValueHolder.Value)) { Mode = System.Windows.Data.BindingMode.TwoWay });
        string mismatchInitial = "";
        rows.AddRange(await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "SelectedValuePath=Id (int), source object property holding \"20\"",
                mismatchCombo,
                _ => [$"shown: SelectedIndex {mismatchInitial}; after selecting index 2, source = {mismatch.Value} ({mismatch.Value?.GetType().Name})"],
                Act: _ =>
                {
                    mismatchInitial = mismatchCombo.SelectedIndex.ToString();
                    mismatchCombo.SelectedIndex = 2;
                    return Task.CompletedTask;
                }),
        ]));

        var enumCombo = new ComboBox { ItemsSource = Enum.GetValues<Priority>(), SelectedValuePath = "value__" };
        rows.AddRange(await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "enum items, SelectedValuePath=value__",
                enumCombo,
                _ => [$"SelectedItem {enumCombo.SelectedItem}, SelectedValue {WpfProbe.Describe(enumCombo.SelectedValue)}"],
                Act: _ =>
                {
                    enumCombo.SelectedIndex = 1;
                    return Task.CompletedTask;
                }),
        ]));

        // SelectedValue をコードで先に設定し、あとから ItemsSource を入れる。
        var codeFirst = new ComboBox { SelectedValuePath = nameof(Item.Id), DisplayMemberPath = nameof(Item.Name) };
        codeFirst.SelectedValue = 20;
        codeFirst.ItemsSource = Items;
        rows.AddRange(await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case("SelectedValue = 20 set before ItemsSource (code)", codeFirst, _ => [$"SelectedIndex {codeFirst.SelectedIndex}"]),
        ]));

        // SelectedValue をバインドし、表示した後で ItemsSource を入れる。
        var bound = new ValueHolder { Value = 30 };
        var boundFirst = new ComboBox { SelectedValuePath = nameof(Item.Id), DisplayMemberPath = nameof(Item.Name), DataContext = bound };
        boundFirst.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new System.Windows.Data.Binding(nameof(ValueHolder.Value)) { Mode = System.Windows.Data.BindingMode.TwoWay });
        rows.AddRange(await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "SelectedValue bound to 30, ItemsSource assigned after display",
                boundFirst,
                _ => [$"SelectedIndex {boundFirst.SelectedIndex}, source {bound.Value}"],
                Act: _ =>
                {
                    boundFirst.ItemsSource = Items;
                    return Task.CompletedTask;
                }),
        ]));

        return rows;
    }

    private sealed class ValueHolder
    {
        public object? Value { get; set; }
    }

    private sealed class StringHolder
    {
        public string? Value { get; set; }
    }

    private static string Throws(Action action)
    {
        try
        {
            action();
            return "no exception";
        }
        catch (Exception e)
        {
            // 文言は OS の言語で変わるため、型だけを出す。XAML では XamlParseException に包まれるので内側の型も出す。
            Exception inner = e.InnerException ?? e;
            return e == inner ? e.GetType().Name : $"{e.GetType().Name} (inner {inner.GetType().Name})";
        }
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
                    // ToString をオーバーライドしていないため、DisplayMemberPath が無いと型の完全名が出る。長いので言い換える。
                    DisplayedText(combo) is string shown && shown == typeof(Item).FullName ? "type name (Item.ToString())" : DisplayedText(combo) ?? "(nothing)",
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

        // 親要素だけに指定した場合（継承した Language）も測る。
        // DatePicker 自身に指定が無くても、値の出どころが Default でなくなるため。
        var settings = new (string? Own, string? Parent)[]
        {
            (null, null), ("en-US", null), ("de-DE", null), (null, "de-DE"),
        };

        foreach ((string? own, string? parent) in settings)
        {
            foreach (string currentCulture in new[] { "ja-JP", "en-US" })
            {
                rows.Add(await MeasureDateCultureAsync(date, own, parent, currentCulture));
            }
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> MeasureDateCultureAsync(
        DateTime date, string? language, string? parentLanguage, string currentCulture)
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
            if (parentLanguage is not null)
            {
                host.Language = XmlLanguage.GetLanguage(parentLanguage);
            }
            host.Children.Add(picker);

            string label = (language, parentLanguage) switch
            {
                (not null, _) => $"xml:lang=\"{language}\"",
                (null, not null) => $"parent: xml:lang=\"{parentLanguage}\"",
                _ => "(not set)",
            };

            List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    label,
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
