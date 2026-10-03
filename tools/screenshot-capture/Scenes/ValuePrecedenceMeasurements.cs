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

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    public static Task<List<IReadOnlyList<Loc>>> StyleTriggerPrecedenceAsync() =>
        WpfProbe.MeasureAsync(
        [
            new WpfProbe.LocCase(
                T("local Background + trigger", "ローカルの Background + トリガー"),
                BuildTriggerCase(StyleWithoutDefault, BorderWithLocalValue, hasError: true),
                ReadBackgroundSource),
            new WpfProbe.LocCase(
                T("Setter default + trigger", "Setter の既定値 + トリガー"),
                BuildTriggerCase(StyleWithDefault, BorderWithoutLocalValue, hasError: true),
                ReadBackgroundSource),
            new WpfProbe.LocCase(
                T("Setter default, trigger not met", "Setter の既定値、トリガーの条件を満たさない"),
                BuildTriggerCase(StyleWithDefault, BorderWithoutLocalValue, hasError: false),
                ReadBackgroundSource),
            new WpfProbe.LocCase(
                T("local Background, then ClearValue", "ローカルの Background、続けて ClearValue"),
                BuildTriggerCase(StyleWithDefault, BorderWithLocalValue, hasError: true),
                ReadBackgroundSource,
                Act: root =>
                {
                    TargetBorder(root).ClearValue(Border.BackgroundProperty);
                    return Task.CompletedTask;
                }),
        ]);

    private static IReadOnlyList<Loc> ReadBackgroundSource(FrameworkElement root) =>
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

    public static Task<List<IReadOnlyList<Loc>>> ResourceSwapAsync() =>
        WpfProbe.MeasureAsync(
        [
            new WpfProbe.LocCase(T("StaticResource, before swap", "StaticResource、差し替え前"), BuildResourceCase("StaticResource"), ReadBackground),
            new WpfProbe.LocCase(T("StaticResource, after swap", "StaticResource、差し替え後"), BuildResourceCase("StaticResource"), ReadBackground, SwapResourceAsync),
            new WpfProbe.LocCase(T("DynamicResource, before swap", "DynamicResource、差し替え前"), BuildResourceCase("DynamicResource"), ReadBackground),
            new WpfProbe.LocCase(T("DynamicResource, after swap", "DynamicResource、差し替え後"), BuildResourceCase("DynamicResource"), ReadBackground, SwapResourceAsync),
            new WpfProbe.LocCase(T("StaticResource, brush.Color changed", "StaticResource、brush.Color を変更"), BuildResourceCase("StaticResource"), ReadBackgroundAndFrozen, ChangeBrushColorAsync),
            new WpfProbe.LocCase(T("DynamicResource, brush.Color changed", "DynamicResource、brush.Color を変更"), BuildResourceCase("DynamicResource"), ReadBackgroundAndFrozen, ChangeBrushColorAsync),
        ]);

    private static bool s_wasFrozen;

    private static IReadOnlyList<Loc> ReadBackgroundAndFrozen(FrameworkElement root) =>
        [T($"{WpfProbe.Describe(TargetBorder(root).Background)} (brush was frozen: {WpfProbe.Describe(s_wasFrozen)})", $"{WpfProbe.Describe(TargetBorder(root).Background)}（ブラシが Freeze されていたか: {WpfProbe.Describe(s_wasFrozen)}）")];

    /// <summary>リソースを差し替えず、同じブラシの Color を書き換える。Freeze されていなければ書き換えられる。</summary>
    private static Task ChangeBrushColorAsync(FrameworkElement root)
    {
        var brush = (SolidColorBrush)root.Resources["PanelBrush"];
        s_wasFrozen = brush.IsFrozen;
        if (!brush.IsFrozen)
        {
            brush.Color = Colors.Red;
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<Loc> ReadBackground(FrameworkElement root) =>
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

    public static async Task<List<IReadOnlyList<Loc>>> RelayCommandRequeryAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // 委譲型は自前のイベントを持たず、RaiseCanExecuteChanged で発火するものが無いため、その組み合わせは測らない。
        Loc keyInput = T(KeyInput, "TextBox にキー入力");
        (Loc Name, Func<Func<bool>, RelayCommandBase> Create, Loc[] Triggers)[] implementations =
        [
            ("RequerySuggested", canExecute => new RequeryRelayCommand(canExecute), ["(nothing)", "InvalidateRequerySuggested", keyInput]),
            (T("own event", "独自のイベント"), canExecute => new ManualRelayCommand(canExecute), ["(nothing)", "InvalidateRequerySuggested", "RaiseCanExecuteChanged", keyInput]),
        ];

        foreach ((Loc name, Func<Func<bool>, RelayCommandBase> create, Loc[] triggers) in implementations)
        {
            foreach (Loc trigger in triggers)
            {
                Loc label = T($"{name.En} / {trigger.En}", $"{name.Ja} / {trigger.Ja}");
                bool allowed = false;
                RelayCommandBase command = create(() => allowed);

                if (trigger.En == KeyInput)
                {
                    rows.Add(await MeasureKeyInputAsync(label, command, () => allowed = true));
                    continue;
                }

                var button = new Button { Content = "Run", Command = command, Width = 80 };

                rows.Add(await MeasureButtonAsync(label, button, () =>
                {
                    allowed = true;
                    if (trigger.En == "InvalidateRequerySuggested")
                    {
                        CommandManager.InvalidateRequerySuggested();
                    }
                    else if (trigger.En == "RaiseCanExecuteChanged")
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

    private const string KeyInput = "key typed in a TextBox";

    /// <summary>
    /// 条件を変えたあと、何も呼ばずに隣の TextBox へ InputManager を通してキー入力（a）を送る。
    /// ユーザーの入力を受けて WPF が再問い合わせを行うかを見る。
    /// </summary>
    private static async Task<IReadOnlyList<Loc>> MeasureKeyInputAsync(Loc label, RelayCommandBase command, Action change)
    {
        var textBox = new TextBox { Width = 120 };
        var button = new Button { Content = "Run", Command = command, Width = 80 };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Children = { textBox, button } };
        var window = new Window { Title = label.En, Content = panel, Width = 320, Height = 120 };

        try
        {
            await Capture.ShowAndSettleAsync(window);
            await DemoProbe.FrontAsync(panel);
            await DemoProbe.FocusAsync(textBox);

            string before = WpfProbe.Describe(button.IsEnabled);
            change();
            DemoProbe.TypeLetters(textBox, "a");
            await Capture.SettleAsync(window);

            if (textBox.Text != "a")
            {
                throw new InvalidOperationException($"キー入力が TextBox に届いていない（\"{textBox.Text}\"）。");
            }

            return [label, before, WpfProbe.Describe(button.IsEnabled)];
        }
        finally
        {
            window.Topmost = false;
            window.Close();
        }
    }

    private static async Task<IReadOnlyList<Loc>> MeasureButtonAsync(Loc label, Button button, Action change)
    {
        var host = new Grid();
        host.Children.Add(button);

        string before = string.Empty;

        List<IReadOnlyList<Loc>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.LocCase(
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

        public virtual void RaiseCanExecuteChanged()
        {
        }
    }

    /// <summary>CanExecuteChanged の購読を CommandManager.RequerySuggested へ転送する実装。</summary>
    private sealed class RequeryRelayCommand(Func<bool> canExecute) : RelayCommandBase(canExecute)
    {
        public override event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    /// <summary>自前のイベントを保持し、明示的に発火する実装。</summary>
    private sealed class ManualRelayCommand(Func<bool> canExecute) : RelayCommandBase(canExecute)
    {
        public override event EventHandler? CanExecuteChanged;

        public override void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
