using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

#pragma warning disable WPF0001 // ThemeMode は実験的 API として公開されている。このシーンは全体でそれを使う。

/// <summary>
/// 記事「WPF Fluent テーマで ThemeMode を実行時に切り替えても一部が追従しない問題」の検証と図。
///
/// <c>Application.ThemeMode</c> を Light から Dark へ切り替え、ウィンドウの構成ごとに
/// Fluent のブラシが追従するかを測る。あわせて、ブラシの参照方法（StaticResource /
/// DynamicResource / コードでの FindResource）による違いと、記事の実装例に載せた
/// 切り替えヘルパーが追従しないウィンドウを直せるかを確かめる。
/// </summary>
internal sealed class FluentThemeModeSwitchScene : IScene
{
    private const string FluentLightUri =
        "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml";

    /// <summary>Fluent.Light.xaml を中でマージしている、アプリ独自の Styles.xaml に相当する辞書。</summary>
    private const string StylesUri =
        "pack://application:,,,/ScreenshotCapture;component/Scenes/FluentThemeMode/Styles.xaml";

    /// <summary>ウィンドウが追従したかを判定するのに使う Fluent のブラシ。</summary>
    private const string ProbeKey = "TextFillColorPrimaryBrush";

    /// <summary>
    /// 記事の「問題」に載せている XAML。StaticResource と DynamicResource で
    /// 同じブラシを参照するテキストと、Fluent のスタイルが当たる標準コントロールを並べる。
    /// </summary>
    private const string ContentXaml = """
        <Border Background="{DynamicResource ApplicationBackgroundBrush}" Padding="20">
          <StackPanel>
            <TextBlock x:Name="DynamicText" Text="DynamicResource"
                       Foreground="{DynamicResource TextFillColorPrimaryBrush}" />
            <TextBlock x:Name="StaticText" Text="StaticResource" Margin="0,8,0,0"
                       Foreground="{StaticResource TextFillColorPrimaryBrush}" />
            <StackPanel Orientation="Horizontal" Margin="0,14,0,0">
              <Button x:Name="SaveButton" Content="Save" />
              <CheckBox Content="Overwrite" Margin="14,0,0,0" VerticalAlignment="Center" />
            </StackPanel>
          </StackPanel>
        </Border>
        """;

    /// <summary>
    /// ThemeMode.System の主張の前提（AppsUseLightTheme が 0）が、実行環境で成り立ったか。
    /// 成り立たない環境では、その主張を検証記録へ出さない。
    /// </summary>
    private bool _systemDarkMeasured;

    public IReadOnlyList<string> Verifies =>
    [
        "Application.ThemeMode の変更は、Application.Resources 直下の Fluent 辞書を Fluent.Light.xaml から Fluent.Dark.xaml へ差し替えること",
        "Window.ThemeMode を指定していないウィンドウと None を指定したウィンドウは切り替えに追従し、Light を指定したウィンドウは追従しないこと",
        "Window.Resources に Fluent.Light.xaml を手でマージしたウィンドウは Window.ThemeMode が Light と読め、切り替えに追従しないこと",
        "ThemeMode を設定する前に Application.Resources 直下へ手でマージした Fluent.Light.xaml は、ThemeMode の切り替えで Fluent.Dark.xaml に置き換わること",
        "Styles.xaml の中にネストした Fluent.Light.xaml は置き換わらず、ThemeMode の辞書より後ろに残るため、Dark にしても Light のブラシが使われ続けること",
        "StaticResource とコードで FindResource して代入したブラシは追従せず、DynamicResource だけが追従すること",
        "ReadLocalValue の型で、DynamicResource（ResourceReferenceExpression）かどうかを判別できること",
        "SystemColors のキーを DynamicResource で参照した場合は、ReadLocalValue が ResourceReferenceExpression でも ThemeMode の切り替えに追従しないこと",
        "TextBlock.ForegroundProperty と Control.ForegroundProperty が同じ依存関係プロパティであること",
        "Fluent のスタイルから色が決まる Button では、Foreground のローカル値が無い（UnsetValue）こと",
        "記事の DumpThemeState が、ネストした辞書・ウィンドウの Fluent 辞書・固定されたブラシを出力に表すこと",
        "記事の切り替えヘルパーを通すと、Window.ThemeMode や Window.Resources の Fluent 辞書で固定されていたウィンドウも追従し、ネストした辞書の構成は追従しないこと",
        .. (_systemDarkMeasured
            ? ["AppsUseLightTheme が 0 の環境で、ThemeMode.System が Fluent.xaml をマージし、Dark と同じブラシを選ぶこと"]
            : Array.Empty<string>()),
    ];

