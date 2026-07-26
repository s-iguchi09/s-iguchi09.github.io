using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF Fluent テーマの TextBox でクリアボタンを非表示にする方法」の図。
/// 既定でクリアボタンが現れる状態と、記事の方法 1 を適用して消えた状態を取得する。
/// </summary>
internal sealed class FluentClearButtonScene : IScene
{
    /// <summary>.NET 10 は "DeleteButton"、.NET 9 は "ClearButton"。</summary>
    private static readonly string[] ClearButtonPartNames = ["DeleteButton", "ClearButton"];

    public string Slug => "wpf-fluent-textbox-hide-clear-button";

    public async Task CaptureAsync(SceneContext context)
    {
        Window standard = BuildWindow(out TextBox defaultTextBox);
        await context.ShootAsync(
            standard,
            "fluent-clear-button-default.png",
            _ => FocusAsync(defaultTextBox));

        Window hidden = BuildWindow(out TextBox hiddenTextBox);
        await context.ShootAsync(
            hidden,
            "fluent-clear-button-hidden.png",
            async _ =>
            {
                await FocusAsync(hiddenTextBox);
                HideClearButtonPart(hiddenTextBox);
            });
    }

    /// <summary>
    /// Fluent テーマを適用し、テキスト入力済みの単一行 TextBox だけを置いたウィンドウ。
    /// </summary>
    private static Window BuildWindow(out TextBox textBox)
    {
        textBox = new TextBox
        {
            Text = "invoice",
            Width = 260,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var window = new Window
        {
            Title = "Fluent TextBox",
            Width = 360,
            Height = 150,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            // Mica を無効化するため、Fluent の明るいテーマ相当の背景を明示する。
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)),
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = textBox,
            },
        };

#pragma warning disable WPF0001 // ThemeMode は実験的 API として公開されている。
        window.ThemeMode = ThemeMode.Light;
#pragma warning restore WPF0001

        return window;
    }

    /// <summary>
    /// クリアボタンは IsKeyboardFocusWithin が true のときだけ現れるため、
    /// キャプチャ前にキーボードフォーカスを入れる。
    /// </summary>
    private static async Task FocusAsync(TextBox textBox)
    {
        textBox.Focus();
        textBox.CaretIndex = textBox.Text.Length;
        await Task.Delay(200);
    }

    /// <summary>
    /// 記事の方法 1（名前付きパーツにローカル値で Collapsed を設定する）と同じ処理。
    /// </summary>
    private static void HideClearButtonPart(TextBox textBox)
    {
        textBox.ApplyTemplate();

        foreach (string partName in ClearButtonPartNames)
        {
            if (textBox.Template?.FindName(partName, textBox) is UIElement clearButton)
            {
                clearButton.Visibility = Visibility.Collapsed;
            }
        }
    }
}
