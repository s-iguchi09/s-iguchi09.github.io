using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で Style の Trigger / DataTrigger が効かない原因」の図。
/// 同じスタイルを適用した 2 つの Border に対し、片方だけ Background をローカル値として
/// 直接指定し、DataTrigger の条件が成立してもローカル値側は変化しないことを実際の描画で示す。
///
/// スタイルと参照側をまとめて解析する必要があるため、ウィンドウの内容を 1 つの XAML で記述する。
/// </summary>
internal sealed class StyleTriggerLocalValueScene : IScene
{
    private const string ContentXaml =
        """
        <Grid Margin="18">
          <Grid.Resources>
            <Style x:Key="StatusBox" TargetType="Border">
              <Setter Property="Background" Value="White" />
              <Setter Property="BorderBrush" Value="#9AA4B2" />
              <Setter Property="BorderThickness" Value="1" />
              <Setter Property="Padding" Value="18,6" />
              <Style.Triggers>
                <DataTrigger Binding="{Binding HasError}" Value="True">
                  <Setter Property="Background" Value="#FFD4D4" />
                </DataTrigger>
              </Style.Triggers>
            </Style>
            <Style TargetType="TextBlock">
              <Setter Property="FontFamily" Value="Consolas, Courier New" />
              <Setter Property="FontSize" Value="13" />
              <Setter Property="Foreground" Value="#333D4D" />
              <Setter Property="VerticalAlignment" Value="Center" />
            </Style>
          </Grid.Resources>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="Auto" />
          </Grid.ColumnDefinitions>
          <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
          </Grid.RowDefinitions>

          <TextBlock Grid.Row="0" Grid.Column="0" Margin="0,0,24,0"
                     Text="&lt;Border Background=&quot;White&quot; /&gt;" />
          <Border Grid.Row="0" Grid.Column="1" Style="{StaticResource StatusBox}" Background="White">
            <TextBlock Text="HasError = True" />
          </Border>

          <TextBlock Grid.Row="1" Grid.Column="0" Margin="0,12,24,0" Text="&lt;Border /&gt;" />
          <Border Grid.Row="1" Grid.Column="1" Margin="0,12,0,0" Style="{StaticResource StatusBox}">
            <TextBlock Text="HasError = True" />
          </Border>
        </Grid>
        """;

    public IReadOnlyList<string> Verifies =>
    [
        "ローカル値を持つ要素では、条件が成立しても Style の Trigger が反映されないこと",
        "そのときの実効値の BaseValueSource が Local になること",
        "既定値を Setter へ移すと Trigger が反映され、BaseValueSource が変わること",
        "ClearValue でローカル値を取り除いても Trigger が反映されるようになること",
    ];

    public string Slug => "wpf-style-trigger-not-working-local-value";

    public async Task CaptureAsync(SceneContext context)
    {
        var content = SceneContext.LoadXaml<Grid>(ContentXaml);

        var window = new Window
        {
            Title = "Style Trigger / Local Value",
            Content = content,
            // 記事の再現コードと同じく、トリガーの条件が成立した状態で表示する。
            DataContext = new ErrorState(true),
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };

        await context.ShootAsync(window, "style-trigger-local-value.png");

        await context.SaveTableAsync(
            "Border.Background, DataTrigger on HasError",
            ["configuration", "effective value (BaseValueSource)"],
            await ValuePrecedenceMeasurements.StyleTriggerPrecedenceAsync(),
            "style-trigger-precedence.svg");
    }

    /// <summary>DataTrigger の条件に使う ViewModel 相当の状態。</summary>
    private sealed record ErrorState(bool HasError);
}
