using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenshotCapture.Scenes.ValueConverter;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF IValueConverter の書き方: bool→Visibility の反転と enum の表示名」の計測。
///
/// 記事と同じコンバーター・ビューモデル・XAML を表示し、ビューモデルの値を変えてから
/// バインド先の Visibility・Text、コンバーターが受け取った ConverterParameter の型、
/// 後ろの要素の位置、例外の届き先を読む。
/// </summary>
internal sealed class ValueConverterScene : IScene
{
    private const string LocalNamespace = "clr-namespace:ScreenshotCapture.Scenes.ValueConverter;assembly=ScreenshotCapture";
    private const string SystemNamespace = "clr-namespace:System;assembly=System.Runtime";

    public IReadOnlyList<string> Verifies =>
    [
        "組み込みの BooleanToVisibilityConverter と記事の BoolToVisibilityConverter（既定）が true を Visible、false を Collapsed にし、ソースが null と文字列の \"True\" のときも Collapsed になること",
        "BooleanToVisibilityConverter には公開の設定できるプロパティが無く、記事の BoolToVisibilityConverter には TrueValue と FalseValue があること",
        "BoolToVisibilityConverter の TrueValue・FalseValue を XAML で設定すると、反転（true→Collapsed、false→Visible）と false→Hidden になること",
        "ConverterParameter=True と書いた場合、parameter is true で判定する実装では反転せず、sys:Boolean のリソースを渡すと反転すること",
        "ConverterParameter に属性の文字列で書いた値（True・1・Hidden）は System.String で届き、x:Static と StaticResource で渡すと Visibility・Boolean で届くこと",
        "parameter を文字列の \"True\" と比べる実装は ConverterParameter=True で反転し、ConverterParameter=Ture と綴りを誤ると例外を出さずに反転しないこと。FalseValue=\"Hiden\" と誤ると XamlReader.Parse で XamlParseException になること",
        "Hidden では後ろの要素の位置が Visible と同じで、Collapsed では隠した要素の高さの分だけ上へ詰まること",
        "記事の EnumDescriptionConverter が、Description 属性の文字列、属性の無い値の名前、定義の無い値の数字、[Flags] の組み合わせの ToString() の結果、null の空文字列を返すこと",
        "GetField と GetCustomAttribute の結果を null と確かめない実装は、属性の無い値と null で NullReferenceException、定義の無い値で ArgumentNullException を投げ、ソースのセッターを呼んだ側へ例外が上がって Text が前の値のまま残ること",
        "ComboBox の ItemTemplate でコンバーターを使うと、項目と選択欄は表示名になり、選んだ項目の値は列挙型（SelectedItem の型は OrderStatus）のままビューモデルへ入ること",
        "Status を変えたとき、コンバーターのバインドと、StatusText の変更も通知するビューモデルのプロパティは新しい表示名になり、StatusText を通知しない実装では古い表示名のまま残ること",
    ];

    public string Slug => "wpf-ivalueconverter-visibility-enum-display-name";

