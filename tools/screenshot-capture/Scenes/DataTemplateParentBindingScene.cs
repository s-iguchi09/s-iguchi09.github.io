using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「DataTemplate の中から親の DataContext にバインドできない問題」の図。
/// 同じ DataTemplate 内に、素の <c>{Binding Unit}</c> と
/// <c>RelativeSource AncestorType=ItemsControl</c> 経由の 2 つのバインドを並べ、
/// 前者だけが解決できずに空欄のままになることを実際の描画で示す。
///
/// ItemsControl と DataTemplate をまとめて解析する必要があるため、
/// ウィンドウの内容を 1 つの XAML で記述する。
/// </summary>
internal sealed class DataTemplateParentBindingScene : IScene
{
    private const string ContentXaml =
        """
        <StackPanel Margin="18">
          <StackPanel.Resources>
            <Style TargetType="TextBlock">
              <Setter Property="FontFamily" Value="Consolas, Courier New, monospace" />
              <Setter Property="FontSize" Value="12" />
              <Setter Property="Foreground" Value="#333D4D" />
              <Setter Property="VerticalAlignment" Value="Center" />
            </Style>
            <Style x:Key="Cell" TargetType="Border">
              <Setter Property="BorderBrush" Value="#9AA4B2" />
              <Setter Property="BorderThickness" Value="1" />
              <Setter Property="Padding" Value="8,3" />
              <Setter Property="Margin" Value="0,0,10,0" />
              <Setter Property="VerticalAlignment" Value="Center" />
            </Style>
          </StackPanel.Resources>

          <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Width="60" Text="Name" FontWeight="Bold" />
            <TextBlock Width="60" Text="Value" FontWeight="Bold" />
            <TextBlock Width="120" FontWeight="Bold" Text="{}{Binding Unit}" />
            <TextBlock Width="320" FontWeight="Bold" TextWrapping="Wrap"
                       Text="{}{Binding DataContext.Unit, RelativeSource={RelativeSource AncestorType={x:Type ItemsControl}}}" />
          </StackPanel>

          <ItemsControl ItemsSource="{Binding Items}">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <StackPanel Orientation="Horizontal" Margin="0,3">
                  <TextBlock Width="60" Text="{Binding Name}" />
                  <TextBlock Width="60" Text="{Binding Value}" />
                  <Border Width="110" Style="{StaticResource Cell}">
                    <TextBlock Text="{Binding Unit}" />
                  </Border>
                  <Border Width="110" Style="{StaticResource Cell}">
                    <TextBlock Text="{Binding DataContext.Unit, RelativeSource={RelativeSource AncestorType={x:Type ItemsControl}}}" />
                  </Border>
                </StackPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
        """;

    public string Slug => "wpf-datatemplate-parent-datacontext-binding";

    public async Task CaptureAsync(SceneContext context)
    {
        var content = SceneContext.LoadXaml<StackPanel>(ContentXaml);

        var window = new Window
        {
            Title = "DataTemplate / Parent DataContext",
            Content = content,
            // Unit は親の ViewModel だけが持ち、各アイテムは持たない。
            DataContext = new MeasurementList(
                "kg",
                [new Measurement("A", 120), new Measurement("B", 80), new Measurement("C", 240)]),
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };

        await context.ShootAsync(window, "datatemplate-parent-binding.png");
    }

    /// <summary>ItemsControl に設定する親の ViewModel 相当。</summary>
    private sealed record MeasurementList(string Unit, IReadOnlyList<Measurement> Items);

    /// <summary>各アイテム。<c>Unit</c> は持たない。</summary>
    private sealed record Measurement(string Name, int Value);
}
