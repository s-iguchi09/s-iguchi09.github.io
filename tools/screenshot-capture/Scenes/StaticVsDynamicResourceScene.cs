using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の StaticResource と DynamicResource の違い」の図。
/// 同じキーのブラシを実行中に差し替え、参照方法によって反映されるかどうかが
/// 変わることを実際の描画で示す。
///
/// StaticResource は読み込み時に解決されるため、リソースを含んだ XAML 全体を
/// 一度に解析する必要がある。ここではウィンドウの内容をまとめて記述する。
/// </summary>
internal sealed class StaticVsDynamicResourceScene : IScene
{
    private const string ContentXaml =
        """
        <Grid Margin="18">
          <Grid.Resources>
            <SolidColorBrush x:Key="ThemeColor" Color="SkyBlue" />
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

          <TextBlock Grid.Row="0" Grid.Column="0" Text="{}{StaticResource ThemeColor}" Margin="0,0,24,0" />
          <Button Grid.Row="0" Grid.Column="1" Content="Button" Padding="20,5"
                  Background="{StaticResource ThemeColor}" />

          <TextBlock Grid.Row="1" Grid.Column="0" Text="{}{DynamicResource ThemeColor}" Margin="0,12,24,0" />
          <Button Grid.Row="1" Grid.Column="1" Content="Button" Padding="20,5" Margin="0,12,0,0"
                  Background="{DynamicResource ThemeColor}" />
        </Grid>
        """;

    public IReadOnlyList<string> Verifies =>
    [
        "リソースを差し替えたとき、StaticResource で参照した側は値が変わらないこと",
        "DynamicResource で参照した側は値が追随すること",
        "差し替え前は両者が同じ値であること",
    ];

    public string Slug => "wpf-staticresource-vs-dynamicresource";

    public async Task CaptureAsync(SceneContext context)
    {
        var content = SceneContext.LoadXaml<Grid>(ContentXaml);

        var window = new Window
        {
            Title = "StaticResource / DynamicResource",
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };

        await context.ShootAsync(window, "staticresource-vs-dynamicresource.png", async _ =>
        {
            // 記事の再現コードと同じく、実行中にリソースのエントリを差し替える。
            content.Resources["ThemeColor"] = new SolidColorBrush(Colors.OrangeRed);
            await Task.Delay(250);
        });

        await context.SaveTableAsync(
            "Brush resource replaced at run time (White -> Red)",
            ["configuration", "Border.Background"],
            await ValuePrecedenceMeasurements.ResourceSwapAsync(),
            "static-vs-dynamic-resource-update.svg");
    }
}
