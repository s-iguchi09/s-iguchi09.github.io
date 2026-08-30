using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// Fluent テーマがコントロールへ届く経路と、テンプレートの名前付きパーツを実測する部品。
/// </summary>
internal static class FluentThemeMeasurements
{
    /// <summary>
    /// 記事に載せた「余白だけを変える暗黙スタイル」。
    /// これを当てると Fluent のテンプレートが供給されなくなる、というのが記事の主張である。
    /// </summary>
    /// <summary>
    /// <c>Padding</c> だけを指定した暗黙スタイル。<c>Template</c> は指定しない。
    ///
    /// <paramref name="basedOn"/> を渡すと、そのスタイルを引き継ぐ。
    /// 引き継がない場合と引き継ぐ場合で結果が変わるため、両方を測れるようにしている。
    /// </summary>
    private static Style PaddingOnlyStyle(Style? basedOn = null)
    {
        var style = basedOn is null
            ? new Style(typeof(TextBox))
            : new Style(typeof(TextBox), basedOn);

        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        return style;
    }

    /// <summary>
    /// <c>ThemeMode</c> の有無と、アプリ側の暗黙スタイルの有無で、
    /// コントロールに供給されるテンプレートがどう変わるかを測る。
    ///
    /// テンプレートの出どころは、テンプレート内の名前付きパーツで判別できる。
    /// Fluent のテンプレートは <c>DeleteButton</c> を持ち、従来のテーマは持たない。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> ThemeDeliveryAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        rows.Add(await MeasureAsync("no ThemeMode", themeMode: null, implicitStyle: false));
        rows.Add(await MeasureAsync("ThemeMode=Light", themeMode: "Light", implicitStyle: false));
        rows.Add(await MeasureAsync("ThemeMode=Light + implicit Style", themeMode: "Light", implicitStyle: true));
        rows.Add(await MeasureAsync("no ThemeMode + implicit Style", themeMode: null, implicitStyle: true));

        // 記事は ThemeMode 以外に「Fluent.xaml を直接マージする」経路も扱っている。
        // ThemeMode だけを測って結論を書くと、そちらへ一般化できてしまう。
        rows.Add(await MeasureAsync(
            "merge Fluent.xaml directly", themeMode: null, implicitStyle: false, mergeFluent: true));
        rows.Add(await MeasureAsync(
            "merge Fluent.xaml + implicit Style", themeMode: null, implicitStyle: true, mergeFluent: true));

        // ここまでの暗黙スタイルは BasedOn も Template も持たない。
        // 元のスタイルを BasedOn で引き継いだ場合まで同じ結果とは限らないため、別に測る。
        // 経路によって結果が変わりうるので、2 通りとも測る。
        rows.Add(await MeasureAsync(
            "ThemeMode=Light + implicit Style, BasedOn",
            themeMode: "Light",
            implicitStyle: true,
            basedOnThemeStyle: true));
        rows.Add(await MeasureAsync(
            "merge Fluent.xaml + implicit Style, BasedOn",
            themeMode: null,
            implicitStyle: true,
            mergeFluent: true,
            basedOnThemeStyle: true));

