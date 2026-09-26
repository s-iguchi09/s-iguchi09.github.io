using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で入力検証のエラーが表示されない原因」の図。
/// 1 枚目は、同じ「必須項目が空」という状態に対し、バインディングの書き方と検証インターフェイスの
/// 組み合わせだけを変えた 3 つの TextBox を並べ、既定のエラー表示（赤枠）が出る組み合わせと
/// 出ない組み合わせを実際の描画で示す。
/// 2 枚目は、AdornerDecorator を含まない ControlTemplate へ差し替えた Window の上で、
/// AdornerDecorator で包んだ TextBox だけに赤枠が出ることを示す。
/// </summary>
internal sealed class ValidationErrorNotDisplayedScene : IScene
{
    private const string ContentXaml =
        """
        <StackPanel Margin="18">
          <StackPanel.Resources>
            <Style TargetType="TextBlock">
              <Setter Property="FontFamily" Value="Consolas, Courier New" />
              <Setter Property="FontSize" Value="12" />
              <Setter Property="Foreground" Value="#333D4D" />
              <Setter Property="Margin" Value="0,0,0,5" />
            </Style>
            <Style TargetType="TextBox">
              <Setter Property="Width" Value="300" />
              <Setter Property="Padding" Value="3,2" />
              <Setter Property="HorizontalAlignment" Value="Left" />
            </Style>
          </StackPanel.Resources>

          <TextBlock Text="IDataErrorInfo + Text=&quot;{Binding Name}&quot;" />
          <TextBox x:Name="Plain" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />

          <TextBlock Margin="0,16,0,5"
                     Text="IDataErrorInfo + Text=&quot;{Binding Name, ValidatesOnDataErrors=True}&quot;" />
          <TextBox x:Name="WithFlag"
                   Text="{Binding Name, UpdateSourceTrigger=PropertyChanged, ValidatesOnDataErrors=True}" />

          <TextBlock Margin="0,16,0,5" Text="INotifyDataErrorInfo + Text=&quot;{Binding Name}&quot;" />
          <TextBox x:Name="Notify" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>
        """;

    /// <summary>
    /// AdornerDecorator を含まない Window の ControlTemplate。
    /// これを適用したウィンドウでは、既定で得られるアドーナーレイヤーが存在しなくなる。
    /// </summary>
    private const string BareWindowTemplateXaml =
        """
        <ControlTemplate TargetType="Window">
          <Border Background="White">
            <ContentPresenter />
          </Border>
        </ControlTemplate>
        """;

    private const string AdornerLayerXaml =
        """
        <StackPanel Margin="18">
          <StackPanel.Resources>
            <Style TargetType="TextBlock">
              <Setter Property="FontFamily" Value="Consolas, Courier New" />
              <Setter Property="FontSize" Value="12" />
              <Setter Property="Foreground" Value="#333D4D" />
              <Setter Property="Margin" Value="0,0,0,5" />
            </Style>
            <Style TargetType="TextBox">
              <Setter Property="Width" Value="300" />
              <Setter Property="Padding" Value="3,2" />
              <Setter Property="HorizontalAlignment" Value="Left" />
            </Style>
          </StackPanel.Resources>

          <TextBlock Text="&lt;TextBox /&gt;" />
          <TextBox x:Name="Bare" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />

          <TextBlock Margin="0,16,0,5"
                     Text="&lt;AdornerDecorator&gt;&lt;TextBox /&gt;&lt;/AdornerDecorator&gt;" />
          <AdornerDecorator HorizontalAlignment="Left">
            <TextBox x:Name="Decorated" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
          </AdornerDecorator>
        </StackPanel>
        """;

    public IReadOnlyList<string> Verifies =>
    [
        "検証の発生（Validation.Errors）と描画（アドーナー）を分けて測る",
        "IDataErrorInfo は実装しただけでは検証に参加せず、ValidatesOnDataErrors が要ること",
        "INotifyDataErrorInfo は既定で検証に参加すること",
        "ErrorTemplate を null にすると、エラーは保持されたままアドーナーだけが消えること",
        "OneWay でも ValidatesOnTargetUpdated のルールで赤枠が出ること、入力では消えずソースを直すと消えること",
        "OneWay / OneTime のターゲットへコードから代入するとバインドが外れ、SetCurrentValue では外れないこと",
        "ErrorsChanged のプロパティ名がパスと違うと、HasErrors が true でも Validation.HasError が false のままであること",
        "Validation.Error 添付イベントは NotifyOnValidationError を True にしないと発生しないこと",
        "(Validation.Errors)[0].ErrorContent はエラーの解消時にバインドエラー 17 を出し、/ErrorContent は出さないこと",
        "ErrorsChanged をバックグラウンドのスレッドから発生させてもエラーが反映されるか",
        "setter で検証する INotifyDataErrorInfo は、LostFocus ではフォーカスが外れるまで、Explicit では UpdateSource まで検証結果が変わらないこと",
    ];

