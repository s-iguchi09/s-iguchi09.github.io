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
    public IReadOnlyList<string> Verifies =>
    [
        "SystemColors の各キーが実際に返す色と、その相対輝度を読み出す",
    ];

    public string Slug => "wpf-fluent-design-with-systemcolors";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildWindow(fluent: false), "fluent-default-theme.png");
        await context.ShootAsync(BuildWindow(fluent: true), "fluent-systemcolors-card.png");

        await context.SaveTableAsync(
            "what SystemColors keys resolve to on this machine",
            ["key", "value", "relative luminance"],
            FluentThemeMeasurements.SystemColorValues(),
            "systemcolors-values.svg");
    }

    private static Window BuildWindow(bool fluent)
    {
        var window = new Window
        {
            Title = fluent ? "Fluent + SystemColors" : "Default theme",
            Width = 440,
            Height = 280,
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
    /// 記事の「実装例」に載せている XAML と同じカード構成（見出し・説明文・ボタン）。
    /// Fluent 側だけ SystemColors 由来の配色と角丸を与える。
    /// </summary>
    private static UIElement BuildCard(bool fluent)
    {
        var content = new StackPanel();

        var heading = new TextBlock
        {
            Text = "WPF Fluent Style",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
        };
        var body = new TextBlock
        {
            Text = "SystemColors を参照することで、Windows の色設定に依存した色を利用できる。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
        };

        if (fluent)
        {
            // 記事の XAML と同じく、SystemColors 由来のブラシを参照するのは解決策側だけ。
            // 記事は DynamicResource で参照しているため、こちらも動的参照に揃える。
            heading.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.ControlTextBrushKey);
            body.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.GrayTextBrushKey);
        }

        content.Children.Add(heading);
        content.Children.Add(body);
        content.Children.Add(new Button
        {
            Content = "操作を実行",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        var card = new Border
        {
            Padding = new Thickness(24),
            Margin = new Thickness(32),
            Child = content,
        };

        if (fluent)
        {
            card.CornerRadius = new CornerRadius(12);
            card.BorderThickness = new Thickness(1);
            card.Background = new SolidColorBrush(SystemColors.ControlLightColor);
            card.SetResourceReference(Border.BorderBrushProperty, SystemColors.ActiveBorderBrushKey);
        }

        return card;
    }
}
