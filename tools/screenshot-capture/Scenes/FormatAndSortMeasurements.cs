using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 並べ替えの比較器と、<c>Binding.StringFormat</c> の書式化を実測する部品。
/// </summary>
internal static class FormatAndSortMeasurements
{
    // ------------------------------------------------------------------
    // 自然順の並べ替え
    // ------------------------------------------------------------------

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    /// <summary>
    /// 同じ入力を比較器ごとに並べ替え、結果の並びをそのまま出す。
    ///
    /// 「序数でもカルチャでも item10 が item2 より前になる」という主張は、
    /// 3 つを並べて初めて確かめられる。
    /// </summary>
    public static List<IReadOnlyList<string>> SortOrders()
    {
        string[] input = ["item10", "item2", "Item1", "item20", "item3"];

        (string Label, Comparison<string> Compare)[] comparers =
        [
            ("Ordinal", string.CompareOrdinal),
            ("CurrentCulture", (a, b) => string.Compare(a, b, StringComparison.CurrentCulture)),
            ("OrdinalIgnoreCase", (a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase)),
            ("StrCmpLogicalW", StrCmpLogicalW),
        ];

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string label, Comparison<string> compare) in comparers)
        {
            var sorted = new List<string>(input);
            sorted.Sort(compare);
            rows.Add([label, string.Join(", ", sorted)]);
        }

        return rows;
    }

    /// <summary>
    /// <see cref="StrCmpLogicalW"/> が個々の組でどう答えるかを出す。
    /// 大文字小文字を区別しないという性質も、ここで確かめられる。
    /// </summary>
    public static List<IReadOnlyList<string>> LogicalComparisons()
    {
        (string Left, string Right)[] pairs =
        [
            ("item2", "item10"),
            ("item10", "item2"),
            ("item2", "item2"),
            ("Item2", "item2"),
            ("item02", "item2"),
            ("a", "B"),
        ];

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string left, string right) in pairs)
        {
            int logical = StrCmpLogicalW(left, right);
            int ordinal = Math.Sign(string.CompareOrdinal(left, right));
            rows.Add([$"\"{left}\" vs \"{right}\"", Sign(logical), Sign(ordinal)]);
        }

        return rows;
    }

    private static string Sign(int value) => value switch
    {
        < 0 => "-1 (left first)",
        > 0 => "+1 (right first)",
        _ => "0 (equal)",
    };

    // ------------------------------------------------------------------
    // Binding.StringFormat
    // ------------------------------------------------------------------

    private sealed class Money
    {
        public decimal Price { get; } = 1234.5m;

        public DateTime Date { get; } = new(2026, 7, 17);
    }

    /// <summary>
    /// 書式化に使われるカルチャと、ターゲットのプロパティ型による違いを測る。
    ///
    /// 「OS の地域設定ではなくターゲット要素の <c>Language</c> が使われる」ことと、
    /// 「<c>Content</c> では <c>StringFormat</c> が効かない」ことを、実際の表示から確かめる。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> StringFormatAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add([
            "FrameworkElement.Language default",
            "-",
            ((XmlLanguage)FrameworkElement.LanguageProperty.GetMetadata(typeof(FrameworkElement)).DefaultValue).IetfLanguageTag,
        ]);

        rows.Add([
            "CultureInfo.CurrentCulture",
            "-",
            CultureInfo.CurrentCulture.Name,
        ]);

        rows.Add(await MeasureAsync("TextBlock.Text, C", "no ConverterCulture",
            () => Text(nameof(Money.Price), "C", null)));
        rows.Add(await MeasureAsync("TextBlock.Text, C", "ConverterCulture=ja-JP",
            () => Text(nameof(Money.Price), "C", new CultureInfo("ja-JP"))));
        rows.Add(await MeasureAsync("TextBlock.Text, d", "no ConverterCulture",
            () => Text(nameof(Money.Date), "d", null)));
        rows.Add(await MeasureAsync("TextBlock.Text, d", "ConverterCulture=ja-JP",
            () => Text(nameof(Money.Date), "d", new CultureInfo("ja-JP"))));
        rows.Add(await MeasureAsync("Label.Content, StringFormat=C", "-", () => LabelWith(contentStringFormat: false)));
        rows.Add(await MeasureAsync("Label.Content, ContentStringFormat=C", "-", () => LabelWith(contentStringFormat: true)));

        return rows;
    }

    private static async Task<IReadOnlyList<string>> MeasureAsync(
        string label, string condition, Func<(FrameworkElement Host, Func<string> Read)> build)
    {
        (FrameworkElement host, Func<string> read) = build();

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(label, host, _ => [read()]),
        ]);

        return [label, condition, measured[0][1]];
    }

    private static (FrameworkElement Host, Func<string> Read) Text(string path, string format, CultureInfo? culture)
    {
        var block = new TextBlock();
        var binding = new Binding(path)
        {
            Source = new Money(),
            StringFormat = format,
        };

        if (culture is not null)
        {
            binding.ConverterCulture = culture;
        }

        block.SetBinding(TextBlock.TextProperty, binding);

        var grid = new Grid();
        grid.Children.Add(block);
        return (grid, () => block.Text);
    }

    private static (FrameworkElement Host, Func<string> Read) LabelWith(bool contentStringFormat)
    {
        var label = new Label();
        label.SetBinding(ContentControl.ContentProperty, new Binding(nameof(Money.Price))
        {
            Source = new Money(),
            // Content は object 型のため、StringFormat は効かないはずである。
            StringFormat = contentStringFormat ? null : "C",
        });

        if (contentStringFormat)
        {
            label.ContentStringFormat = "C";
        }

        var grid = new Grid();
        grid.Children.Add(label);

        // 実際に描画された文字列を visual ツリーから読む。
        // ここで自前に書式化してしまうと、WPF の挙動ではなく自分の実装を測ることになる。
        return (grid, () => RenderedText(label) ?? "(nothing rendered)");
    }

    /// <summary>要素の下に生成された <see cref="TextBlock"/> の文字列を返す。</summary>
    private static string? RenderedText(DependencyObject root)
    {
        if (root is TextBlock block)
        {
            return block.Text;
        }

        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            string? found = RenderedText(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