    public string Slug => "wpf-validation-error-not-displayed";

    public async Task CaptureAsync(SceneContext context)
    {
        await CaptureActivationAsync(context);
        await CaptureAdornerLayerAsync(context);
    }

    /// <summary>検証方式ごとに既定のエラー表示が出るかどうかを並べた図。</summary>
    private static async Task CaptureActivationAsync(SceneContext context)
    {
        var content = SceneContext.LoadXaml<StackPanel>(ContentXaml);

        // 3 つとも「名前が空」という同じ状態から始める。差はバインディングの書き方と
        // ViewModel が実装するインターフェイスだけになる。
        ((FrameworkElement)content.FindName("Plain")).DataContext = new DataErrorAccount();
        ((FrameworkElement)content.FindName("WithFlag")).DataContext = new DataErrorAccount();
        ((FrameworkElement)content.FindName("Notify")).DataContext = new NotifyErrorAccount();

        var window = new Window
        {
            Title = "Validation Error Display",
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };

        await context.ShootAsync(window, "validation-error-display.png");
    }

    /// <summary>
    /// アドーナーレイヤーが無い視覚ツリーでは既定のエラー表示が描かれないことを示す図。
    /// Window のテンプレートから AdornerDecorator を外し、片方の TextBox だけを
    /// AdornerDecorator で包んで差を作る。検証エラーは 2 つとも発生している。
    /// </summary>
    private static async Task CaptureAdornerLayerAsync(SceneContext context)
    {
        var content = SceneContext.LoadXaml<StackPanel>(AdornerLayerXaml);
        content.DataContext = new NotifyErrorAccount();

        var window = new Window
        {
            Title = "Adorner Layer",
            Content = content,
            Template = SceneContext.LoadXaml<ControlTemplate>(BareWindowTemplateXaml),
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };

        await context.ShootAsync(window, "adorner-layer-required.png", async _ =>
        {
            // 2 つの TextBox が同じエラー状態にあることを、撮影前に確認しておく。
            var bare = (TextBox)content.FindName("Bare");
            var decorated = (TextBox)content.FindName("Decorated");
            if (!Validation.GetHasError(bare) || !Validation.GetHasError(decorated))
            {
                throw new InvalidOperationException("双方が検証エラー状態である前提が崩れている。");
            }

            if (AdornerLayer.GetAdornerLayer(bare) is not null)
            {
                throw new InvalidOperationException("AdornerDecorator を外した側でレイヤーが取得できてしまう。");
            }

            await Task.CompletedTask;
        });

        await context.SaveTableAsync(
            "TextBox bound to an always-invalid source",
            ["configuration", "HasError", "Errors", "adorners"],
            await ValidationAndScopeMeasurements.ValidationStagesAsync(),
            "validation-stages.svg");

        await context.SaveTableAsync(
            "pitfalls: OneWay, assignment, property name, Validation.Error, Errors[0], thread",
            ["case", "measured"],
            [.. await PitfallsAsync(), .. await ValidationAndScopeMeasurements.ErrorsChangedThreadAsync()],
            "validation-pitfalls.svg");
    }

