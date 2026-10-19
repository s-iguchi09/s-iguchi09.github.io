using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ch06.StyleTriggerLocalValue;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 6 章の検証。
///
/// 再現版（Border に Background="White" をローカル値で書く）と修正版（既定値をスタイルの Setter に置く）を表示し、
/// ビューモデルのコマンドで HasError を切り替えて、枠の背景の実効値と BaseValueSource が
/// 記事「WPF で Style の Trigger・DataTrigger が効かない原因と依存関係プロパティの値優先順位」の表と一致するかを確かめる。
/// </summary>
internal sealed class BookACh06StyleTriggerScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 6 章の再現版（Border に Background をローカル値で書く）で、ビューモデルの HasError を true にしても背景は #FFFFFFFF のままで、値の供給元は Local であること（記事の表の「ローカルの Background + トリガー」の行と同じ）",
        "付録サンプル第 6 章の修正版（既定値をスタイルの Setter に置く）で、HasError が true のとき背景が #FFFFD4D4（StyleTrigger）になり、false に戻すと #FFFFFFFF（Style）になること（記事の表の「Setter の既定値 + トリガー」と「トリガーの条件を満たさない」の行と同じ）",
    ];

    public string Slug => "book-a-ch06-style-trigger-local-value";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch06");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>
        {
            new("再現版、「エラーにする」（HasError = true）", "ローカルの Background + トリガー", "#FFFFFFFF (Local)",
                await BackgroundAsync(new ReproView(), showError: true)),
            new("修正版、「エラーにする」（HasError = true）", "Setter の既定値 + トリガー", "#FFFFD4D4 (StyleTrigger)",
                await BackgroundAsync(new FixedView(), showError: true)),
            new("修正版、「エラーにする」の後に「エラーを消す」", "Setter の既定値、トリガーの条件を満たさない", "#FFFFFFFF (Style)",
                await BackgroundAsync(new FixedView(), showError: false)),
        };

        BookAReport.WriteAndAssert(
            "ch06.md", Slug, "第 6 章",
            "WPF で Style の Trigger・DataTrigger が効かない原因と依存関係プロパティの値優先順位",
            "wpf-style-trigger-not-working-local-value",
            "「記事の該当箇所」は記事の 1 つ目の表（構成ごとの実効値）の行。値は枠の Background の色と、DependencyPropertyHelper.GetValueSource の BaseValueSource。",
            checks);
    }

    private static async Task<string> BackgroundAsync(UserControl view, bool showError)
    {
        var vm = new StatusViewModel();
        view.DataContext = vm;
        string result = "";
        await ShowAsync(view, async () =>
        {
            Window window = Window.GetWindow(view)!;
            vm.ShowErrorCommand.Execute(null);
            await Capture.SettleAsync(window, 100);
            if (!showError)
            {
                vm.ClearErrorCommand.Execute(null);
                await Capture.SettleAsync(window, 100);
            }

            var border = (Border)view.FindName("statusBox");
            string color = border.Background is SolidColorBrush brush ? brush.Color.ToString() : border.Background?.ToString() ?? "null";
            result = $"{color} ({DependencyPropertyHelper.GetValueSource(border, Border.BackgroundProperty).BaseValueSource})";
        });
        return result;
    }
}
