using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で RadioButton を enum にバインドすると選択が反映されない問題」の図。
/// 2 つの列挙体のグループを同じ親に並べたとき、GroupName の有無で初期選択の表示が
/// どう変わるかを示す。ViewModel の値は両方とも同じで、違うのは GroupName だけである。
///
/// 本文の「実測では」は、記事の XAML をそのまま XamlReader で読み込む一時プロジェクトを
/// .NET Framework 4.8 と .NET 10 の両方でビルドして測る。記事は両方で同じ結果になると書いているためである。
/// </summary>
internal sealed class RadioButtonEnumBindingScene : IScene
{
    private const string LocalNamespace = "clr-namespace:ScreenshotCapture.Scenes;assembly=ScreenshotCapture";

    private static readonly string[] Targets = ["net48", "net10.0-windows"];

    public IReadOnlyList<string> Verifies =>
    [
        "RadioButton.GroupName の既定値",
        "記事の XAML（5 個）で GroupName を設定しない場合、別プロパティにバインドしていても 1 グループとして相互排他になり、解除が ConvertBack を false で呼び出すこと",
        "GroupName を設定すると解除が起きず、選択の切り替えでは ConvertBack が true でだけ呼ばれること",
        "同じ GroupName は親をまたいでグループになること",
        "ConverterParameter を文字列で書くと、ソースは更新されるが画面はどれも未選択になること",
        "別の ViewModel の同名プロパティや、バインドしていないラジオボタンが混ざっても ConvertBack(false) が起きること",
        "ConvertBack が NotImplementedException を投げると、例外が捕捉されずに上がること",
        "ConvertBack が UnsetValue を返すと、ソースは更新されず FallbackValue も適用されず、検証エラーが残ること",
        "解除時に parameter を返す実装や、bool のラッパープロパティでは、グループがまとまったままでも両方が選択表示されること",
        "Grid の別セル・GroupBox の Header と Content・ItemsControl の Items に置いたラジオボタンが 1 グループになること",
        "以上が .NET Framework 4.8 と .NET 10 で同じであること",
    ];

    public string Slug => "wpf-radiobutton-enum-binding";

    public async Task CaptureAsync(SceneContext context)
    {
        Window window = DemoLayout.BuildPanelWindow(
            "RadioButton + enum",
            [
                new DemoLayout.Panel("GroupName = \"\"", BuildGroup(withGroupName: false)),
                new DemoLayout.Panel("GroupName = \"quality\" / \"pageLayout\"", BuildGroup(withGroupName: true)),
            ]);

        await context.ShootAsync(window, "radiobutton-enum-groupname.png");

        var results = new List<List<(string Label, string Value)>>();
        foreach (string target in Targets)
        {
            results.Add(await RunProbeAsync(target));
        }

        List<(string Label, string Value)> first = results[0];
        var rows = new List<IReadOnlyList<string>>();
        for (int i = 0; i < first.Count; i++)
        {
            string label = first[i].Label;
            if (results.Any(r => r.Count != first.Count || r[i].Label != label))
            {
                throw new InvalidOperationException($"ランタイムごとの出力の行がそろわない（{label}）。");
            }

            // 両方のランタイムで同じ値なら 1 つだけ出す。違う行だけ両方を並べる。
            string[] values = results.Select(r => r[i].Value).ToArray();
            rows.Add([label, values.Distinct().Count() == 1 ? values[0] : string.Join(" / ", Targets.Zip(values, (t, v) => $"{t}: {v}"))]);
        }

        await context.SaveTableAsync(
            $"the article's XAML, measured on {string.Join(" and ", Targets)} (one value = same on both)",
            ["case", "measured"],
            rows,
            "radiobutton-grouping.svg");
    }