    public string Slug => "wpf-fluent-thememode-runtime-switch";

    public async Task CaptureAsync(SceneContext context)
    {
        Application application = Application.Current;
        ThemeMode original = application.ThemeMode;

        try
        {
            await context.SaveTableAsync(
                "Application.Resources.MergedDictionaries per ThemeMode",
                ["Application.ThemeMode", "merged dictionaries"],
                await MeasureAppDictionariesAsync(),
                "thememode-app-dictionaries.svg");

            var appDictionaries = new List<IReadOnlyList<string>>();
            await context.SaveTableAsync(
                "Application.ThemeMode: Light -> Dark",
                ["window setup", "Window.ThemeMode", "brush before", "brush after", "follows"],
                await MeasureWindowSetupsAsync(helper: false, appDictionaries),
                "thememode-follow-matrix.svg");

            await context.SaveTableAsync(
                "App merged dictionaries before and after the switch",
                ["window setup", "ThemeMode", "App merged dictionaries"],
                appDictionaries,
                "thememode-manual-dictionaries.svg");

            await context.SaveTableAsync(
                "ApplyTheme(ThemeMode.Dark) from a Light app",
                ["window setup", "Window.ThemeMode", "brush before", "brush after", "follows"],
                await MeasureWindowSetupsAsync(helper: true),
                "thememode-helper-matrix.svg");

            await context.SaveTableAsync(
                "Application.ThemeMode: Light -> Dark",
                ["how the brush is referenced", "ReadLocalValue", "before", "after", "follows"],
                await MeasureReferenceKindsAsync(),
                "thememode-reference-kinds.svg");

            IReadOnlyList<string> systemRow = await MeasureSystemAsync();

            // 本文の System の主張は、ダーク設定の環境で Dark のブラシが選ばれたことに基づく。
            // それ以外の環境で実行した場合は、主張を検証記録に出さない。
            // ハイコントラスト環境では ThemeMode.System でも Fluent.HC.xaml が選ばれるため、辞書名も確かめる。
            _systemDarkMeasured =
                systemRow[0] == "0" &&
                systemRow[1] == "Fluent.xaml" &&
                systemRow[2] == "#FFFFFFFF";

            await context.SaveTableAsync(
                "ThemeMode.System on this machine",
                ["AppsUseLightTheme", "merged dictionary", ProbeKey],
                [systemRow],
                "thememode-system.svg");

            await context.SaveTableAsync(
                "DumpThemeState(StaticText) after ThemeMode=Dark",
                ["output"],
                await MeasureDumpAsync(),
                "thememode-dump-output.svg");

            await ShootAsync(context);
        }
        finally
        {
            application.ThemeMode = original;
        }
    }

    /// <summary>
    /// 記事の実装例に載せている切り替えヘルパーと同じ処理。
    /// アプリ全体の ThemeMode を変え、各ウィンドウが持つ Fluent 辞書を外して
    /// アプリ側の辞書へ追従させる。
    /// </summary>
    private static void ApplyTheme(ThemeMode mode)
    {
        Application app = Application.Current;
        app.ThemeMode = mode;

        foreach (Window window in app.Windows)
        {
            // ウィンドウ単位の ThemeMode を外す。None にすると、アプリ側の ThemeMode が適用される。
            window.ThemeMode = ThemeMode.None;

            // Window.Resources へ手でマージした Fluent 辞書も、アプリ側の辞書より近くにあるため外す。
            foreach (ResourceDictionary dictionary in window.Resources.MergedDictionaries
                         .Where(IsFluentDictionary)
                         .ToList())
            {
                window.Resources.MergedDictionaries.Remove(dictionary);
            }
        }
    }

