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