    /// <summary>
    /// 記事に載せた XAML と同じ構成でパネルを組む。GroupName 属性の有無だけが 2 つの差である。
    /// </summary>
    private static UIElement BuildGroup(bool withGroupName)
    {
        // XAML と補間文字列はどちらも波かっこを使うため、GroupName の差し替えは
        // 補間ではなくプレースホルダーの置換で行う。
        const string Template =
            """
            <StackPanel>
              <StackPanel.Resources>
                <local:EnumToBooleanConverter x:Key="EnumToBoolean" />
              </StackPanel.Resources>
              <RadioButton Content="Draft" QUALITY_GROUP IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Draft}}" />
              <RadioButton Content="Standard" QUALITY_GROUP IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Standard}}" />
              <RadioButton Content="Fine" QUALITY_GROUP IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Fine}}" />
              <RadioButton Content="Single" LAYOUT_GROUP IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Single}}" />
              <RadioButton Content="Dual" LAYOUT_GROUP IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Dual}}" />
            </StackPanel>
            """;

        string xaml = Template
            .Replace("QUALITY_GROUP", withGroupName ? """GroupName="quality" """ : string.Empty)
            .Replace("LAYOUT_GROUP", withGroupName ? """GroupName="pageLayout" """ : string.Empty);

        var panel = SceneContext.LoadXaml<StackPanel>(xaml, ("local", LocalNamespace));
        var viewModel = new PrintSettingsViewModel();
        panel.DataContext = viewModel;

        // ViewModel が保持している値を、ラジオボタンの表示と並べて見えるようにする。
        panel.Children.Add(new TextBlock
        {
            Text = $"Quality = {viewModel.Quality}\nPageLayout = {viewModel.PageLayout}",
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x3D, 0x4D)),
            Margin = new Thickness(0, 10, 0, 0),
        });

        return panel;
    }

    /// <summary>
    /// 一時プロジェクトを指定の TFM でビルドして実行し、「row \t 条件 \t 値」の行を順に返す。
    /// </summary>
    private static async Task<List<(string Label, string Value)>> RunProbeAsync(string targetFramework)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "radiobutton-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            // BOM を付ける。付けないと、.NET Framework 向けのビルドで日本語コメントが既定のコードページで読まれる。
            var utf8 = new UTF8Encoding(true);
            await File.WriteAllTextAsync(Path.Combine(workspace, "Probe.cs"), ProbeSource, utf8);
            await File.WriteAllTextAsync(
                Path.Combine(workspace, "probe.csproj"),
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{targetFramework}</TargetFramework>
                    <UseWPF>true</UseWPF>
                    <LangVersion>latest</LangVersion>
                    <!-- net48 でも同じソースを通すため、暗黙の using と null 許容注釈は使わない。 -->
                    <ImplicitUsings>disable</ImplicitUsings>
                    <Nullable>disable</Nullable>
                  </PropertyGroup>
                </Project>
                """,
                utf8);

            (int exitCode, string output) = await RunAsync("dotnet", "run -c Release -v q --nologo", workspace);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"{targetFramework} のビルドまたは実行が失敗した。{Environment.NewLine}{output}");
            }

            var rows = new List<(string Label, string Value)>();
            foreach (string raw in output.Split('\n'))
            {
                string[] parts = raw.TrimEnd('\r').Split('\t');
                if (parts.Length == 3 && parts[0] == "row")
                {
                    rows.Add((parts[1], parts[2]));
                }
            }

            if (rows.Count == 0)
            {
                throw new InvalidOperationException($"{targetFramework} の出力に計測結果が無い。{Environment.NewLine}{output}");
            }

            return rows;
        }
        finally
        {
            try { Directory.Delete(workspace, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{fileName} を起動できない。");

        // 両方の読み取りを先に始める。片方だけ読むと、もう片方のパイプが埋まって子プロセスが止まる。
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        // 一時プロジェクトが終わらない場合に呼び出し元まで止まらないよう、時間を区切る。
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} {arguments} が 10 分以内に終わらなかったため止めた。");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        return (process.ExitCode, stdout + stderr);
    }

    /// <summary>
    /// 一時プロジェクトで実行するコード。記事の XAML・ViewModel・コンバーターをそのまま使う。
    /// 選択の操作は、クリックと同じ経路を通るよう UI オートメーションの Select で行う。
    /// </summary>
    private const string ProbeSource = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel;
        using System.Globalization;
        using System.Linq;
        using System.Runtime.InteropServices;
        using System.Threading;
        using System.Windows;
        using System.Windows.Automation.Peers;
        using System.Windows.Automation.Provider;
        using System.Windows.Controls;
        using System.Windows.Data;
        using System.Windows.Markup;
        using System.Windows.Threading;

        namespace RadioProbe
        {
            public enum Quality { Draft, Standard, Fine }

            public enum PageLayout { Single, Dual }

            /// <summary>記事の ViewModel と同じ。</summary>
            public sealed class PrintSettingsViewModel : INotifyPropertyChanged
            {
                private Quality _quality = Quality.Standard;
                private PageLayout _pageLayout = PageLayout.Single;

                public Quality Quality
                {
                    get => _quality;
                    set
                    {
                        if (_quality == value) return;
                        _quality = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quality)));
                    }
                }

                public PageLayout PageLayout
                {
                    get => _pageLayout;
                    set
                    {
                        if (_pageLayout == value) return;
                        _pageLayout = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageLayout)));
                    }
                }

                public event PropertyChangedEventHandler PropertyChanged;
            }

            /// <summary>列挙体の値ごとに bool のプロパティを持つラッパー方式。false の代入は無視する。</summary>
            public sealed class WrapperViewModel : INotifyPropertyChanged
            {
                public static int FalseSets;
                private Quality _quality = Quality.Standard;
                private PageLayout _pageLayout = PageLayout.Single;

                public bool IsDraft { get => _quality == Quality.Draft; set => SetQuality(value, Quality.Draft); }
                public bool IsStandard { get => _quality == Quality.Standard; set => SetQuality(value, Quality.Standard); }
                public bool IsFine { get => _quality == Quality.Fine; set => SetQuality(value, Quality.Fine); }
                public bool IsSingle { get => _pageLayout == PageLayout.Single; set => SetLayout(value, PageLayout.Single); }
                public bool IsDual { get => _pageLayout == PageLayout.Dual; set => SetLayout(value, PageLayout.Dual); }

                private void SetQuality(bool value, Quality quality)
                {
                    if (!value) { FalseSets++; return; }
                    _quality = quality;
                    Raise(nameof(IsDraft)); Raise(nameof(IsStandard)); Raise(nameof(IsFine));
                }

                private void SetLayout(bool value, PageLayout layout)
                {
                    if (!value) { FalseSets++; return; }
                    _pageLayout = layout;
                    Raise(nameof(IsSingle)); Raise(nameof(IsDual));
                }

                private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

                public event PropertyChangedEventHandler PropertyChanged;
            }

            /// <summary>ConvertBack の呼び出しを数える。</summary>
            public static class Calls
            {
                public static int True;
                public static int False;
                public static List<string> FalseParameters = new List<string>();

                public static void Reset() { True = 0; False = 0; FalseParameters.Clear(); }

                public static void Record(object value, object parameter)
                {
                    if (value is true) { True++; } else { False++; FalseParameters.Add(parameter?.ToString() ?? "null"); }
                }
            }

            /// <summary>記事のコンバーター。解除時は Binding.DoNothing を返す。</summary>
            public sealed class EnumToBooleanConverter : IValueConverter
            {
                public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                    => value?.Equals(parameter) == true;

                public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                {
                    Calls.Record(value, parameter);
                    return value is true ? parameter : Binding.DoNothing;
                }
            }

            /// <summary>ConvertBack を実装していないコンバーター。</summary>
            public sealed class NotImplementedConverter : IValueConverter
            {
                public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                    => value?.Equals(parameter) == true;

                public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                {
                    Calls.Record(value, parameter);
                    throw new NotImplementedException();
                }
            }

            /// <summary>解除時に DependencyProperty.UnsetValue を返すコンバーター。</summary>
            public sealed class UnsetValueConverter : IValueConverter
            {
                public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                    => value?.Equals(parameter) == true;

                public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                {
                    Calls.Record(value, parameter);
                    return value is true ? parameter : DependencyProperty.UnsetValue;
                }
            }

            /// <summary>解除時も parameter を返すコンバーター。</summary>
            public sealed class AlwaysParameterConverter : IValueConverter
            {
                public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                    => value?.Equals(parameter) == true;

                public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                {
                    Calls.Record(value, parameter);
                    return parameter;
                }
            }

            public static class Program
            {
                private const string Namespaces =
                    "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                    "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
                    "xmlns:local='clr-namespace:RadioProbe;assembly=probe'";

                /// <summary>「問題」と「実装例」の XAML。GroupName の有無だけが違う。</summary>
                private static string ArticleXaml(string converter, bool groupName, string draftParameter = "{x:Static local:Quality.Draft}", string standardParameter = "{x:Static local:Quality.Standard}", string fineParameter = "{x:Static local:Quality.Fine}", string extra = "")
                {
                    string q = groupName ? " GroupName='quality'" : "";
                    string l = groupName ? " GroupName='pageLayout'" : "";
                    return
                        "<StackPanel " + Namespaces + ">" +
                        "<StackPanel.Resources><local:" + converter + " x:Key='EnumToBoolean' /></StackPanel.Resources>" +
                        "<RadioButton Content='Draft'" + q + " IsChecked='{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter=" + draftParameter + extra + "}' />" +
                        "<RadioButton Content='Standard'" + q + " IsChecked='{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter=" + standardParameter + extra + "}' />" +
                        "<RadioButton Content='Fine'" + q + " IsChecked='{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter=" + fineParameter + extra + "}' />" +
                        "<RadioButton Content='Single'" + l + " IsChecked='{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Single}}' />" +
                        "<RadioButton Content='Dual'" + l + " IsChecked='{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Dual}}' />" +
                        "</StackPanel>";
                }

                private static readonly List<string> Unhandled = new List<string>();

                [STAThread]
                public static void Main()
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                    Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
                    {
                        Unhandled.Add(e.Exception.GetType().Name);
                        e.Handled = true;
                    };

                    Row("runtime", RuntimeInformation.FrameworkDescription);
                    Row("GroupName default", new RadioButton().GroupName.Length == 0 ? "(empty)" : new RadioButton().GroupName);

                    // 「問題」の XAML と「実装例」の XAML。
                    foreach (bool groupName in new[] { false, true })
                    {
                        string name = groupName ? "GroupName set" : "no GroupName";
                        var vm = new PrintSettingsViewModel();
                        Calls.Reset();
                        StackPanel panel = Load(ArticleXaml("EnumToBooleanConverter", groupName), vm);
                        Window window = Show(panel);
                        Row(name + ": checked", Checked(panel));
                        Row(name + ": ConvertBack(false)", FalseCalls());
                        Row(name + ": ViewModel", vm.Quality + " / " + vm.PageLayout);

                        if (groupName)
                        {
                            Calls.Reset();
                            Select(Radio(panel, "Fine"));
                            Row(name + ": select Fine", "ConvertBack(true) " + Calls.True + ", (false) " + Calls.False + "; checked " + Checked(panel) + "; ViewModel " + vm.Quality + " / " + vm.PageLayout);
                            Select(Radio(panel, "Draft"));
                            Row(name + ": select Draft", "checked " + Checked(panel) + "; ViewModel " + vm.Quality + " / " + vm.PageLayout);
                        }

                        window.Close();
                    }

                    // 「問題」の 5 つを、GroupName を書かずに列挙体ごとの StackPanel へ分ける。
                    {
                        // Draft の前と Single の前で内側の StackPanel を開き、最後に内側と外側の両方を閉じる。
                        string split = ArticleXaml("EnumToBooleanConverter", false)
                            .Replace("<RadioButton Content='Draft'", "<StackPanel><RadioButton Content='Draft'")
                            .Replace("<RadioButton Content='Single'", "</StackPanel><StackPanel><RadioButton Content='Single'");
                        split = split.Substring(0, split.LastIndexOf("</StackPanel>")) + "</StackPanel></StackPanel>";
                        Calls.Reset();
                        StackPanel panel = Load(split, new PrintSettingsViewModel());
                        Window window = Show(panel);
                        Row("no GroupName, one StackPanel per enum", "checked " + Checked(panel) + "; ConvertBack(false) " + Calls.False);
                        window.Close();
                    }

                    // 同じ GroupName を、別々の親（Border）の 2 組に与える。
                    {
                        string group =
                            "<StackPanel " + Namespaces + ">" +
                            "<Border><StackPanel><RadioButton Content='A:Draft' GroupName='quality' /><RadioButton Content='A:Standard' GroupName='quality' IsChecked='True' /></StackPanel></Border>" +
                            "<Border><StackPanel><RadioButton Content='B:Draft' GroupName='quality' /><RadioButton Content='B:Standard' GroupName='quality' IsChecked='True' /></StackPanel></Border>" +
                            "</StackPanel>";
                        StackPanel panel = (StackPanel)XamlReader.Parse(group);
                        Window window = Show(panel);
                        Row("GroupName='quality' in two Borders", "checked " + Checked(panel));
                        window.Close();
                    }

                    // ConverterParameter を文字列で書く。
                    {
                        var vm = new PrintSettingsViewModel();
                        StackPanel panel = Load(ArticleXaml("EnumToBooleanConverter", true, "Draft", "Standard", "Fine"), vm);
                        Window window = Show(panel);
                        Row("ConverterParameter=Draft (string): checked", Checked(panel));
                        Select(Radio(panel, "Fine"));
                        Row("ConverterParameter=Draft (string): select Fine", "checked " + Checked(panel) + "; ViewModel " + vm.Quality + " / " + vm.PageLayout);
                        window.Close();
                    }

                    // 別の ViewModel の同じ名前のプロパティにバインドし、同じ GroupName を与える（一覧の各行を模す）。
                    {
                        Calls.Reset();
                        var panel = new StackPanel();
                        foreach (int i in new[] { 1, 2 })
                        {
                            var row = new StackPanel { DataContext = new PrintSettingsViewModel() };
                            row.Children.Add(Bound("row" + i + ":Standard", "q", nameof(PrintSettingsViewModel.Quality), Quality.Standard));
                            panel.Children.Add(row);
                        }

                        Window window = Show(panel);
                        Row("same GroupName, two ViewModels: ConvertBack(false)", FalseCalls());
                        window.Close();
                    }

                    // バインドしていないラジオボタンを 1 つ混ぜる。
                    {
                        Calls.Reset();
                        var panel = new StackPanel { DataContext = new PrintSettingsViewModel() };
                        panel.Children.Add(Bound("Standard", "q", nameof(PrintSettingsViewModel.Quality), Quality.Standard));
                        panel.Children.Add(new RadioButton { Content = "Other", GroupName = "q", IsChecked = true });
                        Window window = Show(panel);
                        Row("unbound RadioButton in the group: ConvertBack(false)", FalseCalls());
                        window.Close();
                    }

                    // ConvertBack が NotImplementedException を投げる。
                    {
                        Calls.Reset();
                        Unhandled.Clear();
                        string caught = "";
                        var vm = new PrintSettingsViewModel();
                        Window window = null;
                        try
                        {
                            window = Show(Load(ArticleXaml("NotImplementedConverter", false), vm));
                        }
                        catch (Exception ex)
                        {
                            caught = ex.GetType().Name;
                        }

                        string escaped = caught.Length > 0 ? caught + " thrown to the caller" : Unhandled.Count > 0 ? string.Join(",", Unhandled.Distinct()) + " unhandled on the dispatcher" : "not raised";
                        Row("ConvertBack throws NotImplementedException", "ConvertBack(false) " + Calls.False + "; " + escaped);
                        window?.Close();
                    }

                    // ConvertBack が UnsetValue を返す。FallbackValue も付ける。
                    {
                        Calls.Reset();
                        var vm = new PrintSettingsViewModel();
                        StackPanel panel = Load(ArticleXaml("UnsetValueConverter", false, extra: ", FallbackValue=True"), vm);
                        Window window = Show(panel);
                        RadioButton standard = Radio(panel, "Standard");
                        string error = Validation.GetHasError(standard) ? "error \"" + Validation.GetErrors(standard)[0].ErrorContent + "\"" : "no error";
                        Row("ConvertBack returns UnsetValue (FallbackValue=True)", "Standard " + (standard.IsChecked == true ? "checked" : "unchecked") + "; " + error + "; source " + vm.Quality + " / " + vm.PageLayout);
                        window.Close();
                    }

                    // 解除時も parameter を返す（GroupName なし）。
                    {
                        Calls.Reset();
                        var vm = new PrintSettingsViewModel();
                        StackPanel panel = Load(ArticleXaml("AlwaysParameterConverter", false), vm);
                        Window window = Show(panel);
                        Row("ConvertBack returns parameter for false (no GroupName)", "checked " + Checked(panel) + "; ConvertBack(false) " + Calls.False);
                        window.Close();
                    }

                    // bool のラッパープロパティ（GroupName なし）。
                    {
                        WrapperViewModel.FalseSets = 0;
                        string wrapper =
                            "<StackPanel " + Namespaces + ">" +
                            "<RadioButton Content='Draft' IsChecked='{Binding IsDraft}' />" +
                            "<RadioButton Content='Standard' IsChecked='{Binding IsStandard}' />" +
                            "<RadioButton Content='Fine' IsChecked='{Binding IsFine}' />" +
                            "<RadioButton Content='Single' IsChecked='{Binding IsSingle}' />" +
                            "<RadioButton Content='Dual' IsChecked='{Binding IsDual}' />" +
                            "</StackPanel>";
                        StackPanel panel = Load(wrapper, new WrapperViewModel());
                        Window window = Show(panel);
                        Row("bool wrapper properties (no GroupName)", "checked " + Checked(panel) + "; setter(false) " + WrapperViewModel.FalseSets);
                        window.Close();
                    }

                    // 暗黙のグループ化の単位。どれも 2 つに IsChecked='True' を与え、残る数を見る。
                    Implicit("Grid, different cells",
                        "<Grid " + Namespaces + "><Grid.ColumnDefinitions><ColumnDefinition /><ColumnDefinition /></Grid.ColumnDefinitions>" +
                        "<RadioButton Content='A' IsChecked='True' /><RadioButton Grid.Column='1' Content='B' IsChecked='True' /></Grid>");
                    Implicit("GroupBox Header and Content",
                        "<GroupBox " + Namespaces + "><GroupBox.Header><RadioButton Content='A' IsChecked='True' /></GroupBox.Header><RadioButton Content='B' IsChecked='True' /></GroupBox>");
                    Implicit("ItemsControl, RadioButtons in Items",
                        "<ItemsControl " + Namespaces + "><RadioButton Content='A' IsChecked='True' /><RadioButton Content='B' IsChecked='True' /></ItemsControl>");
                }

                private static void Implicit(string name, string xaml)
                {
                    var root = (FrameworkElement)XamlReader.Parse(xaml);
                    Window window = Show(root);
                    Row(name, "checked " + Checked(root) + " (both set True)");
                    window.Close();
                }

                private static RadioButton Bound(string content, string group, string path, object parameter)
                {
                    var radio = new RadioButton { Content = content, GroupName = group };
                    radio.SetBinding(RadioButton.IsCheckedProperty, new Binding(path)
                    {
                        Mode = BindingMode.TwoWay,
                        Converter = new EnumToBooleanConverter(),
                        ConverterParameter = parameter,
                    });
                    return radio;
                }

                private static StackPanel Load(string xaml, object dataContext)
                {
                    var panel = (StackPanel)XamlReader.Parse(xaml);
                    panel.DataContext = dataContext;
                    return panel;
                }

                private static Window Show(FrameworkElement content)
                {
                    var window = new Window { Content = content, Width = 320, Height = 320, ShowActivated = false, ShowInTaskbar = false };
                    window.Show();
                    Settle();
                    return window;
                }

                /// <summary>クリックと同じく、UI オートメーションの Select で選択する。</summary>
                private static void Select(RadioButton radio)
                {
                    var provider = (ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(radio).GetPattern(PatternInterface.SelectionItem);
                    provider.Select();
                    Settle();
                }

                private static RadioButton Radio(DependencyObject root, string content)
                    => Radios(root).First(r => (string)r.Content == content);

                private static IEnumerable<RadioButton> Radios(DependencyObject root)
                {
                    foreach (object child in LogicalTreeHelper.GetChildren(root))
                    {
                        if (child is RadioButton radio) yield return radio;
                        if (child is DependencyObject node)
                        {
                            foreach (RadioButton nested in Radios(node)) yield return nested;
                        }
                    }
                }

                private static string Checked(DependencyObject root)
                {
                    string[] names = Radios(root).Where(r => r.IsChecked == true).Select(r => (string)r.Content).ToArray();
                    return names.Length == 0 ? "(none)" : string.Join(" + ", names);
                }

                private static string FalseCalls()
                    => Calls.False + (Calls.False == 0 ? "" : " (" + string.Join(", ", Calls.FalseParameters) + ")");

                private static void Settle()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var frame = new DispatcherFrame();
                        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                        Dispatcher.PushFrame(frame);
                    }
                }

                private static void Row(string label, string value) => Console.WriteLine("row\t" + label + "\t" + value);
            }
        }
        """;
}

/// <summary>記事の例と同じ、印刷品質を表す列挙体。</summary>
public enum Quality
{
    Draft,
    Standard,
    Fine,
}

/// <summary>記事の例と同じ、面付けを表す列挙体。</summary>
public enum PageLayout
{
    Single,
    Dual,
}

/// <summary>初期値を既定値以外に置き、初期選択が表示されるかどうかが分かるようにする。</summary>
public sealed class PrintSettingsViewModel : INotifyPropertyChanged
{
    private Quality _quality = Quality.Standard;
    private PageLayout _pageLayout = PageLayout.Single;

    public Quality Quality
    {
        get => _quality;
        set
        {
            if (_quality == value)
            {
                return;
            }

            _quality = value;
            Raise(nameof(Quality));
        }
    }

    public PageLayout PageLayout
    {
        get => _pageLayout;
        set
        {
            if (_pageLayout == value)
            {
                return;
            }

            _pageLayout = value;
            Raise(nameof(PageLayout));
        }
    }

    private void Raise(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>記事に載せたコンバーターと同じ実装。</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter : Binding.DoNothing;
}
