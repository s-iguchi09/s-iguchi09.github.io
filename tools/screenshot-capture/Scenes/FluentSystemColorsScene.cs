using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で Fluent デザインを追加ライブラリなしで適用する方法」の図。
/// 同じ画面構成を、既定テーマのままの場合と、Fluent テーマ＋SystemColors で
/// 整えた場合とで取得する。
/// </summary>
internal sealed class FluentSystemColorsScene : IScene
{
    public string Slug => "wpf-fluent-design-with-systemcolors";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildWindow(fluent: false), "fluent-default-theme.png");
        await context.ShootAsync(BuildWindow(fluent: true), "fluent-systemcolors-card.png");
    }

    private static Window BuildWindow(bool fluent)
    {
        var window = new Window
        {
            Title = fluent ? "Fluent + SystemColors" : "Default theme",
            Width = 340,
            Height = 300,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        if (fluent)
        {
#pragma warning disable WPF0001 // ThemeMode は実験的 API として公開されている。
            window.ThemeMode = ThemeMode.Light;
#pragma warning restore WPF0001

            // Mica を無効化しているため、Fluent の明るいテーマ相当の背景を明示する。
            window.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        }

        window.Content = BuildCard(fluent);
        return window;
    }

    /// <summary>
    /// 記事の「カード領域」に相当するレイアウト。
    /// Fluent 側だけ SystemColors 由来の配色と角丸を与える。
    /// </summary>
    private static UIElement BuildCard(bool fluent)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Order 1042",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
        });
        content.Children.Add(new TextBox { Text = "invoice" });
        content.Children.Add(new CheckBox
        {
            Content = "Notify",
            IsChecked = true,
            Margin = new Thickness(0, 12, 0, 0),
        });
        content.Children.Add(new Button
        {
            Content = "Save",
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 14, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        var card = new Border
        {
            Padding = new Thickness(18),
            Margin = new Thickness(16),
            Child = content,
        };

        if (fluent)
        {
            card.CornerRadius = new CornerRadius(12);
            card.BorderThickness = new Thickness(1);
            card.Background = new SolidColorBrush(SystemColors.ControlLightColor);
            card.BorderBrush = SystemColors.ActiveBorderBrush;
        }

        return card;
    }
}
