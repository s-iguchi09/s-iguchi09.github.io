using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 「この書き方だと効かない、こう直すと効く」を実測する部品。
///
/// 記事ごとにシーンは既にあるため、ここでは表の行を作るところまでを担う。
/// 呼び出し側のシーンが <see cref="SceneContext.SaveTableAsync"/> で図にする。
/// </summary>
internal static class ValuePrecedenceMeasurements
{
    // ------------------------------------------------------------------
    // Style の Trigger が効かない理由（値優先順位）
    // ------------------------------------------------------------------

    /// <summary>記事の XAML と同じ Style。トリガーは HasError が True のときに背景を変える。</summary>
    private const string StyleWithoutDefault = """
          <Style x:Key="StatusBox" TargetType="Border">
            <Style.Triggers>
              <DataTrigger Binding="{Binding HasError}" Value="True">
                <Setter Property="Background" Value="#FFD4D4" />
              </DataTrigger>
            </Style.Triggers>
          </Style>
        """;

    /// <summary>既定値を Setter へ移した版。ローカル値を使わずに済む。</summary>
    private const string StyleWithDefault = """
          <Style x:Key="StatusBox" TargetType="Border">
            <Setter Property="Background" Value="White" />
            <Style.Triggers>
              <DataTrigger Binding="{Binding HasError}" Value="True">
                <Setter Property="Background" Value="#FFD4D4" />
              </DataTrigger>
            </Style.Triggers>
          </Style>
        """;

    private const string BorderWithLocalValue =
        """<Border x:Name="Target" Style="{StaticResource StatusBox}" Background="White" Width="80" Height="24" />""";

    private const string BorderWithoutLocalValue =
        """<Border x:Name="Target" Style="{StaticResource StatusBox}" Width="80" Height="24" />""";

