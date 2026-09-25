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

    public IReadOnlyList<string> Verifies =>
    [
        "Fluent テーマの TextBox テンプレートに存在する名前付きパーツを列挙する",
        ".NET 10 でのクリアボタンのパーツ名",
        "ThemeMode を設定した場合と Fluent.xaml を直接マージした場合のどちらでもパーツが現れること",
        "BasedOn を書かない暗黙スタイルを同じキーに置くと、どちらの経路でもパーツが消えること",
        "BasedOn で元のスタイルを引き継いだ暗黙スタイルでは、どちらの経路でも自前の Setter が効いたままパーツが残ること",
        "方法 1 で非表示にしたあと ThemeMode を Light から Dark に切り替えると、クリアボタンが作り直されてローカル値が消え、フォーカスで再び表示されること。切り替え後にもう一度同じ処理を呼ぶと非表示に戻ること",
    ];

    public string Slug => "wpf-fluent-textbox-hide-clear-button";

    public async Task CaptureAsync(SceneContext context)
    {
        FluentThemeMeasurements.EnsureNotHighContrast();
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

        await context.SaveTableAsync(
            "TextBox template parts, by how the theme reaches the control",
            ["window", "Style applied", "named parts present", "Padding.Left"],
            await FluentThemeMeasurements.ThemeDeliveryAsync(),
            "fluent-textbox-parts.svg");

        await context.SaveTableAsync(
            "approach 1 (local Collapsed on the part) across a ThemeMode switch",
            ["step", "same part instance", "local Visibility", "Visibility with focus"],
            await ThemeSwitchAsync(),
            "fluent-clear-button-theme-switch.svg");
    }

    /// <summary>
    /// 方法 1 で非表示にしたあと、ウィンドウの ThemeMode を Light から Dark に切り替える。
    /// テーマの切り替えでテンプレートが作り直されると、ローカル値を持たない新しいパーツになる。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> ThemeSwitchAsync()
    {
        Window window = BuildWindow(out TextBox textBox);
        window.ShowActivated = true;
        var rows = new List<IReadOnlyList<string>>();
        try
        {
            await Capture.ShowAndSettleAsync(window);
            await FocusAsync(textBox);
            HideClearButtonPart(textBox);
            await Capture.SettleAsync(window);
            UIElement? first = Part(textBox);
            rows.Add(Row("approach 1 applied (ThemeMode=Light)", first, first));

#pragma warning disable WPF0001 // ThemeMode は実験的 API として公開されている。
            window.ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
            await Capture.SettleAsync(window);
            await FocusAsync(textBox);
            await Capture.SettleAsync(window);
            UIElement? second = Part(textBox);
            rows.Add(Row("after switching to ThemeMode=Dark", first, second));

            HideClearButtonPart(textBox);
            await Capture.SettleAsync(window);
            rows.Add(Row("approach 1 applied again", second, Part(textBox)));
        }
        finally
        {
            window.Close();
        }

        return rows;
    }

    private static UIElement? Part(TextBox textBox)
    {
        textBox.ApplyTemplate();
        return ClearButtonPartNames.Select(name => textBox.Template?.FindName(name, textBox)).OfType<UIElement>().FirstOrDefault();
    }

    private static IReadOnlyList<string> Row(string step, UIElement? previous, UIElement? current)
    {
        if (current is null)
        {
            return [step, "-", "-", "(no part)"];
        }

        object local = current.ReadLocalValue(UIElement.VisibilityProperty);
        return
        [
            step,
            ReferenceEquals(previous, current) ? "yes" : "no",
            local == DependencyProperty.UnsetValue ? "(none)" : local.ToString()!,
            current.Visibility.ToString(),
        ];
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
