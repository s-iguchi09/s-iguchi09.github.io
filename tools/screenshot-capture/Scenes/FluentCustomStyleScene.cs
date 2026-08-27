using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF Fluent テーマでカスタム Style を持つコントロールだけ旧外観に戻る問題」の図。
/// Fluent テーマを適用したうえで、記事の「問題」に載せた暗黙スタイルを
/// <c>Application.Resources</c> 直下へ置いた場合と、記事の「実装例」に載せた
/// <c>Styles.xaml</c> をマージした場合とを、同じ画面構成で取得する。
/// </summary>
internal sealed class FluentCustomStyleScene : IScene
{
    /// <summary>記事の実装例に載せている Styles.xaml と同じ内容のリソースディクショナリ。</summary>
    private const string StylesDictionaryUri =
        "pack://application:,,,/ScreenshotCapture;component/Scenes/FluentCustomStyleResources.xaml";

    /// <summary>記事の「問題」に載せている暗黙スタイル。BasedOn を持たない。</summary>
    private const string ShadowingStyleXaml = """
        <Style TargetType="Button">
          <Setter Property="Padding" Value="16,6" />
        </Style>
        """;

    public string Slug => "wpf-fluent-theme-custom-style-not-applied";

    public async Task CaptureAsync(SceneContext context)
    {
        Application application = Application.Current;

#pragma warning disable WPF0001 // ThemeMode は実験的 API として公開されている。
        ThemeMode original = application.ThemeMode;
        application.ThemeMode = ThemeMode.Light;
#pragma warning restore WPF0001

        var styles = new ResourceDictionary { Source = new Uri(StylesDictionaryUri) };

        try
        {
            // 問題の状態: 記事の App.xaml と同じく、暗黙スタイルを Application.Resources 直下へ置く。
            // 同じ {x:Type Button} キーを占有するため、ThemeMode がマージした Fluent の
            // 暗黙スタイルが隠れる。
            application.Resources[typeof(Button)] = SceneContext.LoadXaml<Style>(ShadowingStyleXaml);
            await context.ShootAsync(BuildWindow(), "implicit-style-shadows-fluent.png");
            application.Resources.Remove(typeof(Button));

            // 解決した状態: 記事の実装例と同じく、BasedOn を持つ暗黙スタイルを別の
            // リソースディクショナリから Application.Resources へマージする。
            application.Resources.MergedDictionaries.Add(styles);
            await context.ShootAsync(BuildWindow(), "implicit-style-basedon-fluent.png");
        }
        finally
        {
            application.Resources.MergedDictionaries.Remove(styles);
            application.Resources.Remove(typeof(Button));
#pragma warning disable WPF0001
            application.ThemeMode = original;
#pragma warning restore WPF0001
        }
    }

    /// <summary>
    /// Button と CheckBox を並べた画面。暗黙スタイルを置いた Button だけが旧外観へ戻り、
    /// 手を加えていない CheckBox は Fluent のままである状況を 1 枚で示す。
    /// </summary>
    private static Window BuildWindow()
    {
        var content = new StackPanel { Margin = new Thickness(28) };

        content.Children.Add(new Button
        {
            Content = "Save",
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        content.Children.Add(new CheckBox
        {
            Content = "Overwrite",
            Margin = new Thickness(0, 18, 0, 0),
        });

        return new Window
        {
            Title = "Fluent theme",
            Width = 300,
            Height = 190,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            // Mica を無効化しているため、Fluent の明るいテーマ相当の背景を明示する。
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)),
            Content = content,
        };
    }
}