    public static Task<List<IReadOnlyList<string>>> StyleTriggerPrecedenceAsync() =>
        WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "local Background + trigger",
                BuildTriggerCase(StyleWithoutDefault, BorderWithLocalValue, hasError: true),
                ReadBackgroundSource),
            new WpfProbe.Case(
                "Setter default + trigger",
                BuildTriggerCase(StyleWithDefault, BorderWithoutLocalValue, hasError: true),
                ReadBackgroundSource),
            new WpfProbe.Case(
                "Setter default, trigger not met",
                BuildTriggerCase(StyleWithDefault, BorderWithoutLocalValue, hasError: false),
                ReadBackgroundSource),
            new WpfProbe.Case(
                "local Background, then ClearValue",
                BuildTriggerCase(StyleWithDefault, BorderWithLocalValue, hasError: true),
                ReadBackgroundSource,
                Act: root =>
                {
                    TargetBorder(root).ClearValue(Border.BackgroundProperty);
                    return Task.CompletedTask;
                }),
        ]);

    private static IReadOnlyList<string> ReadBackgroundSource(FrameworkElement root) =>
        [WpfProbe.ValueAndSource(TargetBorder(root), Border.BackgroundProperty)];

    private static Border TargetBorder(FrameworkElement root) => (Border)root.FindName("Target");

    /// <summary>記事と同じ XAML を組み立て、DataContext に HasError を与える。</summary>
    private static FrameworkElement BuildTriggerCase(string style, string border, bool hasError)
    {
        var grid = SceneContext.LoadXaml<Grid>($"""
            <Grid>
              <Grid.Resources>
            {style}
              </Grid.Resources>
              {border}
            </Grid>
            """);

        grid.DataContext = new TriggerSource { HasError = hasError };
        return grid;
    }

    /// <summary>DataTrigger のバインド先。匿名型はバインドの対象にできないため型を用意する。</summary>
    private sealed class TriggerSource
    {
        public bool HasError { get; init; }
    }

    // ------------------------------------------------------------------
    // StaticResource と DynamicResource の差
    // ------------------------------------------------------------------

    public static Task<List<IReadOnlyList<string>>> ResourceSwapAsync() =>
        WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case("StaticResource, before swap", BuildResourceCase("StaticResource"), ReadBackground),
            new WpfProbe.Case("StaticResource, after swap", BuildResourceCase("StaticResource"), ReadBackground, SwapResourceAsync),
            new WpfProbe.Case("DynamicResource, before swap", BuildResourceCase("DynamicResource"), ReadBackground),
            new WpfProbe.Case("DynamicResource, after swap", BuildResourceCase("DynamicResource"), ReadBackground, SwapResourceAsync),
        ]);

    private static IReadOnlyList<string> ReadBackground(FrameworkElement root) =>
        [WpfProbe.Describe(TargetBorder(root).Background)];

    /// <summary>実行中にリソースを差し替える。キーは同じまま、値だけを変える。</summary>
    private static Task SwapResourceAsync(FrameworkElement root)
    {
        root.Resources["PanelBrush"] = new SolidColorBrush(Colors.Red);
        return Task.CompletedTask;
    }

    private static FrameworkElement BuildResourceCase(string markupExtension) =>
        SceneContext.LoadXaml<Grid>($$"""
            <Grid>
              <Grid.Resources>
                <SolidColorBrush x:Key="PanelBrush" Color="White" />
              </Grid.Resources>
              <Border x:Name="Target" Background="{{{markupExtension}} PanelBrush}" Width="80" Height="24" />
            </Grid>
            """);

    // ------------------------------------------------------------------
    // RelayCommand の CanExecute がボタンへ反映されるか
    // ------------------------------------------------------------------

    public static async Task<List<IReadOnlyList<string>>> RelayCommandRequeryAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        (string Name, Func<Func<bool>, RelayCommandBase> Create)[] implementations =
        [
            ("RequerySuggested", canExecute => new RequeryRelayCommand(canExecute)),
            ("own event", canExecute => new ManualRelayCommand(canExecute)),
        ];

        foreach ((string name, Func<Func<bool>, RelayCommandBase> create) in implementations)
        {
            foreach (string trigger in new[] { "(nothing)", "InvalidateRequerySuggested", "RaiseCanExecuteChanged" })
            {
                bool allowed = false;
                RelayCommandBase command = create(() => allowed);
                var button = new Button { Content = "Run", Command = command, Width = 80 };

                rows.Add(await MeasureButtonAsync($"{name} / {trigger}", button, () =>
                {
                    allowed = true;
                    if (trigger == "InvalidateRequerySuggested")
                    {
                        CommandManager.InvalidateRequerySuggested();
                    }
                    else if (trigger == "RaiseCanExecuteChanged")
                    {
                        command.RaiseCanExecuteChanged();
                    }
                }));
            }
        }

        // 対照。Command が未設定のボタンは判定対象が無く、有効のままになる。
        rows.Add(await MeasureButtonAsync(
            "Command = null", new Button { Content = "Run", Width = 80 }, () => { }));

        return rows;
    }

    private static async Task<IReadOnlyList<string>> MeasureButtonAsync(string label, Button button, Action change)
    {
        var host = new Grid();
        host.Children.Add(button);

        string before = string.Empty;

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                label,
                host,
                _ => [WpfProbe.Describe(button.IsEnabled)],
                Act: _ =>
                {
                    before = WpfProbe.Describe(button.IsEnabled);
                    change();
                    return Task.CompletedTask;
                }),
        ]);

        // MeasureAsync は [ラベル, 読み取り結果] を返す。変更前の値を間に挟む。
        return [label, before, measured[0][1]];
    }

    /// <summary>記事の 2 つの実装に共通の土台。</summary>
    private abstract class RelayCommandBase(Func<bool> canExecute) : ICommand
    {
        private readonly Func<bool> _canExecute = canExecute;

        public abstract event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute();

        public void Execute(object? parameter)
        {
        }

        public abstract void RaiseCanExecuteChanged();
    }

    /// <summary>CanExecuteChanged の購読を CommandManager.RequerySuggested へ転送する実装。</summary>
    private sealed class RequeryRelayCommand(Func<bool> canExecute) : RelayCommandBase(canExecute)
    {
        public override event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>自前のイベントを持たないため、明示的な発火はできない。</summary>
        public override void RaiseCanExecuteChanged()
        {
        }
    }

    /// <summary>自前のイベントを保持し、明示的に発火する実装。</summary>
    private sealed class ManualRelayCommand(Func<bool> canExecute) : RelayCommandBase(canExecute)
    {
        public override event EventHandler? CanExecuteChanged;

        public override void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