        return rows;
    }

    /// <summary>
    /// <c>ThemeMode</c> を使わず、Fluent のリソースディクショナリを直接マージする。
    /// 記事の「App.xaml にリソースディクショナリを追加する」方法にあたる。
    /// </summary>
    private static ResourceDictionary FluentDictionary() => new()
    {
        Source = new Uri(
            "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml",
            UriKind.Absolute),
    };

    private static async Task<IReadOnlyList<string>> MeasureAsync(
        string label,
        string? themeMode,
        bool implicitStyle,
        bool mergeFluent = false,
        bool basedOnThemeStyle = false)
    {
        var box = new TextBox { Text = "sample", Width = 160 };
        var host = new Grid();
        host.Children.Add(box);

        var window = new Window
        {
            Title = label,
            Content = host,
            Width = 260,
            Height = 140,
            ShowActivated = false,
        };

        if (themeMode is not null)
        {
#pragma warning disable WPF0001 // ThemeMode は実験的 API として公開されている。
            window.ThemeMode = themeMode switch
            {
                "Light" => ThemeMode.Light,
                "Dark" => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
#pragma warning restore WPF0001
        }

        if (mergeFluent)
        {
            window.Resources.MergedDictionaries.Add(FluentDictionary());
        }

        if (implicitStyle)
        {
            // BasedOn で元のスタイルを引き継ぐかどうかで結果が変わる。
            // 引き継ぐ場合は、テーマが供給しているスタイルを探してから重ねる。
            Style? baseStyle = null;
            if (basedOnThemeStyle)
            {
                baseStyle = window.TryFindResource(typeof(TextBox)) as Style
                    ?? throw new InvalidOperationException(
                        "引き継ぐ元のスタイルが見つからない。BasedOn の測定になっていない。");
            }

            window.Resources.Add(typeof(TextBox), PaddingOnlyStyle(baseStyle));
        }

        try
        {
            await Capture.ShowAndSettleAsync(window);
            box.ApplyTemplate();

            // Style プロパティに値が入っているかで、暗黙スタイルが当たったかが分かる。
            // 従来のテーマスタイルは Style プロパティを埋めないため null のままになる。
            // Fluent はリソースとして届くため、ここが埋まる。
            string styleSource = box.Style is null ? "theme style" : "implicit style";

            string parts = string.Join(
                ", ",
                new[] { "DeleteButton", "ClearButton", "PART_ContentHost" }
                    .Where(name => box.Template?.FindName(name, box) is not null));

            return
            [
                label,
                styleSource,
                parts.Length == 0 ? "(none)" : parts,
                box.Padding.Left.ToString("0"),
            ];
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// <see cref="SystemColors"/> のキーが実際に返す色を読み出す。
    ///
    /// 「OS の設定に追随する」という主張は、値を並べて初めて確かめられる。
    /// </summary>
    public static List<IReadOnlyList<string>> SystemColorValues()
    {
        (string Label, Color Color)[] targets =
        [
            ("WindowColor", SystemColors.WindowColor),
            ("WindowTextColor", SystemColors.WindowTextColor),
            ("ControlColor", SystemColors.ControlColor),
            // 選択項目のハイライト色と、個人用設定のアクセント色は別のキーである。
            // 混同しやすいため並べて出す。
            ("HighlightColor", SystemColors.HighlightColor),
            ("HighlightTextColor", SystemColors.HighlightTextColor),
            ("AccentColor", SystemColors.AccentColor),
            ("GrayTextColor", SystemColors.GrayTextColor),
        ];

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string label, Color color) in targets)
        {
            rows.Add([
                $"SystemColors.{label}",
                color.ToString(),
                $"{Luminance(color):0.00}",
            ]);
        }

        return rows;
    }

    /// <summary>
    /// 色の参照方法によって、後からのリソース差し替えに追随するかどうかが変わることを測る。
    ///
    /// アプリケーションのリソースにある <see cref="SystemColors.WindowBrushKey"/> を
    /// 差し替え、色を直接読んで焼き込んだ場合とリソースキーで参照した場合とで
    /// 結果が分かれることを示す。
    ///
    /// ここで測っているのはアプリケーションリソースの差し替えへの追随だけである。
    /// OS のテーマ切り替えそのものは測っていないため、そこまで主張しない。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> ColorReferenceTrackingAsync()
    {
        var direct = new Border
        {
            Width = 60,
            Height = 20,
            // 記事が避けるよう述べている書き方。読んだ時点の色をそのまま持つ。
            Background = new SolidColorBrush(SystemColors.WindowColor),
        };

        var viaKey = new Border { Width = 60, Height = 20 };
        viaKey.SetResourceReference(Border.BackgroundProperty, SystemColors.WindowBrushKey);

        var host = new StackPanel();
        host.Children.Add(direct);
        host.Children.Add(viaKey);

        string beforeDirect = string.Empty;
        string beforeViaKey = string.Empty;
        string afterDirect = string.Empty;
        string afterViaKey = string.Empty;

        object key = SystemColors.WindowBrushKey;
        ResourceDictionary resources = Application.Current.Resources;
        bool hadOwnValue = resources.Contains(key);
        object? original = hadOwnValue ? resources[key] : null;

        await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "system brush replacement",
                host,
                _ =>
                {
                    afterDirect = BrushText(direct.Background);
                    afterViaKey = BrushText(viaKey.Background);
                    return [];
                },
                Act: _ =>
                {
                    beforeDirect = BrushText(direct.Background);
                    beforeViaKey = BrushText(viaKey.Background);

                    resources[key] = new SolidColorBrush(Color.FromRgb(0x10, 0x20, 0x30));
                    return Task.CompletedTask;
                }),
        ]);

        // 差し替えたリソースを戻す。以降のシーンへ影響させない。
        if (hadOwnValue)
        {
            resources[key] = original!;
        }
        else
        {
            resources.Remove(key);
        }

        return
        [
            ["new SolidColorBrush(SystemColors.WindowColor)", beforeDirect, afterDirect],
            ["DynamicResource SystemColors.WindowBrushKey", beforeViaKey, afterViaKey],
        ];
    }

    private static string BrushText(Brush? brush) => brush switch
    {
        SolidColorBrush solid => solid.Color.ToString(),
        null => "null",
        _ => brush.GetType().Name,
    };

    /// <summary>相対輝度。背景と前景のコントラストを読み取れるようにする。</summary>
    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }
}
