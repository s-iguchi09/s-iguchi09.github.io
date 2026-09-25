using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で Fluent デザインを追加ライブラリなしで適用する方法」の図。
///
/// 記事の「実装例」の XAML をそのまま読み込み、ThemeMode の Light と Dark で撮る。
/// 以前の実装例は背景と文字に SystemColors を使っていたが、SystemColors は Windows のダークモードでも
/// ThemeMode の切り替えでも値が変わらず、Dark ではコントロールだけが暗くなって背景が白く残る。
/// その以前の形も Dark で撮り、記事の「問題」を図で示す。
/// </summary>
internal sealed class FluentSystemColorsScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "SystemColors の各キーが実際に返す色と、その相対輝度を読み出す（計測した PC の Windows のアプリのモードも記録する）",
        "選択項目の HighlightColor と、個人用設定のアクセント色 AccentColor が別の値であること",
        "色を直接読んで焼き込んだ場合はアプリケーションリソースの差し替えに追随せず、リソースキーを DynamicResource で参照した場合だけ追随すること",
        "ThemeMode を Light と Dark にしたときの、Fluent テーマのブラシのキーと SystemColors のブラシのキーの値",
        "記事の実装例の XAML（Fluent テーマのブラシのキーを使う形）を ThemeMode の Light と Dark で表示した画面",
        "以前の実装例（背景と文字に SystemColors を使う形）を ThemeMode の Dark で表示した画面",
    ];

    public string Slug => "wpf-fluent-design-with-systemcolors";

    /// <summary>
    /// 記事の「実装例 2」の Window と同じ XAML。記事では先頭に x:Class を付けている。
    /// 背景・カード・文字は Fluent テーマのブラシのキー、ボタンはアクセントのキーを DynamicResource で参照する。
    /// </summary>
    internal const string ArticleWindowXaml = """
        <Window Title="Fluent Without External Libraries"
                Width="440" Height="300"
                Background="{DynamicResource ApplicationBackgroundBrush}">

          <Window.Resources>
            <Style x:Key="CardBorderStyle" TargetType="Border">
              <Setter Property="Padding" Value="24" />
              <Setter Property="CornerRadius" Value="12" />
              <Setter Property="BorderThickness" Value="1" />
              <Setter Property="Background" Value="{DynamicResource CardBackgroundFillColorDefaultBrush}" />
              <Setter Property="BorderBrush" Value="{DynamicResource CardStrokeColorDefaultBrush}" />
            </Style>

            <Style x:Key="AccentButtonStyle" TargetType="Button">
              <Setter Property="Padding" Value="14,8" />
              <Setter Property="Margin" Value="0,12,0,0" />
              <Setter Property="HorizontalAlignment" Value="Left" />
              <Setter Property="Foreground" Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
              <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}" />
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="Button">
                    <Border x:Name="Root"
                            Background="{TemplateBinding Background}"
                            CornerRadius="8"
                            Padding="{TemplateBinding Padding}">
                      <ContentPresenter HorizontalAlignment="Center"
                                        VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Root" Property="Opacity" Value="0.92" />
                      </Trigger>
                      <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="Root" Property="Opacity" Value="0.82" />
                      </Trigger>
                      <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="Root" Property="Opacity" Value="0.55" />
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
          </Window.Resources>

          <Grid Margin="32">
            <Border Style="{StaticResource CardBorderStyle}">
              <StackPanel>
                <TextBlock FontSize="24"
                           FontWeight="SemiBold"
                           Foreground="{DynamicResource TextFillColorPrimaryBrush}"
                           Text="WPF Fluent Style" />

                <TextBlock Margin="0,10,0,0"
                           TextWrapping="Wrap"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                           Text="Fluent theme brushes follow the light and dark theme." />

                <Button Style="{StaticResource AccentButtonStyle}"
                        Content="Run Action" />
              </StackPanel>
            </Border>
          </Grid>
        </Window>
        """;

    /// <summary>以前の実装例。背景・カード・文字・ボタンに SystemColors のブラシを使っていた。</summary>
    private const string SystemColorsWindowXaml = """
        <Window Title="SystemColors under ThemeMode Dark"
                Width="440" Height="300"
                Background="{DynamicResource {x:Static SystemColors.WindowBrushKey}}">
          <Grid Margin="32">
            <Border Padding="24" CornerRadius="12" BorderThickness="1"
                    Background="{DynamicResource {x:Static SystemColors.ControlLightBrushKey}}"
                    BorderBrush="{DynamicResource {x:Static SystemColors.ActiveBorderBrushKey}}">
              <StackPanel>
                <TextBlock FontSize="24" FontWeight="SemiBold"
                           Foreground="{DynamicResource {x:Static SystemColors.ControlTextBrushKey}}"
                           Text="WPF Fluent Style" />
                <TextBlock Margin="0,10,0,0" TextWrapping="Wrap"
                           Foreground="{DynamicResource {x:Static SystemColors.GrayTextBrushKey}}"
                           Text="SystemColors stay the same under the dark theme." />
                <Button Margin="0,12,0,0" Padding="14,8" HorizontalAlignment="Left" Content="Run Action" />
              </StackPanel>
            </Border>
          </Grid>
        </Window>
        """;

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildDefaultWindow(), "fluent-default-theme.png");

        // 記事の実装例を、Light と Dark で撮る。Light の画像は記事の代表画像でもあるため、ファイル名を変えない。
        await context.ShootAsync(Themed(SceneContext.LoadXaml<Window>(ArticleWindowXaml), dark: false), "fluent-systemcolors-card.png");
        await context.ShootAsync(Themed(SceneContext.LoadXaml<Window>(ArticleWindowXaml), dark: true), "fluent-card-dark.png");
        await context.ShootAsync(Themed(SceneContext.LoadXaml<Window>(SystemColorsWindowXaml), dark: true), "systemcolors-under-dark.png");

        await context.SaveTableAsync(
            "what SystemColors keys resolve to on this machine",
            ["key", "value", "relative luminance"],
            FluentThemeMeasurements.SystemColorValues(),
            "systemcolors-values.svg");

        await context.SaveTableAsync(
            "does the color follow when the system brush is replaced",
            ["how the color is referenced", "before", "after the replacement"],
            await FluentThemeMeasurements.ColorReferenceTrackingAsync(),
            "systemcolors-tracking.svg");

        await context.SaveTableAsync(
            "brush keys under ThemeMode Light and Dark",
            ["resource key", "ThemeMode=Light", "ThemeMode=Dark"],
            await FluentThemeMeasurements.ThemeBrushValuesAsync(),
            "theme-brush-values.svg");
    }

    private static Window Themed(Window window, bool dark)
    {
        window.ResizeMode = ResizeMode.CanMinimize;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
#pragma warning disable WPF0001 // ThemeMode は実験的 API として公開されている。
        window.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001
        return window;
    }

    /// <summary>
    /// 記事の「問題」の図。テーマを設定しない既定の外観で、同じ構成（見出し・説明文・ボタン）を並べる。
    /// </summary>
    private static Window BuildDefaultWindow()
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "WPF Fluent Style",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
        });
        content.Children.Add(new TextBlock
        {
            Text = "No theme and no local styles: the default look.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
        });
        content.Children.Add(new Button
        {
            Content = "Run Action",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        return new Window
        {
            Title = "Default theme",
            Width = 440,
            Height = 300,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Border { Padding = new Thickness(24), Margin = new Thickness(32), Child = content },
        };
    }
}