    /// <summary>空文字を不正とし、ターゲットの更新時にも評価するルール。</summary>
    private sealed class RequiredRule : ValidationRule
    {
        public RequiredRule() => ValidatesOnTargetUpdated = true;

        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo) =>
            string.IsNullOrEmpty(value as string) ? new ValidationResult(false, "required") : ValidationResult.ValidResult;
    }

    private sealed class Holder : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>最初はエラーを持たず、後から Name にエラーを付けて、指定したプロパティ名で通知するソース。</summary>
    private sealed class MisnamedErrors : INotifyDataErrorInfo
    {
        private bool _invalid;

        public string Name { get; set; } = "x";

        public bool HasErrors => _invalid;

        public IEnumerable GetErrors(string? propertyName) =>
            _invalid && propertyName == nameof(Name) ? new[] { "invalid" } : Array.Empty<string>();

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public void Invalidate(string raisedName)
        {
            _invalid = true;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(raisedName));
        }
    }

    private static System.Windows.Data.Binding OneWayRequired(System.Windows.Data.BindingMode mode)
    {
        var binding = new System.Windows.Data.Binding(nameof(Holder.Name)) { Mode = mode };
        binding.ValidationRules.Add(new RequiredRule());
        return binding;
    }

    private static async Task<List<IReadOnlyList<string>>> PitfallsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        // setter で検証する INotifyDataErrorInfo。入力で空にしてから、フォーカスを移すまでの HasError を見る。
        foreach (System.Windows.Data.UpdateSourceTrigger trigger in new[] { System.Windows.Data.UpdateSourceTrigger.LostFocus, System.Windows.Data.UpdateSourceTrigger.Explicit })
        {
            var account = new NotifyErrorAccount { Name = "abc" };
            var box = new TextBox { Width = 120, DataContext = account };
            box.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(NotifyErrorAccount.Name)) { UpdateSourceTrigger = trigger });
            var other = new Button { Content = "other" };
            var panel = new StackPanel { Children = { box, other } };
            string whileTyping = "";
            string afterLeave = "";
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    $"setter validation, {trigger}: cleared by typing",
                    panel,
                    _ => [$"HasError: while focused {whileTyping}; after focus leaves {afterLeave}" + (trigger == System.Windows.Data.UpdateSourceTrigger.Explicit ? $"; after UpdateSource {WpfProbe.Describe(Validation.GetHasError(box))}" : "")],
                    Act: async _ =>
                    {
                        await DemoProbe.FocusAsync(box);
                        box.SelectAll();
                        DemoProbe.SendKey(System.Windows.Input.Key.Delete);
                        DemoProbe.SendKey(System.Windows.Input.Key.Delete, down: false);
                        await Task.Delay(100);
                        whileTyping = $"{WpfProbe.Describe(Validation.GetHasError(box))} (Text \"{box.Text}\")";
                        await DemoProbe.FocusAsync(other);
                        await Task.Delay(100);
                        afterLeave = WpfProbe.Describe(Validation.GetHasError(box));
                        if (trigger == System.Windows.Data.UpdateSourceTrigger.Explicit)
                        {
                            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                        }
                    }),
            ]));
        }

        // OneWay: 確立時に赤枠が出るか、入力で消えるか、ソースを直すと消えるか。
        {
            var holder = new Holder();
            var box = new TextBox { Width = 120, DataContext = holder };
            box.SetBinding(TextBox.TextProperty, OneWayRequired(System.Windows.Data.BindingMode.OneWay));
            string atStart = "";
            string afterTyping = "";
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    "OneWay + rule, source empty",
                    box,
                    _ => [$"HasError: start {atStart}; typed \"abc\" {afterTyping}; source \"abc\" {WpfProbe.Describe(Validation.GetHasError(box))}"],
                    Act: async _ =>
                    {
                        atStart = WpfProbe.Describe(Validation.GetHasError(box));
                        await DemoProbe.FocusAsync(box);
                        DemoProbe.TypeLetters(box, "abc");
                        await Task.Delay(100);
                        afterTyping = WpfProbe.Describe(Validation.GetHasError(box));
                        holder.Name = "abc";
                    }),
            ]));
        }

        // コードからの代入と SetCurrentValue。
        foreach ((string name, System.Windows.Data.BindingMode mode, bool current) in new[]
        {
            ("OneWay, Text = \"x\" in code", System.Windows.Data.BindingMode.OneWay, false),
            ("OneTime, Text = \"x\" in code", System.Windows.Data.BindingMode.OneTime, false),
            ("OneWay, SetCurrentValue(Text, \"x\")", System.Windows.Data.BindingMode.OneWay, true),
        })
        {
            var box = new TextBox { Width = 120, DataContext = new Holder() };
            box.SetBinding(TextBox.TextProperty, OneWayRequired(mode));
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    name,
                    box,
                    _ => [$"binding {(System.Windows.Data.BindingOperations.GetBinding(box, TextBox.TextProperty) is null ? "removed" : "kept")}, HasError {WpfProbe.Describe(Validation.GetHasError(box))}"],
                    Act: _ =>
                    {
                        if (current)
                        {
                            box.SetCurrentValue(TextBox.TextProperty, "x");
                        }
                        else
                        {
                            box.Text = "x";
                        }

                        return Task.CompletedTask;
                    }),
            ]));
        }

        // ErrorsChanged のプロパティ名の誤り。
        foreach (string raised in new[] { "Namee", "Name" })
        {
            var source = new MisnamedErrors();
            var box = new TextBox { Width = 120, DataContext = source };
            box.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(MisnamedErrors.Name)) { Mode = System.Windows.Data.BindingMode.OneWay });
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    $"error added, ErrorsChanged(\"{raised}\"), path Name",
                    box,
                    _ => [$"HasErrors {WpfProbe.Describe(source.HasErrors)}, Validation.HasError {WpfProbe.Describe(Validation.GetHasError(box))}"],
                    Act: _ =>
                    {
                        source.Invalidate(raised);
                        return Task.CompletedTask;
                    }),
            ]));
        }

        // Validation.Error 添付イベント。
        foreach (bool notify in new[] { false, true })
        {
            var holder = new Holder();
            var box = new TextBox { Width = 120, DataContext = holder };
            var binding = new System.Windows.Data.Binding(nameof(Holder.Name)) { NotifyOnValidationError = notify };
            binding.ValidationRules.Add(new RequiredRule());
            box.SetBinding(TextBox.TextProperty, binding);
            int raisedCount = 0;
            Validation.AddErrorHandler(box, (_, _) => raisedCount++);
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    $"Validation.Error handler, NotifyOnValidationError={notify}",
                    box,
                    _ => [$"Validation.Error raised {raisedCount} times"],
                    Act: _ =>
                    {
                        holder.Name = "a";
                        holder.Name = string.Empty;
                        return Task.CompletedTask;
                    }),
            ]));
        }

        // (Validation.Errors)[0].ErrorContent と /ErrorContent。エラーが解消したときのトレースを数える。
        foreach (string path in new[] { "(Validation.Errors)[0].ErrorContent", "(Validation.Errors)/ErrorContent" })
        {
            var holder = new Holder();
            var box = new TextBox { Width = 120, DataContext = holder };
            box.SetBinding(TextBox.TextProperty, OneWayRequired(System.Windows.Data.BindingMode.OneWay));
            var message = new TextBlock();
            message.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(path) { Source = box });
            var panel = new StackPanel { Children = { box, message } };
            var listener = new CountingListener();
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    $"{path}, error cleared",
                    panel,
                    _ => [$"HasError {WpfProbe.Describe(Validation.GetHasError(box))}, Error 17 traced {listener.Error17}"],
                    Act: async _ =>
                    {
                        System.Diagnostics.PresentationTraceSources.Refresh();
                        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Warning;
                        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
                        try
                        {
                            holder.Name = "abc";
                            await Task.Delay(100);
                        }
                        finally
                        {
                            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                        }
                    }),
            ]));
        }

        return rows;
    }

    /// <summary>データバインドのトレースのうち、Error 17 の行を数える。</summary>
    private sealed class CountingListener : System.Diagnostics.TraceListener
    {
        public int Error17 { get; private set; }

        public override void Write(string? message) => Count(message);

        public override void WriteLine(string? message) => Count(message);

        private void Count(string? message)
        {
            if (message is not null && message.Contains("Error: 17"))
            {
                Error17++;
            }
        }
    }

    /// <summary><see cref="IDataErrorInfo"/> で必須チェックを返す ViewModel。</summary>
    private sealed class DataErrorAccount : INotifyPropertyChanged, IDataErrorInfo
    {
        private string name = string.Empty;

        public string Name
        {
            get => name;
            set
            {
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public string Error => string.Empty;

        public string this[string columnName] =>
            columnName == nameof(Name) && string.IsNullOrWhiteSpace(Name)
                ? "Name is required."
                : string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary><see cref="INotifyDataErrorInfo"/> で必須チェックを返す ViewModel。</summary>
    private sealed class NotifyErrorAccount : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly Dictionary<string, List<string>> errors = [];
        private string name = string.Empty;

        public NotifyErrorAccount() => Validate();

        public string Name
        {
            get => name;
            set
            {
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                Validate();
            }
        }

        public bool HasErrors => errors.Count > 0;

        public IEnumerable GetErrors(string? propertyName) =>
            propertyName is not null && errors.TryGetValue(propertyName, out List<string>? list)
                ? list
                : Array.Empty<string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors[nameof(Name)] = ["Name is required."];
            }
            else
            {
                errors.Remove(nameof(Name));
            }

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Name)));
        }
    }
}