    public async Task CaptureAsync(SceneContext context)
    {
        await ShootLayoutAsync(context);

        await context.SaveTableAsync(
            "Visibility of a Border bound to IsBusy",
            [T("binding", "バインド"), "IsBusy = True", "IsBusy = False", T("source null", "ソースが null"), T("source \"True\" (string)", "ソースが \"True\"（string）")],
            await VisibilityCasesAsync(),
            "bool-to-visibility.svg");

        await context.SaveTableAsync(
            "Public settable instance properties",
            [T("type", "型"), T("properties", "プロパティ")],
            PropertyCases(),
            "converter-properties.svg");

        await context.SaveTableAsync(
            "Type of the parameter argument received by Convert",
            [T("ConverterParameter in XAML", "XAML の ConverterParameter"), T("type", "型"), T("value", "値")],
            await ParameterCasesAsync(),
            "converter-parameter-type.svg");

        await context.SaveTableAsync(
            "A misspelled value in each way of inverting",
            [T("XAML", "XAML"), T("result", "結果")],
            await TypoCasesAsync(),
            "inverse-typo.svg");

        await context.SaveTableAsync(
            "Top of the TextBlock below a 40 px Border, by the Border's Visibility",
            [T("Border", "Border"), T("top of the next TextBlock", "次の TextBlock の上端")],
            await LayoutCasesAsync(),
            "visibility-layout.svg");

        await context.SaveTableAsync(
            "TextBlock.Text bound to an enum value through each converter",
            [T("source value", "ソースの値"), "EnumDescriptionConverter", T("without null checks", "null を確かめない実装")],
            await EnumCasesAsync(),
            "enum-description.svg");

        await context.SaveTableAsync(
            "ComboBox with the converter in its ItemTemplate",
            [T("what is read", "読んだもの"), T("value", "値")],
            await ComboBoxCasesAsync(),
            "enum-combobox.svg");

        await context.SaveTableAsync(
            "Text after Status changes from AwaitingPayment to Shipped",
            [T("display", "表示の作り方"), T("Text before", "変更前の Text"), T("Text after", "変更後の Text")],
            await DerivedPropertyCasesAsync(),
            "converter-vs-viewmodel.svg");
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // ---------------------------------------------------------------- 記事の XAML

    /// <summary>記事の Window.Resources と同じ宣言。</summary>
    private const string ResourcesXaml = """
        <BooleanToVisibilityConverter x:Key="BooleanToVisibility" />
        <local:BoolToVisibilityConverter x:Key="BoolToVisibility" />
        <local:BoolToVisibilityConverter x:Key="InverseBoolToVisibility" TrueValue="Collapsed" FalseValue="Visible" />
        <local:BoolToVisibilityConverter x:Key="BoolToHidden" FalseValue="Hidden" />
        <local:ParameterBoolToVisibilityConverter x:Key="ParameterBoolToVisibility" />
        <local:StringParameterBoolToVisibilityConverter x:Key="StringParameterBoolToVisibility" />
        <local:EnumDescriptionConverter x:Key="EnumDescription" />
        <local:UncheckedEnumDescriptionConverter x:Key="UncheckedEnumDescription" />
        <local:ParameterRecorderConverter x:Key="ParameterRecorder" />
        <sys:Boolean x:Key="True">True</sys:Boolean>
        """;

    /// <summary>リソースと、{Body} に置いた要素を持つ StackPanel を読み込む。</summary>
    private static StackPanel LoadPanel(string body) =>
        SceneContext.LoadXaml<StackPanel>(
            $"""
            <StackPanel Width="360" Margin="8">
              <StackPanel.Resources>
            {ResourcesXaml}
              </StackPanel.Resources>
            {body}
            </StackPanel>
            """,
            ("local", LocalNamespace),
            ("sys", SystemNamespace));

    private static T Find<T>(FrameworkElement root, string name) where T : class =>
        root.FindName(name) as T ?? throw new InvalidOperationException($"{name} が見つからない。");

    // ---------------------------------------------------------------- 図: Visible・Hidden・Collapsed

    /// <summary>記事の XAML の 1 段（上の文字、隠す Border、下の文字）。{Binding} は Visibility のバインド。</summary>
    private const string LayoutXaml = """
        <TextBlock Text="above" />
        <Border x:Name="target" Height="40" Background="#FFB4D2F0" Visibility="{Binding}">
          <TextBlock Text="details" VerticalAlignment="Center" Margin="6,0" />
        </Border>
        <TextBlock x:Name="next" Text="below" />
        """;

    private static readonly (string Caption, string Binding, bool IsBusy)[] LayoutCases =
    [
        ("BoolToVisibility, IsBusy = True", "{Binding IsBusy, Converter={StaticResource BoolToVisibility}}", true),
        ("BoolToHidden, IsBusy = False", "{Binding IsBusy, Converter={StaticResource BoolToHidden}}", false),
        ("BoolToVisibility, IsBusy = False", "{Binding IsBusy, Converter={StaticResource BoolToVisibility}}", false),
    ];

    private static StackPanel BuildLayoutPanel(string binding, bool isBusy)
    {
        StackPanel panel = LoadPanel(LayoutXaml.Replace("{Binding}", binding, StringComparison.Ordinal));
        panel.Width = 170;
        panel.Margin = new Thickness(0);
        panel.DataContext = new OrderViewModel { IsBusy = isBusy };
        return panel;
    }

    private static async Task ShootLayoutAsync(SceneContext context)
    {
        Window window = DemoLayout.BuildPanelWindow(
            "IValueConverter: Visibility",
            LayoutCases.Select(c => new DemoLayout.Panel(c.Caption, BuildLayoutPanel(c.Binding, c.IsBusy))));
        await context.ShootAsync(window, "visibility-hidden-collapsed.png");
    }

    private static async Task<List<IReadOnlyList<Loc>>> LayoutCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string _, string binding, bool isBusy) in LayoutCases)
        {
            StackPanel panel = BuildLayoutPanel(binding, isBusy);
            await ShowAsync(panel, () =>
            {
                var target = Find<Border>(panel, "target");
                var next = Find<TextBlock>(panel, "next");
                rows.Add([$"Visibility = {target.Visibility}", $"{D(Bounds(next, panel).Top)} px"]);
                return Task.CompletedTask;
            });
        }

        return rows;
    }

    // ---------------------------------------------------------------- 表: bool → Visibility

    private static readonly string[] VisibilityBindings =
    [
        "Converter={StaticResource BooleanToVisibility}",
        "Converter={StaticResource BoolToVisibility}",
        "Converter={StaticResource InverseBoolToVisibility}",
        "Converter={StaticResource BoolToHidden}",
        "Converter={StaticResource ParameterBoolToVisibility}, ConverterParameter=True",
        "Converter={StaticResource ParameterBoolToVisibility}, ConverterParameter={StaticResource True}",
        "Converter={StaticResource StringParameterBoolToVisibility}, ConverterParameter=True",
    ];

    private static async Task<List<IReadOnlyList<Loc>>> VisibilityCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach (string binding in VisibilityBindings)
        {
            (string onTrue, string onFalse) = await ReadIsBusyVisibilityAsync(binding);
            string onNull = await ReadValueVisibilityAsync(binding, null);
            string onString = await ReadValueVisibilityAsync(binding, "True");
            rows.Add([binding, onTrue, onFalse, onNull, onString]);
        }

        return rows;
    }

    /// <summary>IsBusy にバインドした Border の Visibility を、true で表示した後と false に変えた後に読む。</summary>
    private static async Task<(string OnTrue, string OnFalse)> ReadIsBusyVisibilityAsync(string binding)
    {
        StackPanel panel = LoadPanel($$"""<Border x:Name="target" Height="20" Background="Gray" Visibility="{Binding IsBusy, {{binding}}}" />""");
        var viewModel = new OrderViewModel { IsBusy = true };
        panel.DataContext = viewModel;
        string onTrue = "", onFalse = "";
        await ShowAsync(panel, async () =>
        {
            var target = Find<Border>(panel, "target");
            onTrue = target.Visibility.ToString();
            viewModel.IsBusy = false;
            await Capture.SettleAsync(Window.GetWindow(panel)!, 0);
            onFalse = target.Visibility.ToString();
        });
        return (onTrue, onFalse);
    }

    /// <summary>object 型のソース（null や文字列を入れる）にバインドした Border の Visibility を読む。</summary>
    private static async Task<string> ReadValueVisibilityAsync(string binding, object? value)
    {
        StackPanel panel = LoadPanel($$"""<Border x:Name="target" Height="20" Background="Gray" Visibility="{Binding Value, {{binding}}}" />""");
        panel.DataContext = new ValueHolder { Value = value };
        string result = "";
        await ShowAsync(panel, () =>
        {
            result = Find<Border>(panel, "target").Visibility.ToString();
            return Task.CompletedTask;
        });
        return result;
    }

    // ---------------------------------------------------------------- 表: 設定できるプロパティ

    private static List<IReadOnlyList<Loc>> PropertyCases()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach (Type type in new[] { typeof(BooleanToVisibilityConverter), typeof(BoolToVisibilityConverter) })
        {
            string[] names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetSetMethod() is not null)
                .Select(p => $"{p.Name} ({p.PropertyType.Name})")
                .ToArray();
            rows.Add([type.Name, names.Length == 0 ? T("(none)", "（なし）") : string.Join(", ", names)]);
        }

        return rows;
    }

    // ---------------------------------------------------------------- 表: 綴りの誤り

    private static async Task<List<IReadOnlyList<Loc>>> TypoCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        foreach (string binding in new[]
        {
            "Converter={StaticResource StringParameterBoolToVisibility}, ConverterParameter=True",
            "Converter={StaticResource StringParameterBoolToVisibility}, ConverterParameter=Ture",
        })
        {
            (string onTrue, string onFalse) = await ReadIsBusyVisibilityAsync(binding);
            rows.Add([binding, Loc.Of($"no exception; True → {onTrue}, False → {onFalse}", $"例外なし。True → {onTrue}、False → {onFalse}")]);
        }

        const string Resource = """<local:BoolToVisibilityConverter x:Key="BoolToHidden" FalseValue="Hiden" />""";
        string thrown = Throws(() => LoadPanel($"""<Border><Border.Resources>{Resource}</Border.Resources></Border>"""));
        rows.Add([Resource, Loc.Of($"{thrown} from XamlReader.Parse", $"XamlReader.Parse で {thrown}")]);

        return rows;
    }

    // ---------------------------------------------------------------- 表: ConverterParameter の型

    private static readonly string[] ParameterForms =
    [
        "ConverterParameter=True",
        "ConverterParameter=1",
        "ConverterParameter=Hidden",
        "ConverterParameter={x:Static Visibility.Hidden}",
        "ConverterParameter={StaticResource True}",
    ];

    private static async Task<List<IReadOnlyList<Loc>>> ParameterCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach (string form in ParameterForms)
        {
            StackPanel panel = LoadPanel($$"""<TextBlock x:Name="target" Text="{Binding IsBusy, Converter={StaticResource ParameterRecorder}, {{form}}}" />""");
            panel.DataContext = new OrderViewModel { IsBusy = true };
            string text = "";
            await ShowAsync(panel, () =>
            {
                text = Find<TextBlock>(panel, "target").Text;
                return Task.CompletedTask;
            });

            string[] parts = text.Split('|');
            if (parts.Length != 2)
            {
                throw new InvalidOperationException($"コンバーターの出力を読めない（{form}: \"{text}\"）。");
            }

            rows.Add([form, parts[0], parts[1]]);
        }

        return rows;
    }

    // ---------------------------------------------------------------- 表: enum → 表示名

    private static readonly (string Label, object? Value)[] EnumValues =
    [
        ("OrderStatus.InProgress", OrderStatus.InProgress),
        ("OrderStatus.Shipped", OrderStatus.Shipped),
        ("(OrderStatus)99", (OrderStatus)99),
        ("FileAttributes.ReadOnly | FileAttributes.Hidden", FileAttributes.ReadOnly | FileAttributes.Hidden),
        ("null", null),
    ];

    private static async Task<List<IReadOnlyList<Loc>>> EnumCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string label, object? value) in EnumValues)
        {
            rows.Add([label, await DescribeEnumAsync("EnumDescription", value), await DescribeEnumAsync("UncheckedEnumDescription", value)]);
        }

        return rows;
    }

    /// <summary>
    /// 表示した後にソースの値を変え、TextBlock.Text と例外の届き先を返す。
    /// 表示は例外の起きない InProgress で始め、変えるのはソースのセッターを呼んだ時点にする。
    /// </summary>
    private static async Task<Loc> DescribeEnumAsync(string converterKey, object? value)
    {
        StackPanel panel = LoadPanel($$$"""<TextBlock x:Name="target" Text="{Binding Value, Converter={StaticResource {{{converterKey}}}}}" />""");
        var probe = new ValueHolder { Value = OrderStatus.InProgress };
        panel.DataContext = probe;
        Loc result = "";
        await ShowAsync(panel, async () =>
        {
            string? unhandled = null;
            void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                unhandled = e.Exception.GetType().Name;
                e.Handled = true;
            }

            Dispatcher dispatcher = panel.Dispatcher;
            dispatcher.UnhandledException += OnUnhandled;
            try
            {
                string? thrown = null;
                try
                {
                    probe.Value = value;
                }
                catch (Exception ex)
                {
                    thrown = ex.GetType().Name;
                }

                await Capture.SettleAsync(Window.GetWindow(panel)!, 0);
                string text = Find<TextBlock>(panel, "target").Text;
                if (thrown is not null)
                {
                    result = Loc.Of($"{thrown} thrown to the setter's caller (Text stays \"{text}\")", $"{thrown} がセッターの呼び出し元へ上がる（Text は \"{text}\" のまま）");
                }
                else if (unhandled is not null)
                {
                    result = Loc.Of($"{unhandled} unhandled on the dispatcher", $"{unhandled} がディスパッチャーで未処理");
                }
                else
                {
                    result = $"\"{text}\"";
                }
            }
            finally
            {
                dispatcher.UnhandledException -= OnUnhandled;
            }
        });

        return result;
    }

    // ---------------------------------------------------------------- 表: ComboBox

    /// <summary>記事の ComboBox の XAML。</summary>
    private const string ComboBoxXaml = """
        <ComboBox x:Name="combo" ItemsSource="{Binding Statuses}" SelectedItem="{Binding Status}">
          <ComboBox.ItemTemplate>
            <DataTemplate>
              <TextBlock Text="{Binding Converter={StaticResource EnumDescription}}" />
            </DataTemplate>
          </ComboBox.ItemTemplate>
        </ComboBox>
        """;

    private static async Task<List<IReadOnlyList<Loc>>> ComboBoxCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        StackPanel panel = LoadPanel(ComboBoxXaml);
        var viewModel = new OrderViewModel { Status = OrderStatus.AwaitingPayment };
        panel.DataContext = viewModel;
        await ShowAsync(panel, async () =>
        {
            var combo = Find<ComboBox>(panel, "combo");
            Window window = Window.GetWindow(panel)!;
            rows.Add([T("selection box text (Status = AwaitingPayment)", "選択欄の文字（Status = AwaitingPayment）"), SelectionBoxText(combo)]);

            combo.IsDropDownOpen = true;
            await Capture.SettleAsync(window, 0);
            var items = Enumerable.Range(0, combo.Items.Count)
                .Select(i => combo.ItemContainerGenerator.ContainerFromIndex(i) as ComboBoxItem
                    ?? throw new InvalidOperationException("項目のコンテナーが作られていない。"))
                .Select(item => string.Join("", Descendants(item).OfType<TextBlock>().Select(t => t.Text)))
                .Select(text => $"\"{text}\"");
            rows.Add([T("item texts in the drop-down", "ドロップダウンの項目の文字"), string.Join(", ", items)]);

            // 選ぶ操作の代わりに SelectedIndex を変える（入力の準備）。読むのはビューモデルの値である。
            combo.SelectedIndex = 1;
            combo.IsDropDownOpen = false;
            await Capture.SettleAsync(window, 0);
            rows.Add([T("Status after selecting the second item", "2 番目の項目を選んだ後の Status"), $"{viewModel.Status}"]);
            rows.Add([T("type of SelectedItem", "SelectedItem の型"), combo.SelectedItem?.GetType().Name ?? "null"]);
            rows.Add([T("selection box text after that", "その後の選択欄の文字"), SelectionBoxText(combo)]);
        });

        return rows;
    }

    private static string SelectionBoxText(ComboBox combo)
    {
        var presenters = Descendants(combo).OfType<ContentPresenter>().Where(p => p.Name == "contentPresenter").ToList();
        if (presenters.Count == 0)
        {
            throw new InvalidOperationException("ComboBox の選択欄（contentPresenter）が見つからない。");
        }

        return $"\"{string.Join("", presenters.SelectMany(p => Descendants(p).OfType<TextBlock>()).Select(t => t.Text))}\"";
    }

    // ---------------------------------------------------------------- 表: コンバーターとビューモデルのプロパティ

    private static async Task<List<IReadOnlyList<Loc>>> DerivedPropertyCasesAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        var notifying = new OrderViewModel { Status = OrderStatus.AwaitingPayment };
        rows.Add(await ChangeStatusAsync(
            T("converter: Text=\"{Binding Status, Converter={StaticResource EnumDescription}}\"", "コンバーター: Text=\"{Binding Status, Converter={StaticResource EnumDescription}}\""),
            "{Binding Status, Converter={StaticResource EnumDescription}}",
            notifying,
            () => notifying.Status = OrderStatus.Shipped));

        var notifying2 = new OrderViewModel { Status = OrderStatus.AwaitingPayment };
        rows.Add(await ChangeStatusAsync(
            T("view model: StatusText, also notified in the Status setter", "ビューモデル: StatusText（Status のセッターで StatusText も通知する）"),
            "{Binding StatusText}",
            notifying2,
            () => notifying2.Status = OrderStatus.Shipped));

        var silent = new SilentOrderViewModel { Status = OrderStatus.AwaitingPayment };
        rows.Add(await ChangeStatusAsync(
            T("view model: StatusText, not notified", "ビューモデル: StatusText（StatusText を通知しない）"),
            "{Binding StatusText}",
            silent,
            () => silent.Status = OrderStatus.Shipped));

        return rows;
    }

    private static async Task<IReadOnlyList<Loc>> ChangeStatusAsync(Loc label, string binding, object viewModel, Action change)
    {
        StackPanel panel = LoadPanel($"""<TextBlock x:Name="target" Text="{binding}" />""");
        panel.DataContext = viewModel;
        string before = "", after = "";
        await ShowAsync(panel, async () =>
        {
            var target = Find<TextBlock>(panel, "target");
            before = target.Text;
            change();
            await Capture.SettleAsync(Window.GetWindow(panel)!, 0);
            after = target.Text;
        });

        return [label, $"\"{before}\"", $"\"{after}\""];
    }
}