    private static bool IsFluentDictionary(ResourceDictionary dictionary) =>
        dictionary.Source?.OriginalString.Contains(
            "PresentationFramework.Fluent;component/Themes/", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// 1 つのウィンドウ構成。<paramref name="ConfigureApp"/> は ThemeMode を設定する前に、
    /// <paramref name="ConfigureWindow"/> はウィンドウを表示する前に実行する。
    /// App.xaml で辞書を宣言し、起動後に ThemeMode を設定する順序を再現するためである。
    /// </summary>
    private sealed record Setup(
        string Label,
        Func<ResourceDictionary?>? ConfigureApp = null,
        Action<Window>? ConfigureWindow = null);

    /// <summary>辞書を Application.Resources へ追加し、後で取り除けるように返す。</summary>
    private static ResourceDictionary AddToApp(ResourceDictionary dictionary)
    {
        Application.Current.Resources.MergedDictionaries.Add(dictionary);
        return dictionary;
    }

    /// <summary>
    /// ウィンドウの構成ごとに、アプリの ThemeMode を Light から Dark へ切り替えて追従を測る。
    /// <paramref name="helper"/> が true のときは、プロパティを直接変える代わりに
    /// <see cref="ApplyTheme"/> を通す。
    /// <paramref name="appDictionaries"/> を渡すと、アプリ側に辞書を置いた構成について
    /// 切り替え前後の Application.Resources.MergedDictionaries の並びを書き込む。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> MeasureWindowSetupsAsync(
        bool helper,
        List<IReadOnlyList<string>>? appDictionaries = null)
    {
        Application app = Application.Current;

        Setup[] setups =
        [
            new("(not set)"),
            new("Window.ThemeMode=None", ConfigureWindow: w => w.ThemeMode = ThemeMode.None),
            new("Window.ThemeMode=Light", ConfigureWindow: w => w.ThemeMode = ThemeMode.Light),
            new(
                "Fluent.Light.xaml in Window.Resources",
                ConfigureWindow: w => w.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = new Uri(FluentLightUri) })),
            // App.xaml の MergedDictionaries に Fluent.Light.xaml を直接書いた状態。
            new(
                "Fluent.Light.xaml in App resources",
                ConfigureApp: () => AddToApp(new ResourceDictionary { Source = new Uri(FluentLightUri) })),
            // App.xaml から Styles.xaml をマージし、その Styles.xaml の中で Fluent.Light.xaml を
            // マージしている状態。
            new(
                "Fluent.Light.xaml nested in Styles.xaml",
                ConfigureApp: () => AddToApp(new ResourceDictionary { Source = new Uri(StylesUri) })),
        ];

        var rows = new List<IReadOnlyList<string>>();

        foreach (Setup setup in setups)
        {
            ResourceDictionary? added = setup.ConfigureApp?.Invoke();
            app.ThemeMode = ThemeMode.Light;
            string appBefore = DescribeDictionaries(app.Resources);

            var window = new Window
            {
                Title = setup.Label,
                Width = 320,
                Height = 200,
                ShowActivated = false,
                Content = SceneContext.LoadXaml<Border>(ContentXaml),
            };

            try
            {
                setup.ConfigureWindow?.Invoke(window);
                await Capture.ShowAndSettleAsync(window);
                string before = BrushText(DynamicText(window).Foreground);

                if (helper)
                {
                    ApplyTheme(ThemeMode.Dark);
                }
                else
                {
                    app.ThemeMode = ThemeMode.Dark;
                }

                await Capture.SettleAsync(window);
                string after = BrushText(DynamicText(window).Foreground);

                rows.Add([
                    setup.Label,
                    window.ThemeMode.Value,
                    before,
                    after,
                    before == after ? "no" : "yes",
                ]);

                if (added is not null)
                {
                    appDictionaries?.Add([setup.Label, "Light", appBefore]);
                    appDictionaries?.Add([setup.Label, "Dark", DescribeDictionaries(app.Resources)]);
                }
            }
            finally
            {
                window.Close();
                if (added is not null)
                {
                    app.Resources.MergedDictionaries.Remove(added);
                }

                app.ThemeMode = ThemeMode.None;
            }
        }

        return rows;
    }

    /// <summary>
    /// MergedDictionaries の並びを 1 行で表す。ネストした辞書は角かっこで中身を示す。
    /// </summary>
    private static string DescribeDictionaries(ResourceDictionary dictionary)
    {
        IEnumerable<string> items = dictionary.MergedDictionaries.Select(d =>
        {
            string name = d.Source is null ? "(no Source)" : Path.GetFileName(d.Source.OriginalString);
            return d.MergedDictionaries.Count == 0 ? name : $"{name}[{DescribeDictionaries(d)}]";
        });

        string text = string.Join(", ", items);
        return text.Length == 0 ? "(empty)" : text;
    }

    /// <summary>
    /// 同じブラシを StaticResource・DynamicResource・コードでの FindResource で参照し、
    /// 切り替えへの追従と、ReadLocalValue で読める値の型を測る。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> MeasureReferenceKindsAsync()
    {
        // 記事の DumpThemeState は TextBlock.ForegroundProperty で Button なども読めると述べている。
        // その前提が崩れていれば、ここで止める。
        if (!ReferenceEquals(TextBlock.ForegroundProperty, Control.ForegroundProperty))
        {
            throw new InvalidOperationException("TextBlock.ForegroundProperty と Control.ForegroundProperty が別のプロパティになっている。");
        }

        Application app = Application.Current;
        app.ThemeMode = ThemeMode.Light;

        Border content = SceneContext.LoadXaml<Border>(ContentXaml);
        var window = new Window { Title = "reference kinds", Width = 320, Height = 220, ShowActivated = false, Content = content };

        var codeText = new TextBlock { Text = "FindResource" };
        ((StackPanel)content.Child).Children.Add(codeText);

        // SystemColors のキーを DynamicResource で参照する。参照は動的でも、SystemColors の値は
        // ThemeMode で変わらないため追従しない、というのを測る（ReadLocalValue は DynamicResource と同じ型になる）。
        var systemText = new TextBlock { Text = "SystemColors" };
        systemText.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.ControlTextBrushKey);
        ((StackPanel)content.Child).Children.Add(systemText);

        try
        {
            await Capture.ShowAndSettleAsync(window);

            // 記事の「問題」で避けるよう述べている書き方。取得した時点のブラシを代入する。
            codeText.Foreground = (Brush)window.FindResource(ProbeKey);

            (string Label, Control? Button, TextBlock? Target)[] targets =
            [
                ("{StaticResource}", null, StaticText(window)),
                ("{DynamicResource}", null, DynamicText(window)),
                ("Foreground = FindResource(...)", null, codeText),
                ("{DynamicResource SystemColors.ControlTextBrushKey}", null, systemText),
                // Foreground を指定していない標準コントロール。色は Fluent のスタイルから来る。
                ("Button (Fluent style)", (Control)content.FindName("SaveButton"), null),
            ];

            var before = targets.Select(t => BrushText(Foreground(t.Button, t.Target))).ToList();

            app.ThemeMode = ThemeMode.Dark;
            await Capture.SettleAsync(window);

            var rows = new List<IReadOnlyList<string>>();
            for (int i = 0; i < targets.Length; i++)
            {
                DependencyObject target = (DependencyObject?)targets[i].Target ?? targets[i].Button!;
                string after = BrushText(Foreground(targets[i].Button, targets[i].Target));
                object local = target.ReadLocalValue(TextBlock.ForegroundProperty);
                rows.Add([
                    targets[i].Label,
                    local == DependencyProperty.UnsetValue ? "(no local value)" : local.GetType().Name,
                    before[i],
                    after,
                    before[i] == after ? "no" : "yes",
                ]);
            }

            return rows;
        }
        finally
        {
            window.Close();
            app.ThemeMode = ThemeMode.None;
        }
    }

    /// <summary>
    /// Application.ThemeMode を順に切り替え、Application.Resources 直下にどの辞書が並ぶかを読む。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> MeasureAppDictionariesAsync()
    {
        Application app = Application.Current;
        var rows = new List<IReadOnlyList<string>>();

        foreach (ThemeMode mode in new[] { ThemeMode.None, ThemeMode.Light, ThemeMode.Dark, ThemeMode.None })
        {
            app.ThemeMode = mode;
            await app.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            string merged = string.Join(
                ", ",
                app.Resources.MergedDictionaries.Select(d =>
                    d.Source is null ? "(no Source)" : Path.GetFileName(d.Source.OriginalString)));

            rows.Add([mode.Value, merged.Length == 0 ? "(empty)" : merged]);
        }

        return rows;
    }

    /// <summary>
    /// ThemeMode.System が、実行環境の「アプリのモード」設定と同じ側のブラシを選ぶかを測る。
    /// OS の設定は読むだけで変更しない。そのため、OS 側を切り替えたときの追従は測っていない。
    /// </summary>
    private static async Task<IReadOnlyList<string>> MeasureSystemAsync()
    {
        Application app = Application.Current;
        object? lightTheme = Microsoft.Win32.Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            null);

        app.ThemeMode = ThemeMode.System;
        var window = new Window
        {
            Title = "System",
            Width = 320,
            Height = 200,
            ShowActivated = false,
            Content = SceneContext.LoadXaml<Border>(ContentXaml),
        };

        try
        {
            await Capture.ShowAndSettleAsync(window);
            string dictionary = app.Resources.MergedDictionaries
                .Where(IsFluentDictionary)
                .Select(d => Path.GetFileName(d.Source!.OriginalString))
                .FirstOrDefault() ?? "(none)";

            return [
                lightTheme?.ToString() ?? "(not set)",
                dictionary,
                BrushText(DynamicText(window).Foreground),
            ];
        }
        finally
        {
            window.Close();
            app.ThemeMode = ThemeMode.None;
        }
    }

    /// <summary>
    /// 記事の「切り分けの手順」に載せている診断メソッドと同じ処理。
    /// </summary>
    private static string DumpThemeState(DependencyObject? probe = null)
    {
        var text = new System.Text.StringBuilder();
        Application app = Application.Current;
        text.AppendLine($"Application.ThemeMode = {app.ThemeMode.Value}");
        AppendDictionaries(text, app.Resources, "App");

        foreach (Window window in app.Windows)
        {
            text.AppendLine($"[{window.Title}] Window.ThemeMode = {window.ThemeMode.Value}");
            AppendDictionaries(text, window.Resources, $"[{window.Title}]");
        }

        if (probe is not null)
        {
            // DynamicResource なら ResourceReferenceExpression、値を固定していれば SolidColorBrush。
            object local = probe.ReadLocalValue(TextBlock.ForegroundProperty);
            string kind = local == DependencyProperty.UnsetValue ? "(no local value)" : local.GetType().Name;
            text.AppendLine($"Foreground local value = {kind}");
        }

        return text.ToString();
    }

    private static void AppendDictionaries(System.Text.StringBuilder text, ResourceDictionary dictionary, string prefix)
    {
        foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
        {
            text.AppendLine($"{prefix} > {merged.Source?.OriginalString ?? "(no Source)"}");
            AppendDictionaries(text, merged, prefix + " >");
        }
    }

    /// <summary>
    /// 3 系統の原因をすべて含む状態で DumpThemeState を実行し、出力を行ごとに返す。
    /// Styles.xaml の中に Fluent.Light.xaml をネストし、SettingsWindow に Window.ThemeMode=Light を指定し、
    /// MainWindow の StaticResource のテキストを probe に渡す。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> MeasureDumpAsync()
    {
        Application app = Application.Current;
        ResourceDictionary styles = AddToApp(new ResourceDictionary { Source = new Uri(StylesUri) });

        // XAML の解析で例外が出ても、辞書の追加と ThemeMode を必ず元へ戻すため、生成も try の中で行う。
        Window? main = null;
        Window? settings = null;

        try
        {
            app.ThemeMode = ThemeMode.Light;

            main = new Window
            {
                Title = "MainWindow", Width = 320, Height = 200, ShowActivated = false,
                Content = SceneContext.LoadXaml<Border>(ContentXaml),
            };
            settings = new Window
            {
                Title = "SettingsWindow", Width = 320, Height = 200, ShowActivated = false,
                Content = SceneContext.LoadXaml<Border>(ContentXaml),
                ThemeMode = ThemeMode.Light,
            };

            await Capture.ShowAndSettleAsync(main);
            await Capture.ShowAndSettleAsync(settings);
            app.ThemeMode = ThemeMode.Dark;
            await Capture.SettleAsync(main);

            return DumpThemeState(StaticText(main))
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => (IReadOnlyList<string>)[line])
                .ToList();
        }
        finally
        {
            main?.Close();
            settings?.Close();
            app.Resources.MergedDictionaries.Remove(styles);
            app.ThemeMode = ThemeMode.None;
        }
    }

    /// <summary>
    /// 記事の「問題」の画面。アプリを Light で起動して 2 つのウィンドウを開き、
    /// Dark へ切り替えた直後の状態を撮る。
    /// ウィンドウ単位で Light を指定した側は明るいまま残り、
    /// 追従した側でも StaticResource のテキストだけが暗い背景に沈む。
    /// </summary>
    private static async Task ShootAsync(SceneContext context)
    {
        Application app = Application.Current;

        // XAML の解析で例外が出ても ThemeMode を元へ戻し、開いたウィンドウを閉じるため、生成も try の中で行う。
        Window? follows = null;
        Window? pinned = null;

        // follows は ShootAsync が撮影後に閉じる。ShootAsync に渡す前に失敗した場合だけ、ここで閉じる。
        bool followsHandedOver = false;

        try
        {
            app.ThemeMode = ThemeMode.Light;

            follows = new Window
            {
                Title = "MainWindow",
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 300,
                ResizeMode = ResizeMode.CanMinimize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = SceneContext.LoadXaml<Border>(ContentXaml),
            };
            pinned = new Window
            {
                Title = "SettingsWindow",
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 300,
                ResizeMode = ResizeMode.CanMinimize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = SceneContext.LoadXaml<Border>(ContentXaml),
                ThemeMode = ThemeMode.Light,
            };

            // 2 枚とも Light の状態で開いてから、アプリ全体を Dark へ切り替える。
            await Capture.ShowAndSettleAsync(pinned);
            followsHandedOver = true;
            await context.ShootAsync(
                follows,
                "switched-main-window.png",
                async _ =>
                {
                    app.ThemeMode = ThemeMode.Dark;
                    await Capture.SettleAsync(pinned);
                });

            await context.SaveShownWindowAsync(pinned, "switched-pinned-window.png");
        }
        finally
        {
            if (!followsHandedOver)
            {
                follows?.Close();
            }

            pinned?.Close();
            app.ThemeMode = ThemeMode.None;
        }
    }

    private static Brush? Foreground(Control? button, TextBlock? text) => button?.Foreground ?? text?.Foreground;

    private static TextBlock DynamicText(Window window) =>
        (TextBlock)((FrameworkElement)window.Content).FindName("DynamicText");

    private static TextBlock StaticText(Window window) =>
        (TextBlock)((FrameworkElement)window.Content).FindName("StaticText");

    private static string BrushText(Brush? brush) => brush switch
    {
        SolidColorBrush solid => solid.Color.ToString(),
        null => "null",
        _ => brush.GetType().Name,
    };
}
